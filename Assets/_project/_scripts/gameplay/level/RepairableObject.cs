using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World repair target. While locked, shows requirement UI in the trigger and enables
/// <c>btnInteract</c> when the player holds all required items. Repair consumes those items,
/// swaps locked/repaired visuals, and disables the trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class RepairableObject : MonoBehaviour
{
    [Serializable]
    public struct RequiredItemEntry
    {
        public string itemId;
        [Min(1)] public int amount;
    }

    static int s_playerLayer = -1;
    static int s_repairableProximityCount;
    static RepairableObject s_currentRepairable;

    [Header("State")]
    [SerializeField] bool locked = true;
    [SerializeField] RequiredItemEntry[] requiredItems;

    [Header("Visuals")]
    [Tooltip("Shown while the object is still locked / unrepaired.")]
    [SerializeField] GameObject lockedVisual;
    [Tooltip("Shown after the object has been repaired.")]
    [SerializeField] GameObject repairedVisual;

    [Header("Lock UI")]
    [Tooltip("World-space lock UI root. Shown only while locked and the player is in the trigger.")]
    [SerializeField] GameObject lockUiRoot;
    [Tooltip("Parent of per-requirement slot children (bg, icon, txtAmount, checked).")]
    [SerializeField] Transform requiredSlotsRoot;
    [Tooltip("Indexed by ItemRarity: Common, Uncommon, Rare, Epic, Legendary, Mythic.")]
    [SerializeField] Sprite[] rarityFrames;

    Collider _triggerCollider;
    int _playerOverlapCount;
    bool _reportedRepairInteract;

    public bool IsLocked => locked;
    public RequiredItemEntry[] RequiredItems => requiredItems;
    public static RepairableObject CurrentRepairable => s_currentRepairable;

    void Awake()
    {
        ResolveRefs();
        _triggerCollider = GetComponent<Collider>();
        EnsureTriggerCollider();
        ApplyVisualState();
        ApplyTriggerEnabled();
    }

    void OnEnable()
    {
        GameEvents.PlayerRunInventoryChanged += OnInventoryChanged;
        GameEvents.DungeonRunStarted += OnDungeonRunStarted;
        RefreshAll();
    }

    void Start()
    {
        // Catalog / run host may finish Awake after OnEnable; refresh once more.
        RefreshAll();
    }

    void OnDisable()
    {
        GameEvents.PlayerRunInventoryChanged -= OnInventoryChanged;
        GameEvents.DungeonRunStarted -= OnDungeonRunStarted;
        _playerOverlapCount = 0;
        UpdateRepairInteract(false);
        if (lockUiRoot != null)
            lockUiRoot.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _playerOverlapCount++;
        RefreshAll();
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (_playerOverlapCount > 0)
            _playerOverlapCount--;

        RefreshAll();
    }

    void OnDungeonRunStarted(Dungeon _)
    {
        RefreshAll();
    }

    void OnInventoryChanged()
    {
        RefreshAll();
    }

    /// <summary>
    /// Consumes all required items, marks repaired, swaps visuals, and disables the trigger.
    /// </summary>
    public bool TryRepair()
    {
        if (!locked || !isActiveAndEnabled)
            return false;

        if (_playerOverlapCount <= 0)
            return false;

        if (!PlayerHasAllRequiredItems())
            return false;

        PlayerRunState run = DungeonRunHost.Instance != null ? DungeonRunHost.Instance.Player : null;
        if (run == null)
            return false;

        if (!TryConsumeRequiredItems(run.Inventory))
            return false;

        locked = false;
        _playerOverlapCount = 0;
        UpdateRepairInteract(false);
        ApplyVisualState();
        ApplyTriggerEnabled();
        RefreshLockUi();
        return true;
    }

    void RefreshAll()
    {
        ResolveRefs();
        ApplyVisualState();
        ApplyTriggerEnabled();
        RefreshLockUi();
        RefreshRepairInteract();
    }

    void RefreshLockUi()
    {
        bool showLockUi = isActiveAndEnabled && locked && _playerOverlapCount > 0;

        if (!showLockUi)
        {
            if (lockUiRoot != null)
                lockUiRoot.SetActive(false);
            return;
        }

        if (lockUiRoot != null)
            lockUiRoot.SetActive(true);

        FillRequirementSlots();
    }

    void RefreshRepairInteract()
    {
        bool canRepair = isActiveAndEnabled
            && locked
            && _playerOverlapCount > 0
            && PlayerHasAllRequiredItems();
        UpdateRepairInteract(canRepair);
    }

    void UpdateRepairInteract(bool canRepair)
    {
        if (_reportedRepairInteract == canRepair)
        {
            if (canRepair)
                s_currentRepairable = this;
            return;
        }

        if (_reportedRepairInteract)
        {
            if (s_repairableProximityCount > 0)
                s_repairableProximityCount--;

            if (s_currentRepairable == this)
                s_currentRepairable = null;
        }

        if (canRepair)
        {
            s_repairableProximityCount++;
            s_currentRepairable = this;
        }

        _reportedRepairInteract = canRepair;
        PlayerInteractButton.SetRepairAvailable(s_repairableProximityCount > 0);
    }

    void ApplyVisualState()
    {
        if (lockedVisual != null)
            lockedVisual.SetActive(locked);

        if (repairedVisual != null)
            repairedVisual.SetActive(!locked);
    }

    void ApplyTriggerEnabled()
    {
        if (_triggerCollider == null)
            _triggerCollider = GetComponent<Collider>();

        if (_triggerCollider != null)
            _triggerCollider.enabled = locked;
    }

    void FillRequirementSlots()
    {
        if (requiredSlotsRoot == null)
            return;

        int requirementCount = requiredItems != null ? requiredItems.Length : 0;
        int slotCount = requiredSlotsRoot.childCount;

        for (int i = 0; i < slotCount; i++)
        {
            Transform slot = requiredSlotsRoot.GetChild(i);
            bool useSlot = i < requirementCount && IsValidRequirement(requiredItems[i]);
            slot.gameObject.SetActive(useSlot);

            if (!useSlot)
                continue;

            ApplySlot(slot, requiredItems[i]);
        }
    }

    void ApplySlot(Transform slot, RequiredItemEntry entry)
    {
        ItemData data = null;
        if (ItemCatalog.Current != null && !string.IsNullOrEmpty(entry.itemId))
            ItemCatalog.Current.TryGetById(entry.itemId, out data);

        Image frame = FindChildImage(slot, "bg");
        Image icon = FindChildImage(slot, "icon");
        TextMeshProUGUI amountLabel = FindAmountLabel(slot);
        Transform checkedMark = FindChildTransform(slot, "checked");

        Sprite preview = data != null ? data.PreviewImage : null;
        if (icon != null)
        {
            icon.sprite = preview;
            icon.enabled = preview != null;
        }

        if (frame != null)
        {
            ItemRarity rarity = data != null ? data.Rarity : ItemRarity.Common;
            Sprite frameSprite = GetRarityFrame(rarity) ?? GetRarityFrame(ItemRarity.Common);
            frame.sprite = frameSprite;
            frame.enabled = frameSprite != null;
        }

        int required = Mathf.Max(1, entry.amount);
        int owned = GetOwnedAmount(entry.itemId);
        if (amountLabel != null)
        {
            amountLabel.text = $"{owned}/{required}";
            amountLabel.enabled = true;
            amountLabel.gameObject.SetActive(true);
        }

        if (checkedMark != null)
            checkedMark.gameObject.SetActive(owned >= required);
    }

    bool PlayerHasAllRequiredItems()
    {
        if (requiredItems == null || requiredItems.Length == 0)
            return false;

        for (int i = 0; i < requiredItems.Length; i++)
        {
            RequiredItemEntry entry = requiredItems[i];
            if (!IsValidRequirement(entry))
                continue;

            int required = Mathf.Max(1, entry.amount);
            if (GetOwnedAmount(entry.itemId) < required)
                return false;
        }

        return HasAnyValidRequirement();
    }

    bool HasAnyValidRequirement()
    {
        if (requiredItems == null)
            return false;

        for (int i = 0; i < requiredItems.Length; i++)
        {
            if (IsValidRequirement(requiredItems[i]))
                return true;
        }

        return false;
    }

    bool TryConsumeRequiredItems(PlayerRunInventory inventory)
    {
        if (inventory == null || requiredItems == null)
            return false;

        for (int i = 0; i < requiredItems.Length; i++)
        {
            RequiredItemEntry entry = requiredItems[i];
            if (!IsValidRequirement(entry))
                continue;

            int required = Mathf.Max(1, entry.amount);
            if (inventory.GetAmount(entry.itemId) < required)
                return false;
        }

        for (int i = 0; i < requiredItems.Length; i++)
        {
            RequiredItemEntry entry = requiredItems[i];
            if (!IsValidRequirement(entry))
                continue;

            int required = Mathf.Max(1, entry.amount);
            if (!inventory.TryRemove(entry.itemId, required))
                return false;
        }

        return true;
    }

    static bool IsValidRequirement(RequiredItemEntry entry)
    {
        return !string.IsNullOrEmpty(entry.itemId);
    }

    static int GetOwnedAmount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;

        PlayerRunState run = DungeonRunHost.Instance != null ? DungeonRunHost.Instance.Player : null;
        if (run == null)
            return 0;

        return run.Inventory.GetAmount(itemId);
    }

    Sprite GetRarityFrame(ItemRarity rarity)
    {
        int index = (int)rarity;
        if (rarityFrames == null || index < 0 || index >= rarityFrames.Length)
            return null;
        return rarityFrames[index];
    }

    void ResolveRefs()
    {
        if (lockedVisual == null)
        {
            Transform locked = transform.Find("locked");
            if (locked != null)
                lockedVisual = locked.gameObject;
        }

        if (repairedVisual == null)
        {
            Transform repaired = transform.Find("repaired");
            if (repaired != null)
                repairedVisual = repaired.gameObject;
        }

        if (lockUiRoot == null)
        {
            Transform ui = transform.Find("ui");
            if (ui != null)
                lockUiRoot = ui.gameObject;
        }

        if (requiredSlotsRoot == null && lockUiRoot != null)
        {
            Transform slots = lockUiRoot.transform.Find("locked/requiredSlots");
            if (slots != null)
                requiredSlotsRoot = slots;
        }
    }

    void EnsureTriggerCollider()
    {
        if (_triggerCollider == null)
            _triggerCollider = GetComponent<Collider>();

        if (_triggerCollider != null && !_triggerCollider.isTrigger)
            Debug.LogWarning($"[{nameof(RepairableObject)}] Collider on '{name}' should be a trigger.", this);
    }

    void Reset()
    {
        ResolveRefs();
    }

    static Image FindChildImage(Transform parent, string childName)
    {
        Transform child = FindChildTransform(parent, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    static Transform FindChildTransform(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        Transform exact = parent.Find(childName);
        if (exact != null)
            return exact;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName || child.name.StartsWith(childName + " ", StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    static TextMeshProUGUI FindAmountLabel(Transform slot)
    {
        Transform labeled = FindChildTransform(slot, "txtAmount");
        if (labeled != null)
        {
            TextMeshProUGUI tmp = labeled.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                return tmp;
        }

        return slot.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (s_playerLayer < 0)
            s_playerLayer = LayerMask.NameToLayer("Player");

        return s_playerLayer >= 0 && other.gameObject.layer == s_playerLayer;
    }
}
