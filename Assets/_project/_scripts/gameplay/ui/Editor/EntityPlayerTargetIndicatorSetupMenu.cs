#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds <see cref="EntityPlayerTargetIndicator"/> to combat entity prefabs that opt in to the player target ring.
/// </summary>
public static class EntityPlayerTargetIndicatorSetupMenu
{
    const string MenuPath = "Tools/UI/Add Player Target Ring To Combat Prefabs";

    static readonly string[] PrefabPaths =
    {
        "Assets/_project/_prefabs/entities/base_combat_entity.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_melee.prefab",
        "Assets/_project/_prefabs/entities/base_combat_entity_chaser_patroller_range.prefab",
    };

    public static void RunFromBatch()
    {
        AddToCombatPrefabs();
    }

    [MenuItem(MenuPath)]
    public static void AddToCombatPrefabs()
    {
        int updated = 0;
        foreach (string path in PrefabPaths)
        {
            if (AddToPrefab(path))
                updated++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[{nameof(EntityPlayerTargetIndicatorSetupMenu)}] Updated {updated} combat prefab(s).");
    }

    static bool AddToPrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogWarning($"[{nameof(EntityPlayerTargetIndicatorSetupMenu)}] Could not load prefab: {path}");
            return false;
        }

        bool changed = false;
        if (root.GetComponent<EntityPlayerTargetIndicator>() == null)
        {
            root.AddComponent<EntityPlayerTargetIndicator>();
            changed = true;
        }

        if (changed)
            PrefabUtility.SaveAsPrefabAsset(root, path);

        PrefabUtility.UnloadPrefabContents(root);
        return changed;
    }
}
#endif
