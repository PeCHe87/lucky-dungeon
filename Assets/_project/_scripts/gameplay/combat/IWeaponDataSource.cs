/// <summary>Optional: exposes the equipped weapon's <see cref="WeaponData"/> for presentation / UI.</summary>
public interface IWeaponDataSource
{
    WeaponData Data { get; }
}
