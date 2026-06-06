using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "FSM/Entity Animation Profile", fileName = "EntityFsmAnimationProfile")]
public sealed class EntityFsmAnimationProfile : ScriptableObject
{
    [SerializeField, Min(0f)] float defaultCrossFadeSeconds = 0.15f;
    [SerializeField] List<EntityFsmAnimationEntry> entries = new List<EntityFsmAnimationEntry>();

    Dictionary<string, EntityFsmAnimationEntry> _cache;
    bool _cacheBuilt;

    public float DefaultCrossFadeSeconds => defaultCrossFadeSeconds;

    public bool TryGetEntry(string stateId, out EntityFsmAnimationEntry entry)
    {
        EnsureCache();
        if (string.IsNullOrWhiteSpace(stateId))
        {
            entry = default;
            return false;
        }

        return _cache.TryGetValue(stateId, out entry);
    }

    public float ResolveCrossFadeSeconds(in EntityFsmAnimationEntry entry)
    {
        return entry.crossFadeSeconds > 0f ? entry.crossFadeSeconds : defaultCrossFadeSeconds;
    }

    void EnsureCache()
    {
        if (_cacheBuilt)
            return;

        _cache = new Dictionary<string, EntityFsmAnimationEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (string.IsNullOrWhiteSpace(e.stateId))
                continue;

            if (_cache.ContainsKey(e.stateId))
            {
                Debug.LogError($"[EntityFsmAnimationProfile] Duplicate entry for '{e.stateId}' on '{name}'.", this);
                continue;
            }

            if (string.IsNullOrWhiteSpace(e.animatorStateName))
            {
                Debug.LogError($"[EntityFsmAnimationProfile] Empty animator state for '{e.stateId}' on '{name}'.", this);
                continue;
            }

            _cache.Add(e.stateId, e);
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
    public const string DefaultAssetPath = "Assets/_project/_animation/EntityCombatAnimationProfile.asset";

    public static void EnsureDefaultAssetExists()
    {
        if (UnityEditor.AssetDatabase.LoadAssetAtPath<EntityFsmAnimationProfile>(DefaultAssetPath) != null)
            return;

        var profile = CreateInstance<EntityFsmAnimationProfile>();
        profile.defaultCrossFadeSeconds = 0.15f;
        profile.entries = new List<EntityFsmAnimationEntry>
        {
            new EntityFsmAnimationEntry
            {
                stateId = "Idle",
                animatorStateName = "Idle",
                crossFadeSeconds = 0.15f,
                layer = 0,
            },
            new EntityFsmAnimationEntry
            {
                stateId = "TakeDamage",
                animatorStateName = "Hit",
                crossFadeSeconds = 0.08f,
                layer = 0,
            },
            new EntityFsmAnimationEntry
            {
                stateId = "Die",
                animatorStateName = "Die",
                crossFadeSeconds = 0.1f,
                layer = 0,
            },
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
#endif
}
