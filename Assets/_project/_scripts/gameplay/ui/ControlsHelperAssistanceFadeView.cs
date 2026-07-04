using System.Collections;
using UnityEngine;

/// <summary>
/// Holds a controls helper panel visible, then fades its <see cref="CanvasGroup"/> to alpha 0.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class ControlsHelperAssistanceFadeView : MonoBehaviour
{
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField, Min(0f)] float displayDuration = 3f;
    [SerializeField, Min(0f)] float fadeOutDuration = 0.75f;

    Coroutine _fadeRoutine;

    void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        RestartFadeRoutine();
    }

    void OnDisable()
    {
        StopFadeRoutine();
    }

    void RestartFadeRoutine()
    {
        StopFadeRoutine();
        _fadeRoutine = StartCoroutine(FadeRoutine());
    }

    void StopFadeRoutine()
    {
        if (_fadeRoutine == null)
            return;

        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
    }

    IEnumerator FadeRoutine()
    {
        if (displayDuration > 0f)
            yield return new WaitForSecondsRealtime(displayDuration);

        if (fadeOutDuration <= 0f)
        {
            SetAlpha(0f);
            _fadeRoutine = null;
            yield break;
        }

        float fadeEndTime = Time.unscaledTime + fadeOutDuration;
        while (Time.unscaledTime < fadeEndTime)
        {
            float remaining = fadeEndTime - Time.unscaledTime;
            float t = 1f - (remaining / fadeOutDuration);
            SetAlpha(Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        SetAlpha(0f);
        _fadeRoutine = null;
    }

    void SetAlpha(float alpha01)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = Mathf.Clamp01(alpha01);
        if (canvasGroup.alpha <= 0f)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
