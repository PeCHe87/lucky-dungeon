using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(FieldOfViewComponent))]
[RequireComponent(typeof(EntityTargetDetectedTelegraph))]
public class NavMeshDetectChaseBehavior : MonoBehaviour, IEntityNavBehavior, IEntityNavChaseDestinationCache, IEntityNavLocomotionGait
{
    enum Phase
    {
        Searching,
        Idle,
        Patrolling,
        TargetDetected,
        Chasing,
        Arrived,
        AttackReady,
        PreAttacking,
        Attacking,
    }

    [SerializeField] FieldOfViewComponent fieldOfView;
    [Tooltip("Optional idle/patrol when no target. Disabled = stationary search (default). Waypoints: add NavMeshWaypointPatrolBehavior + waypoint transforms. Random Radius: set radius only.")]
    [SerializeField] NavMeshIdlePatrolCycle idlePatrolCycle = new NavMeshIdlePatrolCycle();
    [SerializeField] EntityAttackController attackController;
    [SerializeField] EntityTargetDetectedTelegraph targetDetectedTelegraph;
    [SerializeField] float arrivalRadius = 0.5f;
    [Tooltip("How far to search for a valid NavMesh point around the chase target.")]
    [SerializeField] float samplePositionRadius = 2f;
    [Header("Speeds")]
    [Tooltip("NavMeshAgent speed while idle/patrolling/searching. 0 = leave agent speed unchanged.")]
    [SerializeField, Min(0f)] float patrolSpeed;
    [Tooltip("NavMeshAgent speed while chasing. 0 = leave agent speed unchanged.")]
    [SerializeField, Min(0f)] float chaseSpeed;

    [Header("Debug")]
    [SerializeField] bool debugLog;
    [Tooltip("Log horizontal (XZ) distance to target while searching. Throttled by Distance Log Interval.")]
    [SerializeField] bool logDistanceToTargetWhileSearching;
    [Tooltip("Seconds between distance logs. Set to 0 to disable logging.")]
    [SerializeField] float distanceLogInterval = 0.5f;

    Phase _phase = Phase.Searching;
    float _fallbackDetectedRemaining;
    Vector3 _lastChaseSample;
    bool _hasChaseSample;
    float _nextDistanceLogTime;
    float _nextNavMeshHintTime;

