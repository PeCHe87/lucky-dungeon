using System;
using UnityEngine;

/// <summary>
/// Read-only observer of the player's locomotion/combat state from input and existing gameplay components.
/// Priority: TakingDamage > MeleeApproaching > Attacking > Dashing > Running > Walking > Idle.
/// Extend by adding <see cref="PlayerEntityStateKind"/> values and <see cref="IPlayerStateRule"/> implementations.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class PlayerEntityState : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Implements IMoveIntentProvider. If unset, uses first IMoveIntentProvider on this GameObject.")]
    [SerializeField] MonoBehaviour moveIntentProvider;
    [SerializeField] TopDownCharacterMovement movement;
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] PlayerAttackController attackController;
    [SerializeField] PlayerHitReact hitReact;

    [Header("Locomotion thresholds")]
    [SerializeField] float moveDeadzone = 0.08f;
    [SerializeField, Range(0f, 1f)] float runThreshold = 0.75f;

    [Header("Debug")]
    [SerializeField] bool debugLogState;
    [SerializeField] bool debugLogOnStateChange = true;
    [Tooltip("When debug is on, log current state every N seconds even if unchanged. 0 = changes only.")]
    [SerializeField, Min(0f)] float debugLogHeartbeatSeconds = 1f;

    IMoveIntentProvider _moveProvider;
    IPlayerStateRule[] _rules;
    float _nextHeartbeatTime;

    public PlayerEntityStateKind Current { get; private set; }
    public PlayerEntityStateKind Previous { get; private set; }

    public float MoveDeadzone => moveDeadzone;

    /// <summary>True during hit-react; all player input should be ignored.</summary>
    public bool IsInputBlocked => Current == PlayerEntityStateKind.TakingDamage;

    /// <summary>True while walking, running, dashing, or taking damage; attack input should be ignored.</summary>
    public bool IsAttackInputBlocked =>
        IsInputBlocked
        || Current == PlayerEntityStateKind.Walking
        || Current == PlayerEntityStateKind.Running
        || Current == PlayerEntityStateKind.Dashing;

    public event Action<PlayerEntityStateKind, PlayerEntityStateKind> StateChanged;

    void Awake()
    {
        if (moveIntentProvider != null)
            _moveProvider = moveIntentProvider as IMoveIntentProvider;
        if (_moveProvider == null)
            _moveProvider = GetComponent<IMoveIntentProvider>();

        if (movement == null)
            movement = GetComponent<TopDownCharacterMovement>();

        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();

        if (attackController == null)
            attackController = GetComponent<PlayerAttackController>();

        if (hitReact == null)
            hitReact = GetComponent<PlayerHitReact>();

        _rules = BuildRules();
        Current = PlayerEntityStateKind.Idle;
        Previous = PlayerEntityStateKind.Idle;
    }

    void LateUpdate()
    {
        PlayerEntityStateKind next = ResolveState();
        if (next != Current)
        {
            Previous = Current;
            Current = next;
            StateChanged?.Invoke(Previous, Current);
            if (debugLogState && debugLogOnStateChange)
                Debug.Log($"[PlayerEntityState] {name}: {Previous} -> {Current}", this);
        }

        if (debugLogState && debugLogHeartbeatSeconds > 0f && Time.unscaledTime >= _nextHeartbeatTime)
        {
            _nextHeartbeatTime = Time.unscaledTime + debugLogHeartbeatSeconds;
            Debug.Log($"[PlayerEntityState] {name}: {Current}", this);
        }
    }

    PlayerEntityStateKind ResolveState()
    {
        var ctx = new PlayerStateContext(_moveProvider, movement, weaponHolder, attackController, hitReact, moveDeadzone, runThreshold);
        for (int i = 0; i < _rules.Length; i++)
        {
            if (_rules[i].TryResolve(in ctx, out PlayerEntityStateKind state))
                return state;
        }

        return PlayerEntityStateKind.Idle;
    }

    IPlayerStateRule[] BuildRules()
    {
        return new IPlayerStateRule[]
        {
            new TakingDamageStateRule(),
            new MeleeApproachingStateRule(),
            new AttackingStateRule(),
            new DashingStateRule(),
            new RunningStateRule(),
            new WalkingStateRule(),
            new IdleStateRule(),
        };
    }

    readonly struct PlayerStateContext
    {
        public readonly IMoveIntentProvider MoveProvider;
        public readonly TopDownCharacterMovement Movement;
        public readonly WeaponHolder WeaponHolder;
        public readonly PlayerAttackController AttackController;
        public readonly PlayerHitReact HitReact;
        public readonly float MoveDeadzone;
        public readonly float RunThreshold;

        public PlayerStateContext(
            IMoveIntentProvider moveProvider,
            TopDownCharacterMovement movement,
            WeaponHolder weaponHolder,
            PlayerAttackController attackController,
            PlayerHitReact hitReact,
            float moveDeadzone,
            float runThreshold)
        {
            MoveProvider = moveProvider;
            Movement = movement;
            WeaponHolder = weaponHolder;
            AttackController = attackController;
            HitReact = hitReact;
            MoveDeadzone = moveDeadzone;
            RunThreshold = runThreshold;
        }

        public float MoveMagnitude =>
            MoveProvider != null ? MoveProvider.GetMoveIntent().magnitude : 0f;
    }

    interface IPlayerStateRule
    {
        bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state);
    }

    sealed class TakingDamageStateRule : IPlayerStateRule
    {
        public bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state)
        {
            if (ctx.HitReact != null && ctx.HitReact.IsActive)
            {
                state = PlayerEntityStateKind.TakingDamage;
                return true;
            }

            state = default;
            return false;
        }
    }

    sealed class MeleeApproachingStateRule : IPlayerStateRule
    {
        public bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state)
        {
            if (ctx.AttackController != null && ctx.AttackController.IsMeleeApproaching)
            {
                state = PlayerEntityStateKind.MeleeApproaching;
                return true;
            }

            state = default;
            return false;
        }
    }

    sealed class AttackingStateRule : IPlayerStateRule
    {
        public bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state)
        {
            if (ctx.WeaponHolder != null
                && ctx.WeaponHolder.Current is IAttackActivity activity
                && activity.IsAttackActive)
            {
                state = PlayerEntityStateKind.Attacking;
                return true;
            }

            state = default;
            return false;
        }
    }

    sealed class DashingStateRule : IPlayerStateRule
    {
        public bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state)
        {
            if (ctx.Movement != null && ctx.Movement.IsDashing)
            {
                state = PlayerEntityStateKind.Dashing;
                return true;
            }

            state = default;
            return false;
        }
    }

    sealed class RunningStateRule : IPlayerStateRule
    {
        public bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state)
        {
            float mag = ctx.MoveMagnitude;
            if (mag > ctx.MoveDeadzone)
            {
                state = PlayerEntityStateKind.Running;
                return true;
            }

            state = default;
            return false;
        }
    }

    sealed class WalkingStateRule : IPlayerStateRule
    {
        public bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state)
        {
            float mag = ctx.MoveMagnitude;
            if (mag > ctx.MoveDeadzone)
            {
                state = PlayerEntityStateKind.Walking;
                return true;
            }

            state = default;
            return false;
        }
    }

    sealed class IdleStateRule : IPlayerStateRule
    {
        public bool TryResolve(in PlayerStateContext ctx, out PlayerEntityStateKind state)
        {
            state = PlayerEntityStateKind.Idle;
            return true;
        }
    }
}
