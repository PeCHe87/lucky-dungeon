using UnityEngine;

/// <summary>
/// Polls the player's NearestTargetQuery and informs LevelTargetIndicatorAssistant when it changes.
/// </summary>
public sealed class PlayerTargetDetectionBroadcaster : MonoBehaviour
{
    [SerializeField] NearestTargetQuery nearestTargetQuery;
    [SerializeField] LevelTargetIndicatorAssistant assistant;
    [SerializeField, Min(0f)] float pollIntervalSeconds = 0.05f;
    [SerializeField, Min(0f)] float lossGraceSeconds = 0.20f;

    Transform _lastTarget;
    float _timer;
    float _missingTime;

    void Reset()
    {
        nearestTargetQuery = GetComponent<NearestTargetQuery>();
    }

    void Awake()
    {
        if (nearestTargetQuery == null)
            nearestTargetQuery = GetComponent<NearestTargetQuery>();
        if (assistant == null)
            assistant = LevelTargetIndicatorAssistant.Instance;
    }

    void OnEnable()
    {
        _timer = 0f;
        _missingTime = 0f;
        _lastTarget = null;
        PublishCurrent();
    }

    void Update()
    {
        if (pollIntervalSeconds <= 0f)
        {
            PublishCurrent();
            return;
        }

        _timer += Time.deltaTime;
        if (_timer < pollIntervalSeconds)
            return;
        _timer = 0f;

        PublishCurrent();
    }

    void OnDisable()
    {
        if (assistant != null)
            assistant.SetDetectedTarget(null);
        _lastTarget = null;
        _missingTime = 0f;
    }

    void PublishCurrent()
    {
        if (assistant == null)
            assistant = LevelTargetIndicatorAssistant.Instance;
        if (assistant == null || nearestTargetQuery == null)
            return;

        bool found = nearestTargetQuery.TryGetNearestTransform(out Transform nearest);
        if (found)
        {
            _missingTime = 0f;
            PublishIfChanged(nearest);
            return;
        }

        if (_lastTarget == null)
            return;

        if (lossGraceSeconds <= 0f)
        {
            PublishIfChanged(null);
            return;
        }

        float elapsed = pollIntervalSeconds > 0f ? pollIntervalSeconds : Time.deltaTime;
        _missingTime += elapsed;
        if (_missingTime < lossGraceSeconds)
            return;

        _missingTime = 0f;
        PublishIfChanged(null);
    }

    void PublishIfChanged(Transform target)
    {
        if (_lastTarget == target)
            return;

        _lastTarget = target;
        assistant.SetDetectedTarget(target);
    }
}

