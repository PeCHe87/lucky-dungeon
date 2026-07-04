using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Scans for the nearest <see cref="CombatEntityHealth"/> matching configurable priority tiers within
/// <see cref="FieldOfViewComponent.DetectionRadius"/>, then feeds the result into FOV and the blackboard.
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(FieldOfViewComponent))]
public class EntityAlignmentTargetFinder : MonoBehaviour
{
    struct ScanCandidate
    {
        public Collider Collider;
        public Transform EntityRoot;
        public int Tier;
        public float DistSq;
    }

    [SerializeField] FieldOfViewComponent fieldOfView;
    [Tooltip("Lower tier values outrank higher ones. Distance breaks ties within the same tier.")]
    [SerializeField] EntityTargetPriorityRule[] priorityRules;
    [SerializeField, HideInInspector, FormerlySerializedAs("targetAlignment")]
    EntityAlignment legacyTargetAlignment = EntityAlignment.Enemy;
    [Tooltip("Layers scanned for colliders (Entity + Player by default).")]
    [SerializeField] LayerMask scanLayers;
    [Tooltip("When true, new targets must be inside the FOV vision cone. Retention uses distance only (cone is not re-checked after latch).")]
    [SerializeField, FormerlySerializedAs("requireVisionCone")]
    bool requireVisionConeForAcquisition = true;
    [Tooltip("How many times per second to run the overlap scan.")]
    [SerializeField, Min(0.1f)] float scanRate = 5f;
    [Tooltip("Latched target is released only when beyond detectionRadius × this multiplier.")]
    [SerializeField, Min(1f)] float lossRadiusMultiplier = 1.2f;
    [SerializeField, Min(8)] int overlapMaxHits = 32;

    [Header("Hidden target loss")]
    [Tooltip("Release latch when the target stays occluded, exceeds a time threshold, and moves away from the last detected position.")]
    [SerializeField] bool enableHiddenTargetLoss = true;
    [Tooltip("Seconds without full detection while line of sight is blocked before hidden loss can occur.")]
    [SerializeField, Min(0f)] float hiddenLossDelaySeconds = 3f;
    [Tooltip("Horizontal (XZ) distance the target must move from the last detected position to allow hidden loss.")]
    [SerializeField, Min(0f)] float hiddenLossDistance = 3f;

    EntityBlackboard _blackboard;
    NavMeshAgent _agent;
    Transform _latchedTarget;
    float _scanTimer;
    Collider[] _overlapBuffer;
    Vector3 _lastDetectedPosition;
    float _lastDetectedTime;
    bool _hasDetectionMemory;
    readonly List<ScanCandidate> _scanCandidates = new List<ScanCandidate>(16);
    readonly List<int> _sortedTiers = new List<int>(4);

    void Awake()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        _blackboard = GetComponent<EntityBlackboard>();
        _agent = GetComponent<NavMeshAgent>();
        _overlapBuffer = new Collider[overlapMaxHits];

        if (scanLayers.value == 0)
            scanLayers = LayerMask.GetMask("Entity", "Player");

