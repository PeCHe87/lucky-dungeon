using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Player/State Animation Profile", fileName = "PlayerStateAnimationProfile")]
public sealed class PlayerEntityStateAnimationProfile : ScriptableObject
{
    [SerializeField, Min(0f)] float defaultCrossFadeSeconds = 0.15f;
    [SerializeField] List<PlayerEntityStateAnimationEntry> entries = new List<PlayerEntityStateAnimationEntry>();
    [SerializeField] MeleeAttackAnimationSequence meleeAttackSequence;

    Dictionary<PlayerEntityStateKind, PlayerEntityStateAnimationEntry> _cache;
    bool _cacheBuilt;

    public float DefaultCrossFadeSeconds => defaultCrossFadeSeconds;

    public bool TryGetEntry(PlayerEntityStateKind kind, out PlayerEntityStateAnimationEntry entry)
    {
        EnsureCache();
        return _cache.TryGetValue(kind, out entry);
    }

    public float ResolveCrossFadeSeconds(in PlayerEntityStateAnimationEntry entry)
    {
        return entry.crossFadeSeconds > 0f ? entry.crossFadeSeconds : defaultCrossFadeSeconds;
    }

    public MeleeAttackAnimationSequence MeleeAttackSequence => meleeAttackSequence;

    public bool TryGetMeleeAttackState(int index, out string stateName, out float crossFadeSeconds)
    {
        stateName = null;
        crossFadeSeconds = meleeAttackSequence.crossFadeSeconds > 0f
            ? meleeAttackSequence.crossFadeSeconds
            : defaultCrossFadeSeconds;

        if (!meleeAttackSequence.TryGetState(index, out stateName))
            return false;

        return true;
    }

    void EnsureCache()
    {
        if (_cacheBuilt)
            return;

        _cache = new Dictionary<PlayerEntityStateKind, PlayerEntityStateAnimationEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (_cache.ContainsKey(e.kind))
            {
                Debug.LogError(
                    $"[PlayerEntityStateAnimationProfile] Duplicate entry for {e.kind} on '{name}'.",
                    this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(e.animatorStateName))
            {
                Debug.LogError(
                    $"[PlayerEntityStateAnimationProfile] Empty animator state name for {e.kind} on '{name}'.",
                    this);
                continue;
            }

            _cache.Add(e.kind, e);
        }

        _cacheBuilt = true;
    }

    void OnEnable() => InvalidateCache();

    void OnValidate() => InvalidateCache();

    void InvalidateCache()
    {
        _cacheBuilt = false;
        _cache = null;
    }

#if UNITY_EDITOR
    public const string DefaultAssetPath = "Assets/_project/_animation/PlayerDefaultAnimationProfile.asset";

    public static void EnsureDefaultAssetExists()
    {
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerEntityStateAnimationProfile>(DefaultAssetPath) != null)
            return;

        var profile = CreateInstance<PlayerEntityStateAnimationProfile>();
        profile.defaultCrossFadeSeconds = 0.15f;
        profile.entries = new List<PlayerEntityStateAnimationEntry>
        {
            new PlayerEntityStateAnimationEntry
            {
                kind = PlayerEntityStateKind.Idle,
                animatorStateName = "Idle",
                crossFadeSeconds = 0.15f,
                layer = 0,
            },
            new PlayerEntityStateAnimationEntry
            {
                kind = PlayerEntityStateKind.Walking,
                animatorStateName = "Walking",
                crossFadeSeconds = 0.15f,
                layer = 0,
            },
            new PlayerEntityStateAnimationEntry
            {
                kind = PlayerEntityStateKind.Running,
                animatorStateName = "Running",
                crossFadeSeconds = 0.15f,
                layer = 0,
            },
            new PlayerEntityStateAnimationEntry
            {
                kind = PlayerEntityStateKind.Dashing,
                animatorStateName = "Dashing",
                crossFadeSeconds = 0.1f,
                layer = 0,
            },
            new PlayerEntityStateAnimationEntry
            {
                kind = PlayerEntityStateKind.Attacking,
                animatorStateName = "Attacking1",
                crossFadeSeconds = 0.1f,
                layer = 0,
            },
        };
        profile.meleeAttackSequence = new MeleeAttackAnimationSequence
        {
            animatorStateNames = new[] { "Attacking1", "Attacking2", "Attacking3" },
            crossFadeSeconds = 0.1f,
            comboResetSeconds = 1.2f,
            attackCompletionNormalizedTime = 0.95f,
        };

        if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_project/_animation"))
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/_project"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "_project");
            UnityEditor.AssetDatabase.CreateFolder("Assets/_project", "_animation");
        }

        UnityEditor.AssetDatabase.CreateAsset(profile, DefaultAssetPath);
        UnityEditor.AssetDatabase.SaveAssets();
    }

    [ContextMenu("Validate All PlayerEntityStateKind Values")]
    void EditorValidateCoverage()
    {
        EnsureCache();
        var values = (PlayerEntityStateKind[])Enum.GetValues(typeof(PlayerEntityStateKind));
        for (int i = 0; i < values.Length; i++)
        {
            if (!_cache.ContainsKey(values[i]))
                Debug.LogWarning(
                    $"[PlayerEntityStateAnimationProfile] Missing animation entry for {values[i]} on '{name}'.",
                    this);
        }
    }
#endif
}
