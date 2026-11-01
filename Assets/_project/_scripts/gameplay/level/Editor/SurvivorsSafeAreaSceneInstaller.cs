#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures <see cref="SurvivorsSafeArea"/> is on survivorsSafeArea in battleground.unity with goal assigned.
/// </summary>
static class SurvivorsSafeAreaSceneInstaller
{
    const string ScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SessionKey = "SurvivorsSafeAreaSceneInstaller_v1";
    const string SafeAreaName = "survivorsSafeArea";
    const string GoalChildName = "goal";

    [MenuItem("Tools/Lucky Dungeon/Install Survivors Safe Area")]
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
            {
                SessionState.SetBool(SessionKey, true);
                return;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        bool dirty = EnsureSafeArea(scene);
        if (dirty)
            EditorSceneManager.SaveScene(scene);

        if (!wasLoaded && scene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

        SessionState.SetBool(SessionKey, true);
    }

    static bool EnsureSafeArea(Scene scene)
    {
        GameObject safeAreaGo = FindChildByName(scene, SafeAreaName);
        if (safeAreaGo == null)
        {
            Debug.LogWarning($"[{nameof(SurvivorsSafeAreaSceneInstaller)}] Skipped: no '{SafeAreaName}' in {ScenePath}");
            return false;
        }

        bool changed = false;
        var safeArea = safeAreaGo.GetComponent<SurvivorsSafeArea>();
        if (safeArea == null)
        {
            safeArea = safeAreaGo.AddComponent<SurvivorsSafeArea>();
            changed = true;
        }

        Transform goal = safeAreaGo.transform.Find(GoalChildName);
        var so = new SerializedObject(safeArea);
        SerializedProperty goalProp = so.FindProperty("safeGoal");
        if (goalProp != null && goalProp.objectReferenceValue == null && goal != null)
        {
            goalProp.objectReferenceValue = goal;
            so.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (changed)
            Debug.Log($"[{nameof(SurvivorsSafeAreaSceneInstaller)}] Wired SurvivorsSafeArea on '{SafeAreaName}'.");

        return changed;
    }

    static GameObject FindChildByName(Scene scene, string objectName)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
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
