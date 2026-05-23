using UnityEngine;
using UnityEngine.Events;

/// <summary>Pooled ranged attack: spawns a <see cref="DamageProjectile"/> from <see cref="ProjectilePool"/>.</summary>
public sealed class RangedWeapon : MonoBehaviour, IWeapon, IWeaponEquippedPresentation, IAttackActivity
{
    [SerializeField] ProjectilePool projectilePool;
    [Tooltip("World spawn pose uses this transform; offset applied in its local space.")]
    [SerializeField] Transform firePoint;
    [SerializeField] float damage = 10f;
    [SerializeField] float cooldown = 0.35f;
    [SerializeField] float projectileSpeed = 18f;
    [SerializeField] float projectileLifetime = 3f;
    [Tooltip("0 = no max distance (lifetime only).")]
    [SerializeField] float projectileMaxDistance;
    [SerializeField] LayerMask hitLayers = ~0;
    [SerializeField] Vector3 spawnOffset;
    [SerializeField] UnityEvent onAttackPerformed;
    [Tooltip("How long <see cref=\"IAttackActivity.IsAttackActive\"/> stays true after a successful attack.")]
    [SerializeField, Min(0.01f)] float attackActiveDuration = 0.2f;
    [Header("Damage presentation")]
    [SerializeField] DamageElement damageElement = DamageElement.Physical;
    [SerializeField, Range(0f, 1f)] float criticalStrikeChance;
    [Header("Equipped presentation")]
    [Tooltip("Child object(s) with meshes/VFX to show only when this weapon is equipped. Do not use the GameObject with this script if that would disable attack logic.")]
    [SerializeField] GameObject[] equippedVisualRoots;

    float _cooldownRemaining;
    float _attackActiveTimer;

    public bool IsAttackActive => _attackActiveTimer > 0f;

    void Update()
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.deltaTime;
        if (_attackActiveTimer > 0f)
            _attackActiveTimer -= Time.deltaTime;
    }

    public void SetEquippedVisuals(bool equipped)
    {
        if (equippedVisualRoots == null || equippedVisualRoots.Length == 0)
            return;
        for (int i = 0; i < equippedVisualRoots.Length; i++)
        {
            GameObject root = equippedVisualRoots[i];
            if (root != null)
                root.SetActive(equipped);
        }
    }

    public bool TryAttack(in AttackContext ctx)
    {
        if (ctx.attacker == null)
            return false;
        if (_cooldownRemaining > 0f)
            return false;
        if (projectilePool == null)
            return false;

        Vector3 forward = ctx.facing;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        GameObject shot = projectilePool.TryGet();
        if (shot == null)
            return false;

        Transform originTransform = firePoint != null ? firePoint : ctx.attacker;
        Vector3 spawnPos = originTransform.TransformPoint(spawnOffset);
        Quaternion spawnRot = Quaternion.LookRotation(forward, Vector3.up);

        DamageElement element = ctx.damageElementOverride ?? damageElement;
        bool isCrit = ctx.forceCritical || (criticalStrikeChance > 0f && Random.value < criticalStrikeChance);
        var style = new DamageNumberStyle(element, isCrit);

        DamageProjectile projectile = shot.GetComponent<DamageProjectile>();
        if (projectile == null)
        {
            projectilePool.Release(shot);
            return false;
        }

        projectile.Initialize(
            projectilePool,
            ctx.attacker,
            spawnPos,
            spawnRot,
            forward,
            projectileSpeed,
            damage,
            projectileLifetime,
            projectileMaxDistance,
            hitLayers,
            in style);

        shot.SetActive(true);

        _cooldownRemaining = cooldown;
        _attackActiveTimer = attackActiveDuration;
        onAttackPerformed?.Invoke();
        return true;
    }
}
