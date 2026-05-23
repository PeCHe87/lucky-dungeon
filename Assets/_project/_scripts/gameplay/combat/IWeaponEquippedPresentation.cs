/// <summary>Optional: weapon mesh/VFX roots toggled by <see cref="WeaponHolder"/> when equip changes.</summary>
public interface IWeaponEquippedPresentation
{
    void SetEquippedVisuals(bool equipped);
}
