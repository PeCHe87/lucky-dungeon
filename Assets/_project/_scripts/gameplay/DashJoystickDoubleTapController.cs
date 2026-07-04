using UnityEngine;

/// <summary>
/// Executes a directed burst dash when the UI dash button registers dash intent.
/// </summary>
[RequireComponent(typeof(TopDownCharacterMovement))]
[RequireComponent(typeof(CharacterController))]
public sealed class DashJoystickDoubleTapController : MonoBehaviour
{
    [Header("Dash")]
    [SerializeField] float dashCooldown = 2f;
    [SerializeField] float dashDuration = 0.2f;
    [SerializeField] float dashSpeed = 18f;
    [SerializeField] bool requireGroundedToStartDash = true;

    [Header("References")]
    [SerializeField] FeneraxJoystickMoveIntentProvider joystickProvider;
    [SerializeField] TopDownCharacterMovement movement;
    [SerializeField] DamageInvulnerability invulnerability;
    [SerializeField] DashMotionTrailEffect trailEffect;
    [SerializeField] DashCooldownRingView cooldownRing;
    [SerializeField] PlayerEntityState playerEntityState;

    CharacterController _characterController;
    IDashIntentProvider _dashProvider;
    float _cooldownRemaining;

    public float DashDistance => dashSpeed * dashDuration;

    public bool IsDashAvailable =>
        (playerEntityState == null || !playerEntityState.IsInputBlocked)
        && CanAcceptDashInput()
        && IsGroundedForDash();

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        if (movement == null)
            movement = GetComponent<TopDownCharacterMovement>();
        if (joystickProvider == null)
            joystickProvider = GetComponent<FeneraxJoystickMoveIntentProvider>();
        if (joystickProvider != null)
            _dashProvider = joystickProvider;
        if (_dashProvider == null)
            _dashProvider = GetComponent<IDashIntentProvider>();
        if (invulnerability == null)
            invulnerability = GetComponent<DamageInvulnerability>();
        if (trailEffect == null)
            trailEffect = GetComponent<DashMotionTrailEffect>();
        if (cooldownRing == null)
            cooldownRing = GetComponent<DashCooldownRingView>();
        if (playerEntityState == null)
            playerEntityState = GetComponent<PlayerEntityState>();
    }

    void Update()
    {
        if (_cooldownRemaining > 0f)
        {
            _cooldownRemaining -= Time.deltaTime;
            if (cooldownRing != null && dashCooldown > 0f)
            {
                float fill = 1f - (_cooldownRemaining / dashCooldown);
                cooldownRing.SetFill01(fill);
            }
        }
        else if (cooldownRing != null)
        {
            cooldownRing.SetFill01(1f);
        }

        if (_dashProvider != null && _dashProvider.WasDashPressedThisFrame())
            TryStartDash();
    }

    bool CanAcceptDashInput() =>
        _cooldownRemaining <= 0f && movement != null && !movement.IsDashing;

    bool IsGroundedForDash() =>
        !requireGroundedToStartDash
        || _characterController == null
        || _characterController.isGrounded;

    void TryStartDash()
    {
        if (!IsDashAvailable)
            return;

        Vector3 dashDir = movement.GetFacingHorizontalDirection();
        if (!movement.TryStartDirectedDash(dashDir, dashDuration, dashSpeed))
            return;

        _cooldownRemaining = dashCooldown;
        if (invulnerability != null)
            invulnerability.Grant(dashDuration);
        if (trailEffect != null)
            trailEffect.Play(dashDir, dashDuration);
        if (cooldownRing != null)
            cooldownRing.SetFill01(0f);
    }
}
