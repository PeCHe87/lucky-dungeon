#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures listed enemy combat prefabs have <see cref="EntityCombatVfx"/> with empty
/// damage/death VFX slots (assign prefabs in the inspector). Never touches the player.
/// </summary>
[InitializeOnLoad]
public static class EntityCombatVfxPrefabSetup
{
    const string SetupDonePrefKey = "LuckyDungeon.EntityCombatVfxPrefabSetupDone.v1";

    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
        "Assets/_project/_prefabs/entities/enemy_melee_patroller.prefab",
    };

    static EntityCombatVfxPrefabSetup()
    {
        EditorApplication.delayCall += TryAutoSetupOnce;
    }

    static void TryAutoSetupOnce()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryAutoSetupOnce;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    [MenuItem("Lucky Dungeon/Setup/Entity Combat VFX Prefabs")]
    public static void SetupFromMenu()
    {
        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static bool SetupAll()
    {
        int updated = 0;
        for (int i = 0; i < EnemyPrefabPaths.Length; i++)
        {
            if (SetupPrefab(EnemyPrefabPaths[i]))
                updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EntityCombatVfxPrefabSetup] Ensured EntityCombatVfx on {updated}/{EnemyPrefabPaths.Length} enemy prefabs.");
        return updated > 0;
    }

    static bool SetupPrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogWarning($"[EntityCombatVfxPrefabSetup] Missing prefab: {path}");
            return false;
        }

        try
        {
            if (root.GetComponent<PlayerDeathHandler>() != null)
            {
                Debug.LogWarning($"[EntityCombatVfxPrefabSetup] Skipping player-like prefab: {path}");
                return false;
            }

            var vfx = root.GetComponent<EntityCombatVfx>();
            if (vfx == null)
                vfx = root.AddComponent<EntityCombatVfx>();

            WireVfx(vfx, root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WireVfx(EntityCombatVfx vfx, GameObject root)
    {
        var so = new SerializedObject(vfx);
        so.FindProperty("health").objectReferenceValue = root.GetComponent<CombatEntityHealth>();
        so.FindProperty("spawnAnchor").objectReferenceValue = root.transform;
        so.FindProperty("damageVfxPrefab").objectReferenceValue = null;
        so.FindProperty("deathVfxPrefab").objectReferenceValue = null;
        so.FindProperty("damageVfxDestroyDelay").floatValue = 2f;
        so.FindProperty("deathVfxDestroyDelay").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(vfx);
    }
}
#endif
