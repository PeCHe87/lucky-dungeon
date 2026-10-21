#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires <see cref="LootInteractButtonBinder"/> onto battleground btnInteract.
/// </summary>
static class LootInteractButtonSceneInstaller
{
    const string ScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SessionKey = "LootInteractButtonSceneInstaller_v2";
    const string ControlsName = "controls";
    const string ButtonName = "btnInteract";

    [MenuItem("Tools/Loot/Ensure Interact Button On Battleground")]
    static void InstallFromMenu()
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

        bool dirty = false;

        GameObject controls = FindChildByName(scene, ControlsName);
        if (controls == null)
            goto SaveAndExit;

        Transform buttonTransform = controls.transform.Find(ButtonName);
        if (buttonTransform == null)
            goto SaveAndExit;

        GameObject buttonObject = buttonTransform.gameObject;
        var button = buttonObject.GetComponent<Button>();
        if (button == null)
            goto SaveAndExit;

        var binder = buttonObject.GetComponent<LootInteractButtonBinder>();
        if (binder == null)
        {
            binder = buttonObject.AddComponent<LootInteractButtonBinder>();
            dirty = true;
        }

        var so = new SerializedObject(binder);
        SerializedProperty buttonProp = so.FindProperty("button");
        if (buttonProp != null && buttonProp.objectReferenceValue != button)
        {
            buttonProp.objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();
            dirty = true;
        }

        if (buttonObject.activeSelf)
        {
            buttonObject.SetActive(false);
            dirty = true;
        }

    SaveAndExit:
        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LootInteractButtonSceneInstaller] Wired LootInteractButtonBinder on btnInteract.");
        }

        if (!wasLoaded && scene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

        SessionState.SetBool(SessionKey, true);
    }

    static GameObject FindChildByName(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindChildByName(roots[i].transform, name);
            if (found != null)
                return found;
        }

        return null;
    }

    static GameObject FindChildByName(Transform root, string name)
    {
        if (root.name == name)
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
