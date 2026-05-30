using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputMoveIntentProvider : MonoBehaviour, IMoveIntentProvider, IDashIntentProvider, IAttackIntentProvider
{
    [Tooltip("Optional. Pick PlayerControls → Player → Move. Leave empty if you use Player Input on this object or assign Controls Asset below.")]
    [SerializeField] InputActionProperty moveAction;
    [Tooltip("Optional. Pick PlayerControls → Player → Dash. Leave empty to use Player Input / Controls Asset below.")]
    [SerializeField] InputActionProperty dashAction;
    [Tooltip("Optional. Pick PlayerControls → Player → Attack. Leave empty to use Player Input / Controls Asset below.")]
    [SerializeField] InputActionProperty attackAction;
    [Tooltip("Optional. If Move is empty, movement is read from this component's Actions (same as your .inputactions asset). Auto-fills from Player Input on this GameObject.")]
    [SerializeField] PlayerInput playerInput;
    [Tooltip("Optional. If Move is empty and no Player Input: drag PlayerControls here to enable the Player map and use Move.")]
    [SerializeField] InputActionAsset playerControlsAsset;

    InputAction _moveFromEnabledAssetMap;
    InputAction _dashFromEnabledAssetMap;
    InputAction _attackFromEnabledAssetMap;
    bool _enabledPlayerMapOnAsset;

    void Awake()
    {
        if (playerInput == null)
            TryGetComponent(out playerInput);
    }

    void OnEnable()
    {
        _enabledPlayerMapOnAsset = false;
        _moveFromEnabledAssetMap = null;
        _dashFromEnabledAssetMap = null;
        _attackFromEnabledAssetMap = null;

        if (moveAction.action != null)
            moveAction.action.Enable();
        if (dashAction.action != null)
            dashAction.action.Enable();
        if (attackAction.action != null)
            attackAction.action.Enable();

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
                _dashFromEnabledAssetMap = map.FindAction("Dash", throwIfNotFound: false);
                _attackFromEnabledAssetMap = map.FindAction("Attack", throwIfNotFound: false);
            }
        }
    }

    void OnDisable()
    {
        if (moveAction.action != null)
            moveAction.action.Disable();
        if (dashAction.action != null)
            dashAction.action.Disable();
        if (attackAction.action != null)
            attackAction.action.Disable();

        if (_enabledPlayerMapOnAsset && playerControlsAsset != null)
            playerControlsAsset.FindActionMap("Player")?.Disable();

        _moveFromEnabledAssetMap = null;
        _dashFromEnabledAssetMap = null;
        _attackFromEnabledAssetMap = null;
        _enabledPlayerMapOnAsset = false;
    }

    public Vector2 GetMoveIntent()
    {
        InputAction action = moveAction.action;
        if (action == null && playerInput != null && playerInput.actions != null)
            action = playerInput.actions.FindAction("Move", throwIfNotFound: false);
        if (action == null)
            action = _moveFromEnabledAssetMap;

        return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
    }

    public bool WasDashPressedThisFrame()
    {
        InputAction action = dashAction.action;
        if (action == null && playerInput != null && playerInput.actions != null)
            action = playerInput.actions.FindAction("Dash", throwIfNotFound: false);
        if (action == null)
            action = _dashFromEnabledAssetMap;

        return action != null && action.WasPressedThisFrame();
    }

    public bool WasAttackPressedThisFrame()
    {
        InputAction action = attackAction.action;
        if (action == null && playerInput != null && playerInput.actions != null)
            action = playerInput.actions.FindAction("Attack", throwIfNotFound: false);
        if (action == null)
            action = _attackFromEnabledAssetMap;

        bool fromAction = action != null && action.WasPressedThisFrame();
        if (fromAction && FeneraxJoystickMoveIntentProvider.IsPointerOverGameObjectUi())
            fromAction = false;
        return fromAction;
    }

    public bool IsAttackHeld()
    {
        InputAction action = attackAction.action;
        if (action == null && playerInput != null && playerInput.actions != null)
            action = playerInput.actions.FindAction("Attack", throwIfNotFound: false);
        if (action == null)
            action = _attackFromEnabledAssetMap;

        return action != null && action.IsPressed();
    }
}
