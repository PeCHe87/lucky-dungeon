#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates <see cref="State_Attack"/> and wires Chase/Attack FSM transitions.
/// </summary>
[InitializeOnLoad]
public static class EntityFsmAttackSetup
{
    const string AttackStatePath = "Assets/_project/_fsm/states/State_Attack.asset";
    const string ChaseStatePath = "Assets/_project/_fsm/states/State_Chase.asset";
    const string PatrolStatePath = "Assets/_project/_fsm/states/State_Patrol.asset";
    const string IdleStatePath = "Assets/_project/_fsm/states/State_Idle.asset";

    static EntityFsmAttackSetup()
    {
        EditorApplication.delayCall += EnsureOnLoad;
    }

    static void EnsureOnLoad()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureOnLoad;
            return;
        }

        SetupFsmAttackState();
    }

    [MenuItem("Tools/Entities/Setup FSM Attack State")]
    public static void SetupFsmAttackStateMenu()
    {
        SetupFsmAttackState();
    }

    public static bool SetupFsmAttackState()
    {
        var attackState = AssetDatabase.LoadAssetAtPath<AIStateData>(AttackStatePath);
        var chaseState = AssetDatabase.LoadAssetAtPath<AIStateData>(ChaseStatePath);
        var patrolState = AssetDatabase.LoadAssetAtPath<AIStateData>(PatrolStatePath);
        var idleState = AssetDatabase.LoadAssetAtPath<AIStateData>(IdleStatePath);

        if (attackState == null || chaseState == null || patrolState == null || idleState == null)
        {
            Debug.LogWarning("[EntityFsmAttackSetup] Missing one or more FSM state assets.");
            return false;
        }

        bool changed = false;

        if (attackState.stateId != "Attack" || attackState.handlerType != "Attack")
        {
            attackState.stateId = "Attack";
            attackState.handlerType = "Attack";
            changed = true;
        }

        changed |= EnsureAttackStateTransitions(attackState, chaseState, patrolState, idleState);
        changed |= EnsureChaseStateTransitions(chaseState, attackState, patrolState, idleState);

        if (!changed)
            return false;

        EditorUtility.SetDirty(attackState);
        EditorUtility.SetDirty(chaseState);
        AssetDatabase.SaveAssets();
        Debug.Log("[EntityFsmAttackSetup] Updated FSM Attack/Chase transitions.");
        return true;
    }

    static bool EnsureChaseStateTransitions(
        AIStateData chaseState,
        AIStateData attackState,
        AIStateData patrolState,
        AIStateData idleState)
    {
        var desired = new[]
        {
            NewTransition("Attack when in weapon range", attackState, "TargetInAttackRange"),
            NewTransition("Back to Patrol when target lost", patrolState, "TargetLost", ("range", 12f)),
            NewTransition("Stop when target reached", idleState, "TargetReached", ("distance", 1.2f)),
        };

        return ReplaceTransitionsIfDifferent(chaseState, desired);
    }

    static bool EnsureAttackStateTransitions(
        AIStateData attackState,
        AIStateData chaseState,
        AIStateData patrolState,
        AIStateData idleState)
    {
        var desired = new[]
        {
            NewTransition("Back to Patrol when target lost", patrolState, "TargetLost", ("range", 12f)),
            NewTransition("Return to Idle when target cleared", idleState, "TargetNull"),
            NewTransition("Resume chase when out of attack range", chaseState, "TargetBeyondAttackRange"),
        };

        return ReplaceTransitionsIfDifferent(attackState, desired);
    }

    static StateTransition NewTransition(
        string label,
        AIStateData target,
        string conditionType,
        params (string key, float floatValue)[] parameters)
    {
        var transition = new StateTransition
        {
            label = label,
            targetState = target,
            conditions = new System.Collections.Generic.List<Condition>(),
        };

        var condition = new Condition { type = conditionType };
        if (parameters != null)
        {
            condition.parameters = new System.Collections.Generic.List<FSMParam>();
            for (int i = 0; i < parameters.Length; i++)
            {
                condition.parameters.Add(new FSMParam
                {
                    key = parameters[i].key,
                    type = FSMParamType.Float,
                    floatValue = parameters[i].floatValue,
                });
            }
        }

        transition.conditions.Add(condition);
        return transition;
    }

    static bool ReplaceTransitionsIfDifferent(AIStateData state, StateTransition[] desired)
    {
        if (state.transitions == null)
            state.transitions = new System.Collections.Generic.List<StateTransition>();

        if (state.transitions.Count == desired.Length)
        {
            bool same = true;
            for (int i = 0; i < desired.Length; i++)
            {
                if (state.transitions[i].targetState != desired[i].targetState)
                {
                    same = false;
                    break;
                }

                if (state.transitions[i].conditions == null
                    || state.transitions[i].conditions.Count == 0
                    || desired[i].conditions == null
                    || desired[i].conditions.Count == 0)
                {
                    same = false;
                    break;
                }

                if (state.transitions[i].conditions[0].type != desired[i].conditions[0].type)
                {
                    same = false;
                    break;
                }
            }

            if (same)
                return false;
        }

        state.transitions.Clear();
        for (int i = 0; i < desired.Length; i++)
            state.transitions.Add(desired[i]);
        return true;
    }
}
#endif
