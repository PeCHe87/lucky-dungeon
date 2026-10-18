using UnityEngine;

/// <summary>
/// Locks this light to a fixed world-space direction so camera yaw does not change lighting/shadows.
/// (Name kept for scene compatibility; behavior is world-fixed, not camera-relative.)
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class CameraRelativeLight : MonoBehaviour
{
    Quaternion _worldRotation;
    bool _cached;

    void Awake()
    {
        CacheWorldRotation();
    }

    void OnEnable()
    {
        CacheWorldRotation();
    }

    void LateUpdate()
    {
        if (!_cached)
            CacheWorldRotation();

        transform.rotation = _worldRotation;
    }

    void CacheWorldRotation()
    {
        _worldRotation = transform.rotation;
        _cached = true;
    }
}
