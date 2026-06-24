using UnityEngine;

/// <summary>
/// Game-over overlay shown when <see cref="GameEvents.PlayerDied"/> fires.
/// </summary>
public sealed class GameOverScreen : BaseScreen
{
    [SerializeField, Min(0f)] float showDelaySeconds = 1f;

    public override ScreenId Id => ScreenId.GameOver;

    public override float ShowDelaySeconds => showDelaySeconds;
}
