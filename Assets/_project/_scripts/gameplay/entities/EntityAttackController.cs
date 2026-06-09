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

    [Header("Pre-attack telegraph")]
    [Tooltip("When enabled, nav attack phases wind up for Pre Attack Duration before striking.")]
    [SerializeField] bool enablePreAttackTelegraph = true;
    [SerializeField, Min(0.01f)] float preAttackDuration = 0.4f;

    [Header("Damage interrupt")]
    [Tooltip("When enabled, cancels the in-progress attack when this entity takes damage.")]
    [SerializeField] bool cancelAttackOnDamage = true;
    [Tooltip("Blocks starting new attacks for this many seconds after taking damage. 0 = no block (cancel only).")]
    [SerializeField, Min(0f)] float attackCooldownAfterDamage = 0.4f;

    CombatEntityHealth _health;
    float _attackBlockedUntil;
    float _telegraphUntil;
    bool _isAttackCommitActive;

    /// <summary>Raised after <see cref="WeaponHolder.TryAttack"/> succeeds.</summary>
    public event Action AttackStarted;

    /// <summary>Raised after this entity takes damage and attack cancel/block are applied.</summary>
    public event Action DamageInterruptStarted;

    public Transform AttackOriginTransform => facingRoot != null ? facingRoot : transform;
    public Transform AttackFacingTransform => AttackOriginTransform;

    public bool EnablePreAttackTelegraph => enablePreAttackTelegraph;

    public bool IsAttackBlocked => Time.time < _attackBlockedUntil;

    public bool IsTelegraphing => Time.time < _telegraphUntil;

    public bool IsAttackCommitActive => _isAttackCommitActive;

    public bool IsOnlyDamageBlocked =>
        IsAttackBlocked
        && !IsTelegraphing
        && !IsAttackCommitActive
        && !IsWeaponAttackActive
        && (attackAnimator == null || !attackAnimator.IsAttackClipPlaying);

    public bool IsBusy =>
        IsAttackBlocked
        || IsTelegraphing
        || IsWeaponAttackActive
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

    void OnDamaged(float _)
    {
        if (cancelAttackOnDamage)
            CancelActiveAttack();

        if (attackCooldownAfterDamage > 0f)
        {
            float until = Time.time + attackCooldownAfterDamage;
            if (until > _attackBlockedUntil)
                _attackBlockedUntil = until;
        }

        DamageInterruptStarted?.Invoke();
    }

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

    /// <summary>Begins a timed windup facing <paramref name="target"/>; strike is started separately via <see cref="TryAttackTarget"/>.</summary>
    public bool TryBeginPreAttack(Transform target)
    {
        if (!enablePreAttackTelegraph || target == null)
            return false;

        if (IsAttackBlocked || IsTelegraphing)
            return false;

        if (snapFacingToTargetBeforeAttack)
            SnapFacingToward(target.position);

        if (!IsTargetInAttackRange(target))
            return false;

        _telegraphUntil = Time.time + preAttackDuration;
        _isAttackCommitActive = true;
        attackAnimator?.PlayPreAttackClip();
        return true;
    }

    /// <summary>Starts the strike for a committed telegraph without re-checking attack range.</summary>
    public bool TryCommitStrike(Transform optionalTarget)
    {
        if (weaponHolder == null)
            return false;

        if (IsAttackBlocked || IsTelegraphing)
            return false;

        if (optionalTarget != null && snapFacingToTargetBeforeAttack)
            SnapFacingToward(optionalTarget.position);

        var ctx = new AttackContext
        {
            attacker = AttackOriginTransform,
            facing = GetFlatForward(),
            optionalTarget = optionalTarget,
        };

        if (!weaponHolder.TryAttack(in ctx))
            return false;

        AttackStarted?.Invoke();
        return true;
    }

    public void CompleteAttackCommit()
    {
        _isAttackCommitActive = false;
    }

    public bool TryAttackTarget(Transform target)
    {
        if (target == null || weaponHolder == null)
            return false;

        if (IsAttackBlocked || IsTelegraphing)
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
        CancelTelegraph();
        weaponHolder?.CancelActiveAttack();
        attackAnimator?.CancelAttackAnimation();
    }

    void CancelTelegraph()
    {
        _telegraphUntil = 0f;
        _isAttackCommitActive = false;
        attackAnimator?.CancelPreAttackAnimation();
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
