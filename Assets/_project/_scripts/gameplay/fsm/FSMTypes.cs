using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single evaluable condition.
/// 'type' maps to a registered IConditionEvaluator.
/// 'parameters' provides inline configuration for that evaluator.
/// </summary>
[Serializable]
public class Condition
{
    [Tooltip("Must match a registered evaluator key, e.g. 'TimerExpired', 'TargetInRange'")]
    public string type;

    [Tooltip("Enable to log the resolved condition value and its source (blackboard vs literal) every time it is evaluated.")]
    public bool debugLog;

    [Tooltip("Inline parameters specific to this condition type")]
    public List<FSMParam> parameters = new();
}

/// <summary>
/// A guarded transition from the current state to a target state.
/// All conditions must be true for the transition to fire (AND logic).
/// </summary>
[Serializable]
public class StateTransition
{
    [Tooltip("Designer-facing label shown in the Inspector")]
    public string label;

    [Tooltip("All conditions must evaluate true to fire this transition")]
    public List<Condition> conditions = new();

    [Tooltip("The state to transition into when conditions are met")]
    public AIStateData targetState;
}
