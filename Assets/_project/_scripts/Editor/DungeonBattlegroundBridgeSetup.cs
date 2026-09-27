#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Adds <see cref="DungeonExternalCellBridge"/> to battleground for dungeon leave/return testing.</summary>
public static class DungeonBattlegroundBridgeSetup
{
    const string BattlegroundScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SetupDonePrefKey = "LuckyDungeon.DungeonBattlegroundBridgeSetupDone";

    [InitializeOnLoadMethod]
    static void AutoOnce()
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

        if (!System.IO.File.Exists(BattlegroundScenePath))
            return;

        SetupBridge();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    [MenuItem("Tools/Dungeon/Setup Battleground External Cell Bridge")]
    public static void SetupBridgeMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        SetupBridge();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static void SetupBridge()
    {
        var scene = EditorSceneManager.OpenScene(BattlegroundScenePath, OpenSceneMode.Single);
        var bridge = Object.FindFirstObjectByType<DungeonExternalCellBridge>();
        if (bridge == null)
        {
            var go = new GameObject("DungeonExternalCellBridge");
            bridge = go.AddComponent<DungeonExternalCellBridge>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DungeonBattlegroundBridgeSetup] DungeonExternalCellBridge ready (F6 to complete battle cell).");
    }
}
#endif
