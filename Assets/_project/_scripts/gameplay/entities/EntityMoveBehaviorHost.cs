using UnityEngine;

public class EntityMoveBehaviorHost : MonoBehaviour, IMoveIntentProvider
{
    [Tooltip("MonoBehaviour implementing IEntityMoveBehavior (e.g. WaypointPatrolBehavior).")]
    [SerializeField] MonoBehaviour activeBehavior;

    IEntityMoveBehavior _strategy;

    void Awake()
    {
        ResolveStrategy();
    }

    void OnValidate()
    {
        if (activeBehavior != null && activeBehavior is not IEntityMoveBehavior)
            activeBehavior = null;
    }

    void ResolveStrategy()
    {
        _strategy = activeBehavior as IEntityMoveBehavior;
    }

    public Vector2 GetMoveIntent()
    {
        if (_strategy == null && activeBehavior != null)
            ResolveStrategy();

        return _strategy != null ? _strategy.GetMoveIntent() : Vector2.zero;
    }

    public void SetActiveBehavior(IEntityMoveBehavior behavior)
    {
        _strategy = behavior;
        activeBehavior = behavior as MonoBehaviour;
    }

    public void SetActiveBehavior(MonoBehaviour behavior)
    {
        if (behavior != null && behavior is not IEntityMoveBehavior)
        {
            Debug.LogWarning($"{behavior.name} does not implement IEntityMoveBehavior.", behavior);
            return;
        }

        activeBehavior = behavior;
        _strategy = behavior as IEntityMoveBehavior;
    }
}
