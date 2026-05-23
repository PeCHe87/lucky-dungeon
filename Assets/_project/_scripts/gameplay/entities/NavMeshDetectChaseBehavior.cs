using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfViewComponent))]
public class NavMeshDetectChaseBehavior : MonoBehaviour, IEntityNavBehavior
{
    enum Phase
    {
        Searching,
        Pausing,
        Chasing,
        Arrived
    }

    [SerializeField] FieldOfViewComponent fieldOfView;
    [SerializeField] float pauseDuration = 0.5f;
    [SerializeField] float arrivalRadius = 0.5f;
    [Tooltip("How far to search for a valid NavMesh point around the chase target.")]
    [SerializeField] float samplePositionRadius = 2f;

    [Header("Debug")]
    [Tooltip("Log horizontal (XZ) distance to target while searching. Throttled by Distance Log Interval.")]
    [SerializeField] bool logDistanceToTargetWhileSearching = true;
    [Tooltip("Seconds between distance logs. Set to 0 to disable logging.")]
    [SerializeField] float distanceLogInterval = 0.5f;

    Phase _phase = Phase.Searching;
    float _pauseRemaining;
    Vector3 _lastChaseSample;
    bool _hasChaseSample;
    float _nextDistanceLogTime;
    float _nextNavMeshHintTime;

    void Awake()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
    }

    void Reset()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
    }

    void Start()
    {
        EntityNavBehaviorHost host = GetComponentInParent<EntityNavBehaviorHost>();
        if (host != null && !host.IsActiveNavBehavior(this))
        {
            Debug.LogError(
                $"{name}: EntityNavBehaviorHost on '{host.gameObject.name}' is not using this component as Active Behavior — " +
                $"another behavior is ticking instead, so detection never runs. " +
                $"Set Active Behavior to this {nameof(NavMeshDetectChaseBehavior)}.",
                this);
        }
    }

    void LateUpdate()
    {
        if (!logDistanceToTargetWhileSearching || distanceLogInterval <= 0f || _phase != Phase.Searching)
            return;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = GetComponentInParent<NavMeshAgent>();

        if (fieldOfView == null)
            return;

        Vector3 origin = fieldOfView.GetDetectionOrigin(agent);

        if (agent != null && !agent.isOnNavMesh && Time.time >= _nextNavMeshHintTime)
        {
            _nextNavMeshHintTime = Time.time + Mathf.Max(2f, distanceLogInterval);
            Debug.LogWarning(
                $"{name}: NavMeshAgent is not on a NavMesh — EntityNavBehaviorHost skips Tick(), so search/detect never runs. Fix NavMesh bake, spawn position, or EntityNavBehaviorHost warp settings.",
                this);
        }

        LogSearchDistanceIfDue(origin);
    }

    public void Tick(NavMeshAgent agent)
    {
        if (agent == null || !agent.isOnNavMesh || fieldOfView == null)
            return;

        Vector3 origin = fieldOfView.GetDetectionOrigin(agent);

        switch (_phase)
        {
            case Phase.Searching:
                TickSearching(agent, origin);
                break;
            case Phase.Pausing:
                TickPausing(agent);
                break;
            case Phase.Chasing:
                TickChasing(agent, origin);
                break;
            case Phase.Arrived:
                agent.isStopped = true;
                break;
        }
    }

    void TickSearching(NavMeshAgent agent, Vector3 origin)
    {
        agent.isStopped = true;

        if (!fieldOfView.HasTarget)
            return;

        if (fieldOfView.IsTargetInDetectionRange(origin))
        {
            Debug.Log("target detected!");
            agent.ResetPath();
            _phase = Phase.Pausing;
            _pauseRemaining = pauseDuration;
        }
    }

    void LogSearchDistanceIfDue(Vector3 origin)
    {
        if (!logDistanceToTargetWhileSearching || distanceLogInterval <= 0f)
            return;
        if (Time.time < _nextDistanceLogTime)
            return;

        _nextDistanceLogTime = Time.time + distanceLogInterval;

        if (!fieldOfView.HasTarget)
        {
            Debug.Log($"{name}: search — no target assigned (cannot detect).", this);
            return;
        }

        float d = fieldOfView.HorizontalDistanceToTarget(origin);
        bool inRadius = fieldOfView.IsTargetWithinRadius(origin);
        bool inCone = fieldOfView.IsTargetWithinVisionCone(origin);
        bool detected = fieldOfView.IsTargetInDetectionRange(origin);
        Debug.Log(
            $"{name}: search — distance = {d:F2} (radius max = {fieldOfView.DetectionRadius:F2}, view angle = {fieldOfView.ViewAngle:F0}°) " +
            $"in radius = {inRadius}, in vision cone = {inCone}, would detect = {detected}",
            this);
    }

    void TickPausing(NavMeshAgent agent)
    {
        agent.isStopped = true;

        _pauseRemaining -= Time.deltaTime;
        if (_pauseRemaining > 0f)
            return;

        Debug.Log("chase it!");
        _phase = Phase.Chasing;
        _hasChaseSample = false;
    }

    void TickChasing(NavMeshAgent agent, Vector3 origin)
    {
        if (!fieldOfView.HasTarget)
        {
            _phase = Phase.Arrived;
            agent.isStopped = true;
            agent.ResetPath();
            return;
        }

        Vector3 targetPos = fieldOfView.Target.position;

        if (NavMeshChaseDriver.IsWithinXZRadius(origin, targetPos, arrivalRadius))
        {
            _phase = Phase.Arrived;
            agent.isStopped = true;
            agent.ResetPath();
            return;
        }

        agent.stoppingDistance = arrivalRadius;
        agent.isStopped = false;
        NavMeshChaseDriver.RefreshChaseDestinationIfNeeded(
            agent,
            targetPos,
            samplePositionRadius,
            ref _lastChaseSample,
            ref _hasChaseSample);
    }
}
