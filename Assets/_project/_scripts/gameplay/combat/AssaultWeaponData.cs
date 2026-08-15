using UnityEngine;

/// <summary>Assault/ranged-specific balance/config for <see cref="RangedWeapon"/>.</summary>
[CreateAssetMenu(menuName = "Combat/Assault Weapon Data", fileName = "AssaultWeaponData")]
public sealed class AssaultWeaponData : WeaponData
{
    [Header("Projectile")]
    [SerializeField] float projectileSpeed = 18f;
    [SerializeField] float projectileLifetime = 3f;
    [Tooltip("0 = no max distance (lifetime only).")]
    [SerializeField] float projectileMaxDistance;

    [Header("Attack range")]
    [Tooltip("Max horizontal strike distance. 0 = use projectileMaxDistance.")]
    [SerializeField, Min(0f)] float maxAttackRange;
    [Tooltip("Target closer than this is out of attack range (entity will reposition).")]
    [SerializeField, Min(0f)] float minAttackRange = 3f;
    [Tooltip("Total forward cone angle in degrees. 360 = distance-only.")]
    [SerializeField, Range(1f, 360f)] float attackConeAngle = 360f;
    [Tooltip("NavMesh stopping distance buffer subtracted from max attack range.")]
    [SerializeField, Min(0f)] float approachStopDistanceBuffer = 2f;

    [Header("Magazine")]
    [SerializeField, Min(1)] int magazineSize = 6;
    [Tooltip("Magazine reload duration in seconds (scaled time).")]
    [SerializeField, Min(0f)] float reloadTime = 1.5f;

    public float ProjectileSpeed => projectileSpeed;
    public float ProjectileLifetime => projectileLifetime;
    public float ProjectileMaxDistance => projectileMaxDistance;
    public float MaxAttackRange => maxAttackRange;
    public float MinAttackRange => minAttackRange;
    public float AttackConeAngle => attackConeAngle;
    public float ApproachStopDistanceBuffer => approachStopDistanceBuffer;
    public int MagazineSize => magazineSize;
    public float ReloadTime => reloadTime;
}
