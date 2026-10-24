#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="RepairableObject"/> is on repairable_0 with lock UI, visuals, and rarity frames wired.
/// </summary>
[InitializeOnLoad]
public static class RepairableObjectPrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/level/repairable_0.prefab";
    const string BuildingPrefabPath = "Assets/_project/_prefabs/level/building_0.prefab";

    static RepairableObjectPrefabSetup()
    {
        EditorApplication.delayCall += EnsureOnPrefab;
    }

    [MenuItem("Tools/Repairable/Ensure RepairableObject On Prefab")]
    public static void EnsureOnPrefabMenu()
    {
        EnsureOnPrefab();
    }

    static void EnsureOnPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool added = false;
            var repairable = root.GetComponent<RepairableObject>();
            if (repairable == null)
            {
                repairable = root.AddComponent<RepairableObject>();
                added = true;
            }

            Transform ui = root.transform.Find("ui");
            Transform slots = ui != null ? ui.Find("locked/requiredSlots") : null;
            Transform locked = root.transform.Find("locked");
            Transform repaired = root.transform.Find("repaired");

            var so = new SerializedObject(repairable);
            SerializedProperty lockedProp = so.FindProperty("locked");
            SerializedProperty lockedVisual = so.FindProperty("lockedVisual");
            SerializedProperty repairedVisual = so.FindProperty("repairedVisual");
            SerializedProperty lockUiRoot = so.FindProperty("lockUiRoot");
            SerializedProperty requiredSlotsRoot = so.FindProperty("requiredSlotsRoot");
            SerializedProperty rarityFrames = so.FindProperty("rarityFrames");

            bool changed = added;
            bool isLocked = lockedProp == null || lockedProp.boolValue;

            if (lockedProp != null && !lockedProp.boolValue)
            {
                // Keep designer-authored unlocked state; only force locked when newly added.
                if (added)
                {
                    lockedProp.boolValue = true;
                    isLocked = true;
                    changed = true;
                }
            }
            else if (lockedProp != null && added)
            {
                lockedProp.boolValue = true;
                isLocked = true;
                changed = true;
            }

            if (lockedVisual != null && locked != null && lockedVisual.objectReferenceValue != locked.gameObject)
            {
                lockedVisual.objectReferenceValue = locked.gameObject;
                changed = true;
            }

            if (repairedVisual != null && repaired != null && repairedVisual.objectReferenceValue != repaired.gameObject)
            {
                repairedVisual.objectReferenceValue = repaired.gameObject;
                changed = true;
            }

            if (lockUiRoot != null && ui != null && lockUiRoot.objectReferenceValue != ui.gameObject)
            {
                lockUiRoot.objectReferenceValue = ui.gameObject;
                changed = true;
            }

            if (requiredSlotsRoot != null && slots != null && requiredSlotsRoot.objectReferenceValue != slots)
            {
                requiredSlotsRoot.objectReferenceValue = slots;
                changed = true;
            }

            if (CopyRarityFramesFromBuildingDoor(rarityFrames))
                changed = true;

            if (ui != null && ui.gameObject.activeSelf)
            {
                ui.gameObject.SetActive(false);
                changed = true;
            }

            if (locked != null && locked.gameObject.activeSelf != isLocked)
            {
                locked.gameObject.SetActive(isLocked);
                changed = true;
            }

            if (repaired != null && repaired.gameObject.activeSelf == isLocked)
            {
                repaired.gameObject.SetActive(!isLocked);
                changed = true;
            }

            if (!changed)
                return;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[RepairableObjectPrefabSetup] Wired RepairableObject on " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool CopyRarityFramesFromBuildingDoor(SerializedProperty destination)
    {
        if (destination == null)
            return false;

        bool needsCopy = destination.arraySize != 6;
        if (!needsCopy)
        {
            for (int i = 0; i < 6; i++)
            {
                if (destination.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    needsCopy = true;
                    break;
                }
            }
        }

        if (!needsCopy)
            return false;

        GameObject building = PrefabUtility.LoadPrefabContents(BuildingPrefabPath);
        try
        {
            BuildingDoor door = building.GetComponentInChildren<BuildingDoor>(true);
            if (door == null)
                return false;

            var doorSo = new SerializedObject(door);
            SerializedProperty source = doorSo.FindProperty("rarityFrames");
            if (source == null || source.arraySize == 0)
                return false;

            destination.arraySize = source.arraySize;
            for (int i = 0; i < source.arraySize; i++)
            {
                destination.GetArrayElementAtIndex(i).objectReferenceValue =
                    source.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(building);
        }
    }
}
#endif
