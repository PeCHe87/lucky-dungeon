using UnityEngine;

/// <summary>
/// Optional player component: double-tap the virtual joystick to burst-dash with independent cooldown and VFX.
/// Prefab setup: add to the player root alongside <see cref="FeneraxJoystickMoveIntentProvider"/> and
/// <see cref="TopDownCharacterMovement"/>; leave <c>dashIntentProvider</c> pointing at the joystick provider.
/// Assign <see cref="joystickProvider"/> / <see cref="movement"/> or leave empty to auto-resolve on the same GameObject.
/// Add <see cref="DamageInvulnerability"/>, <see cref="DashMotionTrailEffect"/> (set visual root to the mesh parent),
/// and <see cref="DashCooldownRingView"/> on the same object or as children. Scene must wire
/// <c>virtualJoystick</c> on the provider (init scene already does). No extra HUD buttons required.
/// </summary>
[RequireComponent(typeof(TopDownCharacterMovement))]
public sealed class DashJoystickDoubleTapController : MonoBehaviour, IJoystickDoubleTapSink
{
    const float DoubleTapMinInterval = 0.05f;

    [Header("Double-tap")]
    [Tooltip("Max seconds between first finger lift and second touch (250–350 ms recommended).")]
    [SerializeField, Range(0.25f, 0.35f)] float doubleTapMaxInterval = 0.3f;
    [SerializeField] float stickDeadzone = 0.08f;

    [Header("Dash")]
    [SerializeField] float dashCooldown = 2f;
    [SerializeField] float invincibilityDuration = 0.2f;
    [SerializeField] float dashDuration = 0.2f;
    [SerializeField] float dashSpeed = 18f;

    [Header("References")]
    [SerializeField] FeneraxJoystickMoveIntentProvider joystickProvider;
    [SerializeField] TopDownCharacterMovement movement;
    [SerializeField] DamageInvulnerability invulnerability;
    [SerializeField] DashMotionTrailEffect trailEffect;
    [SerializeField] DashCooldownRingView cooldownRing;

    enum TapState
    {
        Idle,
        AwaitingSecondTap,
    }

    TapState _tapState;
    float _firstLiftTime;
    float _cooldownRemaining;

    public float DashDistance => dashSpeed * dashDuration;

    void Awake()
    {
        if (movement == null)
            movement = GetComponent<TopDownCharacterMovement>();
        if (joystickProvider == null)
            joystickProvider = GetComponent<FeneraxJoystickMoveIntentProvider>();
        if (invulnerability == null)
            invulnerability = GetComponent<DamageInvulnerability>();
        if (trailEffect == null)
            trailEffect = GetComponent<DashMotionTrailEffect>();
        if (cooldownRing == null)
            cooldownRing = GetComponent<DashCooldownRingView>();
    }

    void OnEnable()
    {
        if (joystickProvider != null)
            joystickProvider.RegisterDoubleTapSink(this);
    }

    void OnDisable()
    {
        if (joystickProvider != null)
            joystickProvider.UnregisterDoubleTapSink(this);
        _tapState = TapState.Idle;
    }

    void Update()
    {
        if (_tapState == TapState.AwaitingSecondTap
            && Time.unscaledTime - _firstLiftTime > doubleTapMaxInterval)
        {
            _tapState = TapState.Idle;
        }

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
    }

    public void OnJoystickPointerUp()
    {
        if (_cooldownRemaining > 0f || movement.IsDashing)
            return;

        _tapState = TapState.AwaitingSecondTap;
        _firstLiftTime = Time.unscaledTime;
    }

    public void OnJoystickPointerDown(Vector2 normalizedOffsetFromCenter)
    {
        if (_tapState != TapState.AwaitingSecondTap)
            return;

        float elapsed = Time.unscaledTime - _firstLiftTime;
        _tapState = TapState.Idle;

        if (elapsed < DoubleTapMinInterval || elapsed > doubleTapMaxInterval)
            return;
        if (_cooldownRemaining > 0f || movement.IsDashing)
            return;

        Vector3 dashDir = ResolveDashDirection(normalizedOffsetFromCenter);
        if (!movement.TryStartDirectedDash(dashDir, dashDuration, dashSpeed))
            return;

        _cooldownRemaining = dashCooldown;
        if (invulnerability != null)
            invulnerability.Grant(invincibilityDuration);
        if (trailEffect != null)
            trailEffect.Play(dashDir, dashDuration);
        if (cooldownRing != null)
            cooldownRing.SetFill01(0f);
    }

    Vector3 ResolveDashDirection(Vector2 normalizedOffset)
    {
        float deadzoneSq = stickDeadzone * stickDeadzone;
        if (normalizedOffset.sqrMagnitude > deadzoneSq)
        {
            Vector2 stick = normalizedOffset;
            if (stick.sqrMagnitude > 1f)
                stick.Normalize();
            return movement.GetHorizontalMoveDirectionFromStick(stick);
        }

        return movement.GetFacingHorizontalDirection();
    }
}
