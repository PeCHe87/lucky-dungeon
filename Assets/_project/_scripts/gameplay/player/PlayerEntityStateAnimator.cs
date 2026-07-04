using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the player <see cref="Animator"/> from <see cref="PlayerEntityState"/> changes using
/// code-side cross-fades (no animator controller transitions required).
/// Attack combo clips advance on attack button press; presses during playback are queued.
/// Movement input cancels an in-progress attack clip via <see cref="PlayerAttackController.AttackCancelled"/>.
/// </summary>
[DefaultExecutionOrder(112)]
public sealed class PlayerEntityStateAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerEntityState playerEntityState;
    [SerializeField] PlayerAttackController attackController;
    [SerializeField] Animator animator;
    [SerializeField] RuntimeAnimatorController locomotionController;
    [SerializeField] PlayerEntityStateAnimationProfile profile;
    [Tooltip("If unset, uses WeaponHolder on this GameObject.")]
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] CombatTargetFocusLock combatFocusLock;

    [Header("Melee approach")]
    [Tooltip("Used when the animation profile has no MeleeApproaching entry.")]
    [SerializeField] string meleeApproachStateFallback = "MeleeApproach";

    [Header("Debug")]
    [SerializeField] bool logMissingBindings;
    [SerializeField] bool logAttackAnimation;

    MeleeWeapon _meleeWeapon;
    RangedWeapon _rangedWeapon;
    IMoveIntentProvider _moveProvider;
    TopDownCharacterMovement _movement;
    bool _meleeApproachAnimActive;

    RuntimeAnimatorController _defaultController;
    PlayerEntityStateAnimationProfile _defaultProfile;

    readonly Dictionary<PlayerEntityStateKind, int> _stateHashes = new Dictionary<PlayerEntityStateKind, int>();
    readonly HashSet<int> _attackStateHashes = new HashSet<int>();
    int _hitReactStateHash;
    float _hitReactCompletionThreshold = 0.95f;
    int _deathStateHash;
    float _deathCompletionThreshold = 0.95f;
    int _lastPlayedHash = int.MinValue;
    int _lastPlayedLayer;
    int _attackComboIndex;
    float _lastAttackPressTime = float.NegativeInfinity;
    int _queuedAttackPressCount;
    bool _locomotionSyncPending;
    bool _wasMeleeCombatTargetingLocked;
    bool _attackReadyAnimActive;

    void Awake()
    {
        if (playerEntityState == null)
            playerEntityState = GetComponent<PlayerEntityState>();

        if (attackController == null)
            attackController = GetComponent<PlayerAttackController>();

        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();

        if (combatFocusLock == null)
            combatFocusLock = GetComponent<CombatTargetFocusLock>();

        _moveProvider = GetComponent<IMoveIntentProvider>();
        _movement = GetComponent<TopDownCharacterMovement>();
        ResolveMeleeWeapon();
        ResolveRangedWeapon();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false;

#if UNITY_EDITOR
        if (locomotionController == null)
        {
            locomotionController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_project/_animation/PlayerLocomotion.controller");
        }

        if (profile == null)
        {
            profile = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(
                PlayerEntityStateAnimationProfile.DefaultAssetPath);
        }
#endif

        _defaultController = locomotionController;
        _defaultProfile = profile;
        ApplyWeaponAnimationBinding();
    }

    void OnEnable()
    {
        if (playerEntityState != null)
            playerEntityState.StateChanged += OnStateChanged;

        if (attackController != null)
        {
            attackController.AttackPressed += OnAttackPressed;
            attackController.AttackCancelled += OnAttackCancelled;
            attackController.MeleeApproachStarted += OnMeleeApproachStarted;
            attackController.MeleeApproachCancelled += OnMeleeApproachCancelled;
        }

        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;

        if (playerEntityState != null)
            PlayForState(playerEntityState.Current, force: true);
    }

    void OnDisable()
    {
        if (playerEntityState != null)
            playerEntityState.StateChanged -= OnStateChanged;

        if (attackController != null)
        {
            attackController.AttackPressed -= OnAttackPressed;
            attackController.AttackCancelled -= OnAttackCancelled;
            attackController.MeleeApproachStarted -= OnMeleeApproachStarted;
            attackController.MeleeApproachCancelled -= OnMeleeApproachCancelled;
        }

        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;

        _meleeApproachAnimActive = false;
    }

    void OnEquippedWeaponChanged() => ApplyWeaponAnimationBinding();

    void ApplyWeaponAnimationBinding()
    {
        RuntimeAnimatorController controller = _defaultController;
        PlayerEntityStateAnimationProfile activeProfile = _defaultProfile;

        if (weaponHolder != null
            && weaponHolder.Current is MonoBehaviour weaponBehaviour
            && weaponBehaviour is IWeaponAnimationBinding binding)
        {
            if (binding.AnimatorController != null)
                controller = binding.AnimatorController;
            if (binding.AnimationProfile != null)
                activeProfile = binding.AnimationProfile;
        }

        profile = activeProfile;

        if (animator != null && controller != null)
            animator.runtimeAnimatorController = controller;

        _attackComboIndex = 0;
        _queuedAttackPressCount = 0;
        _locomotionSyncPending = false;
        _meleeApproachAnimActive = false;
        _attackReadyAnimActive = false;
        _lastPlayedHash = int.MinValue;

        ResolveMeleeWeapon();
        ResolveRangedWeapon();
        RebuildHashCache();

        if (playerEntityState != null && isActiveAndEnabled)
            PlayForState(playerEntityState.Current, force: true);
    }

    void OnValidate()
    {
        if (profile != null && Application.isPlaying)
            RebuildHashCache();
    }

    void LateUpdate()
    {
        if (animator == null || profile == null)
            return;

        int layer = GetAttackLayer();
        if (playerEntityState == null
            || (playerEntityState.Current != PlayerEntityStateKind.TakingDamage
                && playerEntityState.Current != PlayerEntityStateKind.Dying))
            DrainQueuedAttackPresses(layer);
        TryApplyPendingLocomotion(layer);
        UpdateAttackReadyPresentation();
        UpdateCombatFocusLockGrace();
    }

    void UpdateCombatFocusLockGrace()
    {
        bool locked = IsMeleeCombatTargetingLocked;
        if (_wasMeleeCombatTargetingLocked && !locked)
            combatFocusLock?.NotifyMeleeCombatLockEnded();
        _wasMeleeCombatTargetingLocked = locked;
    }

    void OnAttackPressed(bool isUserPress)
    {
        if (animator == null || profile == null)
            return;

        if (attackController != null && attackController.IsMeleeApproaching)
            return;

        _meleeApproachAnimActive = false;

        if (!profile.MeleeAttackSequence.IsValid)
            return;

        int layer = GetAttackLayer();
        if (IsAttackClipPlaying(layer))
        {
            if (isUserPress)
                _queuedAttackPressCount++;
            return;
        }

        TryPlayNextAttack(layer);
    }

    void OnStateChanged(PlayerEntityStateKind previous, PlayerEntityStateKind current)
    {
        if (current == PlayerEntityStateKind.TakingDamage || current == PlayerEntityStateKind.Dying)
        {
            _queuedAttackPressCount = 0;
            _locomotionSyncPending = false;
            _meleeApproachAnimActive = false;
            _attackReadyAnimActive = false;
            PlayForState(current, force: true);
            return;
        }

        int layer = GetAttackLayer();
        if (IsAttackClipPlaying(layer))
        {
            _locomotionSyncPending = true;
            return;
        }

        if (current == PlayerEntityStateKind.Attacking)
            return;

        if (current == PlayerEntityStateKind.MeleeApproaching || _meleeApproachAnimActive)
            return;

        if (ShouldSuppressLocomotionForHeldAttack())
            return;

        _locomotionSyncPending = false;
        PlayForState(current, force: false);
    }

    void OnMeleeApproachStarted(float duration)
    {
        if (weaponHolder != null && !weaponHolder.IsMeleeEquipped())
            return;

        if (animator == null || profile == null || duration <= 0f)
            return;

        string stateName = ResolveMeleeApproachStateName();
        if (string.IsNullOrWhiteSpace(stateName))
            return;

        int layer = GetLocomotionLayer();
        float crossFade = profile.TryGetEntry(PlayerEntityStateKind.MeleeApproaching, out PlayerEntityStateAnimationEntry entry)
            ? profile.ResolveCrossFadeSeconds(in entry)
            : profile.DefaultCrossFadeSeconds;

        int stateHash = Animator.StringToHash(stateName);
        animator.CrossFadeInFixedTime(stateHash, crossFade, layer, 0f);

        _lastPlayedHash = stateHash;
        _lastPlayedLayer = layer;
        _meleeApproachAnimActive = true;

        if (logAttackAnimation)
            Debug.Log(
                $"[PlayerEntityStateAnimator] MeleeApproach '{stateName}' lunge duration={duration:F2}s.",
                this);
    }

    void OnAttackCancelled()
    {
        _queuedAttackPressCount = 0;
        _locomotionSyncPending = false;
        _attackComboIndex = 0;
        _meleeApproachAnimActive = false;
        _attackReadyAnimActive = false;

        if (profile == null)
            return;

        PlayForState(ResolvePostCancelLocomotionState(), force: false);
    }

    PlayerEntityStateKind ResolvePostCancelLocomotionState()
    {
        if (_movement != null && _movement.IsDashing)
            return PlayerEntityStateKind.Dashing;
        if (HasMoveIntentAboveDeadzone())
            return PlayerEntityStateKind.Running;

        return PlayerEntityStateKind.Idle;
    }

    void OnMeleeApproachCancelled()
    {
        EndMeleeApproachAnimation();
    }

    void EndMeleeApproachAnimation()
    {
        if (!_meleeApproachAnimActive)
            return;

        _meleeApproachAnimActive = false;

        if (playerEntityState == null || profile == null)
            return;

        if (playerEntityState.Current == PlayerEntityStateKind.Attacking)
            return;

        if (ShouldSuppressLocomotionForHeldAttack())
            return;

        PlayForState(playerEntityState.Current, force: false);
    }

    void DrainQueuedAttackPresses(int layer)
    {
        while (_queuedAttackPressCount > 0 && !IsAttackClipPlaying(layer))
        {
            _queuedAttackPressCount--;
            TryPlayNextAttack(layer);
        }
    }

    void TryApplyPendingLocomotion(int layer)
    {
        if (!_locomotionSyncPending || IsAttackClipPlaying(layer) || _queuedAttackPressCount > 0)
            return;

        _locomotionSyncPending = false;
        if (playerEntityState == null
            || playerEntityState.Current == PlayerEntityStateKind.Attacking
            || playerEntityState.Current == PlayerEntityStateKind.TakingDamage
            || playerEntityState.Current == PlayerEntityStateKind.Dying)
            return;

        if (ShouldSuppressLocomotionForHeldAttack())
            return;

        PlayForState(playerEntityState.Current, force: false);
    }

    void UpdateAttackReadyPresentation()
    {
        if (animator == null || profile == null)
            return;

        bool shouldShow = ShouldShowAttackReadyStance();

        if (!shouldShow)
        {
            if (_attackReadyAnimActive)
            {
                _attackReadyAnimActive = false;
                if (playerEntityState != null
                    && !IsAttackClipPlaying(GetAttackLayer())
                    && playerEntityState.Current != PlayerEntityStateKind.TakingDamage
                    && playerEntityState.Current != PlayerEntityStateKind.Dying)
                {
                    PlayForState(playerEntityState.Current, force: false);
                }
            }

            return;
        }

        int layer = GetLocomotionLayer();
        int stateHash = Animator.StringToHash(profile.AttackReadyAnimatorStateName);
        AnimatorStateInfo currentInfo = animator.GetCurrentAnimatorStateInfo(layer);
        if (_attackReadyAnimActive
            && currentInfo.shortNameHash == stateHash
            && stateHash == _lastPlayedHash
            && layer == _lastPlayedLayer)
        {
            return;
        }

        _attackReadyAnimActive = true;
        _meleeApproachAnimActive = false;
        CrossFadeToStateHash(stateHash, profile.ResolveAttackReadyCrossFadeSeconds(), layer, force: false);
    }

    bool ShouldShowAttackReadyStance()
    {
        if (attackController == null || profile == null || !profile.HasAttackReadyState)
            return false;
        if (!attackController.IsAttackHoldActive)
            return false;
        if (!attackController.IsCurrentWeaponOnCooldown)
            return false;
        if (IsAttackClipPlaying(GetAttackLayer()))
            return false;
        if (HasMoveIntentAboveDeadzone())
            return false;
        if (_meleeApproachAnimActive || attackController.IsMeleeApproaching)
            return false;
        if (playerEntityState != null && playerEntityState.Current == PlayerEntityStateKind.TakingDamage)
            return false;
        if (playerEntityState != null && playerEntityState.Current == PlayerEntityStateKind.Dying)
            return false;
        if (IsHitReactClipPlaying)
            return false;
        if (IsDeathClipPlaying)
            return false;
        if (weaponHolder != null
            && weaponHolder.Current is IAttackActivity activity
            && activity.IsAttackActive)
            return false;

        return true;
    }

    bool ShouldSuppressLocomotionForHeldAttack()
    {
        if (attackController == null || !attackController.IsAttackHoldActive)
            return false;
        if (_meleeApproachAnimActive || attackController.IsMeleeApproaching)
            return false;
        if (HasMoveIntentAboveDeadzone())
            return false;

        return true;
    }

    bool HasMoveIntentAboveDeadzone()
    {
        if (_moveProvider == null)
            return false;

        float deadzone = playerEntityState != null ? playerEntityState.MoveDeadzone : 0.08f;
        Vector2 intent = _moveProvider.GetMoveIntent();
        return intent.sqrMagnitude > deadzone * deadzone;
    }

    bool TryPlayNextAttack(int layer)
    {
        MeleeAttackAnimationSequence sequence = profile.MeleeAttackSequence;
        if (!sequence.IsValid)
            return false;

        if (Time.time - _lastAttackPressTime > sequence.comboResetSeconds)
            _attackComboIndex = 0;

        if (!profile.TryGetMeleeAttackState(_attackComboIndex, out string stateName, out _))
            return false;

        PlayAttackState(stateName, layer);

        _attackComboIndex = (_attackComboIndex + 1) % sequence.Count;
        _lastAttackPressTime = Time.time;
        return true;
    }

    int GetAttackLayer()
    {
        if (profile != null
            && profile.TryGetEntry(PlayerEntityStateKind.Attacking, out PlayerEntityStateAnimationEntry attackEntry))
        {
            return attackEntry.layer;
        }

        return 0;
    }

    int GetLocomotionLayer()
    {
        if (profile != null
            && profile.TryGetEntry(PlayerEntityStateKind.MeleeApproaching, out PlayerEntityStateAnimationEntry approachEntry))
        {
            return approachEntry.layer;
        }

        return 0;
    }

    string ResolveMeleeApproachStateName()
    {
        if (profile != null
            && profile.TryGetEntry(PlayerEntityStateKind.MeleeApproaching, out PlayerEntityStateAnimationEntry entry)
            && !string.IsNullOrWhiteSpace(entry.animatorStateName))
        {
            return entry.animatorStateName;
        }

        return meleeApproachStateFallback;
    }

    float GetAttackCompletionThreshold()
    {
        MeleeAttackAnimationSequence sequence = profile.MeleeAttackSequence;
        float threshold = sequence.attackCompletionNormalizedTime;
        return threshold > 0f ? threshold : 0.95f;
    }

    /// <summary>True while a melee attack state is playing and has not reached the combo completion threshold.</summary>
    public bool IsMeleeAttackClipPlaying => IsAttackClipPlaying(GetAttackLayer());

    /// <summary>True while the hurt-react clip is playing and has not reached completion.</summary>
    public bool IsHitReactClipPlaying => IsHitReactClipPlayingOnLayer(GetHitReactLayer());

    /// <summary>True while the death clip is playing and has not reached completion.</summary>
    public bool IsDeathClipPlaying => IsDeathClipPlayingOnLayer(GetDeathLayer());

    /// <summary>True during attack clips, attack-layer transitions, or queued combo inputs.</summary>
    public bool IsMeleeCombatTargetingLocked =>
        IsMeleeAttackClipPlaying
        || _queuedAttackPressCount > 0
        || IsAttackLayerInTransition();

    bool IsAttackLayerInTransition()
    {
        if (animator == null)
            return false;
        return animator.IsInTransition(GetAttackLayer());
    }

    bool IsAttackClipPlaying(int layer)
    {
        if (animator == null || _attackStateHashes.Count == 0)
            return false;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
        if (!_attackStateHashes.Contains(info.shortNameHash))
            return false;

        return info.normalizedTime < GetAttackCompletionThreshold();
    }

    int GetHitReactLayer()
    {
        if (profile != null
            && profile.TryGetEntry(PlayerEntityStateKind.TakingDamage, out PlayerEntityStateAnimationEntry entry))
        {
            return entry.layer;
        }

        return 0;
    }

    bool IsHitReactClipPlayingOnLayer(int layer)
    {
        if (animator == null || _hitReactStateHash == 0)
            return false;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
        if (info.shortNameHash != _hitReactStateHash)
            return false;

        return info.normalizedTime < _hitReactCompletionThreshold;
    }

    int GetDeathLayer()
    {
        if (profile != null
            && profile.TryGetEntry(PlayerEntityStateKind.Dying, out PlayerEntityStateAnimationEntry entry))
        {
            return entry.layer;
        }

        return 0;
    }

    bool IsDeathClipPlayingOnLayer(int layer)
    {
        if (animator == null || _deathStateHash == 0)
            return false;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
        if (info.shortNameHash != _deathStateHash)
            return false;

        return info.normalizedTime < _deathCompletionThreshold;
    }

    void ResolveMeleeWeapon()
    {
        if (weaponHolder != null && weaponHolder.Current is MeleeWeapon current)
            _meleeWeapon = current;
        else
            _meleeWeapon = GetComponentInChildren<MeleeWeapon>(true);
    }

    void ResolveRangedWeapon()
    {
        if (weaponHolder != null && weaponHolder.Current is RangedWeapon current)
            _rangedWeapon = current;
        else
            _rangedWeapon = GetComponentInChildren<RangedWeapon>(true);
    }

    void PlayAttackState(string stateName, int layer)
    {
        if (animator == null)
            return;

        _meleeApproachAnimActive = false;
        _attackReadyAnimActive = false;

        int stateHash = Animator.StringToHash(stateName);
        animator.Play(stateHash, layer, 0f);

        _lastPlayedHash = stateHash;
        _lastPlayedLayer = layer;

        if (logAttackAnimation)
            Debug.Log($"[PlayerEntityStateAnimator] PlayAttackState '{stateName}' on layer {layer}.", this);

        if (weaponHolder != null && weaponHolder.IsRangedEquipped())
        {
            ResolveRangedWeapon();
            if (_rangedWeapon != null)
            {
                _rangedWeapon.ArmFireForCurrentShot();
                if (logAttackAnimation)
                    Debug.Log($"[PlayerEntityStateAnimator] ArmFireForCurrentShot on '{_rangedWeapon.name}'.", this);
            }
            else if (logAttackAnimation)
            {
                Debug.LogWarning(
                    "[PlayerEntityStateAnimator] RangedWeapon not found; ArmFireForCurrentShot skipped.",
                    this);
            }

            return;
        }

        ResolveMeleeWeapon();

        if (_meleeWeapon == null)
        {
            if (logAttackAnimation)
                Debug.LogWarning(
                    "[PlayerEntityStateAnimator] MeleeWeapon not found; ArmHitForCurrentSwing skipped.",
                    this);
            return;
        }

        _meleeWeapon.ArmHitForCurrentSwing();

        if (logAttackAnimation)
            Debug.Log($"[PlayerEntityStateAnimator] ArmHitForCurrentSwing on '{_meleeWeapon.name}'.", this);
    }

    void RebuildHashCache()
    {
        _stateHashes.Clear();
        _attackStateHashes.Clear();
        _hitReactStateHash = 0;
        _deathStateHash = 0;
        if (profile == null)
            return;

        foreach (PlayerEntityStateKind kind in System.Enum.GetValues(typeof(PlayerEntityStateKind)))
        {
            if (!profile.TryGetEntry(kind, out PlayerEntityStateAnimationEntry entry))
                continue;

            _stateHashes[kind] = Animator.StringToHash(entry.animatorStateName);
            if (kind == PlayerEntityStateKind.TakingDamage)
                _hitReactStateHash = _stateHashes[kind];
            if (kind == PlayerEntityStateKind.Dying)
                _deathStateHash = _stateHashes[kind];
        }

        MeleeAttackAnimationSequence sequence = profile.MeleeAttackSequence;
        if (!sequence.IsValid)
            return;

        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence.TryGetState(i, out string stateName))
                _attackStateHashes.Add(Animator.StringToHash(stateName));
        }
    }

    void PlayForState(PlayerEntityStateKind state, bool force)
    {
        if (animator == null || profile == null)
            return;

        if (!profile.TryGetEntry(state, out PlayerEntityStateAnimationEntry entry))
        {
            if (logMissingBindings)
                Debug.LogWarning($"[PlayerEntityStateAnimator] No animation entry for {state} on '{name}'.", this);
            return;
        }

        float crossFade = profile.ResolveCrossFadeSeconds(in entry);
        CrossFadeToStateName(entry.animatorStateName, crossFade, entry.layer, force);
    }

    void CrossFadeToStateName(string stateName, float crossFadeSeconds, int layer, bool force)
    {
        int stateHash = Animator.StringToHash(stateName);
        CrossFadeToStateHash(stateHash, crossFadeSeconds, layer, force);
    }

    void CrossFadeToStateHash(int stateHash, float crossFadeSeconds, int layer, bool force)
    {
        if (animator == null)
            return;

        bool alreadyOnState = stateHash == _lastPlayedHash
            && layer == _lastPlayedLayer
            && animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == stateHash;

        if (!force && alreadyOnState)
        {
            _meleeApproachAnimActive = false;
            if (stateHash != Animator.StringToHash(profile != null ? profile.AttackReadyAnimatorStateName : string.Empty))
                _attackReadyAnimActive = false;
            return;
        }

        _meleeApproachAnimActive = false;
        if (profile == null
            || stateHash != Animator.StringToHash(profile.AttackReadyAnimatorStateName))
        {
            _attackReadyAnimActive = false;
        }

        animator.CrossFadeInFixedTime(stateHash, crossFadeSeconds, layer, 0f);

        _lastPlayedHash = stateHash;
        _lastPlayedLayer = layer;
    }
}
