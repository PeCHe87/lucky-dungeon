using UnityEngine;

/// <summary>Applies a signed delta to a run-level stat modifier on the active session.</summary>
[CreateAssetMenu(menuName = "Dungeon/Actions/Modify Run Stat", fileName = "ModifyRunStatCellAction")]
public sealed class ModifyRunStatCellAction : DungeonCellAction
{
    [SerializeField] RunStatKind statKind = RunStatKind.PlayerMaxHp;
    [Tooltip("Signed amount added to the run modifier (negative decreases).")]
    [SerializeField] float amount = 5f;

    public RunStatKind StatKind => statKind;
    public float Amount => amount;

    public override void Execute(IDungeonRunContext context)
    {
        if (context?.Session == null)
            return;

        context.Session.ApplyStatModifier(statKind, amount);
        Debug.Log($"[Dungeon] ModifyRunStat: {statKind} += {amount}");
        context.CompleteCurrentCell();
    }
}
