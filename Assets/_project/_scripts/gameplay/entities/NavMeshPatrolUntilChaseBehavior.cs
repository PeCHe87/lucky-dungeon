using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshWaypointPatrolBehavior))]
[RequireComponent(typeof(FieldOfViewComponent))]
[RequireComponent(typeof(EntityTargetDetectedTelegraph))]
public class NavMeshPatrolUntilChaseBehavior : MonoBehaviour, IEntityNavBehavior, IEntityNavChaseDestinationCache
{
    enum Phase
    {
        Idle,
        Patrolling,
        TargetDetected,
        Chasing,
        Arrived,
        AttackReady,
        PreAttacking,
        Attacking,
    }

    [Tooltip("Patrol logic while the target is outside detection radius. Not assigned on EntityNavBehaviorHost; only ticked here.")]
    [SerializeField] NavMeshWaypointPatrolBehavior patrolBehavior;
    [Tooltip("Optional idle/patrol timing when no target. Waypoints: assign transforms on NavMeshWaypointPatrolBehavior. Random Radius: set radius here; no waypoints needed.")]
    [SerializeField] NavMeshIdlePatrolCycle idlePatrolCycle = new NavMeshIdlePatrolCycle();
    [SerializeField] FieldOfViewComponent fieldOfView;
    [SerializeField] EntityAttackController attackController;
    [SerializeField] EntityTargetDetectedTelegraph targetDetectedTelegraph;
    [SerializeField] float arrivalRadius = 0.5f;
    [Tooltip("How far to search for a valid NavMesh point around the chase target.")]
    [SerializeField] float samplePositionRadius = 2f;

    [Header("Debug")]
    [Tooltip("Log horizontal (XZ) distance to target while patrolling. Throttled by Distance Log Interval.")]
    [SerializeField] bool logDistanceToTargetWhilePatrolling = true;
    [Tooltip("Seconds between distance logs. Set to 0 to disable logging.")]
    [SerializeField] float distanceLogInterval = 0.5f;

    Phase _phase;
    float _fallbackDetectedRemaining;
    Vector3 _lastChaseSample;
    bool _hasChaseSample;
    float _nextDistanceLogTime;
    float _nextNavMeshHintTime;

    void Awake()
    {
        if (patrolBehavior == null)
            patrolBehavior = GetComponent<NavMeshWaypointPatrolBehavior>();
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        if (attackController == null)
            attackController = GetComponent<EntityAttackController>();
        if (targetDetectedTelegraph == null)
            targetDetectedTelegraph = GetComponent<EntityTargetDetectedTelegraph>();
        idlePatrolCycle.ResolveReferences(this, patrolBehavior);
        _phase = idlePatrolCycle.IsEnabled ? Phase.Idle : Phase.Patrolling;
    }

    void OnEnable()
    {
        if (attackController == null)
            attackController = GetComponent<EntityAttackController>();
        if (attackController != null)
            attackController.DamageInterruptStarted += OnDamageInterruptStarted;

        if (idlePatrolCycle.IsEnabled)
        {
            NavMeshAgent agent = GetAgent();
            idlePatrolCycle.EnterIdle(agent);
            _phase = Phase.Idle;
        }
        else
        {
            _phase = Phase.Patrolling;
        }
    }

    void OnDisable()
    {
        if (attackController != null)
            attackController.DamageInterruptStarted -= OnDamageInterruptStarted;
    }

    public void InvalidateChaseDestinationCache() => _hasChaseSample = false;

    void OnDamageInterruptStarted()
    {
        NavMeshAgent agent = GetAgent();

        if (_phase is Phase.Idle or Phase.Patrolling)
        {
            idlePatrolCycle.CancelToIdle(agent);
            _phase = Phase.Idle;
            return;
        }

        if (_phase == Phase.AttackReady
            && attackController != null
            && attackController.EnableBetweenAttackRecovery
            && attackController.IsWaitingForNextAttack
            && !attackController.IsActivelyAttacking)
        {
            return;
        }

        targetDetectedTelegraph?.Cancel();
        _phase = Phase.Chasing;
        EntityNavChaseAttackSupport.ResetToChaseAfterDamageInterrupt(agent, gameObject, ref _hasChaseSample);
    }

