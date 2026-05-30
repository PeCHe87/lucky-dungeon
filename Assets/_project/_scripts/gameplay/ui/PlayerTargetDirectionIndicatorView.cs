using UnityEngine;

/// <summary>
/// World-space circle + arrow at the player's feet pointing toward the current
/// <see cref="NearestTargetQuery"/> target.
/// </summary>
public sealed class PlayerTargetDirectionIndicatorView : MonoBehaviour
{
    [SerializeField] NearestTargetQuery targetQuery;
    [SerializeField] Transform directionOrigin;
    [SerializeField] GameObject indicatorRoot;
    [SerializeField] RectTransform arrowPivot;
    [SerializeField] float arrowAngleOffset = 180f;
    [SerializeField, Min(0f)] float lossGraceSeconds = 0.2f;

    float _missingTime;
    bool _resolved;

    void Reset()
    {
        targetQuery = GetComponent<NearestTargetQuery>();
        directionOrigin = transform;
    }

    void Awake()
    {
        if (targetQuery == null)
            targetQuery = GetComponent<NearestTargetQuery>();
        if (directionOrigin == null)
            directionOrigin = transform;

        ResolveIfNeeded();
        SetIndicatorVisible(false);
    }

    void OnDisable()
    {
        _missingTime = 0f;
        SetIndicatorVisible(false);
    }

    void LateUpdate()
    {
        if (targetQuery == null)
            return;

        ResolveIfNeeded();

        if (targetQuery.TryGetNearestTransform(out Transform target))
        {
            _missingTime = 0f;
            SetIndicatorVisible(true);
            UpdateArrowDirection(target);
            return;
        }

        if (!IsIndicatorVisible())
            return;

        if (lossGraceSeconds <= 0f)
        {
            SetIndicatorVisible(false);
            return;
        }

        _missingTime += Time.deltaTime;
        if (_missingTime >= lossGraceSeconds)
        {
            _missingTime = 0f;
            SetIndicatorVisible(false);
        }
    }

    void UpdateArrowDirection(Transform target)
    {
        if (arrowPivot == null || directionOrigin == null || target == null)
            return;

        Vector3 origin = directionOrigin.position;
        Vector3 to = target.position - origin;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return;

        float yawDeg = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        arrowPivot.localRotation = Quaternion.Euler(0f, 0f, -yawDeg + arrowAngleOffset);
    }

    void SetIndicatorVisible(bool visible)
    {
        if (indicatorRoot != null && indicatorRoot.activeSelf != visible)
            indicatorRoot.SetActive(visible);
    }

    bool IsIndicatorVisible() =>
        indicatorRoot != null && indicatorRoot.activeSelf;

    void ResolveIfNeeded()
    {
        if (_resolved)
            return;

        _resolved = true;

        if (indicatorRoot == null)
        {
            Transform canvas = transform.Find("Canvas");
            if (canvas != null)
                indicatorRoot = canvas.gameObject;
        }

        if (arrowPivot == null && indicatorRoot != null)
        {
            Transform pivot = indicatorRoot.transform.Find("arrowPivot");
            if (pivot != null)
                arrowPivot = pivot as RectTransform;
            else
            {
                Transform arrow = indicatorRoot.transform.Find("forwardDirection");
                if (arrow != null)
                    arrowPivot = arrow as RectTransform;
            }
        }
    }
}
