using UnityEngine;

/// <summary>Shared layer masks for combat weapons and projectiles.</summary>
public static class CombatHitLayers
{
    /// <summary>Player ranged shots: damage entities and world, not the Player layer.</summary>
    public static LayerMask PlayerRangedProjectile =>
        LayerMask.GetMask("Default", "Obstacle", "Entity", "Destructible");

    /// <summary>Enemy ranged shots: same as player plus Player layer.</summary>
    public static LayerMask EnemyRangedProjectile =>
        LayerMask.GetMask("Default", "Obstacle", "Entity", "Destructible", "Player");
}
