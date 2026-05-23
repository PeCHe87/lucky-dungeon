using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Screen-space float + fade for one pooled damage number. Returns to the presenter pool when finished.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class FloatingDamageTextInstance : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI label;
    [SerializeField] CanvasGroup canvasGroup;

    RectTransform _rect;
    Coroutine _routine;
    System.Action<FloatingDamageTextInstance> _release;

    void Awake()
    {
        EnsureInitialized();
    }

    void EnsureInitialized()
    {
        if (_rect == null)
            _rect = GetComponent<RectTransform>();
        if (label == null)
            label = GetComponent<TextMeshProUGUI>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>Begins motion in parent canvas local space (typically pixels).</summary>
    public void Play(
        System.Action<FloatingDamageTextInstance> release,
        Vector2 startAnchoredPosition,
        string text,
        Color color,
        float fontSize,
        float lifetimeSeconds,
        Vector2 driftPixelsPerSecond)
    {
        EnsureInitialized();
        _release = release;
        if (label != null)
        {
            label.text = text;
            label.color = color;
            label.fontSize = fontSize;
            label.enableAutoSizing = false;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        _rect.anchoredPosition = startAnchoredPosition;
        gameObject.SetActive(true);

        if (_routine != null)
            StopCoroutine(_routine);
        _routine = StartCoroutine(Animate(lifetimeSeconds, driftPixelsPerSecond));
    }

    public void StopAndRelease()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        gameObject.SetActive(false);
        _release?.Invoke(this);
        _release = null;
    }

    IEnumerator Animate(float lifetimeSeconds, Vector2 driftPixelsPerSecond)
    {
        float t = 0f;
        while (t < lifetimeSeconds)
        {
            t += Time.unscaledDeltaTime;
            float u = lifetimeSeconds > 0f ? t / lifetimeSeconds : 1f;
            _rect.anchoredPosition += driftPixelsPerSecond * Time.unscaledDeltaTime;
            if (canvasGroup != null)
                canvasGroup.alpha = 1f - Mathf.Clamp01(u);
            yield return null;
        }

        _routine = null;
        StopAndRelease();
    }
}
