using System.Collections;
using UnityEngine;

/// <summary>
/// Briefly shows a ground ring at <see cref="ringAnchor"/> matching the equipped weapon's
/// omnidirectional target detection radius when <see cref="WeaponHolder"/> equips a weapon.
/// </summary>
public sealed class WeaponDetectionRadiusRingView : MonoBehaviour
{
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] Transform ringAnchor;
    [SerializeField] NearestTargetQuery nearestTargetQuery;
    [SerializeField, Min(0f)] float displayDuration = 2.5f;
    [SerializeField, Min(0.01f)] float fadeOutDuration = 0.75f;
    [SerializeField] float heightOffset = 0.05f;
    [SerializeField, Min(0.001f)] float lineWidth = 0.08f;
    [SerializeField, Min(8)] int segmentCount = 64;
    [SerializeField] Color ringColor = new Color(1f, 0.55f, 0.15f, 0.85f);

    Transform _ringRoot;
    LineRenderer _lineRenderer;
    float _currentRadius;
    Coroutine _pulseRoutine;

    void Awake()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (nearestTargetQuery == null)
            nearestTargetQuery = GetComponent<NearestTargetQuery>();
        if (ringAnchor == null && nearestTargetQuery != null)
            ringAnchor = nearestTargetQuery.QueryOriginTransform;
        if (ringAnchor == null)
            ringAnchor = transform;

        EnsureRingObjects();
    }

    void OnEnable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += OnEquippedWeaponChanged;

        if (weaponHolder != null && weaponHolder.Current != null)
            OnEquippedWeaponChanged();
    }

    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= OnEquippedWeaponChanged;

        StopPulseRoutine();
        SetVisible(false);
    }

    void OnEquippedWeaponChanged()
    {
        if (!TryResolveOmnidirectionalRadius(out float radius))
        {
            StopPulseRoutine();
            SetVisible(false);
            return;
        }

        _currentRadius = radius;
        UpdateCircleGeometry();
        RestartPulseRoutine();
    }

    bool TryResolveOmnidirectionalRadius(out float radius)
    {
        radius = 0f;
        if (weaponHolder != null
            && weaponHolder.Current is MonoBehaviour weaponBehaviour
            && weaponBehaviour is IWeaponTargetDetection detection
            && detection.OmnidirectionalDetectionRadius > 0f)
        {
            radius = detection.OmnidirectionalDetectionRadius;
            return true;
        }

        if (nearestTargetQuery != null && nearestTargetQuery.OmnidirectionalDetectionRadius > 0f)
        {
            radius = nearestTargetQuery.OmnidirectionalDetectionRadius;
            return true;
        }

        return false;
    }

    void RestartPulseRoutine()
    {
        StopPulseRoutine();
        _pulseRoutine = StartCoroutine(PulseRoutine());
    }

    void StopPulseRoutine()
    {
        if (_pulseRoutine == null)
            return;

        StopCoroutine(_pulseRoutine);
        _pulseRoutine = null;
    }

    IEnumerator PulseRoutine()
    {
        SetAlpha(1f);
        SetVisible(true);

        if (displayDuration > 0f)
            yield return new WaitForSeconds(displayDuration);

        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = fadeOutDuration > 0f ? elapsed / fadeOutDuration : 1f;
            SetAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        SetVisible(false);
        _pulseRoutine = null;
    }

    void EnsureRingObjects()
    {
        if (_lineRenderer != null)
            return;

        Transform parent = ringAnchor != null ? ringAnchor : transform;
        _ringRoot = new GameObject("WeaponDetectionRadiusRing").transform;
        _ringRoot.SetParent(parent, false);
        _ringRoot.localPosition = new Vector3(0f, heightOffset, 0f);

        var go = new GameObject("WeaponDetectionRadiusRingLine");
        go.transform.SetParent(_ringRoot, false);
        _lineRenderer = go.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.loop = true;
        _lineRenderer.widthMultiplier = lineWidth;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _lineRenderer.startColor = ringColor;
        _lineRenderer.endColor = ringColor;
        _lineRenderer.positionCount = segmentCount;

        SetVisible(false);
    }

    void UpdateCircleGeometry()
    {
        if (_lineRenderer == null)
            return;

        _lineRenderer.widthMultiplier = lineWidth;
        _lineRenderer.positionCount = segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = (float)i / segmentCount * (Mathf.PI * 2f);
            float x = Mathf.Sin(angle) * _currentRadius;
            float z = Mathf.Cos(angle) * _currentRadius;
            _lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    void SetAlpha(float alpha01)
    {
        if (_lineRenderer == null)
            return;

        Color c = ringColor;
        c.a = ringColor.a * Mathf.Clamp01(alpha01);
        _lineRenderer.startColor = c;
        _lineRenderer.endColor = c;
    }

    void SetVisible(bool visible)
    {
        if (_lineRenderer != null)
            _lineRenderer.enabled = visible;
    }

    void LateUpdate()
    {
        if (_ringRoot == null)
            return;

        _ringRoot.localPosition = new Vector3(0f, heightOffset, 0f);
    }
}
