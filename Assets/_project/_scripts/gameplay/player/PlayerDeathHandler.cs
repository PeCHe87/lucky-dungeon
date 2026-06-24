using UnityEngine;

/// <summary>
/// Player death: blocks input via <see cref="PlayerEntityStateKind.Dying"/>, plays the Die clip,
/// then raises <see cref="GameEvents.PlayerDied"/> after the animation completes.
/// </summary>
[DefaultExecutionOrder(116)]
public sealed class PlayerDeathHandler : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Ends death handling if the Die clip never starts (missing animator binding).")]
    [SerializeField, Min(0.01f)] float deathFallbackDuration = 2f;

    [Header("References")]
    [SerializeField] CombatEntityHealth health;
    [SerializeField] PlayerAttackController attackController;
    [SerializeField] TopDownCharacterMovement movement;
    [SerializeField] PlayerEntityStateAnimator entityStateAnimator;

    bool _active;
    bool _clipStarted;
    bool _globalEventRaised;
    float _fallbackEndTime;

    public bool IsActive => _active;

    void Awake()
    {
        if (health == null)
            health = GetComponent<CombatEntityHealth>();
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
            health.Died += OnDied;
    }

    void OnDisable()
    {
        if (health != null)
            health.Died -= OnDied;
        _active = false;
        _clipStarted = false;
    }

    void OnDied()
    {
        if (_active)
            return;

        attackController?.CancelAttackForInterrupt();
        movement?.CancelDash();

        _active = true;
        _clipStarted = false;
        _globalEventRaised = false;
        _fallbackEndTime = Time.time + deathFallbackDuration;
    }

    void LateUpdate()
    {
        if (!_active || _globalEventRaised)
            return;

        if (entityStateAnimator != null)
        {
            if (entityStateAnimator.IsDeathClipPlaying)
                _clipStarted = true;

            if (_clipStarted && !entityStateAnimator.IsDeathClipPlaying)
            {
                RaiseGlobalEvent();
                return;
            }
        }

        if (!_clipStarted && Time.time >= _fallbackEndTime)
            RaiseGlobalEvent();
    }

    void RaiseGlobalEvent()
    {
        if (_globalEventRaised)
            return;

        _globalEventRaised = true;
        GameEvents.RaisePlayerDied();
    }
}
