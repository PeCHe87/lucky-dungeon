using UnityEngine;

/// <summary>
/// Immutable blueprint for a dungeon cell. Runtime runs reference these via <see cref="DungeonBuilder"/>.
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Cell Data", fileName = "DungeonCellData")]
public sealed class DungeonCellData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id for lookups (e.g. cell_battle_01).")]
    [SerializeField] string cellId;
    [Tooltip("Player-facing name shown in UI.")]
    [SerializeField] string displayName;
    [TextArea(2, 4)]
    [SerializeField] string description;
    [SerializeField] Sprite icon;
    [SerializeField] DungeonCellType cellType = DungeonCellType.Battle;

    [Header("Presentation")]
    [Tooltip("UI prefab instantiated for this cell in the dungeon progress strip.")]
    [SerializeField] DungeonCellView cellPrefab;

    [Header("Behavior")]
    [Tooltip("Configurable action executed when the player resolves this cell.")]
    [SerializeField] DungeonCellAction action;

    public string CellId => cellId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public DungeonCellType CellType => cellType;
    public DungeonCellView CellPrefab => cellPrefab;
    public DungeonCellAction Action => action;
}
