using UnityEngine;

/// <summary>Stub battle cell: completes immediately until battleground scene flow exists.</summary>
[CreateAssetMenu(menuName = "Dungeon/Actions/Battle", fileName = "BattleCellAction")]
public sealed class BattleCellAction : DungeonCellAction
{
    public override void Execute(IDungeonRunContext context)
    {
        if (context == null)
            return;

        Debug.Log("[Dungeon] BattleCellAction stub — completing cell.");
        context.CompleteCurrentCell();
    }
}
