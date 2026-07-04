using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Full-screen transparent panel: a tap (short contact with minimal movement) registers an attack.
/// Place below interactive HUD controls so weapon buttons keep priority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class ScreenTapAttackInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [SerializeField] FeneraxJoystickMoveIntentProvider intentProvider;
    [Tooltip("If unset, uses first PlayerEntityState in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField, Min(1f)] float maxTapMovementPixels = 16f;
    [SerializeField, Range(0.05f, 0.35f)] float maxTapHoldDuration = 0.18f;

    Image _image;
    bool _pointerActive;
    bool _blockedByInteractiveUi;
    Vector2 _downPosition;
    float _downTime;
    float _maxTapMovementPixelsSq;

    void Awake()
    {
        _image = GetComponent<Image>();
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();
        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();

        if (_image != null)
        {
            Color c = _image.color;
            c.a = 0f;
            _image.color = c;
            _image.raycastTarget = true;
        }
    }

    void Start()
    {
        _maxTapMovementPixelsSq = maxTapMovementPixels * maxTapMovementPixels;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAcceptInput())
            return;

        _pointerActive = true;
        _blockedByInteractiveUi = IsBlockedByInteractiveUi(eventData);
        _downPosition = eventData.position;
        _downTime = Time.unscaledTime;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_pointerActive)
            return;

        if ((eventData.position - _downPosition).sqrMagnitude > _maxTapMovementPixelsSq)
            _pointerActive = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pointerActive)
            return;

        _pointerActive = false;

        if (_blockedByInteractiveUi || !CanAcceptInput())
            return;

        float holdDuration = Time.unscaledTime - _downTime;
        if (holdDuration > maxTapHoldDuration)
            return;

        if ((eventData.position - _downPosition).sqrMagnitude > _maxTapMovementPixelsSq)
            return;

        if (intentProvider != null)
            intentProvider.RegisterUiAttackFromUi();
    }

    bool CanAcceptInput() =>
        playerEntityState == null || !playerEntityState.IsInputBlocked;

    bool IsBlockedByInteractiveUi(PointerEventData eventData)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var results = new System.Collections.Generic.List<RaycastResult>();
        eventSystem.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hit = results[i].gameObject;
            if (hit == null || hit == gameObject || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponentInParent<Selectable>() != null)
                return true;

            if (hit.GetComponentInParent<Joystick>() != null)
                return true;

            if (hit.GetComponentInParent<TapOrDragJoystickGate>() != null)
                return true;
        }

        return false;
    }
}
