using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scene popup that compares the player's current slot weapon (left) with a pickup (right).
/// Root stays active so <see cref="Instance"/> is registered; visibility is driven by <c>content</c>.
/// </summary>
[DisallowMultipleComponent]
public sealed class SwapWeaponPopup : MonoBehaviour
{
    const string NoneLabel = "None";
    const string CloseButtonName = "button_close";
    const string EquipButtonName = "btnEquip";

    public static SwapWeaponPopup Instance { get; private set; }

    [SerializeField] TextMeshProUGUI currentWeaponName;
    [SerializeField] TextMeshProUGUI newWeaponName;
    [SerializeField] GameObject contentRoot;
    [SerializeField] Button closeButton;
    [SerializeField] Button equipButton;

    WeaponHolder _holder;
    WeaponData _pendingNewWeapon;
    bool _pauseHeld;

    bool _loggedMissingCloseButton;
    bool _loggedMissingEquipButton;
    bool _closeButtonBound;
    bool _equipButtonBound;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveRefs();
        BindButtons();
        SetContentVisible(false);
    }

    void OnDestroy()
    {
        if (closeButton != null && _closeButtonBound)
            closeButton.onClick.RemoveListener(Hide);

        if (equipButton != null && _equipButtonBound)
            equipButton.onClick.RemoveListener(OnEquipClicked);

        ReleasePause();

        if (Instance == this)
            Instance = null;
    }

    public void Show(WeaponHolder holder, WeaponData currentWeapon, WeaponData newWeapon)
    {
        ResolveRefs();
        BindButtons();

        _holder = holder;
        _pendingNewWeapon = newWeapon;

        ApplyName(currentWeaponName, currentWeapon);
        ApplyName(newWeaponName, newWeapon);
        SetContentVisible(true);
        HoldPause();
    }

    public void Hide()
    {
        SetContentVisible(false);
        ClearPending();
        ReleasePause();
    }

    void HoldPause()
    {
        if (_pauseHeld)
            return;

        GamePause.Pause(this);
        _pauseHeld = true;
    }

    void ReleasePause()
    {
        if (!_pauseHeld)
            return;

        GamePause.Resume(this);
        _pauseHeld = false;
    }

    void OnEquipClicked()
    {
        if (_holder == null || _pendingNewWeapon == null)
        {
            Debug.LogWarning("[SwapWeaponPopup] Equip pressed with no pending weapon/holder.", this);
            return;
        }

        if (!_holder.TryReplaceSlotWeapon(_pendingNewWeapon))
            return;

        Hide();
    }

    void ClearPending()
    {
        _holder = null;
        _pendingNewWeapon = null;
    }

    void SetContentVisible(bool visible)
    {
        if (contentRoot != null)
            contentRoot.SetActive(visible);
    }

    void BindButtons()
    {
        if (!_closeButtonBound && closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
            _closeButtonBound = true;
        }

        if (!_equipButtonBound && equipButton != null)
        {
            equipButton.onClick.AddListener(OnEquipClicked);
            _equipButtonBound = true;
        }
    }

    static void ApplyName(TextMeshProUGUI label, WeaponData data)
    {
        if (label == null)
            return;

        if (data == null || string.IsNullOrEmpty(data.DisplayName))
            label.text = NoneLabel;
        else
            label.text = data.DisplayName;
    }

    void ResolveRefs()
    {
        if (contentRoot == null)
        {
            Transform content = transform.Find("content");
            if (content != null)
                contentRoot = content.gameObject;
        }

        if (currentWeaponName == null)
        {
            Transform panel = FindDeepChild(transform, "currentWeapon");
            if (panel != null)
            {
                Transform label = panel.Find("txtName");
                if (label != null)
                    currentWeaponName = label.GetComponent<TextMeshProUGUI>();
            }
        }

        if (newWeaponName == null)
        {
            Transform panel = FindDeepChild(transform, "newWeapon");
            if (panel != null)
            {
                Transform label = panel.Find("txtName");
                if (label != null)
                    newWeaponName = label.GetComponent<TextMeshProUGUI>();
            }
        }

        if (closeButton == null)
        {
            Transform closeTransform = FindDeepChild(transform, CloseButtonName);
            if (closeTransform != null)
                closeButton = closeTransform.GetComponent<Button>();
        }

        if (closeButton == null && !_loggedMissingCloseButton)
        {
            Debug.LogWarning(
                $"[SwapWeaponPopup] Missing '{CloseButtonName}' Button under '{name}'.",
                this);
            _loggedMissingCloseButton = true;
        }

        if (equipButton == null)
        {
            Transform equipTransform = FindDeepChild(transform, EquipButtonName);
            if (equipTransform != null)
                equipButton = equipTransform.GetComponent<Button>();
        }

        if (equipButton == null && !_loggedMissingEquipButton)
        {
            Debug.LogWarning(
                $"[SwapWeaponPopup] Missing '{EquipButtonName}' Button under '{name}'.",
                this);
            _loggedMissingEquipButton = true;
        }
    }

    static Transform FindDeepChild(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
                return child;

            Transform nested = FindDeepChild(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