    void Start()
    {
        EntityNavBehaviorHost host = GetComponentInParent<EntityNavBehaviorHost>();
        if (host != null && !host.IsActiveNavBehavior(this))
        {
            Debug.LogError(
                $"{name}: EntityNavBehaviorHost on '{host.gameObject.name}' is not using this component as Active Behavior — " +
                $"NavMeshWaypointPatrolBehavior is still ticking instead, so detection and stopping never run. " +
                $"Set Active Behavior to this {nameof(NavMeshPatrolUntilChaseBehavior)} (not the waypoint patrol).",
                this);
        }
    }

    void Reset()
    {
        patrolBehavior = GetComponent<NavMeshWaypointPatrolBehavior>();
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
    }

    void LateUpdate()
    {
        if (!logDistanceToTargetWhilePatrolling || distanceLogInterval <= 0f || _phase != Phase.Patrolling)
            return;

        NavMeshAgent agent = GetAgent();

        if (fieldOfView == null)
            return;

        Vector3 origin = fieldOfView.GetDetectionOrigin(agent);

        if (agent != null && !agent.isOnNavMesh && Time.time >= _nextNavMeshHintTime)
        {
            _nextNavMeshHintTime = Time.time + Mathf.Max(2f, distanceLogInterval);
            Debug.LogWarning(
                $"{name}: NavMeshAgent is not on a NavMesh — EntityNavBehaviorHost skips Tick(), so patrol and detection never run. Fix NavMesh bake, spawn position, or EntityNavBehaviorHost warp settings.",
                this);
        }

        LogPatrolDistanceIfDue(origin);
    }

    public void Tick(NavMeshAgent agent)
    {
        if (agent == null || !agent.isOnNavMesh || fieldOfView == null)
            return;

        if (patrolBehavior == null)
            patrolBehavior = GetComponent<NavMeshWaypointPatrolBehavior>();

        idlePatrolCycle.ResolveReferences(this, patrolBehavior);

        Vector3 origin = fieldOfView.GetDetectionOrigin(agent);

        switch (_phase)
        {
            case Phase.Idle:
                TickIdle(agent, origin);
                break;
            case Phase.Patrolling:
                TickPatrolling(agent, origin);
                break;
            case Phase.TargetDetected:
                TickTargetDetected(agent);
                break;
            case Phase.Chasing:
                TickChasing(agent, origin);
                break;
            case Phase.Arrived:
                TickArrived(agent, origin);
                break;
            case Phase.AttackReady:
                TickAttackReady(agent, origin);
                break;
            case Phase.PreAttacking:
                TickPreAttacking(agent, origin);
                break;
            case Phase.Attacking:
                TickAttacking(agent, origin);
                break;
        }
    }

    void TickIdle(NavMeshAgent agent, Vector3 origin)
    {
        switch (idlePatrolCycle.TickIdle(Time.deltaTime, agent, fieldOfView, origin))
        {
            case NavMeshIdlePatrolTickResult.TargetDetected:
                StopAndDetect(agent, fieldOfView.Target);
                break;
            case NavMeshIdlePatrolTickResult.IdleExpired:
                idlePatrolCycle.EnterPatrol(agent);
                _phase = Phase.Patrolling;
                break;
        }
    }

    void TickArrived(NavMeshAgent agent, Vector3 origin)
    {
        if (!fieldOfView.HasTarget)
        {
            EntityNavChaseAttackSupport.NotifyAggroLost(targetDetectedTelegraph);
            ReturnToNoTargetPhase(agent);
            return;
        }

        switch (EntityNavChaseAttackSupport.TryBeginPreAttackPhase(attackController, fieldOfView.Target, agent))
        {
            case EntityNavChaseAttackSupport.AttackPhaseBeginResult.PreAttacking:
                _phase = Phase.PreAttacking;
                return;
            case EntityNavChaseAttackSupport.AttackPhaseBeginResult.Attacking:
                _phase = Phase.Attacking;
                return;
            case EntityNavChaseAttackSupport.AttackPhaseBeginResult.AttackReady:
                EnterAttackReady();
                return;
        }

        _phase = Phase.Chasing;
        _hasChaseSample = false;
        EntityNavChaseAttackSupport.TryEnableNavLocomotion(agent, gameObject);
    }

