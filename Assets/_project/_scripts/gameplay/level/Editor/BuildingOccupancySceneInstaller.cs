#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ensures building occupancy (enter/exit portals) + identity wiring in battleground.unity.
/// </summary>
static class BuildingOccupancySceneInstaller
{
    const string ScenePath = "Assets/_project/_scenes/battleground.unity";
    const string SessionKey = "BuildingOccupancySceneInstaller_v4";
    const string BuildingName = "building";
    const string EnterTriggerName = "triggerColliderEnter";
    const string ExitTriggerName = "triggerColliderExit";
    const string CeilingName = "ceiling";
    const string DefaultBuildingId = "building_01";

    [MenuItem("Tools/Lucky Dungeon/Install Building Occupancy")]
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

        bool dirty = false;
        dirty |= EnsureOccupancyRoot(scene);
        dirty |= EnsurePortal(scene, EnterTriggerName, BuildingOccupancyTrigger.Kind.Enter);
        dirty |= EnsurePortal(scene, ExitTriggerName, BuildingOccupancyTrigger.Kind.Exit);
        dirty |= EnsureIdentity(scene);
        dirty |= EnsureCeilingReference(scene);

        if (dirty)
            EditorSceneManager.SaveScene(scene);

        if (!wasLoaded && scene.isLoaded && !string.IsNullOrEmpty(activeScene.path))
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

        SessionState.SetBool(SessionKey, true);
    }

    static bool EnsureOccupancyRoot(Scene scene)
    {
        GameObject building = FindChildByName(scene, BuildingName);
        if (building == null)
        {
            Debug.LogWarning($"[{nameof(BuildingOccupancySceneInstaller)}] Skipped: no '{BuildingName}' in {ScenePath}");
            return false;
        }

        bool dirty = false;

        // Remove legacy BuildingOccupancy from portal children.
        BuildingOccupancy[] all = building.GetComponentsInChildren<BuildingOccupancy>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].gameObject == building)
                continue;
            string ownerName = all[i].gameObject.name;
            Object.DestroyImmediate(all[i]);
            dirty = true;
            Debug.Log(
                $"[{nameof(BuildingOccupancySceneInstaller)}] Removed BuildingOccupancy from '{ownerName}'");
        }

        if (building.GetComponent<BuildingOccupancy>() == null)
        {
            building.AddComponent<BuildingOccupancy>();
            dirty = true;
            Debug.Log($"[{nameof(BuildingOccupancySceneInstaller)}] Added BuildingOccupancy to '{BuildingName}'");
        }

        return dirty;
    }

    static bool EnsurePortal(Scene scene, string triggerName, BuildingOccupancyTrigger.Kind kind)
    {
        GameObject building = FindChildByName(scene, BuildingName);
        if (building == null)
            return false;

        Transform trigger = FindRecursive(building.transform, triggerName);
        if (trigger == null)
        {
            Debug.LogWarning(
                $"[{nameof(BuildingOccupancySceneInstaller)}] Skipped: no '{triggerName}' under '{BuildingName}'");
            return false;
        }

        if (trigger.GetComponent<Collider>() == null)
        {
            Debug.LogWarning(
                $"[{nameof(BuildingOccupancySceneInstaller)}] Skipped: no Collider on '{triggerName}'");
            return false;
        }

        BuildingOccupancy occupancy = building.GetComponent<BuildingOccupancy>();
        BuildingOccupancyTrigger portal = trigger.GetComponent<BuildingOccupancyTrigger>();
        bool dirty = false;

        if (portal == null)
        {
            portal = trigger.gameObject.AddComponent<BuildingOccupancyTrigger>();
            dirty = true;
            Debug.Log(
                $"[{nameof(BuildingOccupancySceneInstaller)}] Added BuildingOccupancyTrigger ({kind}) to '{triggerName}'");
        }

        SerializedObject so = new SerializedObject(portal);
        SerializedProperty occupancyProp = so.FindProperty("occupancy");
        SerializedProperty kindProp = so.FindProperty("kind");
        bool changed = false;

        if (occupancyProp != null && occupancyProp.objectReferenceValue != occupancy)
        {
            occupancyProp.objectReferenceValue = occupancy;
            changed = true;
        }

        if (kindProp != null && kindProp.enumValueIndex != (int)kind)
        {
            kindProp.enumValueIndex = (int)kind;
            changed = true;
        }

        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            dirty = true;
        }

        return dirty;
    }

    static bool EnsureIdentity(Scene scene)
    {
        GameObject building = FindChildByName(scene, BuildingName);
        if (building == null)
            return false;

        BuildingIdentity identity = building.GetComponent<BuildingIdentity>();
        if (identity != null)
            return false;

        identity = building.AddComponent<BuildingIdentity>();
        SerializedObject so = new SerializedObject(identity);
        so.FindProperty("buildingId").stringValue = DefaultBuildingId;
        AssignCeiling(so, building.transform);
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log(
            $"[{nameof(BuildingOccupancySceneInstaller)}] Added BuildingIdentity to '{BuildingName}' (id='{DefaultBuildingId}')");
        return true;
    }

    static bool EnsureCeilingReference(Scene scene)
    {
        GameObject building = FindChildByName(scene, BuildingName);
        if (building == null)
            return false;

        BuildingIdentity identity = building.GetComponent<BuildingIdentity>();
        if (identity == null)
            return false;

        SerializedObject so = new SerializedObject(identity);
        SerializedProperty ceilingProp = so.FindProperty("ceiling");
        if (ceilingProp == null || ceilingProp.objectReferenceValue != null)
            return false;

        if (!AssignCeiling(so, building.transform))
            return false;

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[{nameof(BuildingOccupancySceneInstaller)}] Assigned '{CeilingName}' on BuildingIdentity");
        return true;
    }

    static bool AssignCeiling(SerializedObject identitySo, Transform buildingTransform)
    {
        Transform ceiling = buildingTransform.Find(CeilingName);
        if (ceiling == null)
            return false;

        SerializedProperty ceilingProp = identitySo.FindProperty("ceiling");
        if (ceilingProp == null)
            return false;

        ceilingProp.objectReferenceValue = ceiling.gameObject;
        return true;
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
