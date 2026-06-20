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
    const string ChaserPrefabPath = "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab";
    const string ControllerPath = "Assets/_project/_animation/EntityCombat.controller";
    const string TakeDamageStatePath = "Assets/_project/_fsm/states/State_TakeDamage.asset";
    const string DieStatePath = "Assets/_project/_fsm/states/State_Die.asset";
    const string IdleCombatStatePath = "Assets/_project/_fsm/states/State_Idle_Combat.asset";

    static EntityCombatPrefabSetup()
    {
        EditorApplication.delayCall += EnsureCombatEntityPrefabs;
    }

    [MenuItem("Tools/Entities/Setup Base Combat Entity Prefab")]
    public static void EnsureCombatEntityPrefabMenu()
    {
        EnsureCombatEntityPrefabs();
    }

    static void EnsureCombatEntityPrefabs()
    {
        EnsureCombatEntityPrefab(PrefabPath);
        EnsureCombatEntityPrefab(ChaserPrefabPath);
    }

    static void EnsureCombatEntityPrefab(string prefabPath)
    {
        EntityFsmAnimationProfile.EnsureDefaultAssetExists();

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponent<AIStateMachine>() == null)
            {
                Debug.LogWarning("[EntityCombatPrefabSetup] Skipped: no AIStateMachine on " + prefabPath);
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

            if (root.GetComponent<PushbackReceiver>() == null)
            {
                root.AddComponent<PushbackReceiver>();
                changed = true;
            }

            var shake = root.GetComponentInChildren<EntityDamagedShake>(true);
            if (shake != null && shake.enabled)
            {
                shake.enabled = false;
                changed = true;
            }

            var attackerFacing = root.GetComponent<DamageAttackerFacing>();
            if (attackerFacing == null)
            {
                attackerFacing = root.AddComponent<DamageAttackerFacing>();
                changed = true;
            }

            var attackerFacingSo = new SerializedObject(attackerFacing);
            if (!attackerFacingSo.FindProperty("faceAttackerOnDamage").boolValue)
            {
                attackerFacingSo.FindProperty("faceAttackerOnDamage").boolValue = true;
                changed = true;
            }

            Transform body = root.transform.Find("body");
            if (body != null)
            {
                var visualPivot = attackerFacingSo.FindProperty("visualPivot");
                if (visualPivot.objectReferenceValue != body)
                {
                    visualPivot.objectReferenceValue = body;
                    changed = true;
                }
            }

            if (attackerFacingSo.ApplyModifiedPropertiesWithoutUndo())
                changed = true;

            if (!changed)
                return;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log("[EntityCombatPrefabSetup] Updated " + prefabPath);
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
