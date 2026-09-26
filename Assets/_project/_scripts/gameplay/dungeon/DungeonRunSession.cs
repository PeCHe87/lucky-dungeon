using UnityEngine;

/// <summary>
/// Owns the active runtime <see cref="Dungeon"/> plus run-scoped modifiers and currency.
/// Replaced/cleared each run — not a ScriptableObject.
/// </summary>
public sealed class DungeonRunSession
{
    public Dungeon Dungeon { get; private set; }
    public int RunCurrency { get; private set; }
    public float PlayerMaxHpBonus { get; private set; }
    public float PlayerAttackBonus { get; private set; }
    public float EnemyMaxHpBonus { get; private set; }
    public bool HasActiveRun => Dungeon != null;

    public void StartRun(Dungeon dungeon)
    {
        Dungeon = dungeon;
        RunCurrency = 0;
        PlayerMaxHpBonus = 0f;
        PlayerAttackBonus = 0f;
        EnemyMaxHpBonus = 0f;
    }

    public void EndRun()
    {
        Dungeon = null;
        RunCurrency = 0;
        PlayerMaxHpBonus = 0f;
        PlayerAttackBonus = 0f;
        EnemyMaxHpBonus = 0f;
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0)
            return;
        RunCurrency += amount;
    }

    public void ApplyStatModifier(RunStatKind kind, float amount)
    {
        switch (kind)
        {
            case RunStatKind.PlayerMaxHp:
                PlayerMaxHpBonus += amount;
                break;
            case RunStatKind.PlayerAttack:
                PlayerAttackBonus += amount;
                break;
            case RunStatKind.EnemyMaxHp:
                EnemyMaxHpBonus += amount;
                break;
            default:
                Debug.LogWarning($"[DungeonRunSession] Unhandled RunStatKind '{kind}'.");
                break;
        }
    }
}