    void Awake()
    {
        if (fieldOfView == null)
            fieldOfView = GetComponent<FieldOfViewComponent>();
        if (attackController == null)
            attackController = GetComponent<EntityAttackController>();
        if (targetDetectedTelegraph == null)
            targetDetectedTelegraph = GetComponent<EntityTargetDetectedTelegraph>();
        idlePatrolCycle.ResolveReferences(this);
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
            EnterIdlePhase(agent);
        }
        else
        {
            EnterSearchingPhase(GetAgent());
        }
    }

    public bool PreferChaseLocomotion =>
        _phase is Phase.Chasing or Phase.Arrived;

    void OnDisable()
    {
        if (attackController != null)
            attackController.DamageInterruptStarted -= OnDamageInterruptStarted;
    }

    public void InvalidateChaseDestinationCache() => _hasChaseSample = false;

    void OnDamageInterruptStarted()
    {
        NavMeshAgent agent = GetAgent();

        if (_phase is Phase.Searching or Phase.Idle or Phase.Patrolling)
        {
            if (idlePatrolCycle.IsEnabled)
            {
                idlePatrolCycle.CancelToIdle(agent);
                EnterIdlePhase(agent, alreadyEnteredIdleCycle: true);
            }
            else
            {
                EnterSearchingPhase(agent);
            }

            return;
        }

        // Committed pre-attack/attack must finish; do not yank the nav phase back to chase.
        if (attackController != null && attackController.IsAttackCommitActive)
            return;

        if (_phase == Phase.AttackReady
            && attackController != null
            && attackController.EnableBetweenAttackRecovery
            && attackController.IsWaitingForNextAttack
            && !attackController.IsActivelyAttacking)
        {
            return;
        }

        targetDetectedTelegraph?.Cancel();
        EnterChasingPhase(agent, resetChaseSample: true);
        EntityNavChaseAttackSupport.ResetToChaseAfterDamageInterrupt(agent, gameObject, ref _hasChaseSample);
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
        if (!logDistanceToTargetWhileSearching || distanceLogInterval <= 0f)
            return;

        if (_phase != Phase.Searching && _phase != Phase.Patrolling)
            return;

        NavMeshAgent agent = GetAgent();

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

        idlePatrolCycle.ResolveReferences(this);

        Vector3 origin = fieldOfView.GetDetectionOrigin(agent);

        switch (_phase)
        {
            case Phase.Searching:
                TickSearching(agent, origin);
                break;
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
                EnterPatrollingPhase(agent);
                break;
        }
    }

    void TickPatrolling(NavMeshAgent agent, Vector3 origin)
    {
        switch (idlePatrolCycle.TickPatrol(agent, fieldOfView, origin, Time.deltaTime))
        {
            case NavMeshIdlePatrolTickResult.TargetDetected:
                StopAndDetect(agent, fieldOfView.Target);
                return;
            case NavMeshIdlePatrolTickResult.PatrolExpired:
                EnterIdlePhase(agent);
                return;
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

        EnterChasingPhase(agent, resetChaseSample: true);
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
                EnterChasingPhase(agent, resetChaseSample: true);
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
                EnterChasingPhase(agent, resetChaseSample: true);
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
                EnterChasingPhase(agent, resetChaseSample: true);
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

    void TickSearching(NavMeshAgent agent, Vector3 origin)
    {
        agent.isStopped = true;
        ApplyPatrolSpeed(agent);

        if (!fieldOfView.HasTarget)
            return;

        if (fieldOfView.IsTargetInDetectionRange(origin))
        {
            if (debugLog)
                Debug.Log("target detected!", this);
            agent.ResetPath();
            EnterTargetDetected(fieldOfView.Target);
        }
    }

    void StopAndDetect(NavMeshAgent agent, Transform target)
    {
        agent.isStopped = true;
        agent.ResetPath();
        if (debugLog)
            Debug.Log("target detected!", this);
        EnterTargetDetected(target);
    }

    void ReturnToNoTargetPhase(NavMeshAgent agent)
    {
        idlePatrolCycle.ResetPatrolOnAggroLost();

        if (idlePatrolCycle.IsEnabled)
            EnterIdlePhase(agent);
        else
            EnterSearchingPhase(agent);
    }

    void EnterIdlePhase(NavMeshAgent agent, bool alreadyEnteredIdleCycle = false)
    {
        if (!alreadyEnteredIdleCycle)
            idlePatrolCycle.EnterIdle(agent);
        ApplyPatrolSpeed(agent);
        _phase = Phase.Idle;
    }

    void EnterPatrollingPhase(NavMeshAgent agent)
    {
        idlePatrolCycle.EnterPatrol(agent);
        ApplyPatrolSpeed(agent);
        _phase = Phase.Patrolling;
    }

    void EnterSearchingPhase(NavMeshAgent agent)
    {
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
            ApplyPatrolSpeed(agent);
        }

        _phase = Phase.Searching;
    }

    void EnterChasingPhase(NavMeshAgent agent, bool resetChaseSample)
    {
        ApplyChaseSpeed(agent);
        _phase = Phase.Chasing;
        if (resetChaseSample)
            _hasChaseSample = false;
    }

    void ApplyPatrolSpeed(NavMeshAgent agent) =>
        EntityNavChaseAttackSupport.ApplyAgentSpeedIfSet(agent, patrolSpeed);

    void ApplyChaseSpeed(NavMeshAgent agent) =>
        EntityNavChaseAttackSupport.ApplyAgentSpeedIfSet(agent, chaseSpeed);

    void EnterTargetDetected(Transform target)
    {
        _phase = Phase.TargetDetected;
        EntityNavChaseAttackSupport.BeginTargetDetected(
            targetDetectedTelegraph,
            target,
            ref _fallbackDetectedRemaining);
    }

    void LogSearchDistanceIfDue(Vector3 origin)
    {
        if (!logDistanceToTargetWhileSearching || distanceLogInterval <= 0f)
            return;
        if (Time.time < _nextDistanceLogTime)
            return;

        _nextDistanceLogTime = Time.time + distanceLogInterval;

        if (!debugLog)
            return;

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
                if (debugLog)
                    Debug.Log("chase it!", this);
                EnterChasingPhase(agent, resetChaseSample: true);
                break;
        }
    }

    void TickChasing(NavMeshAgent agent, Vector3 origin)
    {
        ApplyChaseSpeed(agent);

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

        if (attackController != null
            && attackController.IsTargetTooCloseForRanged(target))
        {
            EntityNavChaseAttackSupport.UpdateRangedAttackRetreat(attackController, target, agent);
            return;
        }

        agent.stoppingDistance = attackController != null && !attackController.IsMeleeWeaponEquipped
            ? attackController.ApproachStopDistance
            : arrivalRadius;
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

