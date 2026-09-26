using UnityEngine;

/// <summary>Grants run currency to the active session, then completes the cell.</summary>
[CreateAssetMenu(menuName = "Dungeon/Actions/Grant Currency", fileName = "GrantCurrencyCellAction")]
public sealed class GrantCurrencyCellAction : DungeonCellAction
{
    [SerializeField, Min(0)] int amount = 10;

    public int Amount => amount;

    public override void Execute(IDungeonRunContext context)
    {
        if (context?.Session == null)
            return;

        context.Session.AddCurrency(amount);
        Debug.Log($"[Dungeon] GrantCurrency: +{amount} (total {context.Session.RunCurrency})");
        context.CompleteCurrentCell();
    }
}
