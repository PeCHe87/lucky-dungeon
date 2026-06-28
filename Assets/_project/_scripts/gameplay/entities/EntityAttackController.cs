using System;
using UnityEngine;
using UnityEngine.AI;

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
    [Tooltip("While in pre-attack telegraph or active attack, suppress TakeDamage FSM, attack cancel, nav reset, facing snap, and damage shake.")]
    [SerializeField] bool suppressDamageInterruptDuringAttack;
    [Tooltip("While in pre-attack telegraph or active attack, block weapon pushback.")]
    [SerializeField] bool suppressPushbackDuringAttack;

    [Header("Melee engagement")]
    [Tooltip("Max probe travel along push axis to still count as geometry-pinned.")]
    [SerializeField, Min(0f)] float pinnedTravelThreshold = PushbackGeometryProbe.DefaultPinnedTravelThreshold;
    [Tooltip("Extra horizontal distance beyond damage overlap while pinned before releasing to chase.")]
    [SerializeField, Min(0f)] float pinEngagementSlack = 0.35f;
    [SerializeField] bool debugLogEngagement;

    CombatEntityHealth _health;
    float _attackBlockedUntil;
    float _telegraphUntil;
    bool _isAttackCommitActive;
    NavMeshAgent _facingLockAgent;
    bool _agentUpdateRotationBeforeLock;

    /// <summary>True while PreAttack1 or Attack1 clips are playing on the attack animator layer.</summary>
    public bool IsCombatFacingLocked =>
        attackAnimator != null && attackAnimator.IsCombatFacingLocked;

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

    public bool IsActivelyAttacking =>
        IsTelegraphing
        || IsWeaponAttackActive
        || (attackAnimator != null && (attackAnimator.IsPreAttackClipPlaying || attackAnimator.IsAttackClipPlaying));

    public bool ShouldSuppressDamageInterrupt =>
        suppressDamageInterruptDuringAttack && IsActivelyAttacking;

    public bool ShouldSuppressPushback =>
        suppressPushbackDuringAttack && IsActivelyAttacking;

    /// <summary>True when <paramref name="hitObject"/> belongs to an entity suppressing damage interrupts during attack.</summary>
    public static bool ShouldSuppressDamageInterruptOn(GameObject hitObject)
    {
        if (hitObject == null)
            return false;

        var attack = hitObject.GetComponentInParent<EntityAttackController>();
        return attack != null && attack.ShouldSuppressDamageInterrupt;
    }

    /// <summary>True when <paramref name="hitObject"/> belongs to an entity suppressing pushback during attack.</summary>
    public static bool ShouldSuppressPushbackOn(GameObject hitObject)
    {
        if (hitObject == null)
            return false;

        var attack = hitObject.GetComponentInParent<EntityAttackController>();
        return attack != null && attack.ShouldSuppressPushback;
    }

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

        RestoreNavAgentFacingLock();
    }

    void OnDamaged(float _)
    {
        if (ShouldSuppressDamageInterrupt)
            return;

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

        if (weaponHolder.Current is MeleeWeapon)
            return CanMaintainMeleeEngagement(target);

        Vector3 origin = AttackOriginTransform.position;
        Vector3 facing = GetFlatForward();
        Vector3 targetPos = target.position;

        if (weaponHolder.Current is IWeaponAttackRange rangeCheck)
            return rangeCheck.IsTargetWithinAttackRange(origin, facing, targetPos);

        return false;
    }

    /// <summary>
    /// True when melee should continue (within overlap radius or push-pinned at a wall).
    /// </summary>
    public bool CanMaintainMeleeEngagement(Transform target)
    {
        if (target == null || weaponHolder == null || weaponHolder.Current is not MeleeWeapon melee)
            return false;

        Vector3 origin = AttackOriginTransform.position;
        Vector3 targetPos = target.position;
        float distance = NavMeshChaseDriver.HorizontalDistance(origin, targetPos);

        if (distance <= melee.DamageOverlapRadius)
            return true;

        if (IsTargetPushPinned(target, melee)
            && distance <= melee.DamageOverlapRadius + pinEngagementSlack)
        {
            if (debugLogEngagement)
                Debug.Log(
                    $"[EntityAttackController] {name}: maintain melee engagement (pinned, dist={distance:F2}).",
                    this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when a melee swing can reach the target (approach-stop distance + cone).
    /// </summary>
    public bool CanStrikeMeleeTarget(Transform target)
    {
        if (target == null || weaponHolder == null || weaponHolder.Current is not MeleeWeapon melee)
            return false;

        Vector3 origin = AttackOriginTransform.position;
        Vector3 facing = GetFlatForward();
        return melee.IsTargetWithinDamageRange(origin, facing, target.position);
    }

    /// <summary>True when the equipped weapon can strike <paramref name="target"/> this swing.</summary>
    public bool CanStrikeTarget(Transform target) => IsInStrikeRange(target);

    public bool IsMeleeWeaponEquipped => weaponHolder?.Current is MeleeWeapon;

    /// <summary>NavMesh stopping distance while closing for a melee strike.</summary>
    public float MeleeApproachStopDistance =>
        weaponHolder?.Current is MeleeWeapon melee ? melee.ApproachStopDistanceFromTarget : 0f;

    /// <summary>NavMesh stopping distance while closing for a ranged strike.</summary>
    public float RangedApproachStopDistance =>
        weaponHolder?.Current is RangedWeapon ranged ? ranged.ApproachStopDistanceFromTarget : 0f;

    /// <summary>NavMesh stopping distance for the currently equipped weapon.</summary>
    public float ApproachStopDistance
    {
        get
        {
            if (weaponHolder?.Current is MeleeWeapon)
                return MeleeApproachStopDistance;
            if (weaponHolder?.Current is RangedWeapon)
                return RangedApproachStopDistance;
            return 0f;
        }
    }

    public bool IsRangedWeaponEquipped => weaponHolder?.Current is RangedWeapon;

    public bool IsTargetTooCloseForRanged(Transform target)
    {
        if (target == null || weaponHolder?.Current is not RangedWeapon ranged)
            return false;

        return ranged.IsTargetTooClose(AttackOriginTransform.position, target.position);
    }

    public bool IsTargetBeyondMaxRangedEngagement(Transform target)
    {
        if (target == null || weaponHolder?.Current is not RangedWeapon ranged)
            return true;

        return ranged.IsTargetBeyondMaxRange(AttackOriginTransform.position, target.position);
    }

    public float RangedRetreatStopDistanceFromTarget =>
        weaponHolder?.Current is RangedWeapon ranged ? ranged.RetreatStopDistanceFromTarget : 0f;

    /// <summary>
    /// True when melee should release to chase (beyond overlap and not geometry-pinned).
    /// </summary>
    public bool ShouldReleaseMeleeEngagement(Transform target) => !CanMaintainMeleeEngagement(target);

    /// <summary>
    /// True when the target has left melee engagement far enough to resume chasing.
    /// </summary>
    public bool IsTargetBeyondAttackEngagement(Transform target)
    {
        if (target == null || weaponHolder == null || weaponHolder.Current == null)
            return true;

        if (weaponHolder.Current is MeleeWeapon)
            return ShouldReleaseMeleeEngagement(target);

        if (weaponHolder.Current is RangedWeapon)
            return IsTargetBeyondMaxRangedEngagement(target);

        return !IsTargetInAttackRange(target);
    }

    bool IsTargetPushPinned(Transform target, MeleeWeapon melee)
    {
        if (melee.PushbackDistance <= 0f)
            return false;

        Vector3 pushDir = GetFlatForward();
        float resistance = PushbackGeometryProbe.ResolvePushbackResistance(target);
        float probeDist = melee.PushbackDistance * (1f - resistance);
        LayerMask blockLayers = PushbackGeometryProbe.ResolveBlockLayers(target);

        return PushbackGeometryProbe.IsPushPinned(
            target,
            pushDir,
            probeDist,
            blockLayers,
            pinnedTravelThreshold);
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

        if (!IsInStrikeRange(target))
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

        if (!IsInStrikeRange(target))
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

    bool IsInStrikeRange(Transform target)
    {
        if (weaponHolder?.Current is MeleeWeapon)
            return CanStrikeMeleeTarget(target);

        return IsTargetInAttackRange(target);
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
        if (target == null || IsCombatFacingLocked)
            return;

        SnapFacingToward(target.position);
    }

    public void SyncNavAgentFacingLock(NavMeshAgent agent)
    {
        if (IsCombatFacingLocked)
        {
            if (_facingLockAgent != agent)
            {
                RestoreNavAgentFacingLock();
                _facingLockAgent = agent;
                if (agent != null)
                {
                    _agentUpdateRotationBeforeLock = agent.updateRotation;
                    agent.updateRotation = false;
                }
            }

            return;
        }

        RestoreNavAgentFacingLock();
    }

    void RestoreNavAgentFacingLock()
    {
        if (_facingLockAgent != null)
            _facingLockAgent.updateRotation = _agentUpdateRotationBeforeLock;

        _facingLockAgent = null;
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
