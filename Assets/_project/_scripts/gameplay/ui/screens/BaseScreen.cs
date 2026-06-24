using UnityEngine;

public enum ScreenId
{
    None = 0,
    GameOver = 1,
}

/// <summary>
/// Base class for full-screen UI overlays managed by <see cref="ScreensManager"/>.
/// </summary>
public abstract class BaseScreen : MonoBehaviour
{
    public abstract ScreenId Id { get; }

    public virtual float ShowDelaySeconds => 0f;

    public bool IsVisible => gameObject.activeSelf;

    public virtual void Show()
    {
        gameObject.SetActive(true);
        OnShown();
    }

    public virtual void Hide()
    {
        OnHidden();
        gameObject.SetActive(false);
    }

    protected virtual void OnShown() { }

    protected virtual void OnHidden() { }
}
