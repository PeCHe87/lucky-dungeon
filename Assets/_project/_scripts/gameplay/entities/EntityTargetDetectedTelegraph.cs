using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Plays a detection animation and world-space indicator when an entity notices a target,
/// before nav chase behaviors transition to chasing.
/// </summary>
[DefaultExecutionOrder(111)]
public sealed class EntityTargetDetectedTelegraph : MonoBehaviour
{
    public const float FallbackDetectionDuration = 0.5f;

    [Header("First Detection")]
    [FormerlySerializedAs("detectionDuration")]
    [SerializeField, Min(0f)] float firstDetectionDuration = 1.25f;
    [Tooltip("How long the gotcha indicator stays visible on first acquisition. 0 uses First Detection Duration.")]
    [FormerlySerializedAs("indicatorDisplayDuration")]
    [SerializeField, Min(0f)] float firstIndicatorDisplayDuration = 0.8f;

    [Header("Repeat Detection")]
    [Tooltip("Pre-chase pause after aggro was lost once. 0 uses First Detection Duration.")]
    [SerializeField, Min(0f)] float repeatDetectionDuration = 0f;
    [Tooltip("Gotcha indicator duration on repeat acquisition. 0 uses Repeat Detection Duration.")]
    [SerializeField, Min(0f)] float repeatIndicatorDisplayDuration = 0f;

    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] string detectionStateName = "TargetDetected";
    [SerializeField] int animatorLayer = 0;
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.12f;
    [SerializeField] bool faceTargetDuringDetection = true;
    [SerializeField] EntityAttackController attackController;

    [Header("Indicator")]
    [SerializeField] GameObject indicatorRoot;
    [SerializeField] Transform indicatorAnchor;
    [SerializeField] Vector3 indicatorLocalOffset = new Vector3(0f, 2.2f, 0f);
    [SerializeField] bool createIndicatorIfMissing = true;
    [SerializeField, Min(1f)] float indicatorFontSize = 48f;
    [SerializeField] Color indicatorColor = new Color(1f, 0.92f, 0.2f, 1f);
    [SerializeField, Min(0.001f)] float indicatorCanvasWorldScale = 0.012f;

    float _remaining;
    float _indicatorRemaining;
    int _detectionStateHash;
    bool _resolvedIndicator;
    bool _useRepeatTimings;

    public bool IsActive => _remaining > 0f;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (attackController == null)
            attackController = GetComponent<EntityAttackController>();
        if (animator != null)
            animator.applyRootMotion = false;

        if (!string.IsNullOrWhiteSpace(detectionStateName))
            _detectionStateHash = Animator.StringToHash(detectionStateName);

        ResolveIndicatorIfNeeded();
        SetIndicatorVisible(false);
    }

    void OnDisable()
    {
        Cancel();
        _useRepeatTimings = false;
    }

    public void Begin(Transform target)
    {
        float detectionDuration = ResolveDetectionDuration();
        _remaining = detectionDuration;
        _indicatorRemaining = ResolveIndicatorDuration(detectionDuration);

        if (faceTargetDuringDetection && attackController != null && target != null)
            attackController.FaceTarget(target);

        PlayDetectionAnimation();
        SetIndicatorVisible(true);
    }

    public void NotifyAggroLost() => _useRepeatTimings = true;

    public void Cancel() => FinishPhase();

    public void FinishPhase()
    {
        _remaining = 0f;
        _indicatorRemaining = 0f;
        SetIndicatorVisible(false);
    }

    public bool Tick(float deltaTime)
    {
        if (_indicatorRemaining > 0f)
        {
            _indicatorRemaining -= deltaTime;
            if (_indicatorRemaining <= 0f)
                SetIndicatorVisible(false);
        }

        if (_remaining <= 0f)
            return true;

        _remaining -= deltaTime;
        return _remaining <= 0f;
    }

    float ResolveDetectionDuration()
    {
        if (_useRepeatTimings)
        {
            if (repeatDetectionDuration > 0f)
                return repeatDetectionDuration;
            return ResolveFirstDetectionDuration();
        }

        return ResolveFirstDetectionDuration();
    }

    float ResolveFirstDetectionDuration()
        => firstDetectionDuration > 0f ? firstDetectionDuration : FallbackDetectionDuration;

    float ResolveIndicatorDuration(float detectionDuration)
    {
        float configured = _useRepeatTimings
            ? repeatIndicatorDisplayDuration
            : firstIndicatorDisplayDuration;

        return configured > 0f ? configured : detectionDuration;
    }

    void PlayDetectionAnimation()
    {
        if (animator == null || _detectionStateHash == 0)
            return;

        animator.CrossFadeInFixedTime(_detectionStateHash, crossFadeSeconds, animatorLayer, 0f);
    }

    void SetIndicatorVisible(bool visible)
    {
        ResolveIndicatorIfNeeded();
        if (indicatorRoot != null && indicatorRoot.activeSelf != visible)
            indicatorRoot.SetActive(visible);
    }

    void ResolveIndicatorIfNeeded()
    {
        if (_resolvedIndicator)
            return;

        _resolvedIndicator = true;

        if (indicatorRoot != null)
            return;

        Transform anchor = indicatorAnchor != null ? indicatorAnchor : transform;
        Transform found = anchor.Find("ui/gotcha");
        if (found != null)
        {
            indicatorRoot = found.gameObject;
            return;
        }

        found = anchor.Find("ui/detection");
        if (found != null)
        {
            indicatorRoot = found.gameObject;
            return;
        }

        if (!createIndicatorIfMissing)
            return;

        BuildIndicatorHierarchy(anchor);
    }

    void BuildIndicatorHierarchy(Transform anchor)
    {
        var uiGo = new GameObject("ui");
        uiGo.transform.SetParent(anchor, false);
        uiGo.transform.localPosition = indicatorLocalOffset;
        uiGo.transform.localRotation = Quaternion.identity;
        uiGo.transform.localScale = Vector3.one;

        var gotchaGo = new GameObject("gotcha");
        gotchaGo.transform.SetParent(uiGo.transform, false);
        gotchaGo.transform.localPosition = Vector3.zero;
        gotchaGo.transform.localRotation = Quaternion.identity;
        gotchaGo.transform.localScale = Vector3.one;

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(gotchaGo.transform, false);
        canvasGo.transform.localPosition = Vector3.zero;
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * indicatorCanvasWorldScale;

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 110;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(120f, 120f);

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(120f, 120f);

        var label = textGo.AddComponent<TextMeshProUGUI>();
        label.text = "!";
        label.fontSize = indicatorFontSize;
        label.color = indicatorColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        canvasGo.AddComponent<BillboardFacingCamera>();
        indicatorRoot = gotchaGo;
    }
}
