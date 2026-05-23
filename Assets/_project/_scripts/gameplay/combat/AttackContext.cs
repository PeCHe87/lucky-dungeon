using UnityEngine;

/// <summary>Data passed from <see cref="PlayerAttackController"/> to the equipped <see cref="IWeapon"/>.</summary>
public struct AttackContext
{
    public Transform attacker;
    /// <summary>Horizontal-ish attack forward (caller should normalize XZ as needed).</summary>
    public Vector3 facing;
    /// <summary>Current lock target from <see cref="NearestTargetQuery"/>, if any.</summary>
    public Transform optionalTarget;
    /// <summary>If set, overrides the weapon's default element for floating damage / damage typing.</summary>
    public DamageElement? damageElementOverride;
    /// <summary>When true, the attack is treated as a critical hit for presentation (melee may still roll crit separately).</summary>
    public bool forceCritical;
}
