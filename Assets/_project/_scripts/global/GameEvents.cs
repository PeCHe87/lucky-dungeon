using System;

/// <summary>
/// Global gameplay events decoupled from individual prefab references.
/// Subscribe from any scene object (UI, audio, etc.) in OnEnable/OnDisable.
/// </summary>
public static class GameEvents
{
    /// <summary>Fired once after the player death animation finishes.</summary>
    public static event Action PlayerDied;

    internal static void RaisePlayerDied() => PlayerDied?.Invoke();
}
