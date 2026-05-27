using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class TopDownCharacterMovement : MonoBehaviour
{
    [Header("Control")]
    [Tooltip("Implements IMoveIntentProvider (e.g. PlayerInputMoveIntentProvider). If unset, uses first IMoveIntentProvider on this GameObject.")]
    [SerializeField] MonoBehaviour moveIntentProvider;

    [Header("Movement")]
    [SerializeField] float moveSpeed = 6f;
    [Tooltip("If set, Move X/Y follow this camera's horizontal axes (good for isometric). If unset, uses Camera.main.")]
    [SerializeField] Transform cameraTransform;
    [Tooltip("Use world X/Z from input instead of camera-relative.")]
    [SerializeField] bool useWorldSpaceAxes;

    [Header("Facing")]
    [SerializeField] float rotationDegreesPerSecond = 720f;
    [Tooltip("If unset, this transform is rotated. Assign a child to rotate visuals only.")]
    [SerializeField] Transform rotationTarget;
    [SerializeField] float rotationInputDeadzone = 0.01f;
    [Tooltip("Added after LookRotation if the mesh forward axis is not +Z.")]
    [SerializeField] float modelForwardYawOffset;

    [Header("Dash")]
    [SerializeField] float dashDuration = 0.2f;
    [SerializeField] float dashSpeed = 18f;
    [Tooltip("Impulse applied to non-kinematic rigidbodies hit while dashing (horizontal).")]
    [SerializeField] float dashCollisionPushImpulse = 8f;
    [SerializeField] bool requireGroundedToStartDash = true;
    [Tooltip("Only colliders on these layers receive push. If empty, no rigidbodies are pushed.")]
    [SerializeField] LayerMask dashPushableLayers;

    [Header("Gravity & slopes")]
    [SerializeField] float gravity = -30f;
    [SerializeField] float groundedStickDownVelocity = -2f;
    [SerializeField] float slopeLimitDegrees = 45f;

    CharacterController _characterController;
    float _verticalVelocity;
    IMoveIntentProvider _provider;

    float _dashTimeRemaining;
    Vector3 _dashDirection;
    readonly HashSet<Collider> _dashPushApplied = new HashSet<Collider>();

    float _lungeTimeRemaining;
    Vector3 _lungeDirection;
    float _lungeSpeed;

    float _overrideDashSpeed;
    bool _useOverrideDashParams;

    public bool IsDashing => _dashTimeRemaining > 0f;

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _characterController.slopeLimit = slopeLimitDegrees;

        if (moveIntentProvider != null)
            _provider = moveIntentProvider as IMoveIntentProvider;
        if (_provider == null)
            _provider = GetComponent<IMoveIntentProvider>();
    }

    void Update()
    {
        Vector2 intent = _provider != null ? _provider.GetMoveIntent() : Vector2.zero;
        Vector3 moveDir = GetHorizontalMoveDirection(intent);

        bool dashing = _dashTimeRemaining > 0f;

        if (_lungeTimeRemaining > 0f)
            _lungeTimeRemaining -= Time.deltaTime;

        bool lunging = _lungeTimeRemaining > 0f;
        float activeDashSpeed = _useOverrideDashParams ? _overrideDashSpeed : dashSpeed;
        Vector3 horizontal = dashing ? _dashDirection * activeDashSpeed
            : lunging ? _lungeDirection * _lungeSpeed
            : moveDir * moveSpeed;

        float deadzoneSqRot = rotationInputDeadzone * rotationInputDeadzone;
        if (!dashing && moveDir.sqrMagnitude > deadzoneSqRot)
        {
            Transform faceTransform = rotationTarget != null ? rotationTarget : transform;
            Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
            if (modelForwardYawOffset != 0f)
                targetRot *= Quaternion.Euler(0f, modelForwardYawOffset, 0f);
            faceTransform.rotation = Quaternion.RotateTowards(
                faceTransform.rotation,
                targetRot,
                rotationDegreesPerSecond * Time.deltaTime);
        }

        if (_characterController.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = groundedStickDownVelocity;

        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = horizontal * Time.deltaTime;
        motion.y = _verticalVelocity * Time.deltaTime;
        _characterController.Move(motion);

        if (_dashTimeRemaining > 0f)
        {
            _dashTimeRemaining -= Time.deltaTime;
            if (_dashTimeRemaining <= 0f)
                _useOverrideDashParams = false;
        }
    }

    /// <summary>
    /// Starts a directed burst dash (e.g. joystick double-tap). Does not use keyboard dash cooldown on movement.
    /// </summary>
    public bool TryStartDirectedDash(Vector3 worldDirection, float duration, float speed)
    {
        if (_dashTimeRemaining > 0f)
            return false;
        if (requireGroundedToStartDash && !_characterController.isGrounded)
            return false;

        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 1e-6f)
            return false;

        worldDirection.Normalize();
        _dashDirection = worldDirection;
        _dashTimeRemaining = duration;
        _overrideDashSpeed = speed;
        _useOverrideDashParams = true;
        _dashPushApplied.Clear();
        return true;
    }

    public Vector3 GetFacingHorizontalDirection()
    {
        Transform faceTransform = rotationTarget != null ? rotationTarget : transform;
        Vector3 forward = faceTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            return Vector3.forward;
        forward.Normalize();
        return forward;
    }

    public Vector3 GetHorizontalMoveDirectionFromStick(Vector2 stickInput)
    {
        return GetHorizontalMoveDirection(stickInput);
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (_dashTimeRemaining <= 0f || dashCollisionPushImpulse <= 0f)
            return;

        Rigidbody rb = hit.rigidbody;
        if (rb == null || rb.isKinematic)
            return;

        if (dashPushableLayers.value == 0)
            return;
        if ((dashPushableLayers & (1 << hit.collider.gameObject.layer)) == 0)
            return;

        if (!_dashPushApplied.Add(hit.collider))
            return;

        Vector3 pushDir = hit.point - transform.position;
        pushDir.y = 0f;
        if (pushDir.sqrMagnitude < 1e-8f)
            pushDir = _dashDirection;
        else
            pushDir.Normalize();

        rb.AddForce(pushDir * dashCollisionPushImpulse, ForceMode.Impulse);
    }

    public void StartAttackLunge(Vector3 direction, float speed, float duration)
    {
        if (direction.sqrMagnitude < 1e-8f || speed <= 0f || duration <= 0f)
            return;
        _lungeDirection = direction.normalized;
        _lungeSpeed = speed;
        _lungeTimeRemaining = duration;
    }

    /// <summary>Instantly aligns horizontal yaw toward <paramref name="worldPosition"/> (same pivot and yaw offset as move-facing).</summary>
    public void SnapHorizontalFacingTowardWorldPosition(Vector3 worldPosition)
    {
        Transform faceTransform = rotationTarget != null ? rotationTarget : transform;
        Vector3 dir = worldPosition - faceTransform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f)
            return;
        dir.Normalize();
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        if (modelForwardYawOffset != 0f)
            targetRot *= Quaternion.Euler(0f, modelForwardYawOffset, 0f);
        faceTransform.rotation = targetRot;
    }

    Vector3 GetHorizontalMoveDirection(Vector2 input)
    {
        Vector3 dir;
        if (useWorldSpaceAxes)
        {
            dir = new Vector3(input.x, 0f, input.y);
        }
        else
        {
            Transform cam = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
            if (cam == null)
            {
                dir = new Vector3(input.x, 0f, input.y);
            }
            else
            {
                Vector3 forward = cam.forward;
                forward.y = 0f;
                forward.Normalize();
                Vector3 right = cam.right;
                right.y = 0f;
                right.Normalize();
                dir = right * input.x + forward * input.y;
            }
        }

        if (dir.sqrMagnitude > 1f)
            dir.Normalize();

        return dir;
    }
}
