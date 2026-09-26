using UnityEngine;

/// <summary>
/// Configurable behavior for a <see cref="DungeonCellData"/> blueprint.
/// Subclass and create assets to add new cell actions without changing the controller.
/// </summary>
public abstract class DungeonCellAction : ScriptableObject
{
    /// <summary>Resolve this cell for the active run. Call <see cref="IDungeonRunContext.CompleteCurrentCell"/> when done.</summary>
    public abstract void Execute(IDungeonRunContext context);
}
