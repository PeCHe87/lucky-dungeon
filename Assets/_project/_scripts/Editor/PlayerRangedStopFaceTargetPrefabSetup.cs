#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="PlayerRangedStopFaceTarget"/> is on the player prefab.
/// </summary>
[InitializeOnLoad]
static class PlayerRangedStopFaceTargetPrefabSetup
{
    const string PlayerPrefabPath = "Assets/_project/_prefabs/entities/player.prefab";

    static PlayerRangedStopFaceTargetPrefabSetup()
    {
        EditorApplication.delayCall += EnsureComponent;
    }

    [MenuItem("Tools/Player/Setup Player Ranged Stop Face Target")]
    static void SetupMenu() => EnsureComponent();

    static void EnsureComponent()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureComponent;
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root.GetComponent<PlayerEntityState>() == null)
            {
                Debug.LogWarning("[PlayerRangedStopFaceTargetPrefabSetup] Skipped (no PlayerEntityState).");
                return;
            }

            if (root.GetComponent<PlayerRangedStopFaceTarget>() != null)
                return;

            root.AddComponent<PlayerRangedStopFaceTarget>();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[PlayerRangedStopFaceTargetPrefabSetup] Ensured PlayerRangedStopFaceTarget on player prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
