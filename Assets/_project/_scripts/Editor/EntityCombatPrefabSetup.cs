#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wires <see cref="EntityFsmAnimator"/>, combat animation profile, and animator controller on
/// <c>base_combat_entity</c> without hand-editing prefab YAML or script .meta files.
/// </summary>
[InitializeOnLoad]
public static class EntityCombatPrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/entities/base_combat_entity.prefab";
    const string ControllerPath = "Assets/_project/_animation/EntityCombat.controller";
    const string TakeDamageStatePath = "Assets/_project/_fsm/states/State_TakeDamage.asset";
    const string DieStatePath = "Assets/_project/_fsm/states/State_Die.asset";
    const string IdleCombatStatePath = "Assets/_project/_fsm/states/State_Idle_Combat.asset";

    static EntityCombatPrefabSetup()
    {
        EditorApplication.delayCall += EnsureCombatEntityPrefab;
    }

    [MenuItem("Tools/Entities/Setup Base Combat Entity Prefab")]
    public static void EnsureCombatEntityPrefabMenu()
    {
        EnsureCombatEntityPrefab();
    }

    static void EnsureCombatEntityPrefab()
    {
        EntityFsmAnimationProfile.EnsureDefaultAssetExists();

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<AIStateMachine>() == null)
            {
                Debug.LogWarning("[EntityCombatPrefabSetup] Skipped: no AIStateMachine on " + PrefabPath);
                return;
            }

            bool changed = false;

            var fsmAnimator = root.GetComponent<EntityFsmAnimator>();
            if (fsmAnimator == null)
            {
                fsmAnimator = root.AddComponent<EntityFsmAnimator>();
                changed = true;
            }

            var stateMachine = root.GetComponent<AIStateMachine>();
            var profile = AssetDatabase.LoadAssetAtPath<EntityFsmAnimationProfile>(
                EntityFsmAnimationProfile.DefaultAssetPath);
            var animator = root.GetComponentInChildren<Animator>(true);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);

            if (animator != null && controller != null && animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                changed = true;
            }

            changed |= SetSerializedReference(fsmAnimator, "stateMachine", stateMachine);
            changed |= SetSerializedReference(fsmAnimator, "animator", animator);
            changed |= SetSerializedReference(fsmAnimator, "profile", profile);

            var health = root.GetComponent<CombatEntityHealth>();
            if (health != null)
            {
                var takeDamage = AssetDatabase.LoadAssetAtPath<AIStateData>(TakeDamageStatePath);
                var die = AssetDatabase.LoadAssetAtPath<AIStateData>(DieStatePath);
                changed |= SetSerializedReference(health, "takeDamageState", takeDamage);
                changed |= SetSerializedReference(health, "dieState", die);
                changed |= SetSerializedReference(health, "stateMachine", stateMachine);

                var idleCombat = AssetDatabase.LoadAssetAtPath<AIStateData>(IdleCombatStatePath);
                if (stateMachine != null && stateMachine.initialState == null && idleCombat != null)
                {
                    stateMachine.initialState = idleCombat;
                    changed = true;
                }
            }

            var shake = root.GetComponentInChildren<EntityDamagedShake>(true);
            if (shake != null && shake.enabled)
            {
                shake.enabled = false;
                changed = true;
            }

            if (!changed)
                return;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[EntityCombatPrefabSetup] Updated " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool SetSerializedReference(Object target, string propertyName, Object value)
    {
        if (target == null)
            return false;

        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop == null)
            return false;

        if (prop.objectReferenceValue == value)
            return false;

        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }
}
#endif
