using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DontDestroyOnLoad owner of the dungeon run session and progression APIs.
/// Survives dungeon ↔ battleground (and other) scene loads.
/// </summary>
public sealed class DungeonRunHost : MonoBehaviour, IDungeonRunContext
{
    public static DungeonRunHost Instance { get; private set; }

    [SerializeField] string defaultDungeonSceneName = "dungeon";
    [SerializeField, Min(0.01f)] float defaultPlayerMaxHp = 100f;

    DungeonRunSession _session;
    PlayerRunState _player;
    bool _isResolving;

    public DungeonRunSession Session => _session;
    public PlayerRunState Player => _player;
    public bool HasActiveRun => _session != null && _session.HasActiveRun;
    public Dungeon ActiveDungeon => _session?.Dungeon;
    public DungeonCellData CurrentCellDefinition =>
        ActiveDungeon == null || ActiveDungeon.IsComplete
            ? null
            : ActiveDungeon.CurrentCell.Definition;

    public static DungeonRunHost EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var existing = FindFirstObjectByType<DungeonRunHost>();
        if (existing != null)
        {
            existing.InitializeSingleton();
            return Instance;
        }

        var go = new GameObject("DungeonRunHost");
        var host = go.AddComponent<DungeonRunHost>();
        host.InitializeSingleton();
        return Instance;
    }

    void Awake() => InitializeSingleton();

    void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSession();
        _session.SetDungeonSceneName(defaultDungeonSceneName);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void StartRun(Dungeon dungeon)
    {
        if (dungeon == null)
        {
            Debug.LogWarning("[DungeonRunHost] StartRun called with null dungeon.", this);
            return;
        }

        EnsureSession();
        _session.StartRun(dungeon);
        _player.BeginRun(defaultPlayerMaxHp);
        _session.SetDungeonSceneName(defaultDungeonSceneName);

        GameEvents.RaiseDungeonRunStarted(dungeon);

        if (!dungeon.IsComplete)
            GameEvents.RaiseDungeonCellStarted(dungeon.CurrentIndex, dungeon.CurrentCell.Definition);
    }

    public void StartRun(IReadOnlyList<DungeonCellData> cellDefinitions)
    {
        StartRun(DungeonBuilder.BuildDungeon(cellDefinitions));
    }

    /// <summary>Re-raise run events so scene UI can rebind without rebuilding the dungeon.</summary>
    public void ResumeActiveRun()
    {
        Dungeon dungeon = ActiveDungeon;
        if (dungeon == null)
            return;

        GameEvents.RaiseDungeonRunStarted(dungeon);

        if (!dungeon.IsComplete)
            GameEvents.RaiseDungeonCellStarted(dungeon.CurrentIndex, dungeon.CurrentCell.Definition);
        else
            GameEvents.RaiseDungeonRunFinished(dungeon);
    }

    public void ResolveCurrentCell()
    {
        EnsureSession();

        Dungeon dungeon = ActiveDungeon;
        if (dungeon == null || dungeon.IsComplete)
        {
            Debug.LogWarning("[DungeonRunHost] No active cell to resolve.", this);
            return;
        }

        if (_session.IsAwaitingExternalCell)
        {
            Debug.LogWarning("[DungeonRunHost] Already awaiting an external cell scene.", this);
            return;
        }

        if (_isResolving)
        {
            Debug.LogWarning("[DungeonRunHost] Already resolving a cell.", this);
            return;
        }

        DungeonCellData definition = dungeon.CurrentCell.Definition;
        if (definition == null)
        {
            Debug.LogError("[DungeonRunHost] Current cell has null definition.", this);
            return;
        }

        DungeonCellAction action = definition.Action;
        if (action == null)
        {
            Debug.LogWarning(
                $"[DungeonRunHost] Cell '{definition.CellId}' has no action — advancing anyway.",
                definition);
            CompleteCurrentCell();
            return;
        }

        Debug.Log(
            $"[DungeonRunHost] Resolving cell [{dungeon.CurrentIndex}] '{definition.CellId}' ({definition.CellType}).",
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
        EnsureSession();

        Dungeon dungeon = ActiveDungeon;
        if (dungeon == null || dungeon.IsComplete)
            return;

        int completedIndex = dungeon.CurrentIndex;
        DungeonCellData completedDefinition = dungeon.CurrentCell.Definition;

        if (!dungeon.CompleteCurrentCell())
            return;

        _session.ClearExternalAwait();
        GameEvents.RaiseDungeonCellCompleted(completedIndex, completedDefinition);

        if (dungeon.IsComplete)
        {
            // Keep dungeon in session so UI can resume after a scene return; StartRun replaces it.
            Debug.Log("[DungeonRunHost] Dungeon run finished.", this);
            GameEvents.RaiseDungeonRunFinished(dungeon);
            return;
        }

        Debug.Log(
            $"[DungeonRunHost] Advanced to cell [{dungeon.CurrentIndex}] '{dungeon.CurrentCell.Definition?.CellId}'.",
            this);
        GameEvents.RaiseDungeonCellStarted(dungeon.CurrentIndex, dungeon.CurrentCell.Definition);
    }

    public void EndRun()
    {
        EnsureSession();

        Dungeon dungeon = ActiveDungeon;
        if (dungeon != null)
            GameEvents.RaiseDungeonRunFinished(dungeon);

        _session.EndRun();
        _player.Clear();
    }

    /// <summary>
    /// Leaves the dungeon for an external scene without completing the current cell.
    /// Call <see cref="NotifyExternalCellSucceeded"/> when that scene finishes successfully.
    /// </summary>
    public void BeginExternalCellAndLoadScene(string externalSceneName, string dungeonSceneName = null)
    {
        EnsureSession();

        if (!HasActiveRun)
        {
            Debug.LogWarning("[DungeonRunHost] Cannot begin external cell — no active run.", this);
            return;
        }

        if (string.IsNullOrEmpty(externalSceneName))
        {
            Debug.LogError("[DungeonRunHost] External scene name is empty.", this);
            return;
        }

        string returnScene = string.IsNullOrEmpty(dungeonSceneName)
            ? defaultDungeonSceneName
            : dungeonSceneName;

        _session.BeginExternalCell(returnScene);

        Debug.Log(
            $"[DungeonRunHost] Leaving for external scene '{externalSceneName}' (return '{returnScene}').",
            this);
        SceneManager.LoadScene(externalSceneName);
    }

    /// <summary>
    /// Completes the pending external cell and returns to the dungeon scene.
    /// </summary>
    public void NotifyExternalCellSucceeded()
    {
        EnsureSession();

        if (!_session.IsAwaitingExternalCell)
        {
            Debug.LogWarning("[DungeonRunHost] NotifyExternalCellSucceeded with no awaiting external cell.", this);
            return;
        }

        string returnScene = string.IsNullOrEmpty(_session.DungeonSceneName)
            ? defaultDungeonSceneName
            : _session.DungeonSceneName;

        CompleteCurrentCell();
        // Always clear — CompleteCurrentCell may early-out before clearing, which would block GO forever.
        _session.ClearExternalAwait();
        SceneManager.LoadScene(returnScene);
    }

    void EnsureSession()
    {
        if (_session == null)
            _session = new DungeonRunSession();
        if (_player == null)
            _player = new PlayerRunState();
    }
}
