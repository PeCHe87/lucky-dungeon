using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global nested pause via tokens. Call <see cref="Pause"/> / <see cref="Resume"/> with a unique key
/// (e.g. <c>this</c>) from anywhere. Gameplay freezes with <see cref="Time.timeScale"/> while any key is held.
/// </summary>
public static class GamePause
{
    static readonly HashSet<object> s_keys = new HashSet<object>();
    static float s_timeScaleBeforePause = 1f;

    public static bool IsPaused => s_keys.Count > 0;

    public static event Action<bool> PauseChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_keys.Clear();
        s_timeScaleBeforePause = 1f;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        PauseChanged = null;
    }

    public static void Pause(object key)
    {
        if (key == null)
            return;

        bool wasPaused = IsPaused;
        if (!s_keys.Add(key))
            return;

        if (wasPaused)
            return;

        s_timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        PauseChanged?.Invoke(true);
    }

    public static void Resume(object key)
    {
        if (key == null)
            return;

        if (!s_keys.Remove(key))
            return;

        if (IsPaused)
            return;

        Time.timeScale = s_timeScaleBeforePause > 0f ? s_timeScaleBeforePause : 1f;
        AudioListener.pause = false;
        PauseChanged?.Invoke(false);
    }

    public static void ResumeAll()
    {
        if (!IsPaused)
        {
            s_keys.Clear();
            return;
        }

        s_keys.Clear();
        Time.timeScale = s_timeScaleBeforePause > 0f ? s_timeScaleBeforePause : 1f;
        AudioListener.pause = false;
        PauseChanged?.Invoke(false);
    }
}
