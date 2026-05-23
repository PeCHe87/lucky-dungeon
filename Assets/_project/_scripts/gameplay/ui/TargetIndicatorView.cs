using UnityEngine;

/// <summary>
/// Minimal view/controller for an entity's "detected" indicator.
/// Reuses existing prefab child `ui/detection` when present.
/// </summary>
public sealed class TargetIndicatorView : MonoBehaviour
{
    [SerializeField] GameObject detectionRoot;
    [SerializeField] bool disableOnAwake = true;

    bool _resolved;

    void Awake()
    {
        ResolveIfNeeded();
        if (disableOnAwake)
            SetDetected(false);
    }

    public void SetDetected(bool detected)
    {
        ResolveIfNeeded();
        if (detectionRoot != null && detectionRoot.activeSelf != detected)
            detectionRoot.SetActive(detected);
    }

    void ResolveIfNeeded()
    {
        if (_resolved)
            return;

        _resolved = true;

        if (detectionRoot != null)
            return;

        Transform t = transform.Find("ui/detection");
        if (t != null)
            detectionRoot = t.gameObject;
    }
}

