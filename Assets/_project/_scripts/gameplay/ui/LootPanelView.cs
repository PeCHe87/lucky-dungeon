using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporary loot-obtained HUD. Root stays active; toggles the <c>content</c> child,
/// fills slots, hides unused ones, then hides content after <see cref="displayDuration"/>.
/// </summary>
public sealed class LootPanelView : MonoBehaviour
{
    public const int SlotCount = 5;

    [SerializeField] GameObject contentRoot;
    [SerializeField] InventorySlotView[] slots;
    [Tooltip("Indexed by ItemRarity: Common, Uncommon, Rare, Epic, Legendary, Mythic.")]
    [SerializeField] Sprite[] rarityFrames;
    [SerializeField, Min(0.01f)] float displayDuration = 3f;

    Coroutine _hideRoutine;
    static bool s_hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void HookGameEvents()
    {
        if (s_hooked)
            return;

        s_hooked = true;
        GameEvents.LootObtained += OnLootObtainedStatic;
    }

    static void OnLootObtainedStatic(IReadOnlyList<(string itemId, int amount)> entries)
    {
        LootPanelView panel = Object.FindFirstObjectByType<LootPanelView>(FindObjectsInactive.Include);
        if (panel == null)
            panel = EnsurePanelOnSceneLootUi();
        if (panel != null)
            panel.Show(entries);
    }

    static LootPanelView EnsurePanelOnSceneLootUi()
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || t.name != "ui_loot")
                continue;
            if (!t.gameObject.scene.IsValid())
                continue;

            LootPanelView panel = t.GetComponent<LootPanelView>();
            if (panel == null)
                panel = t.gameObject.AddComponent<LootPanelView>();
            return panel;
        }

        return null;
    }

    void Awake()
    {
        ResolveContent();
        EnsureSlots();
        SetContentVisible(false);
    }

    void OnDisable()
    {
        StopHideRoutine();
    }

    public void Show(IReadOnlyList<(string itemId, int amount)> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        ResolveContent();
        EnsureSlots();

        ItemCatalog catalog = ItemCatalog.Current;
        Sprite emptyFrame = GetFrame(ItemRarity.Common);
        int filled = 0;
        int slotCount = slots != null ? Mathf.Min(slots.Length, SlotCount) : 0;

        for (int i = 0; i < entries.Count && filled < slotCount; i++)
        {
            InventorySlotView slot = slots[filled];
            if (slot == null)
                break;

            slot.gameObject.SetActive(true);

            (string itemId, int amount) = entries[i];
            if (catalog == null || !catalog.TryGetById(itemId, out ItemData data) || data == null)
            {
                slot.SetEmpty(emptyFrame);
                filled++;
                continue;
            }

            Sprite frame = GetFrame(data.Rarity) ?? emptyFrame;
            slot.SetItem(frame, data.PreviewImage, amount);
            filled++;
        }

        for (int i = filled; i < slotCount; i++)
        {
            InventorySlotView slot = slots[i];
            if (slot != null)
                slot.gameObject.SetActive(false);
        }

        SetContentVisible(true);

        StopHideRoutine();
        _hideRoutine = StartCoroutine(HideAfterDelay());
    }

    IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, displayDuration));
        _hideRoutine = null;
        SetContentVisible(false);
    }

    void StopHideRoutine()
    {
        if (_hideRoutine == null)
            return;

        StopCoroutine(_hideRoutine);
        _hideRoutine = null;
    }

    void SetContentVisible(bool visible)
    {
        ResolveContent();
        if (contentRoot != null && contentRoot.activeSelf != visible)
            contentRoot.SetActive(visible);
    }

    void ResolveContent()
    {
        if (contentRoot != null)
            return;

        Transform content = transform.Find("content");
        if (content != null)
            contentRoot = content.gameObject;
    }

    Sprite GetFrame(ItemRarity rarity)
    {
        int index = (int)rarity;
        if (rarityFrames == null || index < 0 || index >= rarityFrames.Length)
            return null;
        return rarityFrames[index];
    }

    void EnsureSlots()
    {
        if (slots != null && slots.Length > 0)
        {
            bool allValid = true;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    allValid = false;
                    break;
                }
            }

            if (allValid)
                return;
        }

        var found = new List<InventorySlotView>(SlotCount);
        CollectRowSlots(transform.Find("content/row_0"), found);

        if (found.Count == 0)
            GetComponentsInChildren(true, found);

        if (found.Count > SlotCount)
            found.RemoveRange(SlotCount, found.Count - SlotCount);

        slots = found.ToArray();
    }

    static void CollectRowSlots(Transform row, List<InventorySlotView> destination)
    {
        if (row == null)
            return;

        for (int i = 0; i < row.childCount; i++)
        {
            InventorySlotView slot = row.GetChild(i).GetComponent<InventorySlotView>();
            if (slot == null)
                slot = row.GetChild(i).GetComponentInChildren<InventorySlotView>(true);
            if (slot != null)
                destination.Add(slot);
        }
    }
}
