using UnityEngine;

/// <summary>World-space circular cooldown ring around the player (LineRenderer, not HUD).</summary>
public sealed class DashCooldownRingView : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float radius = 0.85f;
    [SerializeField, Min(0.001f)] float lineWidth = 0.06f;
    [SerializeField, Min(8)] int segmentCount = 48;
    [SerializeField] float heightOffset = 0.05f;
    [SerializeField] Color ringColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] Color backgroundRingColor = new Color(1f, 1f, 1f, 0.15f);

    LineRenderer _lineRenderer;
    LineRenderer _backgroundLine;
    Transform _ringRoot;
    float _fill01 = 1f;

    void Awake()
    {
        EnsureRingObjects();
    }

    void EnsureRingObjects()
    {
        if (_ringRoot != null)
            return;

        _ringRoot = new GameObject("DashCooldownRing").transform;
        _ringRoot.SetParent(transform, false);
        _ringRoot.localPosition = new Vector3(0f, heightOffset, 0f);

        _backgroundLine = CreateLineRenderer("DashCooldownRingBg", backgroundRingColor);
        _lineRenderer = CreateLineRenderer("DashCooldownRingFill", ringColor);
        UpdateRingGeometry();
        SetVisible(false);
    }

    LineRenderer CreateLineRenderer(string objectName, Color color)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(_ringRoot, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.widthMultiplier = lineWidth;
        lr.positionCount = segmentCount + 1;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        return lr;
    }

    public void SetFill01(float fill01)
    {
        _fill01 = Mathf.Clamp01(fill01);
        if (_ringRoot == null)
            EnsureRingObjects();

        bool show = _fill01 < 1f - 1e-4f;
        SetVisible(show);
        if (!show)
            return;

        UpdateRingGeometry();
    }

    void SetVisible(bool visible)
    {
        if (_lineRenderer != null)
            _lineRenderer.enabled = visible;
        if (_backgroundLine != null)
            _backgroundLine.enabled = visible;
    }

    void UpdateRingGeometry()
    {
        if (_backgroundLine != null)
            SetArcPositions(_backgroundLine, 0f, 1f);

        if (_lineRenderer != null)
            SetArcPositions(_lineRenderer, 0f, _fill01);
    }

    void SetArcPositions(LineRenderer lr, float startFill, float endFill)
    {
        int count = segmentCount + 1;
        lr.positionCount = count;
        float startAngle = startFill * Mathf.PI * 2f;
        float endAngle = endFill * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0f : (float)i / (count - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            float x = Mathf.Sin(angle) * radius;
            float z = Mathf.Cos(angle) * radius;
            lr.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    void LateUpdate()
    {
        if (_ringRoot == null)
            return;
        _ringRoot.localPosition = new Vector3(0f, heightOffset, 0f);
    }
}
