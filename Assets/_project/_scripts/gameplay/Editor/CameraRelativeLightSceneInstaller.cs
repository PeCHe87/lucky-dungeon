#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures <see cref="CameraRelativeLight"/> is on the scene light in battleground.unity.
/// </summary>
static class CameraRelativeLightSceneInstaller
{
    const string ScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SessionKey = "CameraRelativeLightSceneInstaller_v1";
    const string LightName = "light";

    [MenuItem("Tools/Lucky Dungeon/Install Camera Relative Light")]
    public static void InstallFromMenu()
    {
        SessionState.SetBool(SessionKey, false);
        EnsureSceneWired();
    }

    [InitializeOnLoadMethod]
    static void ScheduleInstall()
    {
        EditorApplication.delayCall += EnsureSceneWired;
    }

    static void EnsureSceneWired()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureSceneWired;
            return;
        }

        Scene activeScene = EditorSceneManager.GetActiveScene();
        bool wasLoaded = activeScene.path == ScenePath;
        Scene scene;

        if (wasLoaded)
        {
            scene = activeScene;
        }
        else
        {
            if (!System.IO.File.Exists(ScenePath))
                return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        bool dirty = EnsureComponent(scene);

        if (dirty)
            EditorSceneManager.SaveScene(scene);

        if (!wasLoaded && scene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

        SessionState.SetBool(SessionKey, true);
    }

    static bool EnsureComponent(Scene scene)
    {
        GameObject lightObject = FindChildByName(scene, LightName);
        if (lightObject == null)
        {
            Debug.LogWarning($"[{nameof(CameraRelativeLightSceneInstaller)}] Skipped: no '{LightName}' in {ScenePath}");
            return false;
        }

        if (lightObject.GetComponent<Light>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(CameraRelativeLightSceneInstaller)}] Skipped: '{LightName}' has no Light component");
            return false;
        }

        if (lightObject.GetComponent<CameraRelativeLight>() != null)
            return false;

        lightObject.AddComponent<CameraRelativeLight>();
        Debug.Log($"[{nameof(CameraRelativeLightSceneInstaller)}] Added CameraRelativeLight to '{LightName}'");
        return true;
    }

    static GameObject FindChildByName(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name == objectName)
                return roots[i];

            Transform found = FindRecursive(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    static Transform FindRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindRecursive(parent.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
