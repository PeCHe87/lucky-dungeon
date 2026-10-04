using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-facing facade for dungeon run progression. Forwards to <see cref="DungeonRunHost"/>.
/// </summary>
public sealed class DungeonRunController : MonoBehaviour, IDungeonRunContext
{
    DungeonRunHost _host;

    public DungeonRunSession Session => Host != null ? Host.Session : null;
    public PlayerRunState Player => Host != null ? Host.Player : null;
    public Dungeon ActiveDungeon => Host != null ? Host.ActiveDungeon : null;
    public DungeonCellData CurrentCellDefinition => Host != null ? Host.CurrentCellDefinition : null;

    DungeonRunHost Host
    {
        get
        {
            if (_host == null)
                _host = DungeonRunHost.EnsureExists();
            return _host;
        }
    }

    void Awake()
    {
        _host = DungeonRunHost.EnsureExists();
    }

    public void StartRun(Dungeon dungeon) => Host.StartRun(dungeon);

    public void StartRun(IReadOnlyList<DungeonCellData> cellDefinitions) =>
        Host.StartRun(cellDefinitions);

    public void ResolveCurrentCell() => Host.ResolveCurrentCell();

    public void CompleteCurrentCell() => Host.CompleteCurrentCell();

    public void EndRun() => Host.EndRun();

    public void ResumeActiveRun() => Host.ResumeActiveRun();
}
