using UnityEngine;

/// <summary>Weapon reports whether a world target is within its attack radius.</summary>
public interface IWeaponAttackRange
{
    bool IsTargetWithinAttackRange(Vector3 origin, Vector3 facingFlat, Vector3 targetWorldPos);
}
