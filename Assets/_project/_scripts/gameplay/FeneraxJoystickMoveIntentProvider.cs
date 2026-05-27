using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Merges Input System Move (keyboard / gamepad) with a Fenerax Joystick Pack stick.
/// Asset: https://assetstore.unity.com/packages/tools/input-management/joystick-pack-107631
/// After importing, assign any Fixed / Floating / Variable joystick component to <see cref="virtualJoystick"/>.
/// </summary>
public class FeneraxJoystickMoveIntentProvider : MonoBehaviour, IMoveIntentProvider, IDashIntentProvider, IAttackIntentProvider
{
    [Tooltip("Optional. Pick PlayerControls → Player → Move. Leave empty if you use Player Input on this object or assign Controls Asset below.")]
    [SerializeField] InputActionProperty moveAction;
    [Tooltip("Optional. Pick PlayerControls → Player → Attack. Leave empty to use Player Input / Controls Asset below.")]
    [SerializeField] InputActionProperty attackAction;
    [Tooltip("Optional. If Move is empty, movement is read from this component's Actions (same as your .inputactions asset). Auto-fills from Player Input on this GameObject.")]
    [SerializeField] PlayerInput playerInput;
    [Tooltip("Optional. If Move is empty and no Player Input: drag PlayerControls here to enable the Player map and use Move.")]
    [SerializeField] InputActionAsset playerControlsAsset;
    [Tooltip("Assign FixedJoystick, VariableJoystick, or FloatingJoystick from Joystick Pack (Fenerax).")]
    [SerializeField] MonoBehaviour virtualJoystick;
    [SerializeField] float stickDeadzone = 0.08f;

    InputAction _moveFromEnabledAssetMap;
    InputAction _attackFromEnabledAssetMap;
    bool _enabledPlayerMapOnAsset;
    Func<Vector2> _readVirtualStick;
    bool _uiAttackThisFrame;
    IJoystickDoubleTapSink _doubleTapSink;
    JoystickDoubleTapBridge _joystickDoubleTapBridge;

    void Awake()
    {
        if (playerInput == null)
            TryGetComponent(out playerInput);

        CacheVirtualStickReader();
        EnsureJoystickDoubleTapBridge();
    }

    void OnValidate()
    {
        CacheVirtualStickReader();
        EnsureJoystickDoubleTapBridge();
    }

    /// <summary>Registers a sink for joystick double-tap pointer events (e.g. <see cref="DashJoystickDoubleTapController"/>).</summary>
    public void RegisterDoubleTapSink(IJoystickDoubleTapSink sink)
    {
        _doubleTapSink = sink;
        if (_joystickDoubleTapBridge != null)
            _joystickDoubleTapBridge.SetSink(sink);
    }

    public void UnregisterDoubleTapSink(IJoystickDoubleTapSink sink)
    {
        if (_doubleTapSink != sink)
            return;
        _doubleTapSink = null;
        if (_joystickDoubleTapBridge != null)
            _joystickDoubleTapBridge.SetSink(null);
    }

    void EnsureJoystickDoubleTapBridge()
    {
        if (virtualJoystick == null)
        {
            _joystickDoubleTapBridge = null;
            return;
        }

        GameObject joystickObject = virtualJoystick.gameObject;
        if (!joystickObject.TryGetComponent(out _joystickDoubleTapBridge))
            _joystickDoubleTapBridge = joystickObject.AddComponent<JoystickDoubleTapBridge>();

        _joystickDoubleTapBridge.SetSink(_doubleTapSink);
    }

    void CacheVirtualStickReader()
    {
        _readVirtualStick = null;
        if (virtualJoystick == null)
            return;

        Type t = virtualJoystick.GetType();
        PropertyInfo dirProp = t.GetProperty("Direction", BindingFlags.Public | BindingFlags.Instance);
        if (dirProp != null && dirProp.PropertyType == typeof(Vector2))
        {
            _readVirtualStick = () => (Vector2)dirProp.GetValue(virtualJoystick);
            return;
        }

        PropertyInfo hProp = t.GetProperty("Horizontal", BindingFlags.Public | BindingFlags.Instance);
        PropertyInfo vProp = t.GetProperty("Vertical", BindingFlags.Public | BindingFlags.Instance);
        if (hProp != null && vProp != null
            && hProp.PropertyType == typeof(float) && vProp.PropertyType == typeof(float))
        {
            _readVirtualStick = () => new Vector2(
                (float)hProp.GetValue(virtualJoystick),
                (float)vProp.GetValue(virtualJoystick));
        }
    }

