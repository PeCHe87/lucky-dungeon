#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="WeaponCollectible"/> is on the weaponCollectible prefab root.
/// </summary>
[InitializeOnLoad]
public static class WeaponCollectiblePrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/battle/weaponCollectible.prefab";
    const string MenuPath = "Tools/Collectibles/Setup Weapon Collectible Prefab";
    const string SetupDonePrefKey = "LuckyDungeon.WeaponCollectiblePrefabSetupDone";

    static WeaponCollectiblePrefabSetup()
    {
        EditorApplication.delayCall += TryEnsurePrefab;
    }

    [MenuItem(MenuPath)]
    public static void SetupMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        EnsureWeaponCollectiblePrefab(forceLog: true);
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static void TryEnsurePrefab()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryEnsurePrefab;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (EnsureWeaponCollectiblePrefab())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static bool EnsureWeaponCollectiblePrefab(bool forceLog = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            if (forceLog)
                Debug.LogWarning("[WeaponCollectiblePrefabSetup] Missing prefab: " + PrefabPath);
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<WeaponCollectible>() != null)
            {
                if (forceLog)
                    Debug.Log("[WeaponCollectiblePrefabSetup] Prefab already has WeaponCollectible.");
                return true;
            }

            root.AddComponent<WeaponCollectible>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[WeaponCollectiblePrefabSetup] Added WeaponCollectible to " + PrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
