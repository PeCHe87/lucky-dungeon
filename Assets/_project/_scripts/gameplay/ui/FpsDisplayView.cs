using TMPro;
using UnityEngine;

/// <summary>
/// Screen-space FPS readout for on-device playtesting.
/// Resolves child UI references at runtime when unset.
/// </summary>
public sealed class FpsDisplayView : MonoBehaviour
{
    [Header("Visibility")]
    [SerializeField] bool showFps = true;

    [Header("References")]
    [SerializeField] TextMeshProUGUI fpsLabel;

    [Header("Sampling")]
    [SerializeField, Min(0.01f)] float updateInterval = 0.25f;
    [SerializeField, Range(0.01f, 1f)] float smoothing = 0.2f;

    float _smoothedDelta = 1f / 60f;
    float _timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstalledInScene()
    {
        Transform versionTransform = FindVersionTransform();
        if (versionTransform == null || versionTransform.GetComponent<FpsDisplayView>() != null)
            return;

        EnsureInstalled(versionTransform);
    }

    public static FpsDisplayView EnsureInstalled(Transform versionTransform)
    {
        FpsDisplayView view = versionTransform.GetComponent<FpsDisplayView>();
        if (view == null)
            view = versionTransform.gameObject.AddComponent<FpsDisplayView>();

        view.ResolveReferences();
        view.EnsureLabel();
        view.ExpandVersionContainer();
        view.ApplyVisibility();
        return view;
    }

    static Transform FindVersionTransform()
    {
        GameObject uiRoot = GameObject.Find("ui");
        return uiRoot != null ? uiRoot.transform.Find("version") : null;
    }

    void Awake()
    {
        ResolveReferences();
        EnsureLabel();
    }

    void OnEnable()
    {
        ApplyVisibility();
    }

    void OnValidate()
    {
        ApplyVisibility();
    }

    void Update()
    {
        if (!showFps || fpsLabel == null)
            return;

        float delta = Time.unscaledDeltaTime;
        _smoothedDelta = Mathf.Lerp(_smoothedDelta, delta, smoothing);
        _timer += delta;

        if (_timer < updateInterval)
            return;

        _timer = 0f;
        float fps = 1f / _smoothedDelta;
        float ms = _smoothedDelta * 1000f;
        fpsLabel.text = $"{fps:0} FPS ({ms:0.0} ms)";
    }

    void ApplyVisibility()
    {
        ResolveReferences();

        if (fpsLabel != null)
            fpsLabel.gameObject.SetActive(showFps);
    }

    void ResolveReferences()
    {
        if (fpsLabel == null)
        {
            Transform labelTransform = transform.Find("txtFps");
            if (labelTransform != null)
                fpsLabel = labelTransform.GetComponent<TextMeshProUGUI>();
        }
    }

    void EnsureLabel()
    {
        if (fpsLabel != null)
            return;

        Transform existing = transform.Find("txtFps");
        if (existing != null)
        {
            fpsLabel = existing.GetComponent<TextMeshProUGUI>();
            return;
        }

        TextMeshProUGUI referenceLabel = transform.Find("txtVersion")?.GetComponent<TextMeshProUGUI>();

        var labelGo = new GameObject("txtFps", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        labelGo.layer = gameObject.layer;

        var rectTransform = labelGo.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 0f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, -28f);
        rectTransform.sizeDelta = new Vector2(-20f, 22f);

        fpsLabel = labelGo.AddComponent<TextMeshProUGUI>();
        fpsLabel.text = "60 FPS (16.7 ms)";
        fpsLabel.fontSize = 18f;
        fpsLabel.enableAutoSizing = true;
        fpsLabel.fontSizeMin = 12f;
        fpsLabel.fontSizeMax = 18f;
        fpsLabel.alignment = TextAlignmentOptions.TopRight;
        fpsLabel.raycastTarget = false;
        fpsLabel.color = Color.white;

        if (referenceLabel != null)
        {
            fpsLabel.font = referenceLabel.font;
            fpsLabel.fontSharedMaterial = referenceLabel.fontSharedMaterial;
        }
    }

    void ExpandVersionContainer()
    {
        var rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        Vector2 size = rectTransform.sizeDelta;
        if (size.y < 50f)
            rectTransform.sizeDelta = new Vector2(size.x, 50f);
    }
}
