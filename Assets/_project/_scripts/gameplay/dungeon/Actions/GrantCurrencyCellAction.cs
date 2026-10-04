using UnityEngine;

/// <summary>Which run wallet cell actions grant into.</summary>
public enum PlayerRunCurrencyKind
{
    Run = 0,
    Permanent = 1,
}

/// <summary>Grants currency on the active player run, then completes the cell.</summary>
[CreateAssetMenu(menuName = "Dungeon/Actions/Grant Currency", fileName = "GrantCurrencyCellAction")]
public sealed class GrantCurrencyCellAction : DungeonCellAction
{
    [SerializeField] PlayerRunCurrencyKind currencyKind = PlayerRunCurrencyKind.Run;
    [SerializeField, Min(0)] int amount = 10;

    public PlayerRunCurrencyKind CurrencyKind => currencyKind;
    public int Amount => amount;

    public override void Execute(IDungeonRunContext context)
    {
        if (context?.Player?.Wallet == null)
            return;

        PlayerRunWallet wallet = context.Player.Wallet;
        if (currencyKind == PlayerRunCurrencyKind.Permanent)
            wallet.AddPermanent(amount);
        else
            wallet.AddRun(amount);

        Debug.Log(
            $"[Dungeon] GrantCurrency: +{amount} {currencyKind} " +
            $"(run {wallet.RunCurrency}, permanent earned {wallet.PermanentCurrencyEarned})");
        context.CompleteCurrentCell();
    }
}
