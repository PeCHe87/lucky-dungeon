#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures listed enemy combat prefabs have <see cref="EnemyDeathLootDrop"/> wired to
/// <c>loot_base</c> with dropChance 0 (opt-in via inspector tables + chance).
/// Never touches the player prefab.
/// </summary>
[InitializeOnLoad]
public static class EnemyDeathLootDropPrefabSetup
{
    const string SetupDonePrefKey = "LuckyDungeon.EnemyDeathLootDropPrefabSetupDone.v1";
    const string LootPrefabPath = "Assets/_project/_prefabs/level/loot_base.prefab";

    static readonly string[] EnemyPrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
        "Assets/_project/_prefabs/entities/enemy_melee_patroller.prefab",
    };

    static EnemyDeathLootDropPrefabSetup()
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

    [MenuItem("Lucky Dungeon/Setup/Enemy Death Loot Drop Prefabs")]
    public static void SetupFromMenu()
    {
        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static bool SetupAll()
    {
        var lootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootPrefabPath);
        if (lootPrefab == null)
        {
            Debug.LogWarning($"[EnemyDeathLootDropPrefabSetup] Missing loot prefab: {LootPrefabPath}");
            return false;
        }

        if (lootPrefab.GetComponent<InteractableLoot>() == null)
        {
            Debug.LogWarning($"[EnemyDeathLootDropPrefabSetup] Loot prefab missing InteractableLoot: {LootPrefabPath}");
            return false;
        }

        int updated = 0;
        for (int i = 0; i < EnemyPrefabPaths.Length; i++)
        {
            if (SetupPrefab(EnemyPrefabPaths[i], lootPrefab))
                updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyDeathLootDropPrefabSetup] Ensured EnemyDeathLootDrop on {updated}/{EnemyPrefabPaths.Length} enemy prefabs.");
        return updated > 0;
    }

    static bool SetupPrefab(string path, GameObject lootPrefab)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogWarning($"[EnemyDeathLootDropPrefabSetup] Missing prefab: {path}");
            return false;
        }

        try
        {
            if (root.GetComponent<PlayerDeathHandler>() != null)
            {
                Debug.LogWarning($"[EnemyDeathLootDropPrefabSetup] Skipping player-like prefab: {path}");
                return false;
            }

            var drop = root.GetComponent<EnemyDeathLootDrop>();
            if (drop == null)
                drop = root.AddComponent<EnemyDeathLootDrop>();

            WireDrop(drop, root, lootPrefab);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void WireDrop(EnemyDeathLootDrop drop, GameObject root, GameObject lootPrefab)
    {
        var so = new SerializedObject(drop);
        so.FindProperty("health").objectReferenceValue = root.GetComponent<CombatEntityHealth>();
        so.FindProperty("lootPrefab").objectReferenceValue = lootPrefab;
        so.FindProperty("dropChance").floatValue = 0f;
        so.FindProperty("spawnDelay").floatValue = 0.15f;
        so.FindProperty("destroyLootAfterOpened").boolValue = true;
        so.FindProperty("destroyLootDelay").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(drop);
    }
}
#endif
