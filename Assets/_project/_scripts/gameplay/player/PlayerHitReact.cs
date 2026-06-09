using UnityEngine;

/// <summary>
/// Player hit-react: blocks input while the hurt animation plays, ignores further hits during that window,
/// then grants post-hit invulnerability via <see cref="DamageInvulnerability"/>.
/// </summary>
[DefaultExecutionOrder(115)]
public sealed class PlayerHitReact : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField, Min(0f)] float invulnerabilityDuration = 0.5f;
    [Tooltip("Ends hit-react if the hurt clip never starts (missing animator binding).")]
    [SerializeField, Min(0.01f)] float hitReactFallbackDuration = 0.3f;

    [Header("References")]
    [SerializeField] CombatEntityHealth health;
    [SerializeField] DamageInvulnerability invulnerability;
    [SerializeField] PlayerAttackController attackController;
    [SerializeField] TopDownCharacterMovement movement;
    [SerializeField] PlayerEntityStateAnimator entityStateAnimator;

    bool _active;
    bool _clipStarted;
    float _fallbackEndTime;

    public bool IsActive => _active;

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();
        if (invulnerability == null)
            invulnerability = GetComponent<DamageInvulnerability>();
        if (attackController == null)
            attackController = GetComponent<PlayerAttackController>();
        if (movement == null)
            movement = GetComponent<TopDownCharacterMovement>();
        if (entityStateAnimator == null)
            entityStateAnimator = GetComponent<PlayerEntityStateAnimator>();
    }

    void OnEnable()
    {
        if (health != null)
            health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (health != null)
            health.Damaged -= OnDamaged;
        _active = false;
        _clipStarted = false;
    }

    void OnDamaged(float amount)
    {
        if (health == null || health.CurrentHitPoints <= 0f)
            return;

        BeginHitReact();
    }

    void BeginHitReact()
    {
        attackController?.CancelAttackForInterrupt();
        movement?.CancelDash();

        _active = true;
        _clipStarted = false;
        _fallbackEndTime = Time.time + hitReactFallbackDuration;
    }

    void LateUpdate()
    {
        if (!_active)
            return;

        if (entityStateAnimator != null)
        {
            if (entityStateAnimator.IsHitReactClipPlaying)
                _clipStarted = true;

            if (_clipStarted && !entityStateAnimator.IsHitReactClipPlaying)
            {
                EndHitReact();
                return;
            }
        }

        if (!_clipStarted && Time.time >= _fallbackEndTime)
            EndHitReact();
    }

    void EndHitReact()
    {
        _active = false;
        _clipStarted = false;

        if (invulnerability != null && invulnerabilityDuration > 0f)
            invulnerability.Grant(invulnerabilityDuration);
    }
}
