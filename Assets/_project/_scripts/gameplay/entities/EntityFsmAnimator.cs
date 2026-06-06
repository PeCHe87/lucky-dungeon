using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives an entity <see cref="Animator"/> from <see cref="AIStateMachine"/> transitions using code-side cross-fades.
/// </summary>
[DefaultExecutionOrder(112)]
public sealed class EntityFsmAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] AIStateMachine stateMachine;
    [SerializeField] Animator animator;
    [SerializeField] EntityFsmAnimationProfile profile;

    [Header("Debug")]
    [SerializeField] bool logMissingBindings;

    readonly Dictionary<string, int> _stateHashes = new Dictionary<string, int>();
    int _lastPlayedHash = int.MinValue;
    int _lastPlayedLayer;

    void Awake()
    {
        if (stateMachine == null)
            stateMachine = GetComponent<AIStateMachine>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
            animator.applyRootMotion = false;

#if UNITY_EDITOR
        EntityFsmAnimationProfile.EnsureDefaultAssetExists();
        if (profile == null)
        {
            profile = UnityEditor.AssetDatabase.LoadAssetAtPath<EntityFsmAnimationProfile>(
                EntityFsmAnimationProfile.DefaultAssetPath);
        }
#endif

        RebuildHashCache();
    }

    void OnEnable()
    {
        if (stateMachine != null)
            stateMachine.StateChanged += OnStateChanged;

        PlayForCurrentState(force: true);
    }

    void OnDisable()
    {
        if (stateMachine != null)
            stateMachine.StateChanged -= OnStateChanged;
    }

    void OnValidate()
    {
        if (profile != null && Application.isPlaying)
            RebuildHashCache();
    }

    void OnStateChanged(AIStateData previous, AIStateData current)
    {
        if (current == null)
            return;

        PlayForStateId(current.stateId, force: false);
    }

    void PlayForCurrentState(bool force)
    {
        if (stateMachine == null || stateMachine.CurrentStateData == null)
            return;

        PlayForStateId(stateMachine.CurrentStateData.stateId, force);
    }

    void PlayForStateId(string stateId, bool force)
    {
        if (animator == null || profile == null || string.IsNullOrWhiteSpace(stateId))
            return;

        if (!profile.TryGetEntry(stateId, out EntityFsmAnimationEntry entry))
        {
            if (logMissingBindings)
                Debug.LogWarning($"[EntityFsmAnimator] No animation entry for FSM state '{stateId}' on '{name}'.", this);
            return;
        }

        float crossFade = profile.ResolveCrossFadeSeconds(in entry);
        CrossFadeToStateName(entry.animatorStateName, crossFade, entry.layer, force);
    }

    void RebuildHashCache()
    {
        _stateHashes.Clear();
        if (profile == null)
            return;

        // Warm common ids used by combat entities.
        TryCacheHash("Idle");
        TryCacheHash("TakeDamage");
        TryCacheHash("Die");
    }

    void TryCacheHash(string stateId)
    {
        if (profile.TryGetEntry(stateId, out EntityFsmAnimationEntry entry)
            && !string.IsNullOrWhiteSpace(entry.animatorStateName))
        {
            _stateHashes[stateId] = Animator.StringToHash(entry.animatorStateName);
        }
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
            return;

        animator.CrossFadeInFixedTime(stateHash, crossFadeSeconds, layer, 0f);
        _lastPlayedHash = stateHash;
        _lastPlayedLayer = layer;
    }
}
