#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="DestructibleHitFlash"/> is on the base destructible prefab root once Unity imports scripts
/// (avoids hand-editing prefab YAML / script .meta from outside the Editor).
/// </summary>
[InitializeOnLoad]
public static class DestructibleHitFlashPrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/level/baseDestructible.prefab";

    static DestructibleHitFlashPrefabSetup()
    {
        EditorApplication.delayCall += EnsureHitFlashOnPrefab;
    }

    [MenuItem("Tools/Destructibles/Ensure Hit Flash On Base Prefab")]
    public static void EnsureHitFlashOnPrefabMenu()
    {
        EnsureHitFlashOnPrefab();
    }

    static void EnsureHitFlashOnPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<BaseDestructibleObject>() == null)
            {
                Debug.LogWarning("[DestructibleHitFlashPrefabSetup] Skipped: no BaseDestructibleObject on " + PrefabPath);
                return;
            }

            if (root.GetComponent<DestructibleHitFlash>() != null)
                return;

            root.AddComponent<DestructibleHitFlash>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[DestructibleHitFlashPrefabSetup] Added DestructibleHitFlash to " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
