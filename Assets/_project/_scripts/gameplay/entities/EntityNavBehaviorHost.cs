using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EntityNavBehaviorHost : MonoBehaviour
{
    [Tooltip("If true, warps the agent onto the NavMesh on Start when not already placed on it.")]
    [SerializeField] bool warpToNavMeshOnStart = true;
    [SerializeField] float warpSearchRadius = 4f;
    [Tooltip("MonoBehaviour implementing IEntityNavBehavior (e.g. NavMeshWaypointPatrolBehavior, NavMeshDetectChaseBehavior, NavMeshPatrolUntilChaseBehavior).")]
    [SerializeField] MonoBehaviour activeBehavior;

    NavMeshAgent _agent;
    IEntityNavBehavior _strategy;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        ResolveStrategy();
    }

    void Start()
    {
        if (warpToNavMeshOnStart && _agent != null && !_agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, warpSearchRadius, NavMesh.AllAreas))
                _agent.Warp(hit.position);
        }
    }

    void OnValidate()
    {
        if (activeBehavior != null && activeBehavior is not IEntityNavBehavior)
            activeBehavior = null;
    }

    void ResolveStrategy()
    {
        _strategy = activeBehavior as IEntityNavBehavior;
    }

    /// <summary>True if this component is the one receiving Tick() from the host.</summary>
    public bool IsActiveNavBehavior(Component behavior) => activeBehavior == behavior;

    void Update()
    {
        if (_strategy == null && activeBehavior != null)
            ResolveStrategy();

        if (_strategy != null && _agent != null && _agent.isOnNavMesh)
            _strategy.Tick(_agent);
    }

    public void SetActiveBehavior(IEntityNavBehavior behavior)
    {
        _strategy = behavior;
        activeBehavior = behavior as MonoBehaviour;
    }

    public void SetActiveBehavior(MonoBehaviour behavior)
    {
        if (behavior != null && behavior is not IEntityNavBehavior)
        {
            Debug.LogWarning($"{behavior.name} does not implement IEntityNavBehavior.", behavior);
            return;
        }

        activeBehavior = behavior;
        _strategy = behavior as IEntityNavBehavior;
    }
}
