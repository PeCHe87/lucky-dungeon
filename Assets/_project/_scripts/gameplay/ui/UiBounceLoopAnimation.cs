using System.Collections;
using UnityEngine;

/// <summary>
/// Loops a scale-up / scale-down bounce on this transform.
/// Useful for drawing attention to HUD controls while they are visible.
/// </summary>
public sealed class UiBounceLoopAnimation : MonoBehaviour
{
    [Tooltip("Peak local scale multiplier relative to the rest scale (1 = no bounce).")]
    [SerializeField, Min(1f)] float peakScale = 1.15f;

    [Tooltip("How fast one bounce (up then down) plays. Higher is faster.")]
    [SerializeField, Min(0.01f)] float speed = 1.5f;

    [Tooltip("Seconds to wait at rest scale after each bounce. 0 = continuous.")]
    [SerializeField, Min(0f)] float pauseBetweenBounces = 0.35f;

    [Tooltip("When enabled, bounce uses unscaled time (keeps moving while the game is paused).")]
    [SerializeField] bool useUnscaledTime;

    Vector3 _restScale = Vector3.one;
    Coroutine _loop;

    void OnEnable()
    {
        _restScale = transform.localScale;
        if (_restScale.sqrMagnitude < 1e-8f)
            _restScale = Vector3.one;

        RestartLoop();
    }

    void OnDisable()
    {
        StopLoop();
        transform.localScale = _restScale;
    }

    public void RestartLoop()
    {
        StopLoop();
        if (!isActiveAndEnabled)
            return;

        _loop = StartCoroutine(BounceLoop());
    }

    void StopLoop()
    {
        if (_loop == null)
            return;

        StopCoroutine(_loop);
        _loop = null;
    }

    IEnumerator BounceLoop()
    {
        while (true)
        {
            float duration = 1f / Mathf.Max(0.01f, speed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 0 → 1 → 0 over one bounce.
                float blend = Mathf.Sin(t * Mathf.PI);
                float scale = Mathf.Lerp(1f, peakScale, blend);
                transform.localScale = _restScale * scale;
                yield return null;
            }

            transform.localScale = _restScale;

            if (pauseBetweenBounces > 0f)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(pauseBetweenBounces);
                else
                    yield return new WaitForSeconds(pauseBetweenBounces);
            }
        }
    }
}
