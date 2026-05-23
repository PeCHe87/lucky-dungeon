using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Discovers colliders in a horizontal-range (XZ) cylinder via broad-phase box overlap, filters by tag(s) and layers,
/// requires line-of-sight, and resolves the nearest visible target.
/// </summary>
public class NearestTargetQuery : MonoBehaviour
{
    struct Candidate : IComparable<Candidate>
    {
        public Collider Collider;
        public float DistSq;

        public int CompareTo(Candidate other) => DistSq.CompareTo(other.DistSq);
    }

    [Tooltip("World-space origin for planar (XZ) range and overlap center. Uses this transform if unset.")]
    [SerializeField] Transform queryOrigin;
    [SerializeField] float radius = 8f;
    [Tooltip("Non-empty entries must be tags defined in the Unity Tag Manager. Colliders matching any listed tag are candidates.")]
    [SerializeField] string[] targetTags;
    [SerializeField, HideInInspector, FormerlySerializedAs("targetTag")]
    string legacyTargetTag;
    [Tooltip("Layers considered by the overlap broad-phase.")]
    [SerializeField] LayerMask discoveryLayers = ~0;
    [SerializeField] QueryTriggerInteraction overlapQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("Max hits for OverlapBoxNonAlloc; increase if many colliders overlap the query volume.")]
    [SerializeField, Min(1)] int overlapMaxHits = 64;
    [Tooltip("Vertical half-extent (world Y) of the discovery box. XZ half-extents match radius.")]
    [SerializeField, Min(0.01f)] float verticalHalfExtent = 10f;

    [Header("Line of sight")]
    [Tooltip("Local offset from the query origin transform for the ray start (e.g. eye height).")]
    [SerializeField] Vector3 losOriginOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Include blocking geometry and targets so the first hit is authoritative.")]
    [SerializeField] LayerMask losLayers = ~0;
    [SerializeField] QueryTriggerInteraction losQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("If set, colliders on this transform or its children are ignored during discovery.")]
    [SerializeField] Transform colliderIgnoreRoot;

    [Header("View cone")]
    [Tooltip("When enabled, only targets inside the vision cone count as detected. No fallback to targets behind.")]
    [SerializeField] bool requireTargetsInViewCone;
    [Tooltip("When Require is off: prefer the nearest visible target in the vision cone; if none, fall back to nearest visible anywhere.")]
    [SerializeField] bool prioritizeFrontTargets;
    [Tooltip("Total angle in degrees for the forward vision cone in the XZ plane. 360 = no angle restriction.")]
    [SerializeField, Range(1f, 360f)] float frontViewAngle = 90f;
    [Tooltip("Optional transform to use for forward direction. If unset, uses Query Origin (or this transform).")]
    [SerializeField] Transform facingRoot;

    [Header("Debug gizmos")]
    [Tooltip("When selected in Play Mode, draws a wire sphere on the collider bounds of the current nearest visible target.")]
    [SerializeField] bool drawDetectedTargetGizmo = true;
    [SerializeField] bool drawLosDebug;

    Collider[] _overlapBuffer;
    readonly List<Candidate> _candidates = new List<Candidate>(32);
    Collider _debugLosWinner;

    const float LosEpsilon = 1e-4f;
    const float RaycastSlop = 0.02f;

    void Awake()
    {
        TryMigrateLegacyTargetTag();
        _overlapBuffer = new Collider[overlapMaxHits];
    }

    void Reset()
    {
        if (queryOrigin == null)
            queryOrigin = transform;
    }

    void OnValidate()
    {
        TryMigrateLegacyTargetTag();
        if (overlapMaxHits < 1)
            overlapMaxHits = 1;
        if (_overlapBuffer == null || _overlapBuffer.Length != overlapMaxHits)
            _overlapBuffer = new Collider[Mathf.Max(1, overlapMaxHits)];
    }

    Transform OriginTransform => queryOrigin != null ? queryOrigin : transform;

    Vector3 PlanarOrigin => OriginTransform.position;

    Vector3 LosOriginWorld => OriginTransform.TransformPoint(losOriginOffset);

    /// <summary>
    /// Returns the nearest transform (by horizontal XZ distance from the planar origin) that passes target tag(s), range, LOS,
    /// and optionally the view cone when <see cref="requireTargetsInViewCone"/> is enabled.
    /// </summary>
    public bool TryGetNearestTransform(out Transform nearest)
    {
        _debugLosWinner = null;
        nearest = null;
        BuildSortedCandidates();
        if (_candidates.Count == 0)
            return false;

        Vector3 planar = PlanarOrigin;

        if (requireTargetsInViewCone)
            return TryPickNearestVisible(planar, viewConeOnly: true, out nearest);

        if (prioritizeFrontTargets && TryPickNearestVisible(planar, viewConeOnly: true, out nearest))
            return true;

        return TryPickNearestVisible(planar, viewConeOnly: false, out nearest);
    }

    bool TryPickNearestVisible(Vector3 planarOrigin, bool viewConeOnly, out Transform nearest)
    {
        nearest = null;
        for (int i = 0; i < _candidates.Count; i++)
        {
            Collider c = _candidates[i].Collider;
            if (!HasLineOfSight(c))
                continue;
            if (viewConeOnly && !IsPointWithinFrontCone(planarOrigin, c.transform.position))
                continue;
            nearest = c.transform;
            _debugLosWinner = c;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with transforms that pass target tag(s), horizontal range, and LOS, ordered by increasing XZ distance.
    /// </summary>
    public int CollectTargets(List<Transform> buffer, bool clearList = true)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        _debugLosWinner = null;
        if (clearList)
            buffer.Clear();

        BuildSortedCandidates();
        Vector3 planar = PlanarOrigin;
        int added = 0;
        for (int i = 0; i < _candidates.Count; i++)
        {
            Collider c = _candidates[i].Collider;
            if (!HasLineOfSight(c))
                continue;
            if (requireTargetsInViewCone && !IsPointWithinFrontCone(planar, c.transform.position))
                continue;
            buffer.Add(c.transform);
            added++;
        }

        return added;
    }

    void TryMigrateLegacyTargetTag()
    {
        if (string.IsNullOrEmpty(legacyTargetTag))
            return;
        if (HasAnyTargetTagConfigured())
        {
            legacyTargetTag = "";
            return;
        }

        targetTags = new[] { legacyTargetTag };
        legacyTargetTag = "";
    }

    bool HasAnyTargetTagConfigured()
    {
        if (targetTags == null || targetTags.Length == 0)
            return false;
        for (int i = 0; i < targetTags.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(targetTags[i]))
                return true;
        }

        return false;
    }

    bool MatchesTargetTags(Collider c)
    {
        if (c == null || targetTags == null)
            return false;
        for (int i = 0; i < targetTags.Length; i++)
        {
            string tag = targetTags[i];
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            if (c.CompareTag(tag))
                return true;
        }

        return false;
    }

    void BuildSortedCandidates()
    {
        TryMigrateLegacyTargetTag();
        _candidates.Clear();
        if (radius <= 0f || !HasAnyTargetTagConfigured())
            return;

        Vector3 center = PlanarOrigin;
        Vector3 halfExtents = new Vector3(radius, verticalHalfExtent, radius);
        int count = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _overlapBuffer,
            Quaternion.identity,
            discoveryLayers,
            overlapQueryTriggerInteraction);

        Vector3 planar = PlanarOrigin;
        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapBuffer[i];
            if (c == null)
                continue;
            if (colliderIgnoreRoot != null &&
                (c.transform == colliderIgnoreRoot || c.transform.IsChildOf(colliderIgnoreRoot)))
                continue;
            if (!MatchesTargetTags(c))
                continue;
            if (!NavMeshChaseDriver.IsWithinXZRadius(planar, c.transform.position, radius))
                continue;

            float distSq = NavMeshChaseDriver.FlatDistanceSq(planar, c.transform.position);
            UpsertCandidate(c, distSq);
        }

        if (_candidates.Count > 1)
            _candidates.Sort();
    }

    void UpsertCandidate(Collider c, float distSq)
    {
        Transform t = c.transform;
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].Collider.transform != t)
                continue;
            if (distSq >= _candidates[i].DistSq)
                return;
            _candidates[i] = new Candidate { Collider = c, DistSq = distSq };
            return;
        }

        _candidates.Add(new Candidate { Collider = c, DistSq = distSq });
    }

    bool HasLineOfSight(Collider candidate)
    {
        if (candidate == null)
            return false;

        Vector3 from = LosOriginWorld;
        Vector3 aim = candidate.ClosestPoint(from);
        Vector3 to = aim - from;
        float dist = to.magnitude;
        if (dist <= LosEpsilon)
            return true;

        Vector3 dir = to / dist;
        float castDistance = dist + RaycastSlop;
        if (!Physics.Raycast(from, dir, out RaycastHit hit, castDistance, losLayers, losQueryTriggerInteraction))
            return false;

        return IsHitFromCandidate(hit, candidate);
    }

    static bool IsHitFromCandidate(RaycastHit hit, Collider candidate)
    {
        if (hit.collider == candidate)
            return true;
        return hit.transform.IsChildOf(candidate.transform);
    }

    Transform FacingTransform => facingRoot != null ? facingRoot : OriginTransform;

    Vector3 GetForwardFlat()
    {
        Vector3 f = FacingTransform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f)
            f = Vector3.forward;
        return f.normalized;
    }

    bool IsPointWithinFrontCone(Vector3 origin, Vector3 worldPoint)
    {
        if (frontViewAngle >= 360f)
            return true;

        Vector3 to = worldPoint - origin;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return true;

        to.Normalize();
        float half = frontViewAngle * 0.5f;
        return Vector3.Angle(GetForwardFlat(), to) <= half + 1e-4f;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 planar = OriginTransform.position;
        NavMeshChaseDriver.DrawXZWireDisc(planar, radius, new Color(0.2f, 0.8f, 1f, 0.9f));

        if ((requireTargetsInViewCone || prioritizeFrontTargets) && frontViewAngle < 360f)
            DrawVisionConeWire(planar, GetForwardFlat(), radius, frontViewAngle, new Color(0.95f, 0.85f, 0.2f, 1f));

        if (!drawLosDebug && !drawDetectedTargetGizmo)
            return;

        if (!Application.isPlaying)
            return;

        if (_overlapBuffer == null || _overlapBuffer.Length != overlapMaxHits)
            _overlapBuffer = new Collider[Mathf.Max(1, overlapMaxHits)];

        TryGetNearestTransform(out _);

        if (_debugLosWinner == null)
            return;

        if (drawDetectedTargetGizmo)
        {
            Bounds b = _debugLosWinner.bounds;
            float indicatorRadius = Mathf.Max(Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)), 0.12f);
            Color previous = Gizmos.color;
            Gizmos.color = new Color(0.25f, 1f, 0.35f, 1f);
            Gizmos.DrawWireSphere(b.center, indicatorRadius);
            Gizmos.color = previous;
        }

        if (drawLosDebug)
        {
            Vector3 los = LosOriginWorld;
            Vector3 aim = _debugLosWinner.ClosestPoint(los);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(los, aim);
        }
    }

    static void DrawVisionConeWire(Vector3 origin, Vector3 forwardFlat, float radius, float totalAngleDeg, Color color)
    {
        if (radius <= 0f || totalAngleDeg <= 0f)
            return;

        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude < 1e-6f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float halfRad = totalAngleDeg * 0.5f * Mathf.Deg2Rad;
        float baseYaw = Mathf.Atan2(forwardFlat.x, forwardFlat.z);

        const int arcSegments = 48;
        Vector3 left = XZDirectionFromYaw(baseYaw - halfRad);
        Vector3 right = XZDirectionFromYaw(baseYaw + halfRad);

        Color prev = Gizmos.color;
        Gizmos.color = color;

        Gizmos.DrawLine(origin, origin + left * radius);
        Gizmos.DrawLine(origin, origin + right * radius);

        Vector3 prevPt = origin + left * radius;
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = (float)i / arcSegments;
            float yaw = Mathf.Lerp(baseYaw - halfRad, baseYaw + halfRad, t);
            Vector3 p = origin + XZDirectionFromYaw(yaw) * radius;
            Gizmos.DrawLine(prevPt, p);
            prevPt = p;
        }

        Gizmos.color = prev;
    }

    static Vector3 XZDirectionFromYaw(float yaw)
    {
        return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    }
}
