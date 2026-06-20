using System;
using UnityEngine;

/// <summary>Polls <see cref="IAttackIntentProvider"/> and forwards to <see cref="WeaponHolder"/>.</summary>
[DefaultExecutionOrder(99)]
public sealed class PlayerAttackController : MonoBehaviour
{
    /// <param name="isUserPress">True for an explicit press; false for hold auto-repeat or post-approach follow-up.</param>
    public event Action<bool> AttackPressed;
    public event Action AttackPerformed;
    public event Action AttackCancelled;
    public event Action<float> MeleeApproachStarted;
    public event Action MeleeApproachCancelled;
    [Tooltip("Implements IAttackIntentProvider. If unset, uses first IAttackIntentProvider on this GameObject.")]
    [SerializeField] MonoBehaviour attackIntentProvider;
    [Tooltip("Implements IMoveIntentProvider. If unset, uses first IMoveIntentProvider on this GameObject.")]
    [SerializeField] MonoBehaviour moveIntentProvider;
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
    [Tooltip("If unset, uses PlayerEntityStateAnimator on this GameObject.")]
    [SerializeField] PlayerEntityStateAnimator entityStateAnimator;

    IAttackIntentProvider _attackProvider;
    IMoveIntentProvider _moveProvider;
    IDashIntentProvider _dashProvider;
    TopDownCharacterMovement _movement;
    bool _meleeApproachPending;
    bool _hasPendingMeleeContext;
    AttackContext _pendingMeleeContext;
    bool _movementPriorityCancelActive;

    /// <summary>World position used as melee overlap origin (<see cref="AttackContext.attacker"/>).</summary>
    public Transform AttackOriginTransform => transform;

    /// <summary>Transform whose forward defines melee cone facing (<see cref="AttackContext.facing"/>).</summary>
    public Transform AttackFacingTransform => facingRoot != null ? facingRoot : transform;

    public bool IsMeleeApproaching => _meleeApproachPending;

    public bool IsAttackInputHeld =>
        _attackProvider != null && _attackProvider.IsAttackHeld();

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

        if (entityStateAnimator == null)
            entityStateAnimator = GetComponent<PlayerEntityStateAnimator>();

        if (moveIntentProvider != null)
            _moveProvider = moveIntentProvider as IMoveIntentProvider;
        if (_moveProvider == null)
            _moveProvider = GetComponent<IMoveIntentProvider>();

