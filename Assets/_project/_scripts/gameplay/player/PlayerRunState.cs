/// <summary>
/// Player payload for an active dungeon run. Owned by <see cref="DungeonRunHost"/>.
/// Add typed bags (inventory, extra stats) here as the run grows.
/// </summary>
public sealed class PlayerRunState
{
    public PlayerRunWallet Wallet { get; } = new PlayerRunWallet();
    public PlayerRunVitals Vitals { get; } = new PlayerRunVitals();
    public float AttackBonus { get; private set; }

    public void BeginRun(float baseMaxHp)
    {
        AttackBonus = 0f;
        Wallet.BeginRun();
        Vitals.BeginRun(baseMaxHp);
    }

    public void Clear()
    {
        AttackBonus = 0f;
        Wallet.Clear();
        Vitals.Clear();
    }

    public void AddAttackBonus(float amount)
    {
        AttackBonus += amount;
    }
}
