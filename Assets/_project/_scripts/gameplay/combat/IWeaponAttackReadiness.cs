/// <summary>Weapon reports whether cooldown allows starting a new attack (presentation / input gating).</summary>
public interface IWeaponAttackReadiness
{
    bool IsAttackReady { get; }
}
