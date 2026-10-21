#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures <see cref="LootPanelView"/> is on ui_loot with slots, rarity frames,
/// root active, and content hidden.
/// </summary>
[InitializeOnLoad]
public static class LootPanelPrefabSetup
{
    const string PrefabPath = "Assets/_project/_prefabs/ui/inventory/ui_loot.prefab";
    const string FrameFolder =
        "Assets/_assetStore/Layer Lab/GUI Pro-CasualGame/ResourcesData/Sprites/Components/Frame";
    const string SetupDonePrefKey = "LuckyDungeon.LootPanelPrefabSetupDone.v2";

    static LootPanelPrefabSetup()
    {
        EditorApplication.delayCall += TryEnsurePrefab;
    }

    [MenuItem("Tools/Loot/Ensure Loot Panel Prefab")]
    public static void SetupMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        if (EnsurePrefab(forceLog: true))
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static void TryEnsurePrefab()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryEnsurePrefab;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (EnsurePrefab())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static bool EnsurePrefab(bool forceLog = false)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            if (forceLog)
                Debug.LogWarning("[LootPanelPrefabSetup] Missing prefab: " + PrefabPath);
            return false;
        }

        Sprite[] frames = LoadRarityFrames();
        if (frames == null || frames.Length < 6)
        {
            Debug.LogWarning("[LootPanelPrefabSetup] Could not load all rarity frame sprites.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool changed = false;

            var panel = root.GetComponent<LootPanelView>();
            if (panel == null)
            {
                panel = root.AddComponent<LootPanelView>();
                changed = true;
            }

            var slotViews = new List<InventorySlotView>(LootPanelView.SlotCount);
            CollectRowSlots(root.transform.Find("content/row_0"), slotViews);
            if (slotViews.Count == 0)
                root.GetComponentsInChildren(true, slotViews);

            while (slotViews.Count > LootPanelView.SlotCount)
                slotViews.RemoveAt(slotViews.Count - 1);

            Transform content = root.transform.Find("content");

            var so = new SerializedObject(panel);
            SerializedProperty contentRootProp = so.FindProperty("contentRoot");
            SerializedProperty slotsProp = so.FindProperty("slots");
            SerializedProperty framesProp = so.FindProperty("rarityFrames");
            SerializedProperty durationProp = so.FindProperty("displayDuration");

            if (contentRootProp != null && content != null && contentRootProp.objectReferenceValue != content.gameObject)
            {
                contentRootProp.objectReferenceValue = content.gameObject;
                changed = true;
            }

            if (slotsProp != null)
            {
                slotsProp.arraySize = slotViews.Count;
                for (int i = 0; i < slotViews.Count; i++)
                {
                    if (slotsProp.GetArrayElementAtIndex(i).objectReferenceValue != slotViews[i])
                    {
                        slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];
                        changed = true;
                    }
                }
            }

            if (framesProp != null)
            {
                framesProp.arraySize = frames.Length;
                for (int i = 0; i < frames.Length; i++)
                {
                    if (framesProp.GetArrayElementAtIndex(i).objectReferenceValue != frames[i])
                    {
                        framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                        changed = true;
                    }
                }
            }

            if (durationProp != null && durationProp.floatValue < 0.01f)
            {
                durationProp.floatValue = 3f;
                changed = true;
            }

            if (!root.activeSelf)
            {
                root.SetActive(true);
                changed = true;
            }

            if (content != null && content.gameObject.activeSelf)
            {
                content.gameObject.SetActive(false);
                changed = true;
            }

            if (!changed && !forceLog)
                return true;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[LootPanelPrefabSetup] Wired LootPanelView on " + PrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void CollectRowSlots(Transform row, List<InventorySlotView> destination)
    {
        if (row == null)
            return;

        for (int i = 0; i < row.childCount; i++)
        {
            Transform child = row.GetChild(i);
            InventorySlotView slot = child.GetComponent<InventorySlotView>();
            if (slot == null)
                slot = child.GetComponentInChildren<InventorySlotView>(true);
            if (slot == null)
                slot = child.gameObject.AddComponent<InventorySlotView>();
            destination.Add(slot);
        }
    }

    static Sprite[] LoadRarityFrames()
    {
        return new[]
        {
            LoadSprite($"{FrameFolder}/ItemFrame01_Single_Brown.png"),
            LoadSprite($"{FrameFolder}/ItemFrame01_Single_Green.png"),
            LoadSprite($"{FrameFolder}/ItemFrame01_Single_Blue.png"),
            LoadSprite($"{FrameFolder}/ItemFrame01_Single_Purple.png"),
            LoadSprite($"{FrameFolder}/ItemFrame01_Single_Yellow.png"),
            LoadSprite($"{FrameFolder}/ItemFrame01_Single_Red.png"),
        };
    }

    static Sprite LoadSprite(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                return sprite;
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
