using System;
using UnityEngine;

/// <summary>
/// AI attack driver: weapon range checks, facing, and <see cref="WeaponHolder.TryAttack"/>.
/// Used by nav behaviors and FSM <see cref="AttackStateHandler"/>.
/// </summary>
public sealed class EntityAttackController : MonoBehaviour
{
    [SerializeField] WeaponHolder weaponHolder;
    [SerializeField] EntityAttackAnimator attackAnimator;
    [Tooltip("If unset, uses this transform for attack origin and facing.")]
    [SerializeField] Transform facingRoot;
    [SerializeField] bool snapFacingToTargetBeforeAttack = true;

    CombatEntityHealth _health;

    /// <summary>Raised after <see cref="WeaponHolder.TryAttack"/> succeeds.</summary>
    public event Action AttackStarted;

    public Transform AttackOriginTransform => facingRoot != null ? facingRoot : transform;
    public Transform AttackFacingTransform => AttackOriginTransform;

    public bool IsBusy =>
        IsWeaponAttackActive
        || (attackAnimator != null && attackAnimator.IsAttackClipPlaying);

    bool IsWeaponAttackActive =>
        weaponHolder != null
        && weaponHolder.Current is IAttackActivity activity
        && activity.IsAttackActive;

    void Awake()
    {
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (attackAnimator == null)
            attackAnimator = GetComponent<EntityAttackAnimator>();
        if (facingRoot == null)
            facingRoot = transform;

        _health = GetComponent<CombatEntityHealth>();
    }

    void OnEnable()
    {
        if (_health != null)
            _health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (_health != null)
            _health.Damaged -= OnDamaged;
    }

    void OnDamaged(float _) => CancelActiveAttack();

    public bool IsTargetInAttackRange(Transform target)
    {
        if (target == null || weaponHolder == null || weaponHolder.Current == null)
            return false;

        Vector3 origin = AttackOriginTransform.position;
        Vector3 facing = GetFlatForward();
        Vector3 targetPos = target.position;

        if (weaponHolder.Current is IWeaponAttackRange rangeCheck)
            return rangeCheck.IsTargetWithinAttackRange(origin, facing, targetPos);

        if (weaponHolder.Current is MeleeWeapon melee)
            return melee.IsTargetWithinDamageRadius(origin, targetPos);

        return false;
    }

    /// <summary>
    /// True when the target has left melee engagement far enough to resume chasing (includes hysteresis).
    /// </summary>
    public bool IsTargetBeyondAttackEngagement(Transform target)
    {
        if (target == null || weaponHolder == null || weaponHolder.Current == null)
            return true;

        Vector3 origin = AttackOriginTransform.position;
        Vector3 targetPos = target.position;

        if (weaponHolder.Current is MeleeWeapon melee)
        {
            float releaseRadius = melee.DamageOverlapRadius + 0.35f;
            return NavMeshChaseDriver.HorizontalDistance(origin, targetPos) > releaseRadius;
        }

        return !IsTargetInAttackRange(target);
    }

    public bool TryAttackTarget(Transform target)
    {
        if (target == null || weaponHolder == null)
            return false;

        if (snapFacingToTargetBeforeAttack)
            SnapFacingToward(target.position);

        if (!IsTargetInAttackRange(target))
            return false;

        var ctx = new AttackContext
        {
            attacker = AttackOriginTransform,
            facing = GetFlatForward(),
            optionalTarget = target,
        };

        if (!weaponHolder.TryAttack(in ctx))
            return false;

        AttackStarted?.Invoke();
        return true;
    }

    public void CancelActiveAttack()
    {
        weaponHolder?.CancelActiveAttack();
        attackAnimator?.CancelAttackAnimation();
    }

    public void FaceTarget(Transform target)
    {
        if (target != null)
            SnapFacingToward(target.position);
    }

    void SnapFacingToward(Vector3 worldPos)
    {
        Transform root = AttackFacingTransform;
        Vector3 to = worldPos - root.position;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return;
        root.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    Vector3 GetFlatForward()
    {
        Vector3 forward = AttackFacingTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            return Vector3.forward;
        forward.Normalize();
        return forward;
    }
}
