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
    [SerializeField] float damageNumberHeightOffset = 1.5f;

    float _currentHitPoints;
    bool _defeated;

    public EntityAlignment Alignment => alignment;
    public bool IsDefeated => _defeated;
    public float CurrentHitPoints => _currentHitPoints;
    public float MaxHitPoints => maxHitPoints;

    /// <summary>Fired after HP is reduced by a valid hit; argument is damage amount applied this frame.</summary>
    public event Action<float> Damaged;

    /// <summary>Fired once when HP reaches zero, before the Die FSM state runs.</summary>
    public event Action Died;

    void Awake()
    {
        _currentHitPoints = maxHitPoints;

        if (stateMachine == null)
            stateMachine = GetComponent<AIStateMachine>();
    }

    public void TakeDamage(float amount, DamageNumberStyle style)
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
        FloatingDamageTextPresenter.Instance?.Spawn(GetDamageNumberWorldPosition(), amount, style);

        if (_currentHitPoints <= 0f)
        {
            _currentHitPoints = 0f;
            Die();
            return;
        }

        if (takeDamageState != null && stateMachine != null
            && stateMachine.CurrentStateData != takeDamageState)
            stateMachine.ForceInterrupt(takeDamageState);
    }

    void Die()
    {
        if (_defeated)
            return;

        _defeated = true;
        Died?.Invoke();

        if (dieState != null && stateMachine != null)
            stateMachine.ForceTransition(dieState);
    }

    Vector3 GetDamageNumberWorldPosition()
    {
        Vector3 p = damageNumberAnchor != null ? damageNumberAnchor.position : transform.position;
        p.y += damageNumberHeightOffset;
        return p;
    }
}
