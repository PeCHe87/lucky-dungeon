using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the player <see cref="Animator"/> from <see cref="PlayerEntityState"/> changes using
/// code-side cross-fades (no animator controller transitions required).
/// Attack combo clips advance on attack button press; presses during playback are queued so
/// clips are never cancelled mid-swing.
/// </summary>
[DefaultExecutionOrder(110)]
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

    [Header("Melee approach")]
    [Tooltip("Used when the animation profile has no MeleeApproaching entry.")]
    [SerializeField] string meleeApproachStateFallback = "MeleeApproach";
    [SerializeField, Range(0.25f, 3f)] float minApproachAnimSpeed = 0.25f;
    [SerializeField, Range(0.25f, 3f)] float maxApproachAnimSpeed = 3f;

    [Header("Debug")]
    [SerializeField] bool logMissingBindings;
    [SerializeField] bool logAttackAnimation;

    MeleeWeapon _meleeWeapon;
    bool _meleeApproachAnimActive;
    float _defaultAnimatorSpeed = 1f;

    readonly Dictionary<PlayerEntityStateKind, int> _stateHashes = new Dictionary<PlayerEntityStateKind, int>();
    readonly HashSet<int> _attackStateHashes = new HashSet<int>();
    int _lastPlayedHash = int.MinValue;
    int _lastPlayedLayer;
    int _attackComboIndex;
    float _lastAttackPressTime = float.NegativeInfinity;
    int _queuedAttackPressCount;
    bool _locomotionSyncPending;

    void Awake()
    {
        if (playerEntityState == null)
            playerEntityState = GetComponent<PlayerEntityState>();

        if (attackController == null)
            attackController = GetComponent<PlayerAttackController>();

        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();

        ResolveMeleeWeapon();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            if (locomotionController != null)
                animator.runtimeAnimatorController = locomotionController;
            animator.applyRootMotion = false;
            _defaultAnimatorSpeed = animator.speed > 0f ? animator.speed : 1f;
        }

#if UNITY_EDITOR
        if (locomotionController == null)
        {
            locomotionController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/_project/_animation/PlayerLocomotion.controller");
            if (animator != null && locomotionController != null)
                animator.runtimeAnimatorController = locomotionController;
        }

        if (profile == null)
        {
            profile = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(
                PlayerEntityStateAnimationProfile.DefaultAssetPath);
        }
