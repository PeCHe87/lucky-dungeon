using UnityEngine;

/// <summary>
/// Owns the active runtime <see cref="Dungeon"/> plus encounter-scoped modifiers.
/// Player payload lives on <see cref="PlayerRunState"/> via <see cref="DungeonRunHost"/>.
/// Survives scene loads when held by <see cref="DungeonRunHost"/>.
/// </summary>
public sealed class DungeonRunSession
{
    public Dungeon Dungeon { get; private set; }
    public float EnemyMaxHpBonus { get; private set; }
    public bool HasActiveRun => Dungeon != null;

    /// <summary>True while a cell has left the dungeon scene for an external scene (e.g. battle).</summary>
    public bool IsAwaitingExternalCell { get; private set; }

    /// <summary>Scene to load when returning from an external cell.</summary>
    public string DungeonSceneName { get; private set; } = "dungeon";

    public void StartRun(Dungeon dungeon)
    {
        Dungeon = dungeon;
        EnemyMaxHpBonus = 0f;
        IsAwaitingExternalCell = false;
    }

    public void EndRun()
    {
        Dungeon = null;
        EnemyMaxHpBonus = 0f;
        IsAwaitingExternalCell = false;
    }

    public void SetDungeonSceneName(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
            DungeonSceneName = sceneName;
    }

    public void BeginExternalCell(string dungeonSceneName)
    {
        SetDungeonSceneName(dungeonSceneName);
        IsAwaitingExternalCell = true;
    }

    public void ClearExternalAwait()
    {
        IsAwaitingExternalCell = false;
    }

    public void ApplyStatModifier(RunStatKind kind, float amount)
    {
        switch (kind)
        {
            case RunStatKind.EnemyMaxHp:
                EnemyMaxHpBonus += amount;
                break;
            default:
                Debug.LogWarning($"[DungeonRunSession] Unhandled RunStatKind '{kind}'.");
                break;
        }
    }
}
