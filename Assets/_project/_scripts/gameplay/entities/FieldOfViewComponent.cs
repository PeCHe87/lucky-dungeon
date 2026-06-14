using UnityEngine;
using UnityEngine.AI;

public class FieldOfViewComponent : MonoBehaviour
{
    [Tooltip("Transform to detect. If unset, detection queries return false.")]
    [SerializeField] Transform target;

    [Header("Detection")]
    [Tooltip("Origin and forward for cone checks. Uses this transform's position and facing on the XZ plane.")]
    [SerializeField] Transform moveRoot;
    [Tooltip("Max horizontal (XZ) distance for target detection.")]
    [SerializeField] float detectionRadius = 8f;
    [Tooltip("Total vision cone angle in degrees around forward (XZ plane). 360 = radius-only detection, no cone.")]
    [SerializeField, Range(1f, 360f)] float viewAngle = 90f;

    [Header("Line of sight")]
    [Tooltip("When true, initial detection requires a clear ray to the target (walls block acquisition).")]
    [SerializeField] bool requireLineOfSightForDetection = true;
    [Tooltip("Local offset from moveRoot for the line-of-sight ray start (e.g. eye height).")]
    [SerializeField] Vector3 losOriginOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Layers that can block line of sight.")]
    [SerializeField] LayerMask losLayers = ~0;
    [SerializeField] QueryTriggerInteraction losQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("Colliders on this transform or its children are ignored during line-of-sight. Defaults to moveRoot.")]
    [SerializeField] Transform losIgnoreRoot;

    [Header("Debug gizmos")]
    [Tooltip("When this object is selected, draw a line to the target: green if it would be detected, red otherwise.")]
    [SerializeField] bool drawTargetVisibilityRay = true;

    public Transform Target => target;
    public bool HasTarget => target != null;
    public float DetectionRadius => detectionRadius;
    public float ViewAngle => viewAngle;
    public bool RequireLineOfSightForDetection => requireLineOfSightForDetection;

    public void SetTarget(Transform t) => target = t;

    public void ClearTarget() => target = null;

    void Awake()
    {
        EnsureLosIgnoreRoot();
    }

    void Reset()
    {
        if (moveRoot == null)
            moveRoot = transform;
        EnsureLosIgnoreRoot();
    }

    void EnsureLosIgnoreRoot()
    {
        if (losIgnoreRoot == null)
            losIgnoreRoot = moveRoot != null ? moveRoot : transform;
    }

    public Vector3 GetDetectionOrigin(NavMeshAgent agent)
    {
        if (moveRoot != null)
            return moveRoot.position;
        if (agent != null)
            return agent.transform.position;
        return transform.position;
    }

    public Vector3 GetLineOfSightOriginWorld()
    {
        Transform root = moveRoot != null ? moveRoot : transform;
        return root.TransformPoint(losOriginOffset);
    }

    public bool HasLineOfSightToCollider(Collider candidate)
    {
        EnsureLosIgnoreRoot();
        return LineOfSightProbe.HasLineOfSight(
            GetLineOfSightOriginWorld(),
            candidate,
            losIgnoreRoot,
            losLayers,
            losQueryTriggerInteraction);
    }

    public bool HasLineOfSightToTarget()
    {
        if (target == null)
            return false;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c != null && c.enabled && HasLineOfSightToCollider(c))
                return true;
        }

        EnsureLosIgnoreRoot();
        return LineOfSightProbe.HasClearLineToPoint(
            GetLineOfSightOriginWorld(),
            target.position,
            losIgnoreRoot,
            losLayers,
            losQueryTriggerInteraction);
    }

    Transform FacingTransform => moveRoot != null ? moveRoot : transform;

    Vector3 GetForwardFlat()
    {
        Vector3 f = FacingTransform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f)
            f = Vector3.forward;
        return f.normalized;
    }

    public bool IsTargetWithinVisionCone(Vector3 origin)
    {
        if (target == null)
            return false;
        return IsPointWithinVisionCone(origin, target.position);
    }

    public bool IsPointWithinVisionCone(Vector3 origin, Vector3 worldPoint)
    {
        if (viewAngle >= 360f)
            return true;

        Vector3 to = worldPoint - origin;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return true;

        to.Normalize();
        float half = viewAngle * 0.5f;
        return Vector3.Angle(GetForwardFlat(), to) <= half + 1e-4f;
    }

    public bool IsTargetWithinRadius(Vector3 origin)
    {
        if (target == null)
            return false;
        return NavMeshChaseDriver.IsWithinXZRadius(origin, target.position, detectionRadius);
    }

    public bool IsTargetInDetectionRange(Vector3 origin)
    {
        if (target == null)
            return false;
        if (!NavMeshChaseDriver.IsWithinXZRadius(origin, target.position, detectionRadius))
            return false;
        if (!IsPointWithinVisionCone(origin, target.position))
            return false;
        if (requireLineOfSightForDetection && !HasLineOfSightToTarget())
            return false;
        return true;
    }

    public bool IsTargetInDetectionRange(NavMeshAgent agent)
    {
        return IsTargetInDetectionRange(GetDetectionOrigin(agent));
    }

    public float HorizontalDistanceToTarget(Vector3 origin)
    {
        if (target == null)
            return float.PositiveInfinity;
        return NavMeshChaseDriver.HorizontalDistance(origin, target.position);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = GetDetectionOrigin(null);
        Vector3 forward = GetForwardFlat();

        Color discColor = new Color(0.25f, 0.9f, 1f, 0.45f);
        NavMeshChaseDriver.DrawXZWireDisc(origin, detectionRadius, discColor);

        if (viewAngle < 360f)
            DrawVisionConeWire(origin, forward, detectionRadius, viewAngle, new Color(0.95f, 0.85f, 0.2f, 1f));

        Gizmos.color = new Color(0.4f, 1f, 0.5f, 1f);
        Gizmos.DrawLine(origin, origin + forward * Mathf.Min(detectionRadius, 1.5f));

        if (!drawTargetVisibilityRay || target == null)
            return;

        Vector3 losOrigin = GetLineOfSightOriginWorld();
        Vector3 aim = target.position;
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled)
            {
                aim = colliders[i].bounds.center;
                break;
            }
        }

        bool detected = IsTargetInDetectionRange(origin);
        Gizmos.color = detected ? new Color(0.2f, 0.95f, 0.35f, 1f) : new Color(0.95f, 0.25f, 0.2f, 1f);
        Gizmos.DrawLine(losOrigin, aim);
    }

    static void DrawVisionConeWire(Vector3 origin, Vector3 forwardFlat, float radius, float totalAngleDeg, Color color)
    {
        if (radius <= 0f || totalAngleDeg <= 0f)
            return;

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

    static Vector3 XZDirectionFromYaw(float yawRadians)
    {
        return new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
    }
}
