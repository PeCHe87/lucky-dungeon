using UnityEngine;

static class FSMConditionResolve
{
    public static float ResolveFloat(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> parameters, string literalKey, float literalDefault, string bbKeyKey, out bool usedBlackboard)
    {
        var bbKey = FSMParamUtils.GetString(parameters, bbKeyKey, "");
        if (!string.IsNullOrEmpty(bbKey) && bb.TryGet<float>(bbKey, out var v))
        {
            usedBlackboard = true;
            return v;
        }

        usedBlackboard = false;
        return FSMParamUtils.GetFloat(parameters, literalKey, literalDefault);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TIMER EXPIRED
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// True when the blackboard float 'key' is >= 'duration'.
/// Pair with IdleStateHandler which increments "fsm.timer".
/// </summary>
public class TimerExpiredEvaluator : IConditionEvaluator
{
    public bool Evaluate(Condition condition, EntityBlackboard bb)
    {
        var @params = condition.parameters;
        var key = FSMParamUtils.GetString(@params, "key", "fsm.timer");
        var duration = FSMConditionResolve.ResolveFloat(bb, @params, literalKey: "duration", literalDefault: 3f, bbKeyKey: "durationKey", out var usedBlackboard);

        if (condition.debugLog)
            Debug.Log($"[FSM] {bb.name} Condition={condition.type} value={duration} source={(usedBlackboard ? "blackboard" : "literal")}", bb);

        return bb.Get<float>(key) >= duration;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TARGET IN RANGE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// True when bb.Target is not null AND within 'range' units.
/// </summary>
public class TargetInRangeEvaluator : IConditionEvaluator
{
    public bool Evaluate(Condition condition, EntityBlackboard bb)
    {
        var @params = condition.parameters;
        if (bb.Target == null) return false;
        var range = FSMConditionResolve.ResolveFloat(bb, @params, literalKey: "range", literalDefault: 6f, bbKeyKey: "rangeKey", out var usedBlackboard);

        if (condition.debugLog)
            Debug.Log($"[FSM] {bb.name} Condition={condition.type} value={range} source={(usedBlackboard ? "blackboard" : "literal")}", bb);

        return Vector3.Distance(bb.Self.position, bb.Target.position) <= range;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TARGET LOST  (inverse of TargetInRange — useful for returning to Patrol)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// True when bb.Target is null OR farther than 'range' units.
/// Example: {"range":8.0}   (use a slightly larger value than TargetInRange to add hysteresis)
/// </summary>
public class TargetLostEvaluator : IConditionEvaluator
{
    public bool Evaluate(Condition condition, EntityBlackboard bb)
    {
        var @params = condition.parameters;
        if (bb.Target == null) return true;
        var range = FSMConditionResolve.ResolveFloat(bb, @params, literalKey: "range", literalDefault: 8f, bbKeyKey: "rangeKey", out var usedBlackboard);

        if (condition.debugLog)
            Debug.Log($"[FSM] {bb.name} Condition={condition.type} value={range} source={(usedBlackboard ? "blackboard" : "literal")}", bb);

        return Vector3.Distance(bb.Self.position, bb.Target.position) > range;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TARGET REACHED
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// True when bb.Target is within 'distance' units — i.e. close enough to stop.
/// </summary>
public class TargetReachedEvaluator : IConditionEvaluator
{
    public bool Evaluate(Condition condition, EntityBlackboard bb)
    {
        var @params = condition.parameters;
        if (bb.Target == null) return false;
        var distance = FSMConditionResolve.ResolveFloat(bb, @params, literalKey: "distance", literalDefault: 1f, bbKeyKey: "distanceKey", out var usedBlackboard);

        if (condition.debugLog)
            Debug.Log($"[FSM] {bb.name} Condition={condition.type} value={distance} source={(usedBlackboard ? "blackboard" : "literal")}", bb);

        return Vector3.Distance(bb.Self.position, bb.Target.position) <= distance;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// TARGET NULL
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// True when bb.Target == null. Useful for returning to Idle/Patrol after a target is cleared.
/// No paramsJson needed.
/// </summary>
public class TargetNullEvaluator : IConditionEvaluator
{
    public bool Evaluate(Condition condition, EntityBlackboard bb) => bb.Target == null;
}

// ─────────────────────────────────────────────────────────────────────────────
// BLACKBOARD BOOL
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// True when a blackboard bool key equals the expected value.
/// Useful for custom game events (e.g. enemy stunned by a dice face effect).
/// </summary>
public class BlackboardBoolEvaluator : IConditionEvaluator
{
    public bool Evaluate(Condition condition, EntityBlackboard bb)
    {
        var @params = condition.parameters;
        var key = FSMParamUtils.GetString(@params, "key", "");
        var expected = FSMParamUtils.GetBool(@params, "expected", true);
        return bb.Get<bool>(key) == expected;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ALWAYS TRUE  (useful as a fallback / default transition)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Always returns true. Use as the last transition in a state to guarantee an exit route.
/// No paramsJson needed.
/// </summary>
public class AlwaysTrueEvaluator : IConditionEvaluator
{
    public bool Evaluate(Condition condition, EntityBlackboard bb) => true;
}
