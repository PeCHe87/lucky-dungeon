using UnityEngine;
using UnityEngine.AI;

public enum NavMeshIdlePatrolMode
{
    Waypoints,
    RandomRadius,
}

public enum NavMeshIdlePatrolTickResult
{
    Continue,
    IdleExpired,
    PatrolExpired,
    TargetDetected,
}

/// <summary>
/// Optional idle/patrol cycle while no target is aggroed. Embed on nav behaviors or assign as a sibling component reference.
/// Waypoints: add <see cref="NavMeshWaypointPatrolBehavior"/> and assign transforms on the entity.
/// RandomRadius: picks NavMesh destinations within <see cref="randomRadius"/> of the patrol anchor (same logic as FSM PatrolStateHandler).
/// </summary>
[System.Serializable]
public class NavMeshIdlePatrolCycle
{
    [Header("Cycle")]
    [Tooltip("When enabled, the entity alternates idle and patrol phases until a target is detected or combat ends.")]
    [SerializeField] bool enableIdlePatrolCycle;
    [Tooltip("Seconds to stand still each idle phase. 0 skips straight to patrol.")]
    [SerializeField, Min(0f)] float idleDuration = 3f;
    [Tooltip("Seconds to patrol each patrol phase before returning to idle.")]
    [SerializeField, Min(0f)] float patrolDuration = 8f;

    [Header("Patrol")]
    [SerializeField] NavMeshIdlePatrolMode patrolMode = NavMeshIdlePatrolMode.Waypoints;
    [Tooltip("Used when Patrol Mode is Waypoints. Not assigned on EntityNavBehaviorHost; ticked by the parent nav behavior.")]
    [SerializeField] NavMeshWaypointPatrolBehavior waypointPatrol;

    [Header("Random Radius")]
    [Tooltip("Horizontal radius for random patrol points (Random Radius mode only).")]
    [SerializeField, Min(0.1f)] float randomRadius = 5f;
    [Tooltip("Agent remaining distance at or below this counts as reaching a random point.")]
    [SerializeField, Min(0.05f)] float randomWaypointThreshold = 0.3f;
    [Tooltip("When true, the patrol anchor is locked to the entity position on first patrol entry.")]
    [SerializeField] bool anchorToSpawnOnEnter = true;
    [Tooltip("NavMesh sample search radius around each random point.")]
    [SerializeField, Min(0.1f)] float randomSampleRadius = 2f;

    float _idleRemaining;
    float _patrolRemaining;
    Vector3 _patrolCenter;
    bool _hasPatrolCenter;

    public bool IsEnabled => enableIdlePatrolCycle;

    public void ResolveReferences(MonoBehaviour owner, NavMeshWaypointPatrolBehavior fallback = null)
    {
        if (waypointPatrol == null)
            waypointPatrol = fallback != null ? fallback : owner?.GetComponent<NavMeshWaypointPatrolBehavior>();
    }

    public void EnterIdle(NavMeshAgent agent)
    {
        StopAgent(agent);
        _idleRemaining = idleDuration;
    }

    public void EnterPatrol(NavMeshAgent agent)
    {
        _patrolRemaining = patrolDuration;

        if (patrolMode == NavMeshIdlePatrolMode.Waypoints)
        {
            waypointPatrol?.ResetPatrol();
            if (agent != null)
                agent.isStopped = false;
            return;
        }

        if (anchorToSpawnOnEnter || !_hasPatrolCenter)
            LockPatrolCenter(agent);

        PickRandomDestination(agent);
    }

    public void CancelToIdle(NavMeshAgent agent)
    {
        StopAgent(agent);
        _idleRemaining = idleDuration;
        _patrolRemaining = 0f;
    }

    public NavMeshIdlePatrolTickResult TickIdle(
        float deltaTime,
        NavMeshAgent agent,
        FieldOfViewComponent fieldOfView,
        Vector3 origin)
    {
        if (TryCheckDetection(fieldOfView, origin))
            return NavMeshIdlePatrolTickResult.TargetDetected;

        if (idleDuration <= 0f)
            return NavMeshIdlePatrolTickResult.IdleExpired;

        _idleRemaining -= deltaTime;
        return _idleRemaining <= 0f
            ? NavMeshIdlePatrolTickResult.IdleExpired
            : NavMeshIdlePatrolTickResult.Continue;
    }

    public NavMeshIdlePatrolTickResult TickPatrol(
        NavMeshAgent agent,
        FieldOfViewComponent fieldOfView,
        Vector3 origin,
        float deltaTime)
    {
        if (TryCheckDetection(fieldOfView, origin))
            return NavMeshIdlePatrolTickResult.TargetDetected;

        _patrolRemaining -= deltaTime;
        if (_patrolRemaining <= 0f)
            return NavMeshIdlePatrolTickResult.PatrolExpired;

        switch (patrolMode)
        {
            case NavMeshIdlePatrolMode.Waypoints:
                waypointPatrol?.Tick(agent);
                break;

            case NavMeshIdlePatrolMode.RandomRadius:
                TickRandomRadiusPatrol(agent);
                break;
        }

        return NavMeshIdlePatrolTickResult.Continue;
    }

    /// <summary>Continuous waypoint patrol when the idle/patrol cycle is disabled (PatrolUntilChase default).</summary>
    public void TickContinuousPatrol(NavMeshAgent agent, FieldOfViewComponent fieldOfView, Vector3 origin)
    {
        if (patrolMode == NavMeshIdlePatrolMode.Waypoints)
            waypointPatrol?.Tick(agent);
        else
            TickRandomRadiusPatrol(agent);
    }

    public void ResetPatrolOnAggroLost()
    {
        if (patrolMode == NavMeshIdlePatrolMode.Waypoints)
            waypointPatrol?.ResetPatrol();
    }

    public static bool TryCheckDetection(FieldOfViewComponent fieldOfView, Vector3 origin)
    {
        return fieldOfView != null
            && fieldOfView.HasTarget
            && fieldOfView.IsTargetInDetectionRange(origin);
    }

    public static Vector3 SampleRandomPointInRadius(Vector3 center, float radius)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(0f, radius);
        return center + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
    }

    public static void PickRandomWaypoint(Vector3 patrolCenter, float radius, NavMeshAgent agent)
    {
        Vector3 target = SampleRandomPointInRadius(patrolCenter, radius);
        if (agent != null)
            agent.SetDestination(target);
    }

    void LockPatrolCenter(NavMeshAgent agent)
    {
        _patrolCenter = agent != null ? agent.transform.position : Vector3.zero;
        _hasPatrolCenter = true;
    }

    void PickRandomDestination(NavMeshAgent agent)
    {
        if (agent == null)
            return;

        agent.isStopped = false;
        agent.stoppingDistance = randomWaypointThreshold;

        Vector3 worldTarget = SampleRandomPointInRadius(_patrolCenter, randomRadius);

        if (NavMesh.SamplePosition(worldTarget, out NavMeshHit hit, randomSampleRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(worldTarget);
    }

    void TickRandomRadiusPatrol(NavMeshAgent agent)
    {
        if (agent == null)
            return;

        if (!_hasPatrolCenter)
            LockPatrolCenter(agent);

        agent.isStopped = false;
        agent.stoppingDistance = randomWaypointThreshold;

        if (!agent.pathPending && agent.remainingDistance <= randomWaypointThreshold)
            PickRandomDestination(agent);

        EntityNavChaseAttackSupport.SyncLocomotionFacing(agent, agent.gameObject);
    }

    static void StopAgent(NavMeshAgent agent)
    {
        if (agent == null)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }
}
