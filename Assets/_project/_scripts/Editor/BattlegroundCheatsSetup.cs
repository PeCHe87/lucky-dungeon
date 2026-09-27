#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Ensures <see cref="BattlegroundCheats"/> exists in battleground and binds btnCompleteLevel.</summary>
public static class BattlegroundCheatsSetup
{
    const string BattlegroundScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SetupDonePrefKey = "LuckyDungeon.BattlegroundCheatsSetupDone";

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

        Setup();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    [MenuItem("Tools/Dungeon/Setup Battleground Cheats")]
    public static void SetupMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        Setup();
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(BattlegroundScenePath, OpenSceneMode.Single);

        var cheats = Object.FindFirstObjectByType<BattlegroundCheats>();
        if (cheats == null)
        {
            var go = new GameObject("BattlegroundCheats");
            cheats = go.AddComponent<BattlegroundCheats>();
        }

        Button btn = null;
        var btnGo = GameObject.Find("btnCompleteLevel");
        if (btnGo != null)
            btn = btnGo.GetComponent<Button>();

        var so = new SerializedObject(cheats);
        so.FindProperty("completeLevelButton").objectReferenceValue = btn;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            btn != null
                ? "[BattlegroundCheatsSetup] BattlegroundCheats ready — btnCompleteLevel bound to CompleteLevel()."
                : "[BattlegroundCheatsSetup] BattlegroundCheats added — btnCompleteLevel not found; will auto-find at runtime.");
    }
}
#endif
