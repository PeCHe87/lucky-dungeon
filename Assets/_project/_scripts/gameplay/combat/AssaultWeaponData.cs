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
    [Tooltip("Projectiles spawned per fire. 1 = single shot (revolver/bow).")]
    [SerializeField, Min(1)] int projectileCount = 1;
    [Tooltip("Total horizontal fan width in degrees across all projectiles. 0 = all share facing.")]
    [SerializeField, Min(0f)] float spreadAngleDegrees;
    [Tooltip("Clip frame at which projectiles spawn (delay from attack start).")]
    [SerializeField, Min(0)] int projectileSpawnAtFrame;
    [Tooltip("Frames per second used to convert projectileSpawnAtFrame to delay. Leave 0 to use 30.")]
    [SerializeField, Min(0f)] float projectileFramesPerSecond;

    [Header("Muzzle VFX")]
    [Tooltip("Optional muzzle flash. Empty prefab = no VFX. Timed independently of projectiles.")]
    [SerializeField] RangedMuzzleVfx muzzleVfx;

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
    public int ProjectileCount => projectileCount;
    public float SpreadAngleDegrees => spreadAngleDegrees;
    public int ProjectileSpawnAtFrame => projectileSpawnAtFrame;
    public float ProjectileFramesPerSecond => projectileFramesPerSecond;
    public RangedMuzzleVfx MuzzleVfx => muzzleVfx;
    public float MaxAttackRange => maxAttackRange;
    public float MinAttackRange => minAttackRange;
    public float AttackConeAngle => attackConeAngle;
    public float ApproachStopDistanceBuffer => approachStopDistanceBuffer;
    public int MagazineSize => magazineSize;
    public float ReloadTime => reloadTime;

    public float ResolveProjectileSpawnDelaySeconds()
    {
        float fps = projectileFramesPerSecond > 0f ? projectileFramesPerSecond : 30f;
        return projectileSpawnAtFrame / fps;
    }
}
