#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Wires <see cref="InventorySlotView"/> / <see cref="InventoryPanelView"/> and rarity frame sprites
/// on the inventory HUD prefabs.
/// </summary>
[InitializeOnLoad]
public static class InventoryHudPrefabSetup
{
    const string SlotPrefabPath = "Assets/_project/_prefabs/ui/inventory/ui_inventorySlot.prefab";
    const string PanelPrefabPath = "Assets/_project/_prefabs/ui/inventory/ui_inventory.prefab";
    const string MenuPath = "Tools/UI/Setup Inventory HUD Prefabs";
    const string SetupDonePrefKey = "LuckyDungeon.InventoryHudPrefabSetupDone.v1";
    const string FrameFolder =
        "Assets/_assetStore/Layer Lab/GUI Pro-CasualGame/ResourcesData/Sprites/Components/Frame";

    static InventoryHudPrefabSetup()
    {
        EditorApplication.delayCall += TryEnsurePrefabs;
    }

    [MenuItem(MenuPath)]
    public static void SetupMenu()
    {
        EditorPrefs.DeleteKey(SetupDonePrefKey);
        SetupAll(forceLog: true);
        EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    static void TryEnsurePrefabs()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryEnsurePrefabs;
            return;
        }

        if (EditorPrefs.GetBool(SetupDonePrefKey, false))
            return;

        if (SetupAll())
            EditorPrefs.SetBool(SetupDonePrefKey, true);
    }

    public static bool SetupAll(bool forceLog = false)
    {
        if (!EnsureSlotPrefab(forceLog))
            return false;
        if (!EnsurePanelPrefab(forceLog))
            return false;
        return true;
    }

    static bool EnsureSlotPrefab(bool forceLog)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        if (prefab == null)
        {
            if (forceLog)
                Debug.LogWarning("[InventoryHudPrefabSetup] Missing slot prefab: " + SlotPrefabPath);
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
        try
        {
            InventorySlotView view = root.GetComponent<InventorySlotView>();
            if (view == null)
                view = root.AddComponent<InventorySlotView>();

            Image frame = root.transform.Find("container")?.GetComponent<Image>();
            Image icon = root.transform.Find("container/icon")?.GetComponent<Image>();
            TMPro.TextMeshProUGUI amount =
                root.transform.Find("container/txtAmount")?.GetComponent<TMPro.TextMeshProUGUI>();

            var so = new SerializedObject(view);
            so.FindProperty("frameImage").objectReferenceValue = frame;
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("amountLabel").objectReferenceValue = amount;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
            if (forceLog)
                Debug.Log("[InventoryHudPrefabSetup] Updated " + SlotPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static bool EnsurePanelPrefab(bool forceLog)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        if (prefab == null)
        {
            if (forceLog)
                Debug.LogWarning("[InventoryHudPrefabSetup] Missing panel prefab: " + PanelPrefabPath);
            return false;
        }

        Sprite[] frames = LoadRarityFrames();
        if (frames == null || frames.Length < 6)
        {
            Debug.LogWarning("[InventoryHudPrefabSetup] Could not load all rarity frame sprites.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PanelPrefabPath);
        try
        {
            InventoryPanelView panel = root.GetComponent<InventoryPanelView>();
            if (panel == null)
                panel = root.AddComponent<InventoryPanelView>();

            var slotViews = new List<InventorySlotView>(InventoryPanelView.SlotCount);
            CollectRowSlots(root.transform.Find("content/row_0"), slotViews);
            CollectRowSlots(root.transform.Find("content/row_1"), slotViews);

            for (int i = 0; i < slotViews.Count; i++)
            {
                InventorySlotView slot = slotViews[i];
                if (slot == null)
                    continue;

                Image frame = slot.transform.Find("container")?.GetComponent<Image>();
                Image icon = slot.transform.Find("container/icon")?.GetComponent<Image>();
                TMPro.TextMeshProUGUI amount =
                    slot.transform.Find("container/txtAmount")?.GetComponent<TMPro.TextMeshProUGUI>();

                var slotSo = new SerializedObject(slot);
                slotSo.FindProperty("frameImage").objectReferenceValue = frame;
                slotSo.FindProperty("iconImage").objectReferenceValue = icon;
                slotSo.FindProperty("amountLabel").objectReferenceValue = amount;
                slotSo.ApplyModifiedPropertiesWithoutUndo();
            }

            while (slotViews.Count < InventoryPanelView.SlotCount)
                slotViews.Add(null);
            if (slotViews.Count > InventoryPanelView.SlotCount)
                slotViews.RemoveRange(InventoryPanelView.SlotCount, slotViews.Count - InventoryPanelView.SlotCount);

            var so = new SerializedObject(panel);
            SerializedProperty slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = InventoryPanelView.SlotCount;
            for (int i = 0; i < InventoryPanelView.SlotCount; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotViews[i];

            SerializedProperty framesProp = so.FindProperty("rarityFrames");
            framesProp.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
            if (forceLog)
                Debug.Log("[InventoryHudPrefabSetup] Updated " + PanelPrefabPath);
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