    void OnEnable()
    {
        _enabledPlayerMapOnAsset = false;
        _moveFromEnabledAssetMap = null;
        _attackFromEnabledAssetMap = null;

        if (moveAction.action != null)
            moveAction.action.Enable();
        if (attackAction.action != null)
            attackAction.action.Enable();

        EnsureJoystickDoubleTapBridge();

        if (moveAction.action != null)
            return;

        if (playerInput != null)
            return;

        if (playerControlsAsset != null)
        {
            InputActionMap map = playerControlsAsset.FindActionMap("Player");
            if (map != null)
            {
                map.Enable();
                _enabledPlayerMapOnAsset = true;
                _moveFromEnabledAssetMap = map.FindAction("Move", throwIfNotFound: false);
                _attackFromEnabledAssetMap = map.FindAction("Attack", throwIfNotFound: false);
            }
        }
    }

    void OnDisable()
    {
        if (moveAction.action != null)
            moveAction.action.Disable();
        if (attackAction.action != null)
            attackAction.action.Disable();

        if (_enabledPlayerMapOnAsset && playerControlsAsset != null)
            playerControlsAsset.FindActionMap("Player")?.Disable();

        _moveFromEnabledAssetMap = null;
        _attackFromEnabledAssetMap = null;
        _enabledPlayerMapOnAsset = false;
    }

    /// <summary>No-op; dash is joystick double-tap only via <see cref="DashJoystickDoubleTapController"/>.</summary>
    public void RegisterUiDashFromUi() { }

    /// <summary>Called from UI (e.g. <see cref="UiAttackButtonBinder"/>) to request an attack on the next gameplay read.</summary>
    public void RegisterUiAttackFromUi()
    {
        _uiAttackThisFrame = true;
    }

    public Vector2 GetMoveIntent()
    {
        InputAction action = moveAction.action;
        if (action == null && playerInput != null && playerInput.actions != null)
            action = playerInput.actions.FindAction("Move", throwIfNotFound: false);
        if (action == null)
            action = _moveFromEnabledAssetMap;

        Vector2 fromActions = action != null ? action.ReadValue<Vector2>() : Vector2.zero;

        if (_readVirtualStick == null)
            return fromActions;

        Vector2 fromStick = _readVirtualStick();
        float dz = stickDeadzone * stickDeadzone;
        if (fromStick.sqrMagnitude > dz)
        {
            if (fromStick.sqrMagnitude > 1f)
                fromStick.Normalize();
            return fromStick;
        }

        return fromActions;
    }

    public bool WasDashPressedThisFrame() => false;

    public bool WasAttackPressedThisFrame()
    {
        bool fromUi = _uiAttackThisFrame;
        _uiAttackThisFrame = false;

        InputAction action = attackAction.action;
        if (action == null && playerInput != null && playerInput.actions != null)
            action = playerInput.actions.FindAction("Attack", throwIfNotFound: false);
        if (action == null)
            action = _attackFromEnabledAssetMap;

        bool fromAction = action != null && action.WasPressedThisFrame();
        // Same pointer/click can map to Attack and UI (e.g. weapon equip buttons). Never block UI-driven attack.
        if (fromAction && IsPointerOverGameObjectUi())
            fromAction = false;

        return fromUi || fromAction;
    }

    /// <summary>True when a pointer/touch is over a UI object so gameplay Attack should not consume the same press.</summary>
    public static bool IsPointerOverGameObjectUi()
    {
        EventSystem es = EventSystem.current;
        if (es == null || !es.isActiveAndEnabled)
            return false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            int fingerId = Input.GetTouch(i).fingerId;
            if (es.IsPointerOverGameObject(fingerId))
                return true;
        }

        // Input System touch devices (when touches are not mirrored to UnityEngine.Input.touchCount)
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var touches = touchscreen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl touch = touches[i];
                if (!touch.press.isPressed)
                    continue;
                int touchId = touch.touchId.ReadValue();
                if (es.IsPointerOverGameObject(touchId))
                    return true;
            }
        }

        return es.IsPointerOverGameObject();
    }
}
