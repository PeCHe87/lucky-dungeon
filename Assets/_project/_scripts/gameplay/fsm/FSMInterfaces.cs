/// <summary>
/// Implement one class per state behaviour (Idle, Patrol, Chase, Attack, …).
/// Register it with StateHandlerRegistry.Register("Key", new MyHandler()) in the bootstrap.
///
/// params is a typed ScriptableObject configured on the AIStateData asset.
/// </summary>
public interface IStateHandler
{
    /// <summary>Called once when the FSM enters this state.</summary>
    void OnEnter(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params);

    /// <summary>Called every frame while this state is active.</summary>
    void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt);

    /// <summary>Called once when the FSM leaves this state.</summary>
    void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params);
}

/// <summary>
/// Implement one class per condition type (TimerExpired, TargetInRange, …).
/// Register it with ConditionEvaluatorRegistry.Register("Key", new MyEvaluator()).
///
/// params is a typed ScriptableObject configured on the Condition in a transition.
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>Returns true when the condition is satisfied.</summary>
    bool Evaluate(Condition condition, EntityBlackboard bb);
}
