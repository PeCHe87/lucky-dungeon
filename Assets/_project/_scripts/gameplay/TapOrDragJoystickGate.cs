using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Defers Joystick Pack input until the pointer moves beyond a pixel threshold.
/// A release before that threshold registers a screen tap attack instead of movement.
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Joystick))]
public sealed class TapOrDragJoystickGate : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] FeneraxJoystickMoveIntentProvider intentProvider;
    [Tooltip("If unset, uses first PlayerEntityState in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField, Min(1f)] float dragActivationPixels = 16f;
    [SerializeField, Range(0.05f, 0.35f)] float maxTapHoldDuration = 0.18f;

    Joystick _joystick;
    bool _pointerActive;
    bool _dragActive;
    int _activePointerId = int.MinValue;
    Vector2 _downPosition;
    float _downTime;
    float _dragActivationPixelsSq;

    void Awake()
    {
        _joystick = GetComponent<Joystick>();
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();
        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();
    }

    void Start()
    {
        _dragActivationPixelsSq = dragActivationPixels * dragActivationPixels;
        if (_joystick != null)
            _joystick.enabled = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_pointerActive || !CanAcceptInput())
            return;

        _pointerActive = true;
        _dragActive = false;
        _activePointerId = eventData.pointerId;
        _downPosition = eventData.position;
        _downTime = Time.unscaledTime;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_pointerActive || eventData.pointerId != _activePointerId || !CanAcceptInput())
            return;

        if (_dragActive)
        {
            _joystick.OnDrag(eventData);
            return;
        }

        if ((eventData.position - _downPosition).sqrMagnitude <= _dragActivationPixelsSq)
            return;

        _dragActive = true;
        _joystick.OnPointerDown(eventData);
        _joystick.OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pointerActive || eventData.pointerId != _activePointerId)
            return;

        _pointerActive = false;
        _activePointerId = int.MinValue;

        if (_dragActive)
        {
            _joystick.OnPointerUp(eventData);
            _dragActive = false;
            return;
        }

        if (!CanAcceptInput())
            return;

        float holdDuration = Time.unscaledTime - _downTime;
        if (holdDuration > maxTapHoldDuration)
            return;

        if (intentProvider != null)
            intentProvider.RegisterUiAttackFromUi();
    }

    bool CanAcceptInput() =>
        playerEntityState == null || !playerEntityState.IsInputBlocked;
}
