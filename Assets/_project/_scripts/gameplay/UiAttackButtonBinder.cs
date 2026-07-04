using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(101)]
[RequireComponent(typeof(Button))]
public class UiAttackButtonBinder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] FeneraxJoystickMoveIntentProvider intentProvider;
    [Tooltip("If unset, uses first PlayerEntityState in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] float enabledAlpha = 1f;
    [Tooltip("When enabled, holding the attack button keeps attack input active until release. When disabled, only the initial press triggers an attack.")]
    [SerializeField] bool enableHoldAttack = true;

    Button _button;
    CanvasGroup _canvasGroup;
    bool _hasIntentProvider;

    void Awake()
    {
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();

        _hasIntentProvider = intentProvider != null;

        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();

        _button = GetComponent<Button>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void LateUpdate()
    {
        RefreshBlockedVisual();
    }

    void OnDestroy()
    {
        if (_hasIntentProvider)
            intentProvider.RegisterUiAttackPointerUp();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_hasIntentProvider || IsAttackBlocked())
            return;

        if (enableHoldAttack)
            intentProvider.RegisterUiAttackPointerDown();
        else
            intentProvider.RegisterUiAttackFromUi();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_hasIntentProvider || !enableHoldAttack)
            return;
        intentProvider.RegisterUiAttackPointerUp();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_hasIntentProvider || !enableHoldAttack)
            return;
        intentProvider.RegisterUiAttackPointerUp();
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
