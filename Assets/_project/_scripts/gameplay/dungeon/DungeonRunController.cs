using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the active dungeon run: start, resolve current cell action, advance, finish.
/// </summary>
public sealed class DungeonRunController : MonoBehaviour, IDungeonRunContext
{
    [Header("Optional debug start")]
    [Tooltip("If set and startRunOnAwake is true, builds a dungeon from this list on Start.")]
    [SerializeField] DungeonCellData[] debugCellDefinitions;
    [SerializeField] bool startRunOnAwake;

    DungeonRunSession _session;
    bool _isResolving;

    public DungeonRunSession Session => _session;
    public Dungeon ActiveDungeon => _session?.Dungeon;
    public DungeonCellData CurrentCellDefinition =>
        ActiveDungeon == null || ActiveDungeon.IsComplete
            ? null
            : ActiveDungeon.CurrentCell.Definition;

    void Awake()
    {
        _session = new DungeonRunSession();
    }

    void Start()
    {
        // Start after other OnEnables so UI presenters can subscribe to GameEvents.
        if (startRunOnAwake && debugCellDefinitions != null && debugCellDefinitions.Length > 0)
            StartRun(DungeonBuilder.BuildDungeon(debugCellDefinitions));
    }

    public void StartRun(Dungeon dungeon)
    {
        if (dungeon == null)
        {
            Debug.LogWarning("[DungeonRunController] StartRun called with null dungeon.", this);
            return;
        }

        _session.StartRun(dungeon);
        GameEvents.RaiseDungeonRunStarted(dungeon);

        if (!dungeon.IsComplete)
            GameEvents.RaiseDungeonCellStarted(dungeon.CurrentIndex, dungeon.CurrentCell.Definition);
    }

    public void StartRun(IReadOnlyList<DungeonCellData> cellDefinitions)
    {
        StartRun(DungeonBuilder.BuildDungeon(cellDefinitions));
    }

    /// <summary>
    /// Simulates resolving the current cell (runs its action, which completes and advances).
    /// Wire UI (e.g. btnGo) to this.
    /// </summary>
    public void ResolveCurrentCell()
    {
        Dungeon dungeon = ActiveDungeon;
        if (dungeon == null || dungeon.IsComplete)
        {
            Debug.LogWarning("[DungeonRunController] No active cell to resolve.", this);
            return;
        }

        if (_isResolving)
        {
            Debug.LogWarning("[DungeonRunController] Already resolving a cell.", this);
            return;
        }

        DungeonCellData definition = dungeon.CurrentCell.Definition;
        if (definition == null)
        {
            Debug.LogError("[DungeonRunController] Current cell has null definition.", this);
            return;
        }

        DungeonCellAction action = definition.Action;
        if (action == null)
        {
            // Still advance so GO always progresses the map during development.
            Debug.LogWarning(
                $"[DungeonRunController] Cell '{definition.CellId}' has no action — advancing anyway.",
                definition);
            CompleteCurrentCell();
            return;
        }

        Debug.Log(
            $"[DungeonRunController] Resolving cell [{dungeon.CurrentIndex}] '{definition.CellId}' ({definition.CellType}).",
            this);

        _isResolving = true;
        try
        {
            action.Execute(this);
        }
        finally
        {
            _isResolving = false;
        }
    }

    public void CompleteCurrentCell()
    {
        Dungeon dungeon = ActiveDungeon;
        if (dungeon == null || dungeon.IsComplete)
            return;

        int completedIndex = dungeon.CurrentIndex;
        DungeonCellData completedDefinition = dungeon.CurrentCell.Definition;

        if (!dungeon.CompleteCurrentCell())
            return;

        GameEvents.RaiseDungeonCellCompleted(completedIndex, completedDefinition);

        if (dungeon.IsComplete)
        {
            Debug.Log("[DungeonRunController] Dungeon run finished.", this);
            GameEvents.RaiseDungeonRunFinished(dungeon);
            _session.EndRun();
            return;
        }

        Debug.Log(
            $"[DungeonRunController] Advanced to cell [{dungeon.CurrentIndex}] '{dungeon.CurrentCell.Definition?.CellId}'.",
            this);
        GameEvents.RaiseDungeonCellStarted(dungeon.CurrentIndex, dungeon.CurrentCell.Definition);
    }

    public void EndRun()
    {
        Dungeon dungeon = ActiveDungeon;
        if (dungeon != null)
            GameEvents.RaiseDungeonRunFinished(dungeon);

        _session.EndRun();
    }
}