    void TickPreAttacking(NavMeshAgent agent, Vector3 origin)
    {
        switch (EntityNavChaseAttackSupport.TickPreAttackPhase(attackController, fieldOfView, agent, origin))
        {
            case EntityNavChaseAttackSupport.AttackTickResult.ResumeSearching:
                EntityNavChaseAttackSupport.NotifyAggroLost(targetDetectedTelegraph);
                ReturnToNoTargetPhase(agent);
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.ResumeChasing:
                _phase = Phase.Chasing;
                _hasChaseSample = false;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayAttacking:
                _phase = Phase.Attacking;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayPreAttacking:
                _phase = Phase.PreAttacking;
                break;
        }
    }

    void TickAttacking(NavMeshAgent agent, Vector3 origin)
    {
        switch (EntityNavChaseAttackSupport.TickAttackPhase(attackController, fieldOfView, agent, origin))
        {
            case EntityNavChaseAttackSupport.AttackTickResult.ResumeSearching:
                EntityNavChaseAttackSupport.NotifyAggroLost(targetDetectedTelegraph);
                ReturnToNoTargetPhase(agent);
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.ResumeChasing:
                _phase = Phase.Chasing;
                _hasChaseSample = false;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayPreAttacking:
                _phase = Phase.PreAttacking;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayAttacking:
                _phase = Phase.Attacking;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayAttackReady:
                EnterAttackReady();
                break;
        }
    }

    void TickAttackReady(NavMeshAgent agent, Vector3 origin)
    {
        switch (EntityNavChaseAttackSupport.TickAttackReadyPhase(attackController, fieldOfView, agent, origin))
        {
            case EntityNavChaseAttackSupport.AttackTickResult.ResumeSearching:
                attackController?.EndBetweenAttackRecovery();
                EntityNavChaseAttackSupport.NotifyAggroLost(targetDetectedTelegraph);
                ReturnToNoTargetPhase(agent);
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.ResumeChasing:
                attackController?.EndBetweenAttackRecovery();
                _phase = Phase.Chasing;
                _hasChaseSample = false;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayPreAttacking:
                attackController?.EndBetweenAttackRecovery();
                _phase = Phase.PreAttacking;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayAttacking:
                attackController?.EndBetweenAttackRecovery();
                _phase = Phase.Attacking;
                break;
            case EntityNavChaseAttackSupport.AttackTickResult.StayAttackReady:
                _phase = Phase.AttackReady;
                break;
        }
    }

    void EnterAttackReady()
    {
        if (_phase != Phase.AttackReady)
            attackController?.EnterBetweenAttackRecovery();
        _phase = Phase.AttackReady;
    }

    void TickPatrolling(NavMeshAgent agent, Vector3 origin)
    {
        if (idlePatrolCycle.IsEnabled)
        {
            switch (idlePatrolCycle.TickPatrol(agent, fieldOfView, origin, Time.deltaTime))
            {
                case NavMeshIdlePatrolTickResult.TargetDetected:
                    StopAndDetect(agent, fieldOfView.Target);
                    return;
                case NavMeshIdlePatrolTickResult.PatrolExpired:
                    idlePatrolCycle.EnterIdle(agent);
                    _phase = Phase.Idle;
                    return;
            }

            return;
        }

        if (NavMeshIdlePatrolCycle.TryCheckDetection(fieldOfView, origin))
        {
            StopAndDetect(agent, fieldOfView.Target);
            return;
        }

        idlePatrolCycle.TickContinuousPatrol(agent, fieldOfView, origin);
    }

