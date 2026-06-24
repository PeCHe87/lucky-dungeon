#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wires ranged combat prefab components without hand-editing prefab YAML or script .meta files.
/// </summary>
[InitializeOnLoad]
public static class EntityRangedCombatPrefabSetup
{
    const string RangePrefabPath =
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab";
    const string EntityBowControllerPath =
        "Assets/_project/_animation/EntityCombat_Bow.controller";
    const string EntityAttackStateName = "Attack1";
    const string EntityAttackClipName = "Bow_Attack_Fire";
    const string FireEventFunctionName = "OnRangedFireFrame";

    static EntityRangedCombatPrefabSetup()
    {
        EditorApplication.delayCall += EnsureRangeCombatPrefab;
    }

    [MenuItem("Tools/Entities/Setup Ranged Combat Entity Prefab")]
    public static void EnsureRangeCombatPrefabMenu()
    {
        EnsureRangeCombatPrefab();
    }

    static void EnsureRangeCombatPrefab()
    {
        RangedAttackAnimationEventSetup.EnsureRangedFireAnimationEvents();
        ValidateEntityBowAttackFireEvents();

        GameObject root = PrefabUtility.LoadPrefabContents(RangePrefabPath);
        try
        {
            if (root.GetComponent<WeaponHolder>() == null)
            {
                Debug.LogWarning("[EntityRangedCombatPrefabSetup] Skipped: no WeaponHolder on " + RangePrefabPath);
                return;
            }

            bool changed = false;

            if (root.GetComponent<EntityWeaponDetectionRadiusSync>() == null)
            {
                root.AddComponent<EntityWeaponDetectionRadiusSync>();
                changed = true;
            }

            var weaponHolder = root.GetComponent<WeaponHolder>();
            var rangedWeapon = root.GetComponentInChildren<RangedWeapon>(true);
            if (rangedWeapon != null)
            {
                var weaponHolderSo = new SerializedObject(weaponHolder);
                var startingWeapon = weaponHolderSo.FindProperty("startingWeapon");
                var rangedWeaponSlot = weaponHolderSo.FindProperty("rangedWeapon");
                var rangedMb = rangedWeapon as MonoBehaviour;

                if (startingWeapon.objectReferenceValue != rangedMb)
                {
                    startingWeapon.objectReferenceValue = rangedMb;
                    changed = true;
                }

                if (rangedWeaponSlot.objectReferenceValue != rangedMb)
                {
                    rangedWeaponSlot.objectReferenceValue = rangedMb;
                    changed = true;
                }

                if (weaponHolderSo.ApplyModifiedPropertiesWithoutUndo())
                    changed = true;
            }

            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                var fireReceiver = animator.GetComponent<RangedAttackAnimationEventReceiver>();
                if (fireReceiver == null)
                {
                    fireReceiver = animator.gameObject.AddComponent<RangedAttackAnimationEventReceiver>();
                    changed = true;
                }

                var receiverSo = new SerializedObject(fireReceiver);
                var receiverRanged = receiverSo.FindProperty("rangedWeapon");
                var receiverHolder = receiverSo.FindProperty("weaponHolder");
                var rangedMb = rangedWeapon as MonoBehaviour;

                if (receiverRanged.objectReferenceValue != rangedMb)
                {
                    receiverRanged.objectReferenceValue = rangedMb;
                    changed = true;
                }

                if (receiverHolder.objectReferenceValue != weaponHolder)
                {
                    receiverHolder.objectReferenceValue = weaponHolder;
                    changed = true;
                }

                if (receiverSo.ApplyModifiedPropertiesWithoutUndo())
                    changed = true;
            }

            if (rangedWeapon != null)
            {
                var rangedSo = new SerializedObject(rangedWeapon);
                LayerMask enemyHitLayers = CombatHitLayers.EnemyRangedProjectile;
                var hitLayers = rangedSo.FindProperty("hitLayers");
                if (hitLayers.intValue != enemyHitLayers.value)
                {
                    hitLayers.intValue = enemyHitLayers.value;
                    changed = true;
                }

                Transform bowVisual = FindChildByName(root.transform, "BowBasic");
                if (bowVisual != null)
                {
                    var visuals = rangedSo.FindProperty("equippedVisualRoots");
                    if (visuals.arraySize != 1 || visuals.GetArrayElementAtIndex(0).objectReferenceValue != bowVisual.gameObject)
                    {
                        visuals.arraySize = 1;
                        visuals.GetArrayElementAtIndex(0).objectReferenceValue = bowVisual.gameObject;
                        changed = true;
                    }
                }

                if (rangedSo.ApplyModifiedPropertiesWithoutUndo())
                    changed = true;
            }

            if (!changed)
                return;

            PrefabUtility.SaveAsPrefabAsset(root, RangePrefabPath);
            Debug.Log("[EntityRangedCombatPrefabSetup] Updated " + RangePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    static void ValidateEntityBowAttackFireEvents()
    {
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(EntityBowControllerPath);
        if (controller == null)
            return;

        var animatorController = controller as UnityEditor.Animations.AnimatorController;
        if (animatorController == null)
            return;

        foreach (var layer in animatorController.layers)
        {
            foreach (var state in layer.stateMachine.states)
            {
                if (!string.Equals(state.state.name, EntityAttackStateName, StringComparison.Ordinal))
                    continue;

                AnimationClip clip = state.state.motion as AnimationClip;
                if (clip == null)
                {
                    Debug.LogWarning(
                        $"[EntityRangedCombatPrefabSetup] {EntityBowControllerPath} state '{EntityAttackStateName}' has no AnimationClip motion.",
                        animatorController);
                    return;
                }

                if (!string.Equals(clip.name, EntityAttackClipName, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        $"[EntityRangedCombatPrefabSetup] {EntityBowControllerPath} state '{EntityAttackStateName}' uses clip '{clip.name}', expected '{EntityAttackClipName}'. " +
                        $"Ensure '{FireEventFunctionName}' exists on the clip actually assigned to Attack1.",
                        clip);
                }

                bool hasFireEvent = false;
                foreach (AnimationEvent animationEvent in AnimationUtility.GetAnimationEvents(clip))
                {
                    if (animationEvent.functionName == FireEventFunctionName)
                    {
                        hasFireEvent = true;
                        break;
                    }
                }

                if (!hasFireEvent)
                {
                    Debug.LogWarning(
                        $"[EntityRangedCombatPrefabSetup] Clip '{clip.name}' used by '{EntityAttackStateName}' has no '{FireEventFunctionName}' event. " +
                        "Run Knight Undead/Combat/Setup Ranged Fire Animation Events.",
                        clip);
                }

                return;
            }
        }
    }
}
#endif
