/// <summary>
/// Run-scoped currencies. Reset with the dungeon run; not a meta wallet.
/// </summary>
public sealed class PlayerRunWallet
{
    public int RunCurrency { get; private set; }
    public int PermanentCurrencyEarned { get; private set; }

    public void BeginRun()
    {
        RunCurrency = 0;
        PermanentCurrencyEarned = 0;
        GameEvents.RaisePlayerRunWalletChanged();
    }

    public void Clear() => BeginRun();

    public void AddRun(int amount)
    {
        if (amount <= 0)
            return;

        RunCurrency += amount;
        GameEvents.RaisePlayerRunWalletChanged();
    }

    public void AddPermanent(int amount)
    {
        if (amount <= 0)
            return;

        PermanentCurrencyEarned += amount;
        GameEvents.RaisePlayerRunWalletChanged();
    }

    public bool TrySpendRun(int amount)
    {
        if (amount <= 0 || RunCurrency < amount)
            return false;

        RunCurrency -= amount;
        GameEvents.RaisePlayerRunWalletChanged();
        return true;
    }
}
