using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-wide pool of <see cref="DungeonCellData"/> blueprints for builders/generators.
/// Not a run path — use <see cref="DungeonBuilder.BuildDungeon"/> to assemble a runtime dungeon.
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Cell Catalog", fileName = "DungeonCellCatalog")]
public sealed class DungeonCellCatalog : ScriptableObject
{
    [SerializeField] DungeonCellData[] cells;

    Dictionary<string, DungeonCellData> _byId;
    bool _loggedDuplicateIds;

    public static DungeonCellCatalog Current { get; private set; }

    public IReadOnlyList<DungeonCellData> All => cells ?? System.Array.Empty<DungeonCellData>();

    public static void SetCurrent(DungeonCellCatalog catalog)
    {
        Current = catalog;
        if (catalog != null)
            catalog.InvalidateLookup();
    }

    public bool TryGetById(string cellId, out DungeonCellData data)
    {
        data = null;
        if (string.IsNullOrEmpty(cellId))
            return false;

        EnsureLookup();
        return _byId.TryGetValue(cellId, out data);
    }

    public DungeonCellData GetById(string cellId)
    {
        if (TryGetById(cellId, out DungeonCellData data))
            return data;

        Debug.LogWarning($"[DungeonCellCatalog] No cell with id '{cellId}'.", this);
        return null;
    }

    void OnEnable() => InvalidateLookup();

    void InvalidateLookup()
    {
        _byId = null;
        _loggedDuplicateIds = false;
    }

    void EnsureLookup()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, DungeonCellData>();
        if (cells == null)
            return;

        for (int i = 0; i < cells.Length; i++)
        {
            DungeonCellData cell = cells[i];
            if (cell == null)
                continue;

            string id = cell.CellId;
            if (string.IsNullOrEmpty(id))
                continue;

            if (_byId.ContainsKey(id))
            {
                if (!_loggedDuplicateIds)
                {
                    Debug.LogWarning(
                        $"[DungeonCellCatalog] Duplicate cellId '{id}'. Keeping the first entry.",
                        this);
                    _loggedDuplicateIds = true;
                }
                continue;
            }

            _byId.Add(id, cell);
        }
    }
}
