using UnityEngine;

/// <summary>
/// Keeps exactly one detected target indicator active at a time (nearest-only).
/// </summary>
public sealed class LevelTargetIndicatorAssistant : MonoBehaviour
{
    static LevelTargetIndicatorAssistant _instance;
    public static LevelTargetIndicatorAssistant Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            _instance = FindFirstObjectByType<LevelTargetIndicatorAssistant>();
            if (_instance != null)
                return _instance;

            var go = new GameObject(nameof(LevelTargetIndicatorAssistant));
            _instance = go.AddComponent<LevelTargetIndicatorAssistant>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    Transform _currentTarget;
    EntityPlayerTargetIndicator _currentIndicator;

    void Start()
    {
        EnsurePlayerBroadcasterExists();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void EnsurePlayerBroadcasterExists()
    {
        if (FindFirstObjectByType<PlayerTargetDetectionBroadcaster>() != null)
            return;

        NearestTargetQuery query = FindFirstObjectByType<NearestTargetQuery>();
        if (query == null)
            return;

        var broadcaster = query.gameObject.AddComponent<PlayerTargetDetectionBroadcaster>();
        broadcaster.enabled = true;
    }

    public void SetDetectedTarget(Transform target)
    {
        if (_currentTarget == target)
            return;

        if (_currentIndicator != null)
        {
            _currentIndicator.SetDetectedByPlayer(false);
            _currentIndicator = null;
        }

        _currentTarget = target;
        if (target == null)
            return;

        if (!EntityPlayerTargetIndicator.TryGet(target, out var indicator) || !indicator.CanShowWhenPlayerDetects)
            return;

        _currentIndicator = indicator;
        indicator.SetDetectedByPlayer(true);
    }
}
