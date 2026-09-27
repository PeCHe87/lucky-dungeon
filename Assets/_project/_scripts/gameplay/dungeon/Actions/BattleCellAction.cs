using UnityEngine;

/// <summary>
/// Battle cell: leaves the dungeon for a battleground scene without completing the cell.
/// Battleground should call <see cref="DungeonRunHost.NotifyExternalCellSucceeded"/> on win.
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Actions/Battle", fileName = "BattleCellAction")]
public sealed class BattleCellAction : DungeonCellAction
{
    [Tooltip("Scene loaded when this cell is resolved (must be in Build Settings).")]
    [SerializeField] string battleSceneName = "battleground";

    [Tooltip("Dungeon scene to return to after battle. Empty uses host default.")]
    [SerializeField] string dungeonSceneName = "dungeon";

    public override void Execute(IDungeonRunContext context)
    {
        DungeonRunHost host = DungeonRunHost.Instance ?? DungeonRunHost.EnsureExists();
        if (host == null)
        {
            Debug.LogError("[Dungeon] BattleCellAction: no DungeonRunHost.");
            return;
        }

        Debug.Log($"[Dungeon] BattleCellAction — loading '{battleSceneName}'.");
        host.BeginExternalCellAndLoadScene(battleSceneName, dungeonSceneName);
    }
}
