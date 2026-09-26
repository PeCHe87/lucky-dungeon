using UnityEngine;

/// <summary>Stub shop cell: completes immediately until shop UI flow exists.</summary>
[CreateAssetMenu(menuName = "Dungeon/Actions/Shop", fileName = "ShopCellAction")]
public sealed class ShopCellAction : DungeonCellAction
{
    public override void Execute(IDungeonRunContext context)
    {
        if (context == null)
            return;

        Debug.Log("[Dungeon] ShopCellAction stub — completing cell.");
        context.CompleteCurrentCell();
    }
}