        _dashProvider = GetComponent<IDashIntentProvider>();
    }

    void Update()
    {
        if (playerEntityState != null && playerEntityState.IsInputBlocked)
            return;

        if (!HasMovementPriorityInput())
        {
            _movementPriorityCancelActive = false;
            return;
        }

        if (!IsAttackInProgress())
            return;

        CancelAttackForMovementPriority();
    }

    void LateUpdate()
    {
        if (_attackProvider == null || weaponHolder == null)
            return;

        if (_meleeApproachPending)
        {
            if (weaponHolder.Current is MeleeWeapon meleeCheck && !meleeCheck.EnableApproachLunge)
            {
                CancelPendingMeleeApproach();
                return;
            }

            if (weaponHolder.Current is MeleeWeapon meleePending
                && _hasPendingMeleeContext
                && _pendingMeleeContext.optionalTarget != null
                && !meleePending.ShouldApplyMeleeLunge(
                    AttackOriginTransform.position,
                    _pendingMeleeContext.optionalTarget))
            {
                _movement?.CancelApproachLunge();
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
        if (playerEntityState != null && playerEntityState.IsInputBlocked)
            return;
        if (playerEntityState != null && playerEntityState.IsAttackInputBlocked)
            return;

        if (_movement != null && _movement.IsLunging)
            return;

        if (entityStateAnimator != null
            && entityStateAnimator.IsMeleeAttackClipPlaying
            && held
            && !pressed)
            return;

        if (!TryBuildAttackContext(out AttackContext ctx))
            return;

        bool performed;
        if (weaponHolder.Current is MeleeWeapon melee)
            performed = TryProcessMeleeAttack(melee, in ctx, pressed);
        else if (weaponHolder.Current is RangedWeapon ranged)
            performed = TryProcessRangedAttack(ranged, in ctx);
        else
            performed = weaponHolder.TryAttack(in ctx);

        if (!_meleeApproachPending)
        {
            if (pressed)
                AttackPressed?.Invoke(true);
            else if (performed)
                AttackPressed?.Invoke(false);
        }

        if (performed)
        {
            _movementPriorityCancelActive = false;
            EngageCombatTargetIfNeeded(ctx.optionalTarget);
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
            targetQuery.TryGetNearestTransform(out optionalTarget);
            EngageCombatTargetIfNeeded(optionalTarget);
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

    bool TryProcessRangedAttack(RangedWeapon ranged, in AttackContext ctx)
    {
        return ranged.TryBeginAttack(in ctx);
    }

    bool TryProcessMeleeAttack(MeleeWeapon melee, in AttackContext ctx, bool pressed)
    {
        Vector3 origin = AttackOriginTransform.position;
        Transform target = ctx.optionalTarget;

        if (target == null || !melee.ShouldApplyMeleeLunge(origin, target))
            return melee.TryBeginAttack(in ctx);

        if (!melee.EnableApproachLunge || _movement == null)
            return melee.TryBeginAttack(in ctx);

        if (!melee.TryComputeApproachLunge(
                origin,
                target.position,
                out _,
                out float maxTravel,
                out float speed))
        {
            return melee.TryBeginAttack(in ctx);
        }

        if (!_movement.StartApproachLunge(target, melee.DamageOverlapRadius, maxTravel, speed))
            return melee.TryBeginAttack(in ctx);

        _pendingMeleeContext = ctx;
        _hasPendingMeleeContext = true;
        _meleeApproachPending = true;
        EngageCombatTargetIfNeeded(target);

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
            EndApproachPresentation();
            return;
        }

        AttackContext ctx = _pendingMeleeContext;
        _hasPendingMeleeContext = false;

        bool performed = melee.TryBeginAttack(in ctx);
        if (!performed)
        {
            EndApproachPresentation();
            return;
        }

        _movementPriorityCancelActive = false;
        EngageCombatTargetIfNeeded(ctx.optionalTarget);
        AttackPressed?.Invoke(false);
        AttackPerformed?.Invoke();
        LogProcessedAttackIfEnabled();
    }

    void EngageCombatTargetIfNeeded(Transform target)
    {
        if (targetQuery == null || target == null)
            return;
        targetQuery.EngageCombatTarget(target);
    }

    void EndApproachPresentation() => MeleeApproachCancelled?.Invoke();

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

    bool HasMovementPriorityInput()
    {
        if (_movement != null && _movement.IsDashing)
            return true;

        if (_dashProvider != null && _dashProvider.WasDashPressedThisFrame())
            return true;

        if (_moveProvider == null)
            return false;

        float deadzone = playerEntityState != null ? playerEntityState.MoveDeadzone : 0.08f;
        Vector2 intent = _moveProvider.GetMoveIntent();
        return intent.sqrMagnitude > deadzone * deadzone;
    }

    bool IsAttackInProgress()
    {
        if (_movementPriorityCancelActive)
            return false;

        if (_meleeApproachPending)
            return true;

        if (entityStateAnimator != null && entityStateAnimator.IsMeleeAttackClipPlaying)
            return true;

        if (weaponHolder != null
            && weaponHolder.Current is IAttackActivity activity
            && activity.IsAttackActive)
            return true;

        return _movement != null && _movement.IsLunging;
    }

    public void CancelAttackForInterrupt()
    {
        CancelAttackForMovementPriority();
    }

    void CancelAttackForMovementPriority()
    {
        if (_meleeApproachPending)
            CancelPendingMeleeApproach();
        else if (_movement != null && _movement.IsLunging)
            _movement.CancelApproachLunge();

        weaponHolder?.CancelActiveAttack();
        _movementPriorityCancelActive = true;
        AttackCancelled?.Invoke();
    }

    void LogProcessedAttackIfEnabled()
    {
#if UNITY_EDITOR
        if (!logProcessedAttack)
            return;

        IWeapon w = weaponHolder.Current;
        MonoBehaviour wmb = w as MonoBehaviour;
        string label = wmb != null
            ? $"'{wmb.name}' ({wmb.GetType().Name})"
            : (w != null ? $"({w.GetType().Name})" : "<none>");
        Debug.Log($"{nameof(PlayerAttackController)} on {name}: attack processed with <color=yellow>{label}</color>", this);
#endif
    }
}
