#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds <c>AttackReady</c> to entity combat animator controllers and wires
/// <see cref="EntityAttackAnimator.betweenAttackStateName"/> on combat prefabs.
/// </summary>
[InitializeOnLoad]
static class EntityCombatAnimationEditorSetup
{
    const string FighterControllerPath = "Assets/_project/_animation/EntityCombat_Fighter.controller";
    const string BowControllerPath = "Assets/_project/_animation/EntityCombat_Bow.controller";
    const string AttackReadyStateName = "AttackReady";
    const string MeleeBlockFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/09_Fighter/Stander@Fighter_Block.FBX";
    const string BowBlockFbxPath =
        "Assets/_assetStore/Shinabro/Platform_Animation/Animation/04_Bow/Stander@Bow_Block.FBX";

    static readonly string[] CombatPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
    };

    static EntityCombatAnimationEditorSetup()
    {
        EditorApplication.delayCall += EnsureEntityCombatAttackReadyAssets;
    }

    [MenuItem("Tools/Entities/Setup Entity Combat AttackReady Animation")]
    public static void EnsureEntityCombatAttackReadyAssetsMenu()
    {
        EnsureEntityCombatAttackReadyAssets();
    }

    static void EnsureEntityCombatAttackReadyAssets()
    {
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            FighterControllerPath,
            MeleeBlockFbxPath,
            "Fighter_Block_Loop");
        PlayerBowAnimationEditorSetup.EnsureAttackReadyStateOnController(
            BowControllerPath,
            BowBlockFbxPath,
            "Bow_Block_Loop");

        for (int i = 0; i < CombatPrefabPaths.Length; i++)
            WireCombatPrefabAttackAnimator(CombatPrefabPaths[i]);
    }

    static void WireCombatPrefabAttackAnimator(string prefabPath)
    {
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
}
#endif