        EnsurePriorityRulesMigrated();
    }

    void Reset()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        if (scanLayers.value == 0)
            scanLayers = LayerMask.GetMask("Entity", "Player");
        if (priorityRules == null || priorityRules.Length == 0)
            priorityRules = EntityTargetPriorityRules.CreateDefaultRules(EntityAlignment.Ally);
    }

    void OnValidate()
    {
        EnsurePriorityRulesMigrated();
    }

    void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer < 1f / scanRate)
            return;
        _scanTimer = 0f;

        Scan();
    }

    void Scan()
    {
        if (fieldOfView == null)
            return;

        EnsurePriorityRulesMigrated();

        Vector3 origin = fieldOfView.GetDetectionOrigin(_agent);
        float acquireRadius = fieldOfView.DetectionRadius;
        float lossRadius = ResolveLossRadius(acquireRadius);
        Transform previousLatch = _latchedTarget;

        if (_latchedTarget != null && !IsTargetStillValid(_latchedTarget, origin, lossRadius))
            _latchedTarget = null;

        if (_latchedTarget != null)
        {
            EnsureFieldOfViewTarget(_latchedTarget);
            if (fieldOfView.IsTargetInDetectionRange(origin))
                RecordDetection(_latchedTarget.position);
            else if (ShouldLoseHiddenTarget(_latchedTarget))
                _latchedTarget = null;
        }

        if (_latchedTarget == null)
            _latchedTarget = FindNearestCandidate(origin, acquireRadius);

        if (_latchedTarget != null && _latchedTarget != previousLatch)
            RecordDetection(_latchedTarget.position);

        ApplyTarget(_latchedTarget);
    }

    void EnsurePriorityRulesMigrated()
    {
        if (priorityRules != null && priorityRules.Length > 0)
            return;

        priorityRules = EntityTargetPriorityRules.CreateDefaultRules(legacyTargetAlignment);
    }

    void EnsureFieldOfViewTarget(Transform latch)
    {
        if (fieldOfView.Target != latch)
            fieldOfView.SetTarget(latch);
    }

    void RecordDetection(Vector3 worldPosition)
    {
        _lastDetectedPosition = worldPosition;
        _lastDetectedTime = Time.time;
        _hasDetectionMemory = true;
    }

    void ClearDetectionMemory()
    {
        _lastDetectedPosition = default;
        _lastDetectedTime = 0f;
        _hasDetectionMemory = false;
    }

    bool ShouldLoseHiddenTarget(Transform target)
    {
        if (!enableHiddenTargetLoss || target == null || fieldOfView == null)
            return false;

        if (!fieldOfView.RequireLineOfSightForDetection)
            return false;

        if (!fieldOfView.HasLineOfSightToTarget())
        {
            if (!_hasDetectionMemory)
                return false;

            if (Time.time - _lastDetectedTime < hiddenLossDelaySeconds)
                return false;

            if (hiddenLossDistance <= 0f)
                return true;

            return NavMeshChaseDriver.HorizontalDistance(_lastDetectedPosition, target.position) >= hiddenLossDistance;
        }

        return false;
    }

    Transform FindNearestCandidate(Vector3 origin, float acquireRadius)
    {
        if (acquireRadius <= 0f || priorityRules == null || priorityRules.Length == 0)
            return null;

        _scanCandidates.Clear();
        CollectScanCandidates(origin, acquireRadius);
        if (_scanCandidates.Count == 0)
            return null;

        BuildSortedTiers();
        for (int i = 0; i < _sortedTiers.Count; i++)
        {
            if (TryPickNearestInTier(_sortedTiers[i], out Transform pick))
                return pick;
        }

        return null;
    }

    void CollectScanCandidates(Vector3 origin, float acquireRadius)
    {
        int count = Physics.OverlapSphereNonAlloc(
            origin,
            acquireRadius,
            _overlapBuffer,
            scanLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null)
                continue;

            if (!TryResolveCandidate(col, origin, acquireRadius, out Transform entityRoot, out int tier, out float distSq))
                continue;

            UpsertScanCandidate(col, entityRoot, tier, distSq);
        }
    }

    void UpsertScanCandidate(Collider col, Transform entityRoot, int tier, float distSq)
    {
        for (int i = 0; i < _scanCandidates.Count; i++)
        {
            if (_scanCandidates[i].EntityRoot != entityRoot)
                continue;
            if (!DetectionTargetRank.IsBetter(tier, distSq, _scanCandidates[i].Tier, _scanCandidates[i].DistSq))
                return;
            _scanCandidates[i] = new ScanCandidate
            {
                Collider = col,
                EntityRoot = entityRoot,
                Tier = tier,
                DistSq = distSq,
            };
            return;
        }

        _scanCandidates.Add(new ScanCandidate
        {
            Collider = col,
            EntityRoot = entityRoot,
            Tier = tier,
            DistSq = distSq,
        });
    }

    void BuildSortedTiers()
    {
        _sortedTiers.Clear();
        for (int i = 0; i < priorityRules.Length; i++)
        {
            int tier = priorityRules[i].tier;
            if (!_sortedTiers.Contains(tier))
                _sortedTiers.Add(tier);
        }

        _sortedTiers.Sort();
    }

    bool TryPickNearestInTier(int tier, out Transform nearest)
    {
        nearest = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < _scanCandidates.Count; i++)
        {
            ScanCandidate candidate = _scanCandidates[i];
            if (candidate.Tier != tier)
                continue;
            if (!HasLineOfSightForTier(candidate.Collider, tier))
                continue;
            if (candidate.DistSq >= bestDistSq)
                continue;

            bestDistSq = candidate.DistSq;
            nearest = candidate.EntityRoot;
        }

        return nearest != null;
    }

    bool HasLineOfSightForTier(Collider candidate, int candidateTier)
    {
        if (fieldOfView == null || !fieldOfView.RequireLineOfSightForDetection)
            return true;

        return fieldOfView.HasLineOfSightToCollider(
            candidate,
            hit => IsLowerPriorityTargetHit(hit, candidateTier));
    }

    bool IsLowerPriorityTargetHit(Collider hit, int candidateTier)
    {
        if (!EntityTargetPriorityRules.TryResolveRule(hit, priorityRules, out int hitTier))
            return false;
        return hitTier > candidateTier;
    }

    bool TryResolveCandidate(
        Collider col,
        Vector3 origin,
        float acquireRadius,
        out Transform entityRoot,
        out int tier,
        out float distSq)
    {
        entityRoot = null;
        tier = int.MaxValue;
        distSq = float.MaxValue;

        if (col.transform == transform || col.transform.IsChildOf(transform))
            return false;

        if (!EntityTargetPriorityRules.TryResolveRule(col, priorityRules, out tier))
            return false;

        var health = col.GetComponentInParent<CombatEntityHealth>();
        if (health == null || health.IsDefeated)
            return false;

        entityRoot = health.transform;
        Vector3 targetPos = entityRoot.position;

        if (!NavMeshChaseDriver.IsWithinXZRadius(origin, targetPos, acquireRadius))
            return false;

        if (requireVisionConeForAcquisition && !fieldOfView.IsPointWithinVisionCone(origin, targetPos))
            return false;

        distSq = NavMeshChaseDriver.FlatDistanceSq(origin, targetPos);
        return true;
    }

    bool IsTargetStillValid(Transform target, Vector3 origin, float lossRadius)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        var health = target.GetComponent<CombatEntityHealth>();
        if (health == null || health.IsDefeated)
            return false;

        if (!EntityTargetPriorityRules.TargetMatchesAnyRule(target, priorityRules))
            return false;

        return NavMeshChaseDriver.IsWithinXZRadius(origin, target.position, lossRadius);
    }

    void ApplyTarget(Transform target)
    {
        if (target != null)
            fieldOfView.SetTarget(target);
        else
        {
            fieldOfView.ClearTarget();
            ClearDetectionMemory();
        }

        if (_blackboard != null)
            _blackboard.Target = target;
    }

    float ResolveLossRadius(float acquireRadius) =>
        fieldOfView.HasExplicitLossRadius
            ? fieldOfView.LossDetectionRadius
            : acquireRadius * lossRadiusMultiplier;

    void OnDrawGizmosSelected()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        if (fieldOfView == null)
            return;

        Vector3 origin = fieldOfView.GetDetectionOrigin(_agent);
        float acquireRadius = fieldOfView.DetectionRadius;
        float lossRadius = ResolveLossRadius(acquireRadius);

        NavMeshChaseDriver.DrawXZWireDisc(origin, acquireRadius, new Color(0.25f, 0.9f, 1f, 0.55f));
        NavMeshChaseDriver.DrawXZWireDisc(origin, lossRadius, new Color(0.25f, 0.9f, 1f, 0.2f));

        if (_latchedTarget != null)
        {
            Gizmos.color = new Color(0.2f, 0.95f, 0.35f, 1f);
            Gizmos.DrawLine(origin, _latchedTarget.position);
        }

        if (_hasDetectionMemory)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.9f);
            Gizmos.DrawWireSphere(_lastDetectedPosition, 0.35f);
        }
    }
}
