/// <summary>Equipped weapon resolves how an attack behaves (melee, ranged, etc.).</summary>
public interface IWeapon
{
    /// <returns>True if an attack was performed (e.g. not blocked by cooldown).</returns>
    bool TryAttack(in AttackContext ctx);
}
