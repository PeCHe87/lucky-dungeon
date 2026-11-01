using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(101)]
[RequireComponent(typeof(Button))]
public sealed class UiSwapWeaponButtonBinder : MonoBehaviour, IPointerDownHandler
{
    const string WeaponIconPath = "icon";
    const string AmmoPanelPath = "ammo";
    const string AmmoIconPath = "ammo/icon";
    const string AmmoLabelPath = "ammo/txtAmmo";

    [SerializeField] WeaponHolder weaponHolder;
    [Tooltip("If unset, uses first PlayerEntityState in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] float enabledAlpha = 1f;

    [Header("Equipped weapon")]
    [SerializeField] Image weaponIcon;

    [Header("Inventory ammo")]
    [SerializeField] GameObject ammoPanel;
    [SerializeField] Image ammoIcon;
    [SerializeField] TextMeshProUGUI ammoLabel;

    Button _button;
    CanvasGroup _canvasGroup;
    string _trackedAmmoItemId;
    int _lastShownAmmoAmount = int.MinValue;

    void Awake()
    {
        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();

        if (weaponHolder == null && playerEntityState != null)
            weaponHolder = playerEntityState.GetComponent<WeaponHolder>();

        if (weaponHolder == null)
            weaponHolder = FindFirstObjectByType<WeaponHolder>();

        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ResolveUiRefs();
    }

    void OnEnable()
    {
        ResolveUiRefs();

        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;

        GameEvents.PlayerRunInventoryChanged += OnInventoryChanged;
        GameEvents.LootObtained += OnLootObtained;
        GameEvents.DungeonRunStarted += OnDungeonRunStarted;
        RefreshEquippedWeaponUi();
    }

    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;

        GameEvents.PlayerRunInventoryChanged -= OnInventoryChanged;
        GameEvents.LootObtained -= OnLootObtained;
        GameEvents.DungeonRunStarted -= OnDungeonRunStarted;
    }

    void LateUpdate()
    {
        RefreshBlockedVisual();
        SyncTrackedAmmoAmountIfChanged();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsSwapBlocked())
            return;

        if (weaponHolder != null)
            weaponHolder.SwapEquippedWeapon();
    }

    void OnEquippedWeaponChanged() => RefreshEquippedWeaponUi();

    void OnDungeonRunStarted(Dungeon _) => RefreshEquippedWeaponUi();

    void OnInventoryChanged() => RefreshAmmoPanel();

    void OnLootObtained(IReadOnlyList<(string itemId, int amount)> entries)
    {
        if (string.IsNullOrEmpty(_trackedAmmoItemId) || entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].itemId == _trackedAmmoItemId)
            {
                RefreshAmmoPanel();
                return;
            }
        }
    }

    void RefreshEquippedWeaponUi()
    {
        RefreshWeaponIcon();
        RefreshAmmoPanel();
    }

    void SyncTrackedAmmoAmountIfChanged()
    {
        if (string.IsNullOrEmpty(_trackedAmmoItemId))
            return;

        int amount = GetInventoryAmount(_trackedAmmoItemId);
        if (amount == _lastShownAmmoAmount)
            return;

        RefreshAmmoPanel();
    }

    bool IsSwapBlocked() =>
        playerEntityState != null && playerEntityState.IsAttackInputBlocked;

    void RefreshBlockedVisual()
    {
        if (_button == null)
            return;

        bool blocked = IsSwapBlocked();
        _button.interactable = !blocked;
        if (_canvasGroup != null)
            _canvasGroup.alpha = blocked ? disabledAlpha : enabledAlpha;
    }

    void ResolveUiRefs()
    {
        if (weaponIcon == null)
        {
            Transform icon = transform.Find(WeaponIconPath);
            if (icon != null)
                weaponIcon = icon.GetComponent<Image>();
        }

        if (ammoPanel == null)
        {
            Transform panel = transform.Find(AmmoPanelPath);
            if (panel != null)
                ammoPanel = panel.gameObject;
        }

        if (ammoIcon == null)
        {
            Transform icon = transform.Find(AmmoIconPath);
            if (icon != null)
                ammoIcon = icon.GetComponent<Image>();
        }

        if (ammoLabel == null)
        {
            Transform label = transform.Find(AmmoLabelPath);
            if (label != null)
                ammoLabel = label.GetComponent<TextMeshProUGUI>();
        }
    }

    void RefreshWeaponIcon()
    {
        ResolveUiRefs();
        if (weaponIcon == null)
            return;

        Sprite iconSprite = null;
        if (weaponHolder?.Current is IWeaponDataSource source && source.Data != null)
            iconSprite = source.Data.Icon;

        weaponIcon.sprite = iconSprite;
        weaponIcon.enabled = iconSprite != null;
    }

    void RefreshAmmoPanel()
    {
        ResolveUiRefs();

        bool isRanged = weaponHolder != null && weaponHolder.IsRangedEquipped();
        if (ammoPanel != null && ammoPanel.activeSelf != isRanged)
            ammoPanel.SetActive(isRanged);

        if (!isRanged)
        {
            _trackedAmmoItemId = null;
            _lastShownAmmoAmount = int.MinValue;
            return;
        }

        ItemData ammoItem = null;
        if (weaponHolder.Current is RangedWeapon ranged
            && ranged.Data is AssaultWeaponData assaultData)
        {
            ammoItem = assaultData.RequiredAmmoItem;
        }

        int amount = 0;
        Sprite iconSprite = null;
        if (ammoItem != null && !string.IsNullOrEmpty(ammoItem.ItemId))
        {
            _trackedAmmoItemId = ammoItem.ItemId;
            amount = GetInventoryAmount(_trackedAmmoItemId);
            iconSprite = ammoItem.PreviewImage;
        }
        else
        {
            _trackedAmmoItemId = null;
        }

        _lastShownAmmoAmount = amount;

        if (ammoLabel != null)
            ammoLabel.text = amount.ToString();

        if (ammoIcon != null)
        {
            ammoIcon.sprite = iconSprite;
            ammoIcon.enabled = iconSprite != null;
        }
    }

    static int GetInventoryAmount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;

        PlayerRunInventory inventory = DungeonRunHost.Instance != null
            ? DungeonRunHost.Instance.Player?.Inventory
            : null;
        return inventory != null ? inventory.GetAmount(itemId) : 0;
    }
}
