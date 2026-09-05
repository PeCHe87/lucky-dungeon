using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Game-over overlay shown when <see cref="GameEvents.PlayerDied"/> fires.
/// </summary>
public sealed class GameOverScreen : BaseScreen
{
    [SerializeField, Min(0f)] float showDelaySeconds = 1f;
    [SerializeField] Button retryButton;

    Transform _content;

    public override ScreenId Id => ScreenId.GameOver;

    public override float ShowDelaySeconds => showDelaySeconds;

    void Awake()
    {
        _content = transform.Find("content");

        if (retryButton == null)
            retryButton = GetComponentInChildren<Button>(true);

        if (retryButton != null)
            retryButton.onClick.AddListener(RestartScene);
    }

    void OnDestroy()
    {
        if (retryButton != null)
            retryButton.onClick.RemoveListener(RestartScene);
    }

    protected override void OnShown()
    {
        if (_content != null)
            _content.gameObject.SetActive(true);
    }

    void RestartScene()
    {
        if (retryButton != null)
            retryButton.interactable = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
