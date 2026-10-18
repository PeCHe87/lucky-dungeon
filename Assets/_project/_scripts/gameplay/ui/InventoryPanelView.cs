using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD inventory panel bound to <see cref="PlayerRunInventory"/> via <see cref="GameEvents"/>.
/// Shows up to <see cref="SlotCount"/> stacks in inventory order (newest unique first).
/// </summary>
public sealed class InventoryPanelView : MonoBehaviour
{
    public const int SlotCount = 10;

    [SerializeField] InventorySlotView[] slots;
    [Tooltip("Indexed by ItemRarity: Common, Uncommon, Rare, Epic, Legendary, Mythic.")]
    [SerializeField] Sprite[] rarityFrames;

    readonly List<(string itemId, int amount)> _entries = new List<(string, int)>(SlotCount);

    void Awake() => EnsureSlots();

    void OnEnable()
    {
        GameEvents.PlayerRunInventoryChanged += Refresh;
        GameEvents.DungeonRunStarted += OnDungeonRunStarted;
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.PlayerRunInventoryChanged -= Refresh;
        GameEvents.DungeonRunStarted -= OnDungeonRunStarted;
    }

    void OnDungeonRunStarted(Dungeon _) => Refresh();

    void Refresh()
    {
        EnsureSlots();

        Sprite emptyFrame = GetFrame(ItemRarity.Common);
        PlayerRunInventory inventory = DungeonRunHost.Instance != null
            ? DungeonRunHost.Instance.Player?.Inventory
            : null;

        _entries.Clear();
        if (inventory != null)
            inventory.CopyOrderedEntries(_entries, SlotCount);

        ItemCatalog catalog = ItemCatalog.Current;
        int filled = 0;
        for (int i = 0; i < _entries.Count && filled < SlotCount; i++)
        {
            InventorySlotView slot = GetSlot(filled);
            if (slot == null)
                break;

            (string itemId, int amount) = _entries[i];
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

        for (int i = filled; i < SlotCount; i++)
        {
            InventorySlotView slot = GetSlot(i);
            if (slot != null)
                slot.SetEmpty(emptyFrame);
        }
    }

    Sprite GetFrame(ItemRarity rarity)
    {
        int index = (int)rarity;
        if (rarityFrames == null || index < 0 || index >= rarityFrames.Length)
            return null;
        return rarityFrames[index];
    }

    InventorySlotView GetSlot(int index)
    {
        if (slots == null || index < 0 || index >= slots.Length)
            return null;
        return slots[index];
    }

    void EnsureSlots()
    {
        if (slots != null && slots.Length == SlotCount)
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
        CollectRowSlots(transform.Find("content/row_1"), found);

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