    void StopAndDetect(NavMeshAgent agent, Transform target)
    {
        agent.isStopped = true;
        agent.ResetPath();
        EnterTargetDetected(target);
    }

    void ReturnToNoTargetPhase(NavMeshAgent agent)
    {
        idlePatrolCycle.ResetPatrolOnAggroLost();

        if (idlePatrolCycle.IsEnabled)
        {
            idlePatrolCycle.EnterIdle(agent);
            _phase = Phase.Idle;
        }
        else
        {
            _phase = Phase.Patrolling;
        }
    }

    void EnterTargetDetected(Transform target)
    {
        _phase = Phase.TargetDetected;
        EntityNavChaseAttackSupport.BeginTargetDetected(
            targetDetectedTelegraph,
            target,
            ref _fallbackDetectedRemaining);
    }

    void LogPatrolDistanceIfDue(Vector3 origin)
    {
        if (!logDistanceToTargetWhilePatrolling || distanceLogInterval <= 0f)
            return;
        if (Time.time < _nextDistanceLogTime)
            return;

        _nextDistanceLogTime = Time.time + distanceLogInterval;

        if (!fieldOfView.HasTarget)
        {
            Debug.Log($"{name}: patrol — no target assigned (cannot detect).", this);
            return;
        }

        float d = fieldOfView.HorizontalDistanceToTarget(origin);
        bool inRadius = fieldOfView.IsTargetWithinRadius(origin);
        bool inCone = fieldOfView.IsTargetWithinVisionCone(origin);
        bool detected = fieldOfView.IsTargetInDetectionRange(origin);
        Debug.Log(
            $"{name}: patrol — distance = {d:F2} (radius max = {fieldOfView.DetectionRadius:F2}, view angle = {fieldOfView.ViewAngle:F0}°) " +
            $"in radius = {inRadius}, in vision cone = {inCone}, would detect = {detected}",
            this);
    }

    void TickTargetDetected(NavMeshAgent agent)
    {
        switch (EntityNavChaseAttackSupport.TickTargetDetectedPhase(
            agent,
            fieldOfView,
            targetDetectedTelegraph,
            ref _fallbackDetectedRemaining))
        {
            case EntityNavChaseAttackSupport.TargetDetectedTickResult.Cancelled:
                ReturnToNoTargetPhase(agent);
                break;
            case EntityNavChaseAttackSupport.TargetDetectedTickResult.Complete:
                _phase = Phase.Chasing;
                _hasChaseSample = false;
                break;
        }
    }

    void TickChasing(NavMeshAgent agent, Vector3 origin)
    {
        if (!fieldOfView.HasTarget)
        {
            EntityNavChaseAttackSupport.NotifyAggroLost(targetDetectedTelegraph);
            _phase = Phase.Arrived;
            agent.isStopped = true;
            agent.ResetPath();
            return;
        }

        Transform target = fieldOfView.Target;
        EntityNavChaseAttackSupport.SyncChaseFacing(attackController, target, agent);

        Vector3 targetPos = target.position;

        switch (EntityNavChaseAttackSupport.TryBeginPreAttackPhase(attackController, target, agent))
        {
            case EntityNavChaseAttackSupport.AttackPhaseBeginResult.PreAttacking:
                _phase = Phase.PreAttacking;
                return;
            case EntityNavChaseAttackSupport.AttackPhaseBeginResult.Attacking:
                _phase = Phase.Attacking;
                return;
            case EntityNavChaseAttackSupport.AttackPhaseBeginResult.AttackReady:
                EnterAttackReady();
                return;
        }

        agent.stoppingDistance = arrivalRadius;
        if (!EntityNavChaseAttackSupport.TryEnableNavLocomotion(agent, gameObject))
            return;

        NavMeshChaseDriver.RefreshChaseDestinationIfNeeded(
            agent,
            targetPos,
            samplePositionRadius,
            ref _lastChaseSample,
            ref _hasChaseSample);
    }

    NavMeshAgent GetAgent()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = GetComponentInParent<NavMeshAgent>();
        return agent;
    }
}
