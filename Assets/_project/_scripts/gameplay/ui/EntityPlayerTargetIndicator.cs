using UnityEngine;

/// <summary>
/// Opt-in ground ring shown while the player has this entity as their detected target.
/// </summary>
public sealed class EntityPlayerTargetIndicator : MonoBehaviour
{
    [SerializeField] bool showWhenPlayerDetects = true;
    [SerializeField] Transform ringAnchor;
    [SerializeField, Min(0.01f)] float ringRadius = 0.55f;
    [SerializeField] float heightOffset = 0.05f;
    [SerializeField, Min(0.001f)] float lineWidth = 0.08f;
    [SerializeField, Min(8)] int segmentCount = 64;
    [SerializeField] Color ringColor = new Color(0.2f, 0.95f, 1f, 0.9f);

    Transform _ringRoot;
    LineRenderer _lineRenderer;
    bool _detectedByPlayer;

    public bool CanShowWhenPlayerDetects => showWhenPlayerDetects;

    void Awake()
    {
        if (ringAnchor == null)
            ringAnchor = transform;

        EnsureRingObjects();
        SetDetectedByPlayer(false);
    }

    void OnDisable()
    {
        SetDetectedByPlayer(false);
    }

    public void SetDetectedByPlayer(bool detected)
    {
        _detectedByPlayer = detected && showWhenPlayerDetects;
        SetVisible(_detectedByPlayer);
    }

    public static bool TryGet(Transform detectedRoot, out EntityPlayerTargetIndicator indicator)
    {
        indicator = null;
        if (detectedRoot == null)
            return false;

        indicator = detectedRoot.GetComponentInParent<EntityPlayerTargetIndicator>();
        return indicator != null;
    }

    void EnsureRingObjects()
    {
        if (_lineRenderer != null)
            return;

        Transform parent = ringAnchor != null ? ringAnchor : transform;
        _ringRoot = new GameObject("PlayerTargetRing").transform;
        _ringRoot.SetParent(parent, false);
        _ringRoot.localPosition = new Vector3(0f, heightOffset, 0f);

        var go = new GameObject("PlayerTargetRingLine");
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

        UpdateCircleGeometry();
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
            float x = Mathf.Sin(angle) * ringRadius;
            float z = Mathf.Cos(angle) * ringRadius;
            _lineRenderer.SetPosition(i, new Vector3(x, 0f, z));
        }
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
