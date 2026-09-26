/// <summary>
/// Facade dungeon cell actions use so they stay free of scene MonoBehaviours.
/// </summary>
public interface IDungeonRunContext
{
    DungeonRunSession Session { get; }
    DungeonCellData CurrentCellDefinition { get; }
    void CompleteCurrentCell();
}
