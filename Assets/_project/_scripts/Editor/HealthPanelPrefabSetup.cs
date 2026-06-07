#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="HealthPanelView"/> is on the health panel HUD prefab.
/// </summary>
[InitializeOnLoad]
public static class HealthPanelPrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/ui/healthPanel.prefab";

    static HealthPanelPrefabSetup()
    {
        EditorApplication.delayCall += EnsureHealthPanelView;
    }

    static void EnsureHealthPanelView()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureHealthPanelView;
            return;
        }

        SetupHealthPanelPrefab();
    }

    [MenuItem("Tools/UI/Setup Health Panel Prefab")]
    public static void SetupHealthPanelPrefabMenu()
    {
        SetupHealthPanelPrefab();
    }

    public static void SetupHealthPanelPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            if (root.GetComponent<HealthPanelView>() != null)
                return;

            root.AddComponent<HealthPanelView>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[HealthPanelPrefabSetup] Added HealthPanelView to " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
