using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global registry mapping string keys to IStateHandler instances.
/// Populated once at startup by AISystemBootstrap.
/// </summary>
public static class StateHandlerRegistry
{
    private static readonly Dictionary<string, IStateHandler> _handlers = new();

    public static void Register(string key, IStateHandler handler)
    {
        if (_handlers.ContainsKey(key))
            Debug.LogWarning($"[FSM] StateHandler '{key}' is being overwritten.");
        _handlers[key] = handler;
    }

    /// <summary>Returns the handler for the given key, or null (with a log error).</summary>
    public static IStateHandler Get(string key)
    {
        if (_handlers.TryGetValue(key, out var handler)) return handler;
        Debug.LogError($"[FSM] No StateHandler registered for key: '{key}'. " +
                       "Did you forget to register it in AISystemBootstrap?");
        return null;
    }
}

/// <summary>
/// Global registry mapping string keys to IConditionEvaluator instances.
/// Populated once at startup by AISystemBootstrap.
/// </summary>
public static class ConditionEvaluatorRegistry
{
    private static readonly Dictionary<string, IConditionEvaluator> _evaluators = new();

    public static void Register(string key, IConditionEvaluator evaluator)
    {
        if (_evaluators.ContainsKey(key))
            Debug.LogWarning($"[FSM] ConditionEvaluator '{key}' is being overwritten.");
        _evaluators[key] = evaluator;
    }

    /// <summary>Evaluates a Condition against the blackboard. Returns false if evaluator not found.</summary>
    public static bool Evaluate(Condition condition, EntityBlackboard bb)
    {
        if (_evaluators.TryGetValue(condition.type, out var evaluator))
            return evaluator.Evaluate(condition, bb);

        Debug.LogError($"[FSM] No ConditionEvaluator registered for key: '{condition.type}'. " +
                       "Did you forget to register it in AISystemBootstrap?");
        return false;
    }
}
