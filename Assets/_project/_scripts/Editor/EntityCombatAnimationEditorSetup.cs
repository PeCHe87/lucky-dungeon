#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Adds <c>AttackReady</c> / <c>Walk</c> to entity combat animator controllers and wires
/// related fields on combat prefabs.
/// </summary>
[InitializeOnLoad]
static class EntityCombatAnimationEditorSetup
{
    const string FighterControllerPath = "Assets/_project/_animation/EntityCombat_Fighter.controller";
    const string BowControllerPath = "Assets/_project/_animation/EntityCombat_Bow.controller";
    const string AttackReadyStateName = "AttackReady";
    const string WalkStateName = "Walk";
    const string MeleeBlockFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Block.FBX";
    const string BowBlockFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Block.FBX";
    const string MeleeWalkFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Walk_F.FBX";
    const string MeleeWalkClipName = "Fighter_Walk_F";

    static readonly string[] CombatPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
        "Assets/_project/_prefabs/entities/enemy_melee_patroller.prefab",
    };

    static readonly string[] MeleeLocomotionPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/enemy_melee_patroller.prefab",
    };

    static EntityCombatAnimationEditorSetup()
    {
        EditorApplication.delayCall += EnsureEntityCombatAnimationAssets;
    }

    [MenuItem("Tools/Entities/Setup Entity Combat AttackReady Animation")]
    public static void EnsureEntityCombatAttackReadyAssetsMenu()
    {
        EnsureEntityCombatAnimationAssets();
    }

    [MenuItem("Tools/Entities/Setup Entity Combat Walk Animation")]
    public static void EnsureEntityCombatWalkAssetsMenu()
    {
        EnsureEntityCombatAnimationAssets();
    }

    static void EnsureEntityCombatAnimationAssets()
    {
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            FighterControllerPath,
            MeleeBlockFbxPath,
            "Fighter_Block_Loop");
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            BowControllerPath,
            BowBlockFbxPath,
            "Bow_Block_Loop");

        EnsureWalkStateOnController(FighterControllerPath, MeleeWalkFbxPath, MeleeWalkClipName);

        for (int i = 0; i < CombatPrefabPaths.Length; i++)
            WireCombatPrefabAttackAnimator(CombatPrefabPaths[i]);

        for (int i = 0; i < MeleeLocomotionPrefabPaths.Length; i++)
            WireMeleePrefabWalkLocomotion(MeleeLocomotionPrefabPaths[i]);
    }

    static void EnsureWalkStateOnController(string controllerPath, string fbxPath, string clipName)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            return;

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        for (int i = 0; i < root.states.Length; i++)
        {
            if (string.Equals(root.states[i].state.name, WalkStateName, StringComparison.Ordinal))
                return;
        }

        AnimationClip clip = LoadClip(fbxPath, clipName);
        if (clip == null)
            return;

        var state = root.AddState(WalkStateName);
        state.motion = clip;
        state.speed = 1f;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EntityCombatAnimationEditorSetup] Added '{WalkStateName}' to {controllerPath}");
    }

    static AnimationClip LoadClip(string fbxPath, string clipName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip
                && string.Equals(clip.name, clipName, StringComparison.Ordinal)
                && !clip.name.StartsWith("__", StringComparison.Ordinal))
            {
                return clip;
            }
        }

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__", StringComparison.Ordinal))
                return clip;
        }

        Debug.LogWarning($"[EntityCombatAnimationEditorSetup] Clip '{clipName}' not found at {fbxPath}");
        return null;
    }

    static void WireCombatPrefabAttackAnimator(string prefabPath)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var attackAnimator = root.GetComponentInChildren<EntityAttackAnimator>(true);
            if (attackAnimator == null)
                return;

            var so = new SerializedObject(attackAnimator);
            var betweenAttack = so.FindProperty("betweenAttackStateName");
            if (betweenAttack.stringValue == AttackReadyStateName)
                return;

            betweenAttack.stringValue = AttackReadyStateName;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[EntityCombatAnimationEditorSetup] Set betweenAttackStateName on {prefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WireMeleePrefabWalkLocomotion(string prefabPath)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var locomotion = root.GetComponentInChildren<EntityNavLocomotionAnimator>(true);
            if (locomotion == null)
                return;

            var so = new SerializedObject(locomotion);
            var walkState = so.FindProperty("walkStateName");
            if (walkState == null || walkState.stringValue == WalkStateName)
                return;

            walkState.stringValue = WalkStateName;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Debug.Log($"[EntityCombatAnimationEditorSetup] Set walkStateName on {prefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
