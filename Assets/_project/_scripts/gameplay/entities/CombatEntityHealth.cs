using System;
using UnityEngine;

/// <summary>
/// Living entity vitals: HP, damage reception, and FSM hooks for hit-react and death.
/// Death lifecycle is owned by <see cref="DieStateHandler"/> via <see cref="AIStateMachine"/>.
/// </summary>
public sealed class CombatEntityHealth : MonoBehaviour, IDamageable, IDefeatable
{
    [Header("Vitals")]
    [SerializeField, Min(0.01f)] float maxHitPoints = 30f;
    [SerializeField] EntityAlignment alignment = EntityAlignment.Enemy;

    [Header("FSM")]
    [SerializeField] AIStateMachine stateMachine;
    [SerializeField] AIStateData takeDamageState;
    [SerializeField] AIStateData dieState;

    [Header("Presentation")]
    [Tooltip("World anchor for floating damage text. Defaults to this transform.")]
    [SerializeField] Transform damageNumberAnchor;
    [SerializeField, Min(0f)] float damageNumberHeightOffset = 1.5f;

    float _currentHitPoints;
    bool _defeated;
    EntityAttackController _attackController;
    Vector3 _lastDeathPushDirection;
    float _lastDeathPushForce;

    public EntityAlignment Alignment => alignment;
    public bool IsDefeated => _defeated;
    public float CurrentHitPoints => _currentHitPoints;
    public float MaxHitPoints => maxHitPoints;
    public Vector3 LastDeathPushDirection => _lastDeathPushDirection;
    public float LastDeathPushForce => _lastDeathPushForce;

    /// <summary>Fired after HP is reduced by a valid hit; argument is damage amount applied this frame.</summary>
    public event Action<float> Damaged;

    /// <summary>Fired after HP is reduced by a valid hit; includes optional attacker metadata.</summary>
    public event Action<float, DamageHitInfo> DamagedWithHitInfo;

    /// <summary>Fired whenever current HP changes (damage now; healing can reuse this later).</summary>
    public event Action HealthChanged;

    /// <summary>Fired once when HP reaches zero, before the Die FSM state runs.</summary>
    public event Action Died;

    void Awake()
    {
        _currentHitPoints = maxHitPoints;

        if (stateMachine == null)
            stateMachine = GetComponent<AIStateMachine>();

        _attackController = GetComponent<EntityAttackController>();

        // Enemies (not the player) get ragdoll death without requiring every prefab to be authored.
        if (GetComponent<PlayerDeathHandler>() == null && GetComponent<EntityRagdoll>() == null)
            gameObject.AddComponent<EntityRagdoll>();
    }

    public void TakeDamage(float amount, DamageNumberStyle style)
    {
        TakeDamage(amount, style, default);
    }

    public void TakeDamage(float amount, DamageNumberStyle style, in DamageHitInfo hitInfo)
    {
        if (_defeated || amount <= 0f)
            return;

        if (TryGetComponent(out DamageInvulnerability invuln) && invuln.IsInvulnerable)
            return;

        if (TryGetComponent(out PlayerHitReact hitReact) && hitReact.IsActive)
            return;

        if (TryGetComponent(out TopDownCharacterMovement movement) && movement.IsDashing)
            return;

        _currentHitPoints -= amount;
        Damaged?.Invoke(amount);
        DamagedWithHitInfo?.Invoke(amount, hitInfo);
        HealthChanged?.Invoke();
        FloatingDamageTextPresenter.Instance?.Spawn(GetFloatingTextWorldPosition(), amount, style);

        if (_currentHitPoints <= 0f)
        {
            _currentHitPoints = 0f;
            Die(in hitInfo);
            return;
        }

        if (takeDamageState != null && stateMachine != null
            && stateMachine.CurrentStateData != takeDamageState
            && !IsDamageInterruptSuppressed())
            stateMachine.ForceInterrupt(takeDamageState);
    }

    bool IsDamageInterruptSuppressed() =>
        _attackController != null && _attackController.ShouldSuppressDamageInterrupt;

    /// <summary>Overwrite current and max HP (player run restore). Does not revive a defeated entity.</summary>
    public void ApplyVitals(float current, float max)
    {
        float newMax = Mathf.Max(0.01f, max);
        float newCurrent = Mathf.Clamp(current, 0f, newMax);
        if (Mathf.Approximately(newMax, maxHitPoints) && Mathf.Approximately(newCurrent, _currentHitPoints))
            return;

        maxHitPoints = newMax;
        _currentHitPoints = newCurrent;
        HealthChanged?.Invoke();
    }

    public void Heal(float amount)
    {
        if (_defeated || amount <= 0f)
            return;

        float before = _currentHitPoints;
        _currentHitPoints = Mathf.Min(_currentHitPoints + amount, maxHitPoints);
        if (Mathf.Approximately(_currentHitPoints, before))
            return;

        HealthChanged?.Invoke();
    }

    void Die(in DamageHitInfo hitInfo)
    {
        if (_defeated)
            return;

        ResolveDeathImpulse(in hitInfo);
        _defeated = true;
        Died?.Invoke();

        if (dieState != null && stateMachine != null)
            stateMachine.ForceTransition(dieState);
    }

    void ResolveDeathImpulse(in DamageHitInfo hitInfo)
    {
        _lastDeathPushForce = Mathf.Max(0f, hitInfo.deathPushForce);

        Vector3 dir = hitInfo.pushDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-8f && hitInfo.HasAttacker)
        {
            dir = transform.position - hitInfo.attacker.position;
            dir.y = 0f;
        }

        if (dir.sqrMagnitude < 1e-8f)
            dir = transform.forward;
        else
            dir.Normalize();

        _lastDeathPushDirection = dir;
    }

    public Vector3 GetFloatingTextWorldPosition()
    {
        Vector3 p = damageNumberAnchor != null ? damageNumberAnchor.position : transform.position;
        p.y += damageNumberHeightOffset;
        return p;
    }
}
