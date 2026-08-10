using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pooled ranged attack: spawns a <see cref="DamageProjectile"/> from <see cref="ProjectilePool"/> on animation fire frame.
/// Balance/config comes from <see cref="AssaultWeaponData"/>.
/// </summary>
public sealed class RangedWeapon : MonoBehaviour, IWeapon, IWeaponDataSource, IAttackActivity, IWeaponAnimationBinding, IWeaponTargetDetection, IWeaponAttackReadiness, IWeaponAttackRange
{
    [SerializeField] AssaultWeaponData data;
    [SerializeField] ProjectilePool projectilePool;
    [Tooltip("World spawn pose uses this transform; offset applied in its local space. May be rebound from spawned visual muzzle.")]
    [SerializeField] Transform firePoint;
    [SerializeField] Vector3 spawnOffset;
    [SerializeField] UnityEvent onAttackPerformed;

    float _cooldownRemaining;
    float _attackActiveTimer;
    bool _hasArmedContext;
    AttackContext _armedContext;
    bool _hasPendingFireContext;
    AttackContext _pendingFireContext;

    void Awake()
    {
        if (data == null)
            Debug.LogWarning($"{nameof(RangedWeapon)} on {name}: {nameof(data)} is not assigned.", this);
    }

    public WeaponData Data => data;
    public RuntimeAnimatorController AnimatorController => data != null ? data.AnimatorController : null;
    public PlayerEntityStateAnimationProfile AnimationProfile => data != null ? data.AnimationProfile : null;
    public float TargetDetectionRadius => data != null ? data.TargetDetectionRadius : 15f;
    public float OmnidirectionalDetectionRadius => data != null ? data.OmnidirectionalDetectionRadius : 20f;

    public float EffectiveMaxAttackRange
    {
        get
        {
            if (data == null)
                return 20f;
            if (data.MaxAttackRange > 0f)
                return data.MaxAttackRange;
            if (data.ProjectileMaxDistance > 0f)
                return data.ProjectileMaxDistance;
            return 20f;
        }
    }

    public float ApproachStopDistanceFromTarget =>
        data == null
            ? 0f
            : Mathf.Max(data.MinAttackRange + 0.5f, EffectiveMaxAttackRange - data.ApproachStopDistanceBuffer);

    public float MinAttackRange => data != null ? data.MinAttackRange : 0f;

    public bool IsTargetTooClose(Vector3 origin, Vector3 targetWorldPos) =>
        data != null
        && data.MinAttackRange > 0f
        && NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos) < data.MinAttackRange;

    public bool IsTargetBeyondMaxRange(Vector3 origin, Vector3 targetWorldPos) =>
        NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos) > EffectiveMaxAttackRange;

    public float RetreatStopDistanceFromTarget => MinAttackRange + 0.5f;

    public bool IsAttackActive => _attackActiveTimer > 0f;

    public bool IsAttackReady => _cooldownRemaining <= 0f;

    /// <summary>Updates the projectile spawn origin (typically the muzzle on a spawned bow visual).</summary>
    public void SetFirePoint(Transform point) => firePoint = point;

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

    public bool IsTargetWithinAttackRange(Vector3 origin, Vector3 facingFlat, Vector3 targetWorldPos)
    {
        if (data == null)
            return false;

        float distance = NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos);
        if (distance < data.MinAttackRange || distance > EffectiveMaxAttackRange)
            return false;

        float attackConeAngle = data.AttackConeAngle;
        if (attackConeAngle >= 360f)
            return true;

        Vector3 to = targetWorldPos - origin;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return true;
        to.Normalize();
        float halfCone = attackConeAngle * 0.5f;
        return Vector3.Angle(facingFlat, to) <= halfCone + 0.01f;
    }

    public bool TryAttack(in AttackContext ctx) => TryBeginAttack(in ctx);

    /// <summary>Begins a ranged shot: cooldown, arms context for the next clip start. No projectile yet.</summary>
    public bool TryBeginAttack(in AttackContext ctx)
    {
        if (data == null || ctx.attacker == null)
            return false;
        if (_cooldownRemaining > 0f)
            return false;

        _cooldownRemaining = data.Cooldown;
        _armedContext = ctx;
        _hasArmedContext = true;
        _attackActiveTimer = data.AttackActiveDuration;
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
        if (data == null || ctx.attacker == null || projectilePool == null)
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

        DamageElement element = ctx.damageElementOverride ?? data.DamageElement;
        bool isCrit = ctx.forceCritical || (data.CriticalStrikeChance > 0f && Random.value < data.CriticalStrikeChance);
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
            data.ProjectileSpeed,
            data.Damage,
            data.ProjectileLifetime,
            data.ProjectileMaxDistance,
            data.HitLayers,
            in style,
            data.PushbackDistance,
            data.PushbackDuration);

        shot.SetActive(true);
        onAttackPerformed?.Invoke();
    }
}
