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
    [Tooltip("If unset, uses first PlayerAttackController in the scene.")]
    [SerializeField] PlayerAttackController attackController;
    [SerializeField, Min(1f)] float dragActivationPixels = 16f;
    [SerializeField, Range(0.05f, 0.35f)] float maxTapHoldDuration = 0.18f;
    [Tooltip("If the stick finger is still held after an attack cancel, resume move automatically once this idle time passes with no new attack.")]
    [SerializeField, Min(0f)] float autoResumeMoveAfterAttackSeconds = 0.35f;

    Joystick _joystick;
    bool _pointerActive;
    bool _dragActive;
    int _activePointerId = int.MinValue;
    int _suppressedPointerId = int.MinValue;
    bool _stickSuppressed;
    bool _hasSuppressedPosition;
    Vector2 _suppressedLastPosition;
    float _lastAttackCancelUnscaledTime = -999f;
    Vector2 _downPosition;
    Vector2 _lastStickScreenPos;
    float _downTime;
    float _dragActivationPixelsSq;

    /// <summary>True after attack (or other) force-cancel until resume (re-drag or idle timeout).</summary>
    public bool IsStickSuppressed => _stickSuppressed;

    /// <summary>True while the stick is actively driving movement.</summary>
    public bool IsActivelyDragging => _pointerActive && _dragActive && !_stickSuppressed;

    void Awake()
    {
        _joystick = GetComponent<Joystick>();
        if (intentProvider == null)
            intentProvider = FindFirstObjectByType<FeneraxJoystickMoveIntentProvider>();
        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();
        if (attackController == null)
            attackController = FindFirstObjectByType<PlayerAttackController>();
    }

    void Start()
    {
        _dragActivationPixelsSq = dragActivationPixels * dragActivationPixels;
        if (_joystick != null)
            _joystick.enabled = false;
    }

    void LateUpdate()
    {
        TryAutoResumeAfterAttackIdle();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanAcceptInput())
            return;

        // Same finger still down after force-cancel: track position for auto-resume, don't re-bind yet.
        if (eventData.pointerId == _suppressedPointerId)
        {
            _suppressedLastPosition = eventData.position;
            _hasSuppressedPosition = true;
            return;
        }

        // Second finger on the stick UI while already steering: treat as attack (screen-tap priority).
        if (_pointerActive)
        {
            if (eventData.pointerId != _activePointerId && intentProvider != null)
                intentProvider.RegisterUiAttackFromUi();
            return;
        }

        _pointerActive = true;
        _dragActive = false;
        _activePointerId = eventData.pointerId;
        _downPosition = eventData.position;
        _lastStickScreenPos = eventData.position;
        _downTime = Time.unscaledTime;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Keep last position while suppressed so auto-resume does not need touch-id matching.
        if (eventData.pointerId == _suppressedPointerId)
        {
            _suppressedLastPosition = eventData.position;
            _hasSuppressedPosition = true;
            return;
        }

        if (!_pointerActive || eventData.pointerId != _activePointerId || !CanAcceptInput())
            return;

        _lastStickScreenPos = eventData.position;

        if (_dragActive)
        {
            // Attack started after drag began: drop stick immediately.
            if (IsAttackBlockingLocomotion())
            {
                ForceCancelActivePointer();
                return;
            }

            _joystick.OnDrag(eventData);
            return;
        }

        if ((eventData.position - _downPosition).sqrMagnitude <= _dragActivationPixelsSq)
            return;

        // Resume only after a fresh drag, and only when not attacking.
        if (_stickSuppressed && IsAttackBlockingLocomotion())
            return;

        _stickSuppressed = false;
        _suppressedPointerId = int.MinValue;
        _hasSuppressedPosition = false;
        _dragActive = true;
        _joystick.OnPointerDown(eventData);
        _joystick.OnDrag(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == _suppressedPointerId)
        {
            _suppressedPointerId = int.MinValue;
            _stickSuppressed = false;
            _hasSuppressedPosition = false;
            return;
        }

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

    /// <summary>
    /// Clears stick input immediately. Movement resumes after re-drag, or automatically if the
    /// stick finger stays held and no new attack arrives within <see cref="autoResumeMoveAfterAttackSeconds"/>.
    /// </summary>
    public void ForceCancelActivePointer()
    {
        if (_pointerActive)
        {
            _suppressedPointerId = _activePointerId;
            _suppressedLastPosition = _lastStickScreenPos;
            _hasSuppressedPosition = true;
        }

        ClearJoystickInput();

        _stickSuppressed = true;
        _lastAttackCancelUnscaledTime = Time.unscaledTime;
        _pointerActive = false;
        _dragActive = false;
        _activePointerId = int.MinValue;
    }

    void TryAutoResumeAfterAttackIdle()
    {
        if (!_stickSuppressed || _suppressedPointerId == int.MinValue)
            return;
        if (autoResumeMoveAfterAttackSeconds <= 0f)
            return;
        if (Time.unscaledTime - _lastAttackCancelUnscaledTime < autoResumeMoveAfterAttackSeconds)
            return;
        if (!CanAcceptInput() || IsAttackBlockingLocomotion())
            return;
        if (!_hasSuppressedPosition)
            return;

        EventSystem es = EventSystem.current;
        if (es == null || _joystick == null)
            return;

        int resumedId = _suppressedPointerId;
        Vector2 screenPos = _suppressedLastPosition;
        var data = new PointerEventData(es)
        {
            pointerId = resumedId,
            position = screenPos
        };

        _stickSuppressed = false;
        _suppressedPointerId = int.MinValue;
        _hasSuppressedPosition = false;
        _pointerActive = true;
        _dragActive = true;
        _activePointerId = resumedId;
        _downPosition = screenPos;
        _lastStickScreenPos = screenPos;
        _downTime = Time.unscaledTime;

        // Floating/Variable joystick: OnPointerDown shows the background again.
        _joystick.OnPointerDown(data);
        _joystick.OnDrag(data);
    }

    void ClearJoystickInput()
    {
        if (_joystick == null)
            return;

        EventSystem es = EventSystem.current;
        if (es == null)
            return;

        _joystick.OnPointerUp(new PointerEventData(es) { pointerId = _activePointerId });
    }

    bool IsAttackBlockingLocomotion() =>
        attackController != null && attackController.IsAttackBlockingLocomotion;

    bool CanAcceptInput() =>
        playerEntityState == null || !playerEntityState.IsInputBlocked;
}
