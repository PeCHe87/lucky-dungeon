using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds ephemeral runtime <see cref="Dungeon"/> instances from cell definition blueprints.</summary>
public static class DungeonBuilder
{
    /// <summary>
    /// Assembles a linear dungeon from the given cell definitions (order = run path).
    /// The first cell starts <see cref="DungeonCellStatus.Active"/>; the rest are Locked.
    /// </summary>
    public static Dungeon BuildDungeon(IReadOnlyList<DungeonCellData> cellDefinitions)
    {
        if (cellDefinitions == null || cellDefinitions.Count == 0)
        {
            Debug.LogWarning("[DungeonBuilder] BuildDungeon called with no cell definitions.");
            return new Dungeon(System.Array.Empty<DungeonCell>());
        }

        var cells = new DungeonCell[cellDefinitions.Count];
        for (int i = 0; i < cellDefinitions.Count; i++)
        {
            DungeonCellData definition = cellDefinitions[i];
            if (definition == null)
            {
                Debug.LogWarning($"[DungeonBuilder] Null cell definition at index {i}.");
            }

            DungeonCellStatus status = i == 0
                ? DungeonCellStatus.Active
                : DungeonCellStatus.Locked;
            cells[i] = new DungeonCell(definition, status);
        }

        return new Dungeon(cells);
    }
}
