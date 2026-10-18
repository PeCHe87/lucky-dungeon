#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures <see cref="BuildingDoor"/> is on building/door in battleground.unity.
/// </summary>
static class BuildingDoorSceneInstaller
{
    const string ScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SessionKey = "BuildingDoorSceneInstaller_v2";
    const string BuildingName = "building";
    const string DoorName = "door";
    const string ArtChildName = "art";

    [MenuItem("Tools/Lucky Dungeon/Install Building Door")]
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

        bool dirty = EnsureDoor(scene);

        if (dirty)
            EditorSceneManager.SaveScene(scene);

        if (!wasLoaded && scene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

        SessionState.SetBool(SessionKey, true);
    }

    static bool EnsureDoor(Scene scene)
    {
        GameObject building = FindChildByName(scene, BuildingName);
        if (building == null)
        {
            Debug.LogWarning($"[{nameof(BuildingDoorSceneInstaller)}] Skipped: no '{BuildingName}' in {ScenePath}");
            return false;
        }

        Transform door = FindRecursive(building.transform, DoorName);
        if (door == null)
        {
            Debug.LogWarning(
                $"[{nameof(BuildingDoorSceneInstaller)}] Skipped: no '{DoorName}' under '{BuildingName}'");
            return false;
        }

        // Prefer the hinge root under building (not nested mesh also named door).
        Transform hingeRoot = null;
        for (int i = 0; i < building.transform.childCount; i++)
        {
            Transform child = building.transform.GetChild(i);
            if (child.name == DoorName)
            {
                hingeRoot = child;
                break;
            }
        }

        if (hingeRoot == null)
            hingeRoot = door;

        BuildingDoor buildingDoor = hingeRoot.GetComponent<BuildingDoor>();
        bool dirty = false;

        if (buildingDoor == null)
        {
            buildingDoor = hingeRoot.gameObject.AddComponent<BuildingDoor>();
            dirty = true;
            Debug.Log($"[{nameof(BuildingDoorSceneInstaller)}] Added BuildingDoor to '{hingeRoot.name}'");
        }

        Transform art = hingeRoot.Find(ArtChildName);
        if (art != null)
        {
            SerializedObject so = new SerializedObject(buildingDoor);
            SerializedProperty hingeProp = so.FindProperty("hinge");
            if (hingeProp != null && hingeProp.objectReferenceValue != art)
            {
                hingeProp.objectReferenceValue = art;
                so.ApplyModifiedPropertiesWithoutUndo();
                dirty = true;
                Debug.Log($"[{nameof(BuildingDoorSceneInstaller)}] Assigned art hinge on '{hingeRoot.name}'");
            }
        }

        return dirty;
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
