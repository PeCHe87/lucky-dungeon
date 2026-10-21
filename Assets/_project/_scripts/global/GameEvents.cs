using System;
using System.Collections.Generic;

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

    /// <summary>Fired when run or permanent-earned currency on the active player run changes.</summary>
    public static event Action PlayerRunWalletChanged;

    /// <summary>Fired when run-scoped player HP (current, base max, or bonus) changes.</summary>
    public static event Action PlayerRunVitalsChanged;

    /// <summary>Fired when run inventory stacks are added or cleared.</summary>
    public static event Action PlayerRunInventoryChanged;

    /// <summary>Fired when at least one searchable loot is in player proximity (true) or none remain (false).</summary>
    public static event Action<bool> LootInteractAvailableChanged;

    /// <summary>Fired after a loot container finishes opening with the items just granted (merged by id).</summary>
    public static event Action<IReadOnlyList<(string itemId, int amount)>> LootObtained;

    internal static void RaisePlayerDied() => PlayerDied?.Invoke();

    internal static void RaiseDungeonRunStarted(Dungeon dungeon) =>
        DungeonRunStarted?.Invoke(dungeon);

    internal static void RaiseDungeonCellStarted(int index, DungeonCellData definition) =>
        DungeonCellStarted?.Invoke(index, definition);

    internal static void RaiseDungeonCellCompleted(int index, DungeonCellData definition) =>
        DungeonCellCompleted?.Invoke(index, definition);

    internal static void RaiseDungeonRunFinished(Dungeon dungeon) =>
        DungeonRunFinished?.Invoke(dungeon);

    internal static void RaisePlayerRunWalletChanged() => PlayerRunWalletChanged?.Invoke();

    internal static void RaisePlayerRunVitalsChanged() => PlayerRunVitalsChanged?.Invoke();

    internal static void RaisePlayerRunInventoryChanged() => PlayerRunInventoryChanged?.Invoke();

    internal static void RaiseLootInteractAvailableChanged(bool available) =>
        LootInteractAvailableChanged?.Invoke(available);

    internal static void RaiseLootObtained(IReadOnlyList<(string itemId, int amount)> entries) =>
        LootObtained?.Invoke(entries);
}
