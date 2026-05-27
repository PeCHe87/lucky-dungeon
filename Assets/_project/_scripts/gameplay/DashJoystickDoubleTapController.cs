using UnityEngine;

/// <summary>
/// Double-tap the virtual joystick (two quick taps, not drag-then-tap) to burst-dash.
/// Dragging to move never arms a dash; only two short contacts within a tight window count.
/// </summary>
[RequireComponent(typeof(TopDownCharacterMovement))]
public sealed class DashJoystickDoubleTapController : MonoBehaviour, IJoystickDoubleTapSink
{
    const float NoPendingTap = -1f;

    [Header("Double-tap")]
    [Tooltip("Max seconds between first tap release and second tap press.")]
    [SerializeField, Range(0.1f, 0.25f)] float doubleTapMaxInterval = 0.18f;
    [Tooltip("Ignore second tap if it comes too soon after the first release (debounce).")]
    [SerializeField, Range(0.02f, 0.08f)] float doubleTapMinInterval = 0.05f;
    [Tooltip("Stick deflection above this during a contact counts as a drag, not a tap.")]
    [SerializeField] float stickDeadzone = 0.08f;
    [Tooltip("Contacts longer than this are holds/drags, not taps.")]
    [SerializeField, Range(0.1f, 0.25f)] float maxTapHoldDuration = 0.18f;

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

    bool _gestureActive;
    float _gestureDownTime;
    float _gestureMaxOffsetSq;

    float _firstTapEndTime = NoPendingTap;
    float _cooldownRemaining;

    float TapDeadzoneSq => stickDeadzone * stickDeadzone;

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
        ResetGesture();
        _firstTapEndTime = NoPendingTap;
    }

    void Update()
    {
        if (_firstTapEndTime > NoPendingTap
            && Time.unscaledTime - _firstTapEndTime > doubleTapMaxInterval)
        {
            _firstTapEndTime = NoPendingTap;
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

    public void OnJoystickPointerDown(Vector2 normalizedOffsetFromCenter)
    {
        if (!CanAcceptDashInput())
            return;

        float now = Time.unscaledTime;
        if (_firstTapEndTime > NoPendingTap)
        {
            float sinceFirstTap = now - _firstTapEndTime;
            if (sinceFirstTap >= doubleTapMinInterval && sinceFirstTap <= doubleTapMaxInterval)
            {
                _firstTapEndTime = NoPendingTap;
                TryStartDash(normalizedOffsetFromCenter);
                return;
            }

            _firstTapEndTime = NoPendingTap;
        }

        BeginGesture(normalizedOffsetFromCenter);
    }

    public void OnJoystickPointerMove(Vector2 normalizedOffsetFromCenter)
    {
        if (!_gestureActive)
            return;

        float sq = normalizedOffsetFromCenter.sqrMagnitude;
        if (sq > _gestureMaxOffsetSq)
            _gestureMaxOffsetSq = sq;
    }

    public void OnJoystickPointerUp()
    {
        if (!_gestureActive)
            return;

        float now = Time.unscaledTime;
        bool wasTap = IsTapGesture(now - _gestureDownTime, _gestureMaxOffsetSq);
        ResetGesture();

        if (!wasTap || !CanAcceptDashInput())
        {
            _firstTapEndTime = NoPendingTap;
            return;
        }

        _firstTapEndTime = now;
    }

    void BeginGesture(Vector2 normalizedOffset)
    {
        _gestureActive = true;
        _gestureDownTime = Time.unscaledTime;
        _gestureMaxOffsetSq = normalizedOffset.sqrMagnitude;
    }

    void ResetGesture()
    {
        _gestureActive = false;
        _gestureMaxOffsetSq = 0f;
    }

    bool IsTapGesture(float holdDuration, float maxOffsetSq)
    {
        return holdDuration <= maxTapHoldDuration && maxOffsetSq <= TapDeadzoneSq;
    }

    bool CanAcceptDashInput()
    {
        return _cooldownRemaining <= 0f && movement != null && !movement.IsDashing;
    }

    void TryStartDash(Vector2 normalizedOffset)
    {
        if (!CanAcceptDashInput())
            return;

        Vector3 dashDir = ResolveDashDirection(normalizedOffset);
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
        if (normalizedOffset.sqrMagnitude > TapDeadzoneSq)
        {
            Vector2 stick = normalizedOffset;
            if (stick.sqrMagnitude > 1f)
                stick.Normalize();
            return movement.GetHorizontalMoveDirectionFromStick(stick);
        }

        return movement.GetFacingHorizontalDirection();
    }
}
