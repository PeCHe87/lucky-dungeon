using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(101)]
[RequireComponent(typeof(Button))]
public class UiAttackButtonBinder : MonoBehaviour
{
    [SerializeField] FeneraxJoystickMoveIntentProvider intentProvider;
    [Tooltip("If unset, uses first PlayerEntityState in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] float enabledAlpha = 1f;

    Button _button;
    CanvasGroup _canvasGroup;

    void Awake()
    {
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();

        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();

        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        _button.onClick.AddListener(OnAttackClicked);
    }

    void LateUpdate()
    {
        RefreshBlockedVisual();
    }

    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnAttackClicked);
    }

    void OnAttackClicked()
    {
        if (IsAttackBlocked())
            return;
        if (intentProvider != null)
            intentProvider.RegisterUiAttackFromUi();
    }

    bool IsAttackBlocked() =>
        playerEntityState != null && playerEntityState.IsAttackInputBlocked;

    void RefreshBlockedVisual()
    {
        if (_button == null)
            return;

        bool blocked = IsAttackBlocked();
        _button.interactable = !blocked;
        if (_canvasGroup != null)
            _canvasGroup.alpha = blocked ? disabledAlpha : enabledAlpha;
    }
}

/// <summary>Shows a child <c>selection</c> object when the corresponding melee/ranged slot is equipped on <see cref="WeaponHolder"/>.</summary>
public sealed class WeaponSlotSelectionIndicator : MonoBehaviour
{
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] GameObject selectionVisual;
    [Tooltip("If true, this control tracks the melee slot; otherwise the ranged slot.")]
    [SerializeField] bool representsMelee;

    bool _listening;

    void Awake()
    {
        if (selectionVisual == null)
        {
            Transform t = transform.Find("selection");
            if (t != null)
                selectionVisual = t.gameObject;
        }
    }

    void OnEnable()
    {
        TrySubscribeToHolder();
    }

    void OnDisable()
    {
        if (weaponHolder != null && _listening)
        {
            weaponHolder.EquippedWeaponChanged -= OnWeaponChanged;
            _listening = false;
        }
    }

    void Start()
    {
        // After all Awakes (WeaponHolder initial equip); selection reflects equipped type, not button press side effects.
        TrySubscribeToHolder();
        Refresh();
    }

    void OnWeaponChanged() => Refresh();

    void TrySubscribeToHolder()
    {
        if (weaponHolder == null)
            weaponHolder = FindFirstObjectByType<WeaponHolder>();
        if (weaponHolder == null || _listening)
            return;
        weaponHolder.EquippedWeaponChanged += OnWeaponChanged;
        _listening = true;
    }

    void Refresh()
    {
        TrySubscribeToHolder();
        if (selectionVisual == null)
        {
            Transform t = transform.Find("selection");
            if (t != null)
                selectionVisual = t.gameObject;
        }
        if (weaponHolder == null || selectionVisual == null)
            return;
        bool on = representsMelee ? weaponHolder.IsMeleeEquipped() : weaponHolder.IsRangedEquipped();
        selectionVisual.SetActive(on);
    }
}
