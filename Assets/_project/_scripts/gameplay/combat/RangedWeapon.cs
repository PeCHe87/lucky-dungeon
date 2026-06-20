using UnityEngine;
using UnityEngine.Events;

/// <summary>Pooled ranged attack: spawns a <see cref="DamageProjectile"/> from <see cref="ProjectilePool"/> on animation fire frame.</summary>
public sealed class RangedWeapon : MonoBehaviour, IWeapon, IWeaponEquippedPresentation, IAttackActivity, IWeaponAnimationBinding, IWeaponTargetDetection
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
    [Header("Pushback")]
    [Tooltip("Horizontal travel applied to victims along attacker forward on hit. 0 = none.")]
    [SerializeField, Min(0f)] float pushbackDistance = 0.4f;
    [SerializeField, Min(0.01f)] float pushbackDuration = 0.08f;
    [Header("Equipped presentation")]
    [Tooltip("Child object(s) with meshes/VFX to show only when this weapon is equipped. Do not use the GameObject with this script if that would disable attack logic.")]
    [SerializeField] GameObject[] equippedVisualRoots;
    [Header("Animation")]
    [SerializeField] RuntimeAnimatorController animatorController;
    [SerializeField] PlayerEntityStateAnimationProfile animationProfile;

    public RuntimeAnimatorController AnimatorController => animatorController;
    public PlayerEntityStateAnimationProfile AnimationProfile => animationProfile;

    [Header("Target detection")]
    [Tooltip("Primary XZ detection radius pushed to NearestTargetQuery when this weapon is equipped.")]
    [SerializeField, Min(0.01f)] float targetDetectionRadius = 15f;
    [Tooltip("360° fallback detection radius pushed to NearestTargetQuery when this weapon is equipped.")]
    [SerializeField, Min(0.01f)] float omnidirectionalDetectionRadius = 20f;

    public float TargetDetectionRadius => targetDetectionRadius;
    public float OmnidirectionalDetectionRadius => omnidirectionalDetectionRadius;

    float _cooldownRemaining;
    float _attackActiveTimer;
    bool _hasArmedContext;
    AttackContext _armedContext;
    bool _hasPendingFireContext;
    AttackContext _pendingFireContext;

    public bool IsAttackActive => _attackActiveTimer > 0f;

    public void CancelAttack()
    {
        _attackActiveTimer = 0f;
        _hasArmedContext = false;
        _hasPendingFireContext = false;
    }

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

    public bool TryAttack(in AttackContext ctx) => TryBeginAttack(in ctx);

    /// <summary>Begins a ranged shot: cooldown, arms context for the next clip start. No projectile yet.</summary>
    public bool TryBeginAttack(in AttackContext ctx)
    {
        if (ctx.attacker == null)
            return false;
        if (_cooldownRemaining > 0f)
            return false;

        _cooldownRemaining = cooldown;
        _armedContext = ctx;
        _hasArmedContext = true;
        _attackActiveTimer = attackActiveDuration;
        return true;
    }

    /// <summary>Called when an attack animator state starts; links armed context to the clip's fire event.</summary>
    public void ArmFireForCurrentShot()
    {
        if (!_hasArmedContext)
            return;

        _pendingFireContext = _armedContext;
        _hasPendingFireContext = true;
        _hasArmedContext = false;
    }

    /// <summary>Called from animation event <c>OnRangedFireFrame</c> on the Animator object.</summary>
    public void ApplyPendingFire()
    {
        if (!_hasPendingFireContext)
            return;

        AttackContext ctx = _pendingFireContext;
        _hasPendingFireContext = false;
        SpawnProjectile(in ctx);
    }

    void SpawnProjectile(in AttackContext ctx)
    {
        if (ctx.attacker == null || projectilePool == null)
            return;

        Vector3 forward = ctx.facing;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        GameObject shot = projectilePool.TryGet();
        if (shot == null)
            return;

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
            return;
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
            in style,
            pushbackDistance,
            pushbackDuration);

        shot.SetActive(true);
        onAttackPerformed?.Invoke();
    }
}
