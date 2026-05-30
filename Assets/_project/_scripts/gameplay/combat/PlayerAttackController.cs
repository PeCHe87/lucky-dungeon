using System;
using UnityEngine;

/// <summary>Polls <see cref="IAttackIntentProvider"/> and forwards to <see cref="WeaponHolder"/>.</summary>
[DefaultExecutionOrder(101)]
public sealed class PlayerAttackController : MonoBehaviour
{
    public event Action AttackPressed;
    public event Action AttackPerformed;
    public event Action<float> MeleeApproachStarted;
    public event Action MeleeApproachCancelled;
    [Tooltip("Implements IAttackIntentProvider. If unset, uses first IAttackIntentProvider on this GameObject.")]
    [SerializeField] MonoBehaviour attackIntentProvider;
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] NearestTargetQuery targetQuery;
    [Tooltip("If unset, uses this transform for attack facing (XZ forward).")]
    [SerializeField] Transform facingRoot;
    [Tooltip("When enabled, horizontal yaw snaps toward the nearest target from NearestTargetQuery before TryAttack. When disabled, facing is unchanged.")]
    [SerializeField] bool snapFacingToNearestTargetBeforeAttack = true;
    [Tooltip("If true, logs the weapon used each time an attack is processed (i.e. TryAttack succeeded).")]
    [SerializeField] bool logProcessedAttack;
    [Tooltip("If unset, uses PlayerEntityState on this GameObject or in the scene.")]
    [SerializeField] PlayerEntityState playerEntityState;

    IAttackIntentProvider _attackProvider;
    TopDownCharacterMovement _movement;
    bool _meleeApproachPending;
    bool _hasPendingMeleeContext;
    AttackContext _pendingMeleeContext;

    /// <summary>World position used as melee overlap origin (<see cref="AttackContext.attacker"/>).</summary>
    public Transform AttackOriginTransform => transform;

    /// <summary>Transform whose forward defines melee cone facing (<see cref="AttackContext.facing"/>).</summary>
    public Transform AttackFacingTransform => facingRoot != null ? facingRoot : transform;

    public bool IsMeleeApproaching => _meleeApproachPending;

    void Awake()
    {
        if (attackIntentProvider != null)
            _attackProvider = attackIntentProvider as IAttackIntentProvider;
        if (_attackProvider == null)
            _attackProvider = GetComponent<IAttackIntentProvider>();

        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();

        _movement = GetComponent<TopDownCharacterMovement>();

        if (playerEntityState == null)
            playerEntityState = GetComponent<PlayerEntityState>();
        if (playerEntityState == null)
            playerEntityState = FindFirstObjectByType<PlayerEntityState>();
    }

    void LateUpdate()
    {
        if (_attackProvider == null || weaponHolder == null)
            return;

        if (_meleeApproachPending)
        {
            if (_movement != null && _movement.IsDashing)
            {
                CancelPendingMeleeApproach();
                return;
            }

            if (weaponHolder.Current is MeleeWeapon meleeCheck && !meleeCheck.EnableApproachLunge)
            {
                CancelPendingMeleeApproach();
                return;
            }

            if (_movement != null && !_movement.IsLunging)
            {
                CompletePendingMeleeAttack();
                return;
            }

            return;
        }

        bool pressed = _attackProvider.WasAttackPressedThisFrame();
        bool held = _attackProvider.IsAttackHeld();
        if (!pressed && !held)
            return;
        if (playerEntityState != null && playerEntityState.IsAttackInputBlocked)
            return;

        if (_movement != null && _movement.IsLunging)
            return;

        if (!TryBuildAttackContext(out AttackContext ctx))
            return;

        bool performed;
        if (weaponHolder.Current is MeleeWeapon melee)
            performed = TryProcessMeleeAttack(melee, in ctx, pressed);
        else
            performed = weaponHolder.TryAttack(in ctx);

        if (!_meleeApproachPending && (pressed || performed))
            AttackPressed?.Invoke();

        if (performed)
        {
            AttackPerformed?.Invoke();
            LogProcessedAttackIfEnabled();
        }
    }

    bool TryBuildAttackContext(out AttackContext ctx)
    {
        Transform attacker = transform;
        Transform face = facingRoot != null ? facingRoot : transform;

        Transform optionalTarget = null;
        if (targetQuery != null)
        {
            if (targetQuery.IsDetectionSuspended && targetQuery.TryGetFrozenCombatTarget(out Transform frozen))
                optionalTarget = frozen;
            else if (targetQuery.TryGetNearestTransform(out Transform t))
                optionalTarget = t;
        }

        if (snapFacingToNearestTargetBeforeAttack && optionalTarget != null)
        {
            if (_movement != null)
                _movement.SnapHorizontalFacingTowardWorldPosition(optionalTarget.position);
            else
            {
                Vector3 dir = optionalTarget.position - face.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-8f)
                    face.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        Vector3 f = face.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-8f)
            f = Vector3.forward;
        else
            f.Normalize();

        ctx = new AttackContext
        {
            attacker = attacker,
            facing = f,
            optionalTarget = optionalTarget
        };
        return true;
    }

    bool TryProcessMeleeAttack(MeleeWeapon melee, in AttackContext ctx, bool pressed)
    {
        Vector3 origin = ctx.attacker.position;
        Transform target = ctx.optionalTarget;

        if (target == null || melee.IsTargetWithinDamageRadius(origin, target.position))
            return melee.TryBeginAttack(in ctx);

        if (!melee.EnableApproachLunge || _movement == null)
            return melee.TryBeginAttack(in ctx);

        if (!melee.TryComputeApproachLunge(
                origin,
                target.position,
                out float stopDistance,
                out float maxTravel,
                out float speed))
        {
            return melee.TryBeginAttack(in ctx);
        }

        _movement.StartApproachLunge(target.position, stopDistance, maxTravel, speed);
        _pendingMeleeContext = ctx;
        _hasPendingMeleeContext = true;
        _meleeApproachPending = true;

        if (melee.TryGetApproachLungeDuration(origin, target.position, out float approachDuration))
            MeleeApproachStarted?.Invoke(approachDuration);

        return false;
    }

    void CompletePendingMeleeAttack()
    {
        _meleeApproachPending = false;
        if (!_hasPendingMeleeContext || weaponHolder.Current is not MeleeWeapon melee)
        {
            _hasPendingMeleeContext = false;
            return;
        }

        AttackContext ctx = _pendingMeleeContext;
        _hasPendingMeleeContext = false;

        bool performed = melee.TryBeginAttack(in ctx);
        if (!performed)
            return;

        AttackPressed?.Invoke();
        AttackPerformed?.Invoke();
        LogProcessedAttackIfEnabled();
    }

    void CancelPendingMeleeApproach()
    {
        bool wasPending = _meleeApproachPending;
        _meleeApproachPending = false;
        _hasPendingMeleeContext = false;
        if (_movement != null)
            _movement.CancelApproachLunge();
        if (wasPending)
            MeleeApproachCancelled?.Invoke();
    }

    void LogProcessedAttackIfEnabled()
    {
        if (!logProcessedAttack)
            return;

        IWeapon w = weaponHolder.Current;
        MonoBehaviour wmb = w as MonoBehaviour;
        string label = wmb != null
            ? $"'{wmb.name}' ({wmb.GetType().Name})"
            : (w != null ? $"({w.GetType().Name})" : "<none>");
        Debug.Log($"{nameof(PlayerAttackController)} on {name}: attack processed with <color=yellow>{label}</color>", this);
    }
}
