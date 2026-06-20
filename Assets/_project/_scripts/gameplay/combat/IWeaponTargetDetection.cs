/// <summary>Per-weapon target discovery radii applied to <see cref="NearestTargetQuery"/> on equip.</summary>
public interface IWeaponTargetDetection
{
    float TargetDetectionRadius { get; }
    float OmnidirectionalDetectionRadius { get; }
}
