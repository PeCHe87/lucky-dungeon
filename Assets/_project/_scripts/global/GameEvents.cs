using System;

/// <summary>
/// Global gameplay events decoupled from individual prefab references.
/// Subscribe from any scene object (UI, audio, etc.) in OnEnable/OnDisable.
/// </summary>
public static class GameEvents
{
    /// <summary>Fired once after the player death animation finishes.</summary>
    public static event Action PlayerDied;

    /// <summary>Fired when a dungeon run starts with a newly built runtime dungeon.</summary>
    public static event Action<Dungeon> DungeonRunStarted;

    /// <summary>Fired when a cell becomes the active cell (index + definition).</summary>
    public static event Action<int, DungeonCellData> DungeonCellStarted;

    /// <summary>Fired after a cell is marked completed (index + definition).</summary>
    public static event Action<int, DungeonCellData> DungeonCellCompleted;

    /// <summary>Fired when the run reaches the end of the dungeon (or is ended early).</summary>
    public static event Action<Dungeon> DungeonRunFinished;

    internal static void RaisePlayerDied() => PlayerDied?.Invoke();

    internal static void RaiseDungeonRunStarted(Dungeon dungeon) =>
        DungeonRunStarted?.Invoke(dungeon);

    internal static void RaiseDungeonCellStarted(int index, DungeonCellData definition) =>
        DungeonCellStarted?.Invoke(index, definition);

    internal static void RaiseDungeonCellCompleted(int index, DungeonCellData definition) =>
        DungeonCellCompleted?.Invoke(index, definition);

    internal static void RaiseDungeonRunFinished(Dungeon dungeon) =>
        DungeonRunFinished?.Invoke(dungeon);
}
