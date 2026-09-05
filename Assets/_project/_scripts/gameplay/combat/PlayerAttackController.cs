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
    [Tooltip("If true, logs ignored, buffered, consumed, and cleared attack input.")]
    [SerializeField] bool logIgnoredAttackInput;
    [Tooltip("If true, logs weapon / moving / canProcess on every attack press (works on device).")]
    [SerializeField] bool logAttackPress;
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
    bool _rangedAttackDisengagedUntilRelease;
    bool _hasBufferedAttackPress;
    RangedWeapon _subscribedRangedWeapon;
    MeleeWeapon _subscribedMeleeWeapon;

    /// <summary>World position used as melee overlap origin (<see cref="AttackContext.attacker"/>).</summary>
    public Transform AttackOriginTransform => transform;

    /// <summary>Transform whose forward defines melee cone facing (<see cref="AttackContext.facing"/>).</summary>
    public Transform AttackFacingTransform => facingRoot != null ? facingRoot : transform;

    public bool IsMeleeApproaching => _meleeApproachPending;

    public bool IsAttackInputHeld =>
        _attackProvider != null && _attackProvider.IsAttackHeld();

    /// <summary>True when held attack input should drive combat/animation (false after ranged cancel-by-move until release).</summary>
    public bool IsAttackHoldActive =>
        IsAttackInputHeld && !_rangedAttackDisengagedUntilRelease;

    /// <summary>
    /// True while attack hold, melee approach, active attack window, or melee attack clip
    /// should prevent locomotion resume.
    /// </summary>
    public bool IsAttackBlockingLocomotion =>
        _meleeApproachPending
        || IsAttackHoldActive
        || (entityStateAnimator != null && entityStateAnimator.IsMeleeAttackClipPlaying)
        || (weaponHolder != null
            && weaponHolder.Current is IAttackActivity activity
            && activity.IsAttackActive);

    public bool IsCurrentWeaponOnCooldown =>
        weaponHolder != null
        && weaponHolder.Current is IWeaponAttackReadiness readiness
        && !readiness.IsAttackReady;

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

    void OnEnable()
    {
        if (playerEntityState != null)
            playerEntityState.StateChanged += OnPlayerStateChanged;

        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;

        RefreshWeaponReloadSubscription();
    }

    void OnDisable()
    {
        if (playerEntityState != null)
            playerEntityState.StateChanged -= OnPlayerStateChanged;

        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;

        UnsubscribeWeaponReload();
    }

    void OnEquippedWeaponChanged() => RefreshWeaponReloadSubscription();

    void RefreshWeaponReloadSubscription()
    {
        RangedWeapon nextRanged = weaponHolder != null && weaponHolder.Current is RangedWeapon ranged
            ? ranged
            : null;
        MeleeWeapon nextMelee = weaponHolder != null && weaponHolder.Current is MeleeWeapon melee
            ? melee
            : null;

        if (_subscribedRangedWeapon != nextRanged)
        {
            if (_subscribedRangedWeapon != null)
                _subscribedRangedWeapon.ReloadStarted -= OnWeaponReloadStarted;

            _subscribedRangedWeapon = nextRanged;
            if (_subscribedRangedWeapon != null)
                _subscribedRangedWeapon.ReloadStarted += OnWeaponReloadStarted;
        }

        if (_subscribedMeleeWeapon != nextMelee)
        {
            if (_subscribedMeleeWeapon != null)
                _subscribedMeleeWeapon.ReloadStarted -= OnWeaponReloadStarted;

            _subscribedMeleeWeapon = nextMelee;
            if (_subscribedMeleeWeapon != null)
                _subscribedMeleeWeapon.ReloadStarted += OnWeaponReloadStarted;
        }
    }

    void UnsubscribeWeaponReload()
    {
        if (_subscribedRangedWeapon != null)
        {
            _subscribedRangedWeapon.ReloadStarted -= OnWeaponReloadStarted;
            _subscribedRangedWeapon = null;
        }

        if (_subscribedMeleeWeapon != null)
        {
            _subscribedMeleeWeapon.ReloadStarted -= OnWeaponReloadStarted;
            _subscribedMeleeWeapon = null;
        }
    }

    void OnWeaponReloadStarted() => CancelComboForMagazineEmpty();

    void CancelComboForMagazineEmpty()
    {
        ClearBufferedAttackPress("magazine empty reload");
        weaponHolder?.CancelActiveAttack();
        AttackCancelled?.Invoke();
    }

    void Update()
    {
        ClearRangedAttackDisengageIfReleased();

        if (playerEntityState != null && playerEntityState.IsInputBlocked)
            return;

        if (!HasMovementPriorityInput())
        {
            _movementPriorityCancelActive = false;
            return;
        }

        ClearBufferedAttackPress("movement priority");

        if (!IsAttackInProgress())
            return;

        CancelAttackForMovementPriority();
    }

    void LateUpdate()
    {
        if (_attackProvider == null || weaponHolder == null)
            return;

        bool pressed = _attackProvider.WasAttackPressedThisFrame();
        if (pressed)
        {
            // Safety: cancel stick even if UI cancel ran in a different order this frame.
            if (_attackProvider is FeneraxJoystickMoveIntentProvider moveIntent)
                moveIntent.CancelVirtualStickForAttack();
            LogAttackPressDebug();
        }

        if (_meleeApproachPending)
        {
            if (pressed)
                TryHandleExplicitPressBuffering(pressed);

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

        ClearRangedAttackDisengageIfReleased();

        // Dash keeps priority. Stick no longer blocks an explicit attack press (UI attack force-cancels stick).
        if (_movement != null && _movement.IsDashing)
            return;

        if (HasMovementPriorityInput() && !pressed)
            return;

        if (IsEquippedWeaponReloading())
        {
            ClearBufferedAttackPress("weapon reloading");
            return;
        }

        if (TryConsumeBufferedAttackPress())
            return;

        bool held = IsAttackHoldActive;
        if (!pressed && !held)
            return;
        if (playerEntityState != null && playerEntityState.IsInputBlocked)
            return;
        if (playerEntityState != null && playerEntityState.IsAttackInputBlocked)
        {
            if (pressed)
                TryHandleExplicitPressBuffering(pressed);
            return;
        }

        if (_movement != null && _movement.IsLunging)
            return;

        if (entityStateAnimator != null
            && entityStateAnimator.IsMeleeAttackClipPlaying
            && held
            && !pressed
            && weaponHolder.Current is MeleeWeapon)
            return;

        if (!TryExecuteAttack(pressed, out bool performed))
            return;

        if (pressed && !performed && IsExplicitAttackPressUnavailable())
            TryHandleExplicitPressBuffering(pressed);
    }

    bool IsEquippedWeaponReloading()
    {
        if (weaponHolder == null)
            return false;

        if (weaponHolder.Current is RangedWeapon ranged && ranged.IsReloading)
            return true;

        if (weaponHolder.Current is MeleeWeapon melee && melee.IsReloading)
            return true;

        return false;
    }

    bool IsExplicitAttackPressUnavailable() =>
        IsCurrentWeaponOnCooldown || IsAttackInProgress();

    void TryHandleExplicitPressBuffering(bool pressed)
    {
        if (!pressed)
            return;

        if (playerEntityState != null && playerEntityState.IsInputBlocked)
            return;

        if (IsEquippedWeaponReloading())
        {
            LogIgnoredAttackInput("weapon reloading");
            return;
        }

        if (playerEntityState != null && playerEntityState.IsAttackInputBlocked)
        {
            LogIgnoredAttackInput("locomotion blocked");
            return;
        }

        if (!IsExplicitAttackPressUnavailable())
            return;

        if (!_hasBufferedAttackPress)
        {
            _hasBufferedAttackPress = true;
            LogAttackInputDebug("Attack input buffered.");
            return;
        }

        LogIgnoredAttackInput("buffer already full");
    }

    bool TryConsumeBufferedAttackPress()
    {
        if (!_hasBufferedAttackPress)
            return false;

        if (playerEntityState != null && playerEntityState.IsInputBlocked)
            return false;
        if (playerEntityState != null && playerEntityState.IsAttackInputBlocked)
            return false;
        if (_movement != null && _movement.IsLunging)
            return false;
        if (IsExplicitAttackPressUnavailable())
            return false;

        if (!TryBuildAttackContext(out AttackContext ctx))
            return false;

        bool performed = ExecuteAttackFromContext(in ctx, isUserPress: false);
        if (performed)
            LogAttackInputDebug("Buffered attack input consumed.");

        return performed;
    }

    bool TryExecuteAttack(bool isUserPress, out bool performed)
    {
        performed = false;
        if (!TryBuildAttackContext(out AttackContext ctx))
            return false;

        performed = ExecuteAttackFromContext(in ctx, isUserPress);
        return true;
    }

    bool ExecuteAttackFromContext(in AttackContext ctx, bool isUserPress)
    {
        bool performed;
        if (weaponHolder.Current is MeleeWeapon melee)
            performed = TryProcessMeleeAttack(melee, in ctx, isUserPress);
        else if (weaponHolder.Current is RangedWeapon ranged)
            performed = TryProcessRangedAttack(ranged, in ctx);
        else
            performed = weaponHolder.TryAttack(in ctx);

        if (!_meleeApproachPending && performed)
            AttackPressed?.Invoke(isUserPress);

        if (performed)
        {
            _movementPriorityCancelActive = false;
            ClearBufferedAttackPress();
            EngageCombatTargetIfNeeded(ctx.optionalTarget);
            AttackPerformed?.Invoke();
            LogProcessedAttackIfEnabled();
        }

        return performed;
    }

    void ClearBufferedAttackPress(string reason = null)
    {
        if (!_hasBufferedAttackPress)
            return;

        _hasBufferedAttackPress = false;
        if (!string.IsNullOrEmpty(reason))
            LogAttackInputDebug($"Buffered attack input cleared ({reason}).");
    }

    void OnPlayerStateChanged(PlayerEntityStateKind previous, PlayerEntityStateKind current)
    {
        switch (current)
        {
            case PlayerEntityStateKind.Walking:
            case PlayerEntityStateKind.Running:
            case PlayerEntityStateKind.Dashing:
            case PlayerEntityStateKind.TakingDamage:
            case PlayerEntityStateKind.Dying:
                ClearBufferedAttackPress($"state changed to {current}");
                break;
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
        ClearBufferedAttackPress();
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

        // Stick force-cancelled by attack: do not cancel the attack or treat residual stick as move.
        if (_moveProvider is FeneraxJoystickMoveIntentProvider fenerax
            && fenerax.IsVirtualStickSuppressed)
            return false;

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

        if (IsRangedAttackWaitingForNextShot())
            return true;

        return _movement != null && _movement.IsLunging;
    }

    bool IsRangedAttackWaitingForNextShot()
    {
        if (weaponHolder == null || !weaponHolder.IsRangedEquipped())
            return false;
        if (!IsAttackHoldActive)
            return false;
        if (!IsCurrentWeaponOnCooldown)
            return false;
        if (entityStateAnimator != null && entityStateAnimator.IsMeleeAttackClipPlaying)
            return false;

        return true;
    }

    void ClearRangedAttackDisengageIfReleased()
    {
        if (!_rangedAttackDisengagedUntilRelease)
            return;
        if (_attackProvider == null || !_attackProvider.IsAttackHeld())
            _rangedAttackDisengagedUntilRelease = false;
    }

    public void CancelAttackForInterrupt()
    {
        CancelActiveAttackPresentation(disengageRangedHold: false);
    }

    void CancelAttackForMovementPriority()
    {
        CancelActiveAttackPresentation(disengageRangedHold: true);
    }

    void CancelActiveAttackPresentation(bool disengageRangedHold)
    {
        if (_meleeApproachPending)
            CancelPendingMeleeApproach();
        else if (_movement != null && _movement.IsLunging)
            _movement.CancelApproachLunge();

        weaponHolder?.CancelActiveAttack();
        if (disengageRangedHold && weaponHolder != null && weaponHolder.IsRangedEquipped())
            _rangedAttackDisengagedUntilRelease = true;
        _movementPriorityCancelActive = true;
        ClearBufferedAttackPress("attack cancelled");
        AttackCancelled?.Invoke();
    }

    void LogIgnoredAttackInput(string reason)
    {
        if (!logIgnoredAttackInput)
            return;

        Debug.Log($"[PlayerAttackController] Attack input ignored — {reason}.", this);
    }

    void LogAttackInputDebug(string message)
    {
        if (!logIgnoredAttackInput)
            return;

        Debug.Log($"[PlayerAttackController] {message}", this);
    }

    void LogAttackPressDebug()
    {
        if (!logAttackPress)
            return;

        bool moving = IsStickMoveActive();
        bool suppressed = _moveProvider is FeneraxJoystickMoveIntentProvider fenerax
            && fenerax.IsVirtualStickSuppressed;
        string state = playerEntityState != null ? playerEntityState.Current.ToString() : "<no state>";
        bool canProcess = TryGetAttackPressProcessability(out string reason);
        string canLabel = canProcess ? "true" : $"false reason={reason}";

        Debug.Log(
            $"[PlayerAttackController] Attack press — weapon={FormatEquippedWeaponLabel()}, moving={moving}, stickSuppressed={suppressed}, attackBlockingMove={IsAttackBlockingLocomotion}, state={state}, canProcess={canLabel}",
            this);
    }

    bool IsStickMoveActive()
    {
        if (_moveProvider is FeneraxJoystickMoveIntentProvider fenerax
            && fenerax.IsVirtualStickSuppressed)
            return false;

        if (_moveProvider == null)
            return false;

        float deadzone = playerEntityState != null ? playerEntityState.MoveDeadzone : 0.08f;
        Vector2 intent = _moveProvider.GetMoveIntent();
        return intent.sqrMagnitude > deadzone * deadzone;
    }

    bool TryGetAttackPressProcessability(out string reason)
    {
        if (_meleeApproachPending)
        {
            reason = "melee approach pending (press buffered if available)";
            return false;
        }

        if (_movement != null && _movement.IsDashing)
        {
            reason = "dashing";
            return false;
        }

        if (IsEquippedWeaponReloading())
        {
            reason = "weapon reloading";
            return false;
        }

        if (playerEntityState != null && playerEntityState.IsInputBlocked)
        {
            reason = "input blocked";
            return false;
        }

        if (playerEntityState != null && playerEntityState.IsAttackInputBlocked)
        {
            reason = "attack input blocked";
            return false;
        }

        if (_movement != null && _movement.IsLunging)
        {
            reason = "lunging";
            return false;
        }

        if (weaponHolder == null || weaponHolder.Current == null)
        {
            reason = "no weapon";
            return false;
        }

        if (IsExplicitAttackPressUnavailable())
        {
            reason = "cooldown or attack in progress (may buffer)";
            return false;
        }

        reason = "ok";
        return true;
    }

    string FormatEquippedWeaponLabel()
    {
        if (weaponHolder == null)
            return "<none>";

        IWeapon w = weaponHolder.Current;
        MonoBehaviour wmb = w as MonoBehaviour;
        if (wmb != null)
            return $"'{wmb.name}' ({wmb.GetType().Name})";
        return w != null ? $"({w.GetType().Name})" : "<none>";
    }

    void LogProcessedAttackIfEnabled()
    {
#if UNITY_EDITOR
        if (!logProcessedAttack)
            return;

        Debug.Log(
            $"{nameof(PlayerAttackController)} on {name}: attack processed with <color=yellow>{FormatEquippedWeaponLabel()}</color>",
            this);
#endif
    }
}
