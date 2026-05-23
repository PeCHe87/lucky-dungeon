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
    TargetIndicatorView _currentView;

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

        if (_currentView != null)
            _currentView.SetDetected(false);

        _currentTarget = target;
        _currentView = null;

        if (_currentTarget == null)
            return;

        _currentView = GetOrAddView(_currentTarget);
        if (_currentView != null)
            _currentView.SetDetected(true);
    }

    static TargetIndicatorView GetOrAddView(Transform t)
    {
        if (t == null)
            return null;

        if (t.TryGetComponent(out TargetIndicatorView view))
            return view;

        return t.gameObject.AddComponent<TargetIndicatorView>();
    }
}

