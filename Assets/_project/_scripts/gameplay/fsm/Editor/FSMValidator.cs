#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FSMValidator
{
    [MenuItem("Tools/FSM/Validate All States")]
    public static void ValidateAllStates()
    {
        var guids = AssetDatabase.FindAssets("t:AIStateData");
        int warnCount = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var state = AssetDatabase.LoadAssetAtPath<AIStateData>(path);
            if (state == null) continue;

            warnCount += ValidateState(state);
        }

        Debug.Log($"[FSM Validator] Completed. Warnings: {warnCount}. States scanned: {guids.Length}");
    }

    private static int ValidateState(AIStateData state)
    {
        int warns = 0;

        void Warn(string msg)
        {
            warns++;
            Debug.LogWarning($"[FSM Validator] {msg}", state);
            EditorGUIUtility.PingObject(state);
        }

        if (string.IsNullOrWhiteSpace(state.stateId))
            Warn($"State asset '{state.name}' has empty stateId.");

        if (string.IsNullOrWhiteSpace(state.handlerType))
            Warn($"State '{state.name}' has empty handlerType.");

        if (state.handlerParams == null)
            Warn($"State '{state.name}' handlerParams list is null.");

        if (state.handlerType == "Patrol")
        {
            WarnIfMissingFloat(state, "radius");
            WarnIfMissingFloat(state, "speed");
            WarnIfMissingFloat(state, "waypointThreshold");
        }
        else if (state.handlerType == "Chase")
        {
            WarnIfMissingFloat(state, "speed");
            WarnIfMissingFloat(state, "stoppingDistance");
        }

        if (state.transitions == null)
            return warns;

        for (int ti = 0; ti < state.transitions.Count; ti++)
        {
            var t = state.transitions[ti];
            if (t == null)
            {
                Warn($"State '{state.name}' transition[{ti}] is null.");
                continue;
            }

            if (t.targetState == null)
                Warn($"State '{state.name}' transition[{ti}] '{t.label}' has null targetState.");

            if (t.conditions == null || t.conditions.Count == 0)
                Warn($"State '{state.name}' transition[{ti}] '{t.label}' has no conditions (will never fire unless you add AlwaysTrue).");

            for (int ci = 0; ci < (t.conditions?.Count ?? 0); ci++)
            {
                var c = t.conditions[ci];
                if (c == null)
                {
                    Warn($"State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(c.type))
                {
                    Warn($"State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] has empty type.");
                    continue;
                }

                bool requiresParams =
                    c.type == "TimerExpired" ||
                    c.type == "TargetInRange" ||
                    c.type == "TargetLost" ||
                    c.type == "TargetReached" ||
                    c.type == "BlackboardBool";

                if (requiresParams && (c.parameters == null || c.parameters.Count == 0))
                    Warn($"State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] type={c.type} requires parameters but the list is empty.");

                if (c.type == "TimerExpired")
                {
                    WarnIfMissingString(state, t, c, ti, ci, "key");
                    // duration OR durationKey is required (durationKey reads from blackboard)
                    WarnIfMissingFloatOrKey(state, t, c, ti, ci, "duration", "durationKey");
                }
                else if (c.type == "TargetInRange" || c.type == "TargetLost")
                {
                    // range OR rangeKey is required (rangeKey reads from blackboard)
                    WarnIfMissingFloatOrKey(state, t, c, ti, ci, "range", "rangeKey");
                }
                else if (c.type == "TargetReached")
                {
                    // distance OR distanceKey is required (distanceKey reads from blackboard)
                    WarnIfMissingFloatOrKey(state, t, c, ti, ci, "distance", "distanceKey");
                }
                else if (c.type == "BlackboardBool")
                {
                    WarnIfMissingString(state, t, c, ti, ci, "key");
                    WarnIfMissingBool(state, t, c, ti, ci, "expected");
                }
            }
        }

        return warns;
    }

    private static void WarnIfMissingFloat(AIStateData state, string key)
    {
        if (!FSMParamUtils.TryGet(state.handlerParams, key, out var p) || p.type != FSMParamType.Float)
            Debug.LogWarning($"[FSM Validator] State '{state.name}' handlerType={state.handlerType} expects float param '{key}'.", state);
    }

    private static void WarnIfMissingString(AIStateData state, StateTransition t, Condition c, int ti, int ci, string key)
    {
        if (!FSMParamUtils.TryGet(c.parameters, key, out var p) || p.type != FSMParamType.String)
            Debug.LogWarning($"[FSM Validator] State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] type={c.type} expects string param '{key}'.", state);
    }

    private static void WarnIfMissingFloat(AIStateData state, StateTransition t, Condition c, int ti, int ci, string key)
    {
        if (!FSMParamUtils.TryGet(c.parameters, key, out var p) || p.type != FSMParamType.Float)
            Debug.LogWarning($"[FSM Validator] State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] type={c.type} expects float param '{key}'.", state);
    }

    private static void WarnIfMissingBool(AIStateData state, StateTransition t, Condition c, int ti, int ci, string key)
    {
        if (!FSMParamUtils.TryGet(c.parameters, key, out var p) || p.type != FSMParamType.Bool)
            Debug.LogWarning($"[FSM Validator] State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] type={c.type} expects bool param '{key}'.", state);
    }

    private static void WarnIfMissingFloatOrKey(AIStateData state, StateTransition t, Condition c, int ti, int ci, string floatKey, string bbKey)
    {
        var hasFloat = FSMParamUtils.TryGet(c.parameters, floatKey, out var pf) && pf.type == FSMParamType.Float;
        var hasKey = FSMParamUtils.TryGet(c.parameters, bbKey, out var pk) && pk.type == FSMParamType.String && !string.IsNullOrEmpty(pk.stringValue);

        if (!hasFloat && !hasKey)
            Debug.LogWarning($"[FSM Validator] State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] type={c.type} expects either float '{floatKey}' or string '{bbKey}'.", state);

        if (hasFloat && hasKey)
            Debug.LogWarning($"[FSM Validator] State '{state.name}' transition[{ti}] '{t.label}' condition[{ci}] type={c.type} has both '{floatKey}' and '{bbKey}'. '{bbKey}' will override.", state);
    }
}
#endif