#endif

        RebuildHashCache();
    }

    void OnEnable()
    {
        if (playerEntityState != null)
            playerEntityState.StateChanged += OnStateChanged;

        if (attackController != null)
        {
            attackController.AttackPressed += OnAttackPressed;
            attackController.MeleeApproachStarted += OnMeleeApproachStarted;
            attackController.MeleeApproachCancelled += OnMeleeApproachCancelled;
        }

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
            attackController.MeleeApproachStarted -= OnMeleeApproachStarted;
            attackController.MeleeApproachCancelled -= OnMeleeApproachCancelled;
        }

        ResetAnimatorPlaybackSpeed();
        _meleeApproachAnimActive = false;
    }

    void OnValidate()
    {
        if (profile != null && Application.isPlaying)
            RebuildHashCache();
    }

    void Update()
    {
        if (animator == null || profile == null)
            return;

        int layer = GetAttackLayer();
        DrainQueuedAttackPresses(layer);
        TryApplyPendingLocomotion(layer);
    }

    void OnAttackPressed()
    {
        if (animator == null || profile == null)
            return;

        if (attackController != null && attackController.IsMeleeApproaching)
            return;

        ResetAnimatorPlaybackSpeed();
        _meleeApproachAnimActive = false;

        if (!profile.MeleeAttackSequence.IsValid)
            return;

        int layer = GetAttackLayer();
        if (IsAttackClipPlaying(layer))
        {
            _queuedAttackPressCount++;
            return;
        }

        TryPlayNextAttack(layer);
    }

    void OnStateChanged(PlayerEntityStateKind previous, PlayerEntityStateKind current)
    {
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

        _locomotionSyncPending = false;
        PlayForState(current, force: false);
    }

    void OnMeleeApproachStarted(float duration)
    {
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

        if (_defaultAnimatorSpeed <= 0f)
            _defaultAnimatorSpeed = animator.speed > 0f ? animator.speed : 1f;

        float clipLength = ResolveApproachClipLength(layer, stateHash);

        if (clipLength > 0f)
            animator.speed = Mathf.Clamp(clipLength / duration, minApproachAnimSpeed, maxApproachAnimSpeed);
        else
            animator.speed = _defaultAnimatorSpeed;

        if (logAttackAnimation)
            Debug.Log(
                $"[PlayerEntityStateAnimator] MeleeApproach '{stateName}' duration={duration:F2}s animator.speed={animator.speed:F2}.",
                this);
    }

    void OnMeleeApproachCancelled()
    {
        EndMeleeApproachAnimation();
    }

    void EndMeleeApproachAnimation()
    {
        if (!_meleeApproachAnimActive)
            return;

        ResetAnimatorPlaybackSpeed();
        _meleeApproachAnimActive = false;

        if (playerEntityState == null || profile == null)
            return;

        if (playerEntityState.Current == PlayerEntityStateKind.Attacking)
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
        if (playerEntityState == null || playerEntityState.Current == PlayerEntityStateKind.Attacking)
            return;

        PlayForState(playerEntityState.Current, force: false);
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

    float ResolveApproachClipLength(int layer, int stateHash)
    {
        if (animator == null)
            return 0f;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
        if (current.shortNameHash == stateHash && current.length > 0f)
            return current.length;

        AnimatorClipInfo[] currentClips = animator.GetCurrentAnimatorClipInfo(layer);
        if (currentClips.Length > 0 && currentClips[0].clip != null)
            return currentClips[0].clip.length;

        AnimatorClipInfo[] nextClips = animator.GetNextAnimatorClipInfo(layer);
        if (nextClips.Length > 0 && nextClips[0].clip != null)
            return nextClips[0].clip.length;

        return 0f;
    }

    void ResetAnimatorPlaybackSpeed()
    {
        if (animator == null)
            return;

        float restore = _defaultAnimatorSpeed > 0f ? _defaultAnimatorSpeed : 1f;
        animator.speed = restore;
    }

    float GetAttackCompletionThreshold()
    {
        MeleeAttackAnimationSequence sequence = profile.MeleeAttackSequence;
        float threshold = sequence.attackCompletionNormalizedTime;
        return threshold > 0f ? threshold : 0.95f;
    }

    /// <summary>True while a melee attack state is playing and has not reached the combo completion threshold.</summary>
    public bool IsMeleeAttackClipPlaying => IsAttackClipPlaying(GetAttackLayer());

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

    void ResolveMeleeWeapon()
    {
        if (weaponHolder != null && weaponHolder.Current is MeleeWeapon current)
            _meleeWeapon = current;
        else
            _meleeWeapon = GetComponentInChildren<MeleeWeapon>(true);
    }

    void PlayAttackState(string stateName, int layer)
    {
        if (animator == null)
            return;

        ResetAnimatorPlaybackSpeed();
        _meleeApproachAnimActive = false;

        int stateHash = Animator.StringToHash(stateName);
        animator.Play(stateHash, layer, 0f);

        _lastPlayedHash = stateHash;
        _lastPlayedLayer = layer;

        ResolveMeleeWeapon();

        if (logAttackAnimation)
            Debug.Log($"[PlayerEntityStateAnimator] PlayAttackState '{stateName}' on layer {layer}.", this);

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
        if (profile == null)
            return;

        foreach (PlayerEntityStateKind kind in System.Enum.GetValues(typeof(PlayerEntityStateKind)))
        {
            if (!profile.TryGetEntry(kind, out PlayerEntityStateAnimationEntry entry))
                continue;

            _stateHashes[kind] = Animator.StringToHash(entry.animatorStateName);
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

        if (!force
            && stateHash == _lastPlayedHash
            && layer == _lastPlayedLayer
            && animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == stateHash)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, crossFadeSeconds, layer, 0f);

        _lastPlayedHash = stateHash;
        _lastPlayedLayer = layer;
    }
}
