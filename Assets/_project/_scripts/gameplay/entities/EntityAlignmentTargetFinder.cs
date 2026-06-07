using UnityEngine;
using UnityEngine.AI;

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
    [Tooltip("When true, candidates must also pass the FOV vision cone.")]
    [SerializeField] bool requireVisionCone;
    [Tooltip("How many times per second to run the overlap scan.")]
    [SerializeField, Min(0.1f)] float scanRate = 5f;
    [Tooltip("Latched target is released only when beyond detectionRadius × this multiplier.")]
    [SerializeField, Min(1f)] float lossRadiusMultiplier = 1.2f;
    [SerializeField, Min(8)] int overlapMaxHits = 32;

    EntityBlackboard _blackboard;
    NavMeshAgent _agent;
    Transform _latchedTarget;
    float _scanTimer;
    Collider[] _overlapBuffer;

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
        float lossRadius = acquireRadius * lossRadiusMultiplier;

        if (_latchedTarget != null && !IsTargetStillValid(_latchedTarget, origin, lossRadius))
            _latchedTarget = null;

        Transform best = _latchedTarget;
        if (best == null)
            best = FindNearestCandidate(origin, acquireRadius);

        _latchedTarget = best;
        ApplyTarget(best);
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

        if (requireVisionCone && !fieldOfView.IsPointWithinVisionCone(origin, targetPos))
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

        if (!NavMeshChaseDriver.IsWithinXZRadius(origin, target.position, lossRadius))
            return false;

        if (requireVisionCone && !fieldOfView.IsPointWithinVisionCone(origin, target.position))
            return false;

        return true;
    }

    void ApplyTarget(Transform target)
    {
        if (target != null)
            fieldOfView.SetTarget(target);
        else
            fieldOfView.ClearTarget();

        if (_blackboard != null)
            _blackboard.Target = target;
    }

    void OnDrawGizmosSelected()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        if (fieldOfView == null)
            return;

        Vector3 origin = fieldOfView.GetDetectionOrigin(_agent);
        float acquireRadius = fieldOfView.DetectionRadius;
        float lossRadius = acquireRadius * lossRadiusMultiplier;

        NavMeshChaseDriver.DrawXZWireDisc(origin, acquireRadius, new Color(0.25f, 0.9f, 1f, 0.55f));
        NavMeshChaseDriver.DrawXZWireDisc(origin, lossRadius, new Color(0.25f, 0.9f, 1f, 0.2f));

        if (_latchedTarget != null)
        {
            Gizmos.color = new Color(0.2f, 0.95f, 0.35f, 1f);
            Gizmos.DrawLine(origin, _latchedTarget.position);
        }
    }
}
