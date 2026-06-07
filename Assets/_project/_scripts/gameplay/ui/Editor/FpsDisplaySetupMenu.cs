#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-click scene setup: FPS label under ui/version and <see cref="FpsDisplayView"/> wiring.
/// </summary>
public static class FpsDisplaySetupMenu
{
    const string MenuPath = "Tools/UI/Setup FPS Display";
    const string InitScenePath = "Assets/_project/_scenes/init.unity";

    [MenuItem(MenuPath)]
    public static void SetupFpsDisplay()
    {
        Transform versionTransform = FindVersionTransform();
        if (versionTransform == null)
        {
            Debug.LogError($"[{nameof(FpsDisplaySetupMenu)}] Could not find ui/version in the open scene.");
            return;
        }

        SetupFpsDisplayOnVersion(versionTransform);
    }

    public static void SetupFpsDisplayBatch()
    {
        Scene scene = EditorSceneManager.OpenScene(InitScenePath);
        Transform versionTransform = FindVersionTransform();
        if (versionTransform == null)
        {
            Debug.LogError($"[{nameof(FpsDisplaySetupMenu)}] Could not find ui/version in {InitScenePath}.");
            EditorApplication.Exit(1);
            return;
        }

        SetupFpsDisplayOnVersion(versionTransform);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[{nameof(FpsDisplaySetupMenu)}] Saved FPS display setup to {InitScenePath}.");
    }

    static void SetupFpsDisplayOnVersion(Transform versionTransform)
    {
        FpsDisplayView fpsView = FpsDisplayView.EnsureInstalled(versionTransform);

        using (var so = new SerializedObject(fpsView))
        {
            so.FindProperty("showFps").boolValue = true;
            so.FindProperty("fpsLabel").objectReferenceValue = versionTransform.Find("txtFps")?.GetComponent<TextMeshProUGUI>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(versionTransform.gameObject.scene);
        EditorUtility.SetDirty(versionTransform.gameObject);
        Selection.activeGameObject = versionTransform.gameObject;
        Debug.Log($"[{nameof(FpsDisplaySetupMenu)}] FPS display ready on '{versionTransform.name}'. Toggle Show Fps on FpsDisplayView before building.");
    }

    static Transform FindVersionTransform()
    {
        GameObject uiRoot = GameObject.Find("ui");
        if (uiRoot == null)
            return null;

        return uiRoot.transform.Find("version");
    }
}
#endif
