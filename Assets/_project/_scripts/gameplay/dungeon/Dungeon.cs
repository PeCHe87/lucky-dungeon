using System;
using System.Collections.Generic;

/// <summary>
/// Ephemeral linear dungeon for one run. Built by <see cref="DungeonBuilder"/> and discarded when the run ends.
/// </summary>
public sealed class Dungeon
{
    readonly DungeonCell[] _cells;

    public IReadOnlyList<DungeonCell> Cells => _cells;
    public int CurrentIndex { get; private set; }
    public int CellCount => _cells.Length;
    public bool IsComplete => CellCount == 0 || CurrentIndex >= CellCount;

    public DungeonCell CurrentCell =>
        IsComplete ? default : _cells[CurrentIndex];

    public Dungeon(DungeonCell[] cells)
    {
        _cells = cells ?? Array.Empty<DungeonCell>();
        CurrentIndex = 0;
    }

    /// <summary>Marks the active cell completed and unlocks the next (if any).</summary>
    public bool CompleteCurrentCell()
    {
        if (IsComplete)
            return false;

        DungeonCell current = _cells[CurrentIndex];
        if (current.Status != DungeonCellStatus.Active)
            return false;

        current.Status = DungeonCellStatus.Completed;
        _cells[CurrentIndex] = current;

        int nextIndex = CurrentIndex + 1;
        if (nextIndex < _cells.Length)
        {
            DungeonCell next = _cells[nextIndex];
            next.Status = DungeonCellStatus.Active;
            _cells[nextIndex] = next;
            CurrentIndex = nextIndex;
            return true;
        }

        CurrentIndex = _cells.Length;
        return true;
    }

    public DungeonCell GetCell(int index)
    {
        if (index < 0 || index >= _cells.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return _cells[index];
    }
}
