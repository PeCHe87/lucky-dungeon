using UnityEngine;

/// <summary>
/// Registers all IStateHandlers and IConditionEvaluators before any scene loads.
/// 
/// HOW TO ADD A NEW STATE:
///   1. Create MyStateHandler : IStateHandler in a new file under Handlers/
///   2. Add: StateHandlerRegistry.Register("MyState", new MyStateHandler());
///   3. Create a ScriptableObject asset (Assets → Create → FSM → State)
///   4. Set handlerType = "MyState" and configure handlerParamsJson
///
/// HOW TO ADD A NEW CONDITION:
///   1. Create MyEvaluator : IConditionEvaluator in a new file under Conditions/
///   2. Add: ConditionEvaluatorRegistry.Register("MyCondition", new MyEvaluator());
///   3. Use type = "MyCondition" in any StateTransition inside a ScriptableObject
/// </summary>
public static class AISystemBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterAll()
    {
        RegisterHandlers();
        RegisterEvaluators();
        Debug.Log("[FSM] All handlers and evaluators registered.");
    }

    // ── State Handlers ───────────────────────────────────────────────────────
    static void RegisterHandlers()
    {
        StateHandlerRegistry.Register("Idle",   new IdleStateHandler());
        StateHandlerRegistry.Register("Patrol", new PatrolStateHandler());
        StateHandlerRegistry.Register("Chase",  new ChaseStateHandler());

        // ← Add new handlers here as the game grows
        // StateHandlerRegistry.Register("Attack",  new AttackStateHandler());
        // StateHandlerRegistry.Register("Retreat", new RetreatStateHandler());
        // StateHandlerRegistry.Register("Stunned", new StunnedStateHandler());
    }

    // ── Condition Evaluators ─────────────────────────────────────────────────
    static void RegisterEvaluators()
    {
        ConditionEvaluatorRegistry.Register("TimerExpired",   new TimerExpiredEvaluator());
        ConditionEvaluatorRegistry.Register("TargetInRange",  new TargetInRangeEvaluator());
        ConditionEvaluatorRegistry.Register("TargetLost",     new TargetLostEvaluator());
        ConditionEvaluatorRegistry.Register("TargetReached",  new TargetReachedEvaluator());
        ConditionEvaluatorRegistry.Register("TargetNull",     new TargetNullEvaluator());
        ConditionEvaluatorRegistry.Register("BlackboardBool", new BlackboardBoolEvaluator());
        ConditionEvaluatorRegistry.Register("AlwaysTrue",     new AlwaysTrueEvaluator());

        // ← Add new evaluators here
        // ConditionEvaluatorRegistry.Register("HPBelow",       new HPBelowEvaluator());
        // ConditionEvaluatorRegistry.Register("RandomChance",  new RandomChanceEvaluator());
    }
}
