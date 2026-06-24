using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

/// <summary>
/// Scans for the nearest <see cref="CombatEntityHealth"/> matching a configurable alignment within
/// <see cref="FieldOfViewComponent.DetectionRadius"/>, then feeds the result into FOV and the blackboard.
/// </summary>
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(FieldOfViewComponent))]
public class EntityAlignmentTargetFinder : MonoBehaviour
{
    [SerializeField] FieldOfViewComponent fieldOfView;
    [Tooltip("Only entities with this alignment are valid targets. E.g. set Ally to chase the player.")]
    [SerializeField] EntityAlignment targetAlignment = EntityAlignment.Enemy;
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

    void Awake()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        _blackboard = GetComponent<EntityBlackboard>();
        _agent = GetComponent<NavMeshAgent>();
        _overlapBuffer = new Collider[overlapMaxHits];

        if (scanLayers.value == 0)
            scanLayers = LayerMask.GetMask("Entity", "Player");
    }

    void Reset()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        if (scanLayers.value == 0)
            scanLayers = LayerMask.GetMask("Entity", "Player");
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
        if (acquireRadius <= 0f)
            return null;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            acquireRadius,
            _overlapBuffer,
            scanLayers,
            QueryTriggerInteraction.Ignore);

        Transform best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = _overlapBuffer[i];
            if (col == null)
                continue;

            if (!TryResolveCandidate(col, origin, acquireRadius, out Transform entityRoot, out float distSq))
                continue;

            if (distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = entityRoot;
        }

        return best;
    }

    bool TryResolveCandidate(Collider col, Vector3 origin, float acquireRadius, out Transform entityRoot, out float distSq)
    {
        entityRoot = null;
        distSq = float.MaxValue;

        if (col.transform == transform || col.transform.IsChildOf(transform))
            return false;

        var health = col.GetComponentInParent<CombatEntityHealth>();
        if (health == null || health.Alignment != targetAlignment)
            return false;

        if (health.IsDefeated)
            return false;

        entityRoot = health.transform;
        Vector3 targetPos = entityRoot.position;

        if (!NavMeshChaseDriver.IsWithinXZRadius(origin, targetPos, acquireRadius))
            return false;

        if (requireVisionConeForAcquisition && !fieldOfView.IsPointWithinVisionCone(origin, targetPos))
            return false;

        if (fieldOfView.RequireLineOfSightForDetection && !fieldOfView.HasLineOfSightToCollider(col))
            return false;

        distSq = NavMeshChaseDriver.FlatDistanceSq(origin, targetPos);
        return true;
    }

    bool IsTargetStillValid(Transform target, Vector3 origin, float lossRadius)
    {
        if (target == null || !target.gameObject.activeInHierarchy)
            return false;

        var health = target.GetComponent<CombatEntityHealth>();
        if (health == null || health.Alignment != targetAlignment || health.IsDefeated)
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
