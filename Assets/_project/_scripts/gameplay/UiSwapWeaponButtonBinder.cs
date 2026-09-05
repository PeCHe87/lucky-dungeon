using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(101)]
[RequireComponent(typeof(Button))]
public sealed class UiSwapWeaponButtonBinder : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] WeaponHolder weaponHolder;
    [Tooltip("If unset, uses first PlayerEntityState in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField, Range(0f, 1f)] float disabledAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] float enabledAlpha = 1f;

    Button _button;
    CanvasGroup _canvasGroup;

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
    }

    void LateUpdate()
    {
        RefreshBlockedVisual();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsSwapBlocked())
            return;

        if (weaponHolder != null)
            weaponHolder.SwapEquippedWeapon();
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
}
