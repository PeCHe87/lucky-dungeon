/// <summary>One cell instance inside a runtime <see cref="Dungeon"/> for the current run.</summary>
public struct DungeonCell
{
    public DungeonCellData Definition;
    public DungeonCellStatus Status;

    public DungeonCell(DungeonCellData definition, DungeonCellStatus status)
    {
        Definition = definition;
        Status = status;
    }
}
