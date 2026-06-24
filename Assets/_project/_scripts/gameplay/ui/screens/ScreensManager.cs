using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registers child <see cref="BaseScreen"/> instances, hides them on start,
/// and routes global gameplay events to the appropriate screen.
/// </summary>
public sealed class ScreensManager : MonoBehaviour
{
    readonly Dictionary<ScreenId, BaseScreen> _screens = new Dictionary<ScreenId, BaseScreen>();
    BaseScreen _visibleScreen;
    Coroutine _showDelayCoroutine;

    void Awake()
    {
        RegisterScreens();
        HideAll();
    }

    void OnEnable()
    {
        GameEvents.PlayerDied += OnPlayerDied;
    }

    void OnDisable()
    {
        GameEvents.PlayerDied -= OnPlayerDied;
        CancelShowDelay();
    }

    void OnPlayerDied()
    {
        ShowScreen(ScreenId.GameOver);
    }

    public void ShowScreen(ScreenId id)
    {
        if (id == ScreenId.None)
            return;

        if (!_screens.TryGetValue(id, out BaseScreen screen))
        {
            Debug.LogWarning($"[ScreensManager] No screen registered for {id}.", this);
            return;
        }

        CancelShowDelay();

        float delay = screen.ShowDelaySeconds;
        if (delay > 0f)
            _showDelayCoroutine = StartCoroutine(ShowAfterDelay(screen, delay));
        else
            ShowScreenImmediate(screen);
    }

    public void HideScreen(ScreenId id)
    {
        CancelShowDelay();

        if (!_screens.TryGetValue(id, out BaseScreen screen))
            return;

        screen.Hide();

        if (_visibleScreen == screen)
            _visibleScreen = null;
    }

    public void HideAll()
    {
        CancelShowDelay();

        foreach (BaseScreen screen in _screens.Values)
            screen.Hide();

        _visibleScreen = null;
    }

    void ShowScreenImmediate(BaseScreen screen)
    {
        if (_visibleScreen != null && _visibleScreen != screen)
            _visibleScreen.Hide();

        screen.Show();
        _visibleScreen = screen;
    }

    IEnumerator ShowAfterDelay(BaseScreen screen, float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        _showDelayCoroutine = null;
        ShowScreenImmediate(screen);
    }

    void CancelShowDelay()
    {
        if (_showDelayCoroutine == null)
            return;

        StopCoroutine(_showDelayCoroutine);
        _showDelayCoroutine = null;
    }

    void RegisterScreens()
    {
        _screens.Clear();

        BaseScreen[] screens = GetComponentsInChildren<BaseScreen>(true);
        for (int i = 0; i < screens.Length; i++)
        {
            BaseScreen screen = screens[i];
            if (screen.Id == ScreenId.None)
            {
                Debug.LogWarning($"[ScreensManager] Screen on '{screen.name}' has Id None; skipping.", screen);
                continue;
            }

            if (_screens.ContainsKey(screen.Id))
            {
                Debug.LogWarning(
                    $"[ScreensManager] Duplicate screen Id {screen.Id} on '{screen.name}'; skipping.",
                    screen);
                continue;
            }

            _screens.Add(screen.Id, screen);
        }
    }
}
