#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists FPS display setup into init.unity once after scripts compile.
/// </summary>
[InitializeOnLoad]
public static class FpsDisplaySceneSetup
{
    const string InitScenePath = "Assets/_project/_scenes/init.unity";
    const string SetupDonePrefKey = "LuckyDungeon.FpsDisplaySceneSetupDone";

    static FpsDisplaySceneSetup()
    {
        EditorApplication.delayCall += TryPersistInInitScene;
    }

    static void TryPersistInInitScene()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryPersistInInitScene;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        string previousScenePath = SceneManager.GetActiveScene().path;
        Scene scene = EditorSceneManager.OpenScene(InitScenePath, OpenSceneMode.Single);
        Transform versionTransform = GameObject.Find("ui")?.transform.Find("version");
        if (versionTransform == null)
        {
            RestorePreviousScene(previousScenePath);
            return;
        }

        if (versionTransform.GetComponent<FpsDisplayView>() != null && versionTransform.Find("txtFps") != null)
        {
            EditorPrefs.SetBool(SetupDonePrefKey, true);
            RestorePreviousScene(previousScenePath);
            return;
        }

        FpsDisplayView fpsView = FpsDisplayView.EnsureInstalled(versionTransform);
        using (var so = new SerializedObject(fpsView))
        {
            so.FindProperty("showFps").boolValue = true;
            so.FindProperty("fpsLabel").objectReferenceValue = versionTransform.Find("txtFps")?.GetComponent<TMPro.TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.SaveScene(scene);
        EditorPrefs.SetBool(SetupDonePrefKey, true);
        RestorePreviousScene(previousScenePath);
        Debug.Log("[FpsDisplaySceneSetup] Saved FPS display setup to init.unity.");
    }

    static void RestorePreviousScene(string previousScenePath)
    {
        if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != InitScenePath)
            EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
    }
}
#endif
