using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inspector-authored linear cell list. Builds a runtime <see cref="Dungeon"/> via <see cref="DungeonBuilder"/>
/// and starts or resumes a run on <see cref="DungeonRunHost"/>.
/// </summary>
public sealed class DungeonLayoutBuilder : MonoBehaviour
{
    [Tooltip("Ordered cell blueprints for this dungeon layout.")]
    [SerializeField] DungeonCellData[] cells;

    [SerializeField] DungeonRunController runController;
    [Tooltip("If true, builds a new run on Start when none is active; otherwise resumes the existing run.")]
    [SerializeField] bool buildAndStartOnStart = true;

    public IReadOnlyList<DungeonCellData> Cells =>
        cells ?? System.Array.Empty<DungeonCellData>();

    public int CellCount => cells != null ? cells.Length : 0;

    void Awake()
    {
        if (runController == null)
            runController = GetComponent<DungeonRunController>();
        if (runController == null)
            runController = FindFirstObjectByType<DungeonRunController>();

        DungeonRunHost.EnsureExists();
    }

    void Start()
    {
        if (!buildAndStartOnStart)
            return;

        DungeonRunHost host = DungeonRunHost.EnsureExists();
        if (host.HasActiveRun)
        {
            host.ResumeActiveRun();
            return;
        }

        BuildAndStartRun();
    }

    /// <summary>Builds a fresh runtime dungeon from the inspector cell list.</summary>
    public Dungeon BuildDungeon()
    {
        return DungeonBuilder.BuildDungeon(cells);
    }

    /// <summary>Builds from the inspector list and starts it on the run host.</summary>
    public void BuildAndStartRun()
    {
        if (runController == null && DungeonRunHost.Instance == null)
        {
            Debug.LogWarning("[DungeonLayoutBuilder] No DungeonRunController / Host available.", this);
            return;
        }

        if (cells == null || cells.Length == 0)
        {
            Debug.LogWarning("[DungeonLayoutBuilder] Cell list is empty.", this);
            return;
        }

        Dungeon dungeon = BuildDungeon();
        if (runController != null)
            runController.StartRun(dungeon);
        else
            DungeonRunHost.EnsureExists().StartRun(dungeon);
    }
}
