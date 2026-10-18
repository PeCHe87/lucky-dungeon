/// <summary>
/// Player payload for an active dungeon run. Owned by <see cref="DungeonRunHost"/>.
/// Add typed bags (inventory, extra stats) here as the run grows.
/// </summary>
public sealed class PlayerRunState
{
    public PlayerRunWallet Wallet { get; } = new PlayerRunWallet();
    public PlayerRunVitals Vitals { get; } = new PlayerRunVitals();
    public PlayerRunKeys Keys { get; } = new PlayerRunKeys();
    public PlayerRunInventory Inventory { get; } = new PlayerRunInventory();
    public float AttackBonus { get; private set; }

    public void BeginRun(float baseMaxHp)
    {
        AttackBonus = 0f;
        Wallet.BeginRun();
        Vitals.BeginRun(baseMaxHp);
        Keys.BeginRun();
        Inventory.BeginRun();
    }

    public void Clear()
    {
        AttackBonus = 0f;
        Wallet.Clear();
        Vitals.Clear();
        Keys.Clear();
        Inventory.Clear();
    }

    public void AddAttackBonus(float amount)
    {
        AttackBonus += amount;
    }
}
