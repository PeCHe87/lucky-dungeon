using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Discovers colliders in a horizontal-range (XZ) cylinder via broad-phase box overlap, filters by tag(s) and layers,
/// requires line-of-sight, and resolves the nearest visible target.
/// Once the player engages a target in melee, <see cref="TryGetNearestTransform"/> keeps that target until it leaves
/// melee damage range, dies, or the player moves/dashes.
/// </summary>
[DefaultExecutionOrder(99)]
public class NearestTargetQuery : MonoBehaviour
{
    struct Candidate : IComparable<Candidate>
    {
        public Collider Collider;
        public Transform TargetRoot;
        public int Tier;
        public float DistSq;

        public int CompareTo(Candidate other) => DetectionTargetRank.Compare(Tier, DistSq, other.Tier, other.DistSq);
    }

    [Tooltip("World-space origin for planar (XZ) range and overlap center. Uses this transform if unset.")]
    [SerializeField] Transform queryOrigin;
    [SerializeField] float radius = 8f;
    [Tooltip("Non-empty entries must be tags defined in the Unity Tag Manager. Colliders matching any listed tag are candidates. Earlier tags outrank later tags; distance breaks ties within the same tag.")]
    [SerializeField] string[] targetTags;
    [SerializeField, HideInInspector, FormerlySerializedAs("targetTag")]
    string legacyTargetTag;
    [Tooltip("Layers considered by the overlap broad-phase.")]
    [SerializeField] LayerMask discoveryLayers = ~0;
    [SerializeField] QueryTriggerInteraction overlapQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("Max hits for OverlapBoxNonAlloc; increase if many colliders overlap the query volume.")]
    [SerializeField, Min(1)] int overlapMaxHits = 64;
    [Tooltip("Vertical half-extent (world Y) of the discovery box. XZ half-extents match radius.")]
    [SerializeField, Min(0.01f)] float verticalHalfExtent = 10f;

    [Header("Line of sight")]
    [Tooltip("Local offset from the query origin transform for the ray start (e.g. eye height).")]
    [SerializeField] Vector3 losOriginOffset = new Vector3(0f, 1.5f, 0f);
    [Tooltip("Include blocking geometry and targets so the first hit is authoritative.")]
    [SerializeField] LayerMask losLayers = ~0;
    [SerializeField] QueryTriggerInteraction losQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [Tooltip("Colliders on this transform or its children are ignored during discovery and line-of-sight. Defaults to this GameObject.")]
    [SerializeField] Transform colliderIgnoreRoot;
    [Tooltip("LOS rays to highest-priority targets (tier 0) pass through these layers (e.g. Destructible).")]
    [SerializeField] LayerMask entityLosPenetrateLayers;

    [Header("View cone")]
    [Tooltip("When enabled, only targets inside the vision cone count as detected unless Omnidirectional Fallback is enabled.")]
    [SerializeField] bool requireTargetsInViewCone;
    [Tooltip("When Require is off: prefer the nearest visible target in the vision cone; if none, fall back to nearest visible anywhere.")]
    [SerializeField] bool prioritizeFrontTargets;
    [Tooltip("Total angle in degrees for the forward vision cone in the XZ plane. 360 = no angle restriction.")]
    [SerializeField, Range(1f, 360f)] float frontViewAngle = 90f;
    [Tooltip("Optional transform to use for forward direction. If unset, uses Query Origin (or this transform).")]
    [SerializeField] Transform facingRoot;

    [Header("Omnidirectional fallback")]
    [Tooltip("When the view-cone pass finds nothing, re-query at Omnidirectional Radius with no angle filter.")]
    [SerializeField] bool enableOmnidirectionalFallback;
    [Tooltip("XZ search radius for the 360° fallback pass when the cone pass fails.")]
    [SerializeField, Min(0.01f)] float omnidirectionalRadius = 12f;

    [Header("Target stickiness")]
    [Tooltip("Keeps the current target when the cone pass is empty so omnidirectional fallback cannot retarget behind the player.")]
    [SerializeField] bool enableTargetStickiness = true;
    [Tooltip("Max XZ distance to keep a sticky target. 0 = use Omnidirectional Radius when fallback is enabled, else Radius.")]
    [SerializeField, Min(0f)] float stickyMaxRange;
    [Tooltip("While a melee attack clip is playing (or weapon attack-active), hold sticky and block cone/omni from retargeting.")]
    [SerializeField] bool holdStickyDuringMeleeAnimation = true;
    [Tooltip("If unset, resolved on this GameObject in Awake.")]
    [SerializeField] WeaponHolder weaponHolder;
    [Tooltip("If unset, resolved on this GameObject in Awake.")]
    [SerializeField] PlayerEntityStateAnimator meleeAnimator;
    [Tooltip("While melee combat targeting is locked, skip overlap/LOS queries and return the latched attack focus target only.")]
    [SerializeField] bool suspendDetectionDuringMeleeAnimation = true;
    [Tooltip("After melee combat targeting unlocks, keep detection suspended for this many seconds.")]
    [SerializeField, Min(0f)] float postAttackDetectionHoldSeconds = 0.5f;
    [Tooltip("While attack input is held during post-attack hold, extend hold by this many seconds from now.")]
    [SerializeField, Min(0f)] float heldAttackHoldBonusSeconds = 0.15f;

    [Header("Melee engagement")]
    [Tooltip("Implements IMoveIntentProvider. If unset, uses first on this GameObject.")]
    [SerializeField] MonoBehaviour moveIntentProvider;
    [Tooltip("Implements IDashIntentProvider. If unset, uses first on this GameObject.")]
    [SerializeField] MonoBehaviour dashIntentProvider;
    [Tooltip("If unset, resolved on this GameObject in Awake.")]
    [SerializeField] PlayerAttackController attackController;
    [SerializeField, Min(0f)] float moveCancelDeadzone = 0.08f;
    [SerializeField] bool clearEngagedOnDash = true;
    [Tooltip("Extra XZ distance beyond melee damage radius before engagement clears (reduces lunge flicker).")]
    [SerializeField, Min(0f)] float engagedReleaseRadiusBuffer = 0.2f;

    [Header("Debug gizmos")]
    [Tooltip("When selected in Play Mode, draws a wire sphere on the collider bounds of the current nearest visible target.")]
    [SerializeField] bool drawDetectedTargetGizmo = true;
    [SerializeField] bool drawLosDebug;

    Collider[] _overlapBuffer;
    readonly List<Candidate> _candidates = new List<Candidate>(32);
    Collider _debugLosWinner;
    Transform _stickyTarget;
    Collider _stickyCollider;
    Transform _frozenCombatTarget;
    bool _wasDetectionSuspended;
    Transform _engagedCombatTarget;
    float _postAttackHoldExpireTime;
    bool _wasMeleeCombatTargetingLocked;

    IMoveIntentProvider _moveProvider;
    IDashIntentProvider _dashProvider;

    float _defaultRadius;
    float _defaultOmnidirectionalRadius;

    void Awake()
    {
        TryMigrateLegacyTargetTag();
        EnsureColliderIgnoreRoot();
        _overlapBuffer = new Collider[overlapMaxHits];
        _defaultRadius = radius;
        _defaultOmnidirectionalRadius = omnidirectionalRadius;
        if (entityLosPenetrateLayers.value == 0)
            entityLosPenetrateLayers = LayerMask.GetMask("Destructible");
        if (weaponHolder == null)
            weaponHolder = GetComponent<WeaponHolder>();
        if (meleeAnimator == null)
            meleeAnimator = GetComponent<PlayerEntityStateAnimator>();
        if (attackController == null)
            attackController = GetComponent<PlayerAttackController>();
        ResolveInputProviders();
        ApplyWeaponDetectionRadii();
    }

    void OnEnable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged += ApplyWeaponDetectionRadii;
    }

    void OnDisable()
    {
        if (weaponHolder != null)
            weaponHolder.EquippedWeaponChanged -= ApplyWeaponDetectionRadii;
    }

    /// <summary>Sets active discovery radii (typically from the equipped weapon).</summary>
    public void SetDetectionRadii(float detectionRadius, float omnidirectionalDetectionRadius)
    {
        radius = Mathf.Max(0.01f, detectionRadius);
        if (enableOmnidirectionalFallback)
            omnidirectionalRadius = Mathf.Max(0.01f, omnidirectionalDetectionRadius);
    }

    void ApplyWeaponDetectionRadii()
    {
        if (weaponHolder != null
            && weaponHolder.Current is MonoBehaviour weaponBehaviour
            && weaponBehaviour is IWeaponTargetDetection detection
            && detection.TargetDetectionRadius > 0f
            && detection.OmnidirectionalDetectionRadius > 0f)
        {
            SetDetectionRadii(detection.TargetDetectionRadius, detection.OmnidirectionalDetectionRadius);
            return;
        }

        SetDetectionRadii(_defaultRadius, _defaultOmnidirectionalRadius);
    }

    void LateUpdate()
    {
        UpdatePostAttackHoldTimer();
        TryInvalidateDeadCombatTargets();

        if (_engagedCombatTarget == null)
            return;

        if (TryCancelEngagedFromPlayerInput())
            ClearEngagedCombatTarget();
        else if (!IsEngagedCombatTargetWithinMeleeRange(forRelease: true))
            ClearEngagedCombatTarget();
    }

    void Reset()
    {
        if (queryOrigin == null)
            queryOrigin = transform;
        if (colliderIgnoreRoot == null)
            colliderIgnoreRoot = transform;
        if (entityLosPenetrateLayers.value == 0)
            entityLosPenetrateLayers = LayerMask.GetMask("Destructible");
    }

    void EnsureColliderIgnoreRoot()
    {
        if (colliderIgnoreRoot == null)
            colliderIgnoreRoot = transform;
    }

    void OnValidate()
    {
        TryMigrateLegacyTargetTag();
        if (overlapMaxHits < 1)
            overlapMaxHits = 1;
        if (_overlapBuffer == null || _overlapBuffer.Length != overlapMaxHits)
            _overlapBuffer = new Collider[Mathf.Max(1, overlapMaxHits)];
        if (enableOmnidirectionalFallback && omnidirectionalRadius <= 0f)
            omnidirectionalRadius = 0.01f;
    }

    Transform OriginTransform => queryOrigin != null ? queryOrigin : transform;

    Vector3 PlanarOrigin => OriginTransform.position;

    Vector3 LosOriginWorld => OriginTransform.TransformPoint(losOriginOffset);

    /// <summary>World-space origin used for detection overlap and LOS queries.</summary>
    public Transform QueryOriginTransform => OriginTransform;

    /// <summary>Primary XZ detection radius used for discovery and grace-range fallback.</summary>
    public float DetectionRadius => radius;

    /// <summary>360° fallback detection radius (updated when the equipped weapon changes).</summary>
    public float OmnidirectionalDetectionRadius => omnidirectionalRadius;

    public bool HasEngagedCombatTarget => _engagedCombatTarget != null;

    /// <summary>Locks combat targeting on <paramref name="target"/> until release rules clear it.</summary>
    public void EngageCombatTarget(Transform target)
    {
        if (target == null)
            return;

        Transform normalized = NormalizeEngagedTransform(target);
        if (!IsCombatTargetAlive(normalized))
            return;

        _engagedCombatTarget = normalized;
        SyncStickyAndFrozenFromEngaged();
    }

    public void ClearEngagedCombatTarget()
    {
        _engagedCombatTarget = null;
        _stickyTarget = null;
        _stickyCollider = null;
        _frozenCombatTarget = null;
    }

    public bool TryGetEngagedCombatTarget(out Transform target)
    {
        target = null;
        if (_engagedCombatTarget == null)
            return false;

        if (!IsCombatTargetAlive(_engagedCombatTarget))
        {
            ClearEngagedCombatTarget();
            return false;
        }

        if (!IsEngagedCombatTargetWithinMeleeRange(forRelease: false))
        {
            ClearEngagedCombatTarget();
            return false;
        }

        target = _engagedCombatTarget;
        return true;
    }

    /// <summary>
    /// Returns the nearest transform (by horizontal XZ distance from the planar origin) that passes target tag(s), range, LOS,
    /// and optionally the view cone when view-cone filtering is active.
    /// Target stickiness applies only here, not in <see cref="CollectTargets"/>.
    /// </summary>
    bool IsMeleeCombatTargetingLocked =>
        meleeAnimator != null && meleeAnimator.IsMeleeCombatTargetingLocked;

    /// <summary>True while melee targeting or post-attack hold blocks geometric retargeting.</summary>
    public bool IsDetectionSuspended =>
        suspendDetectionDuringMeleeAnimation
        && (IsMeleeCombatTargetingLocked || Time.time < _postAttackHoldExpireTime);

    /// <summary>Returns the latched combat focus target used while <see cref="IsDetectionSuspended"/>.</summary>
    public bool TryGetFrozenCombatTarget(out Transform target)
    {
        target = null;
        if (_frozenCombatTarget == null)
            return false;
        if (!IsCombatTargetAlive(_frozenCombatTarget))
        {
            ClearFrozenAndStickyTargets();
            return false;
        }

        target = _frozenCombatTarget;
        return true;
    }

    public bool TryGetNearestTransform(out Transform nearest)
    {
        _debugLosWinner = null;
        nearest = null;

        UpdatePostAttackHoldTimer();
        UpdateDetectionSuspensionState();
        TryInvalidateDeadCombatTargets();

        if (IsDetectionSuspended && TryGetFrozenCombatTarget(out nearest))
        {
            TryResolveFrozenColliderForDebug();
            return true;
        }

        Vector3 planar = PlanarOrigin;
        BuildSortedCandidates(GetEffectiveQueryRadius());

        if (TryGetEngagedCombatTarget(out Transform engaged))
        {
            if (TryFindHigherPriorityTarget(engaged, planar, viewConeOnly: false, out Transform higher))
            {
                ClearEngagedCombatTarget();
                nearest = higher;
                OnPickSuccess(nearest);
                return true;
            }

            nearest = engaged;
            CommitEngagedAsCurrentTarget(nearest);
            return true;
        }

        if (enableTargetStickiness
            && _stickyTarget != null
            && TryFindHigherPriorityTarget(_stickyTarget, planar, viewConeOnly: false, out Transform preempt))
        {
            nearest = preempt;
            OnPickSuccess(nearest);
            return true;
        }

        if (!UsesViewConeFilter())
        {
            if (_candidates.Count == 0)
            {
                OnQueryFailed();
                return false;
            }

            if (TryPickNearestByPriority(planar, viewConeOnly: false, out nearest))
            {
                OnPickSuccess(nearest);
                return true;
            }

            OnQueryFailed();
            return false;
        }

        if (enableTargetStickiness
            && ShouldHoldCombatTarget()
            && TryReturnStickyTarget(out nearest, lenient: true))
            return true;

        if (_candidates.Count > 0
            && TryPickNearestByPriority(planar, viewConeOnly: true, out nearest))
        {
            if (enableTargetStickiness
                && ShouldHoldCombatTarget()
                && _stickyTarget != null
                && !IsSameTargetTransform(nearest, _stickyTarget)
                && TryReturnStickyTarget(out nearest, lenient: true))
                return true;

            if (TryFindHigherPriorityTarget(nearest, planar, viewConeOnly: false, out Transform higher))
                nearest = higher;

            OnPickSuccess(nearest);
            return true;
        }

        if (enableTargetStickiness && TryReturnStickyTarget(out nearest))
        {
            if (TryFindHigherPriorityTarget(nearest, planar, viewConeOnly: false, out Transform higher))
            {
                nearest = higher;
                OnPickSuccess(nearest);
            }

            return true;
        }

        if (enableOmnidirectionalFallback)
        {
            BuildSortedCandidates(omnidirectionalRadius);
            if (_candidates.Count > 0
                && TryPickNearestByPriority(planar, viewConeOnly: false, out nearest))
            {
                OnPickSuccess(nearest);
                return true;
            }
        }

        if (prioritizeFrontTargets && !requireTargetsInViewCone)
        {
            if (enableOmnidirectionalFallback)
                BuildSortedCandidates(GetEffectiveQueryRadius());
            if (_candidates.Count > 0
                && TryPickNearestByPriority(planar, viewConeOnly: false, out nearest))
            {
                OnPickSuccess(nearest);
                return true;
            }
        }

        OnQueryFailed();
        return false;
    }

    /// <summary>Clears the sticky target lock (e.g. on room transition).</summary>
    public void ClearStickyTarget()
    {
        _stickyTarget = null;
        _stickyCollider = null;
        _frozenCombatTarget = null;
        ClearEngagedCombatTarget();
    }

    void UpdatePostAttackHoldTimer()
    {
        if (!suspendDetectionDuringMeleeAnimation)
            return;

        bool locked = IsMeleeCombatTargetingLocked;
        if (locked)
        {
            _wasMeleeCombatTargetingLocked = true;
            return;
        }

        if (_wasMeleeCombatTargetingLocked)
        {
            if (HasValidLatchedCombatTarget())
                _postAttackHoldExpireTime = Time.time + postAttackDetectionHoldSeconds;
            _wasMeleeCombatTargetingLocked = false;
        }

        if (Time.time < _postAttackHoldExpireTime
            && HasValidLatchedCombatTarget()
            && attackController != null
            && attackController.IsAttackInputHeld
            && heldAttackHoldBonusSeconds > 0f)
        {
            float desiredEnd = Time.time + postAttackDetectionHoldSeconds + heldAttackHoldBonusSeconds;
            if (desiredEnd > _postAttackHoldExpireTime)
                _postAttackHoldExpireTime = desiredEnd;
        }
    }

    void UpdateDetectionSuspensionState()
    {
        bool suspended = IsDetectionSuspended;
        if (suspended && !_wasDetectionSuspended)
        {
            if (_engagedCombatTarget != null)
                _frozenCombatTarget = _engagedCombatTarget;
            else if (_stickyTarget != null)
                _frozenCombatTarget = _stickyTarget;
        }
        else if (!suspended && _wasDetectionSuspended)
            _frozenCombatTarget = null;

        _wasDetectionSuspended = suspended;
    }

    void TryResolveFrozenColliderForDebug()
    {
        if (_frozenCombatTarget == null)
            return;
        Transform previousSticky = _stickyTarget;
        _stickyTarget = _frozenCombatTarget;
        if (TryResolveStickyCollider(out Collider col))
            _debugLosWinner = col;
        _stickyTarget = previousSticky;
    }

    /// <summary>True while melee swing or short weapon attack-active window should freeze sticky retargeting.</summary>
    public bool ShouldHoldCombatTarget()
    {
        if (HasEngagedCombatTarget)
            return true;

        if (weaponHolder != null
            && weaponHolder.Current is IAttackActivity activity
            && activity.IsAttackActive)
            return true;

        return holdStickyDuringMeleeAnimation
            && meleeAnimator != null
            && meleeAnimator.IsMeleeAttackClipPlaying;
    }

    float GetStickyMaxRange()
    {
        if (stickyMaxRange > 0f)
            return stickyMaxRange;
        return enableOmnidirectionalFallback ? omnidirectionalRadius : radius;
    }

    bool TryReturnStickyTarget(out Transform nearest, bool lenient = false)
    {
        nearest = null;
        if (!IsStickyTargetValid(lenient))
            return false;

        nearest = _stickyTarget;
        _debugLosWinner = _stickyCollider;
        return true;
    }

    bool IsStickyTargetValid(bool lenient = false)
    {
        if (!enableTargetStickiness || _stickyTarget == null)
            return false;

        if (!_stickyTarget.gameObject.activeInHierarchy)
        {
            ClearStickyTarget();
            return false;
        }

        if (!IsCombatTargetAlive(_stickyTarget))
        {
            ClearStickyTarget();
            return false;
        }

        if (!TryResolveStickyCollider(out Collider col))
        {
            ClearStickyTarget();
            return false;
        }

        Vector3 planar = PlanarOrigin;
        if (!NavMeshChaseDriver.IsWithinXZRadius(planar, _stickyTarget.position, GetStickyMaxRange()))
        {
            ClearStickyTarget();
            return false;
        }

        if (!lenient && !HasLineOfSightForTier(col, ResolveTagTier(col)))
        {
            ClearStickyTarget();
            return false;
        }

        _stickyCollider = col;
        return true;
    }

    public static bool AreSameCombatEntity(Transform a, Transform b) => IsSameTargetTransform(a, b);

    static bool IsSameTargetTransform(Transform a, Transform b)
    {
        if (a == b)
            return true;
        if (a == null || b == null)
            return false;
        return a.IsChildOf(b) || b.IsChildOf(a);
    }

    bool TryResolveStickyCollider(out Collider col)
    {
        col = null;
        if (_stickyTarget == null)
            return false;

        if (_stickyCollider != null
            && _stickyCollider.enabled
            && MatchesTargetTags(_stickyCollider)
            && (_stickyCollider.transform == _stickyTarget || _stickyCollider.transform.IsChildOf(_stickyTarget)))
        {
            col = _stickyCollider;
            return true;
        }

        Collider[] colliders = _stickyTarget.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || !c.enabled || !MatchesTargetTags(c))
                continue;
            _stickyCollider = c;
            col = c;
            return true;
        }

        return false;
    }

    void SetStickyTarget(Transform target, Collider collider)
    {
        _stickyTarget = target;
        _stickyCollider = collider;
    }

    void OnPickSuccess(Transform target)
    {
        if (target == null)
            return;
        _frozenCombatTarget = target;
        if (!enableTargetStickiness)
            return;
        SetStickyTarget(target, _debugLosWinner);
    }

    void OnQueryFailed()
    {
        if (!enableTargetStickiness)
            return;
        if (HasEngagedCombatTarget || (ShouldHoldCombatTarget() && _stickyTarget != null))
            return;
        ClearStickyTarget();
    }

    void CommitEngagedAsCurrentTarget(Transform engaged)
    {
        _frozenCombatTarget = engaged;
        if (!enableTargetStickiness)
            return;

        Transform previousSticky = _stickyTarget;
        _stickyTarget = engaged;
        if (TryResolveStickyCollider(out Collider col))
            _debugLosWinner = col;
        else
            _stickyTarget = previousSticky;
    }

    void SyncStickyAndFrozenFromEngaged()
    {
        if (_engagedCombatTarget == null)
            return;
        CommitEngagedAsCurrentTarget(_engagedCombatTarget);
    }

    bool TryInvalidateDeadCombatTargets()
    {
        if (_engagedCombatTarget != null)
        {
            if (IsCombatTargetAlive(_engagedCombatTarget))
                return false;

            ClearEngagedCombatTarget();
            CancelPostAttackHoldOnDeath();
            return true;
        }

        if (_frozenCombatTarget != null && !IsCombatTargetAlive(_frozenCombatTarget))
        {
            ClearFrozenAndStickyTargets();
            CancelPostAttackHoldOnDeath();
            return true;
        }

        if (_stickyTarget != null && !IsCombatTargetAlive(_stickyTarget))
        {
            _stickyTarget = null;
            _stickyCollider = null;
            CancelPostAttackHoldOnDeath();
            return true;
        }

        return false;
    }

    void CancelPostAttackHoldOnDeath()
    {
        _postAttackHoldExpireTime = 0f;
        _wasMeleeCombatTargetingLocked = false;
    }

    void ClearFrozenAndStickyTargets()
    {
        _frozenCombatTarget = null;
        _stickyTarget = null;
        _stickyCollider = null;
    }

    static bool IsCombatTargetAlive(Transform target)
    {
        if (target == null)
            return false;
        if (!target.gameObject.activeInHierarchy)
            return false;

        Transform current = target;
        while (current != null)
        {
            if (current.TryGetComponent(out IDefeatable defeatable) && defeatable.IsDefeated)
                return false;
            current = current.parent;
        }

        return true;
    }

    bool HasValidLatchedCombatTarget()
    {
        if (_engagedCombatTarget != null && IsCombatTargetAlive(_engagedCombatTarget))
            return true;
        if (_frozenCombatTarget != null && IsCombatTargetAlive(_frozenCombatTarget))
            return true;
        if (_stickyTarget != null && IsCombatTargetAlive(_stickyTarget))
            return true;
        return false;
    }

    bool TryCancelEngagedFromPlayerInput()
    {
        ResolveInputProviders();
        if (_moveProvider != null)
        {
            Vector2 intent = _moveProvider.GetMoveIntent();
            float deadzone = moveCancelDeadzone;
            if (intent.sqrMagnitude > deadzone * deadzone)
                return true;
        }

        return clearEngagedOnDash
            && _dashProvider != null
            && _dashProvider.WasDashPressedThisFrame();
    }

    bool IsEngagedCombatTargetWithinMeleeRange(bool forRelease)
    {
        if (_engagedCombatTarget == null)
            return false;

        float meleeRadius = GetMeleeDamageRadius();
        if (meleeRadius <= 0f)
            return true;

        float limit = meleeRadius + (forRelease ? engagedReleaseRadiusBuffer : 0f);
        Vector3 origin = GetMeleeRangeOrigin();
        Vector3 targetPoint = GetEngagedTargetWorldPoint(_engagedCombatTarget);
        return NavMeshChaseDriver.IsWithinXZRadius(origin, targetPoint, limit);
    }

    float GetMeleeDamageRadius()
    {
        if (weaponHolder != null && weaponHolder.Current is MeleeWeapon melee)
            return melee.DamageOverlapRadius;
        return 0f;
    }

    Vector3 GetMeleeRangeOrigin()
    {
        if (attackController != null)
            return attackController.AttackOriginTransform.position;
        return PlanarOrigin;
    }

    Vector3 GetEngagedTargetWorldPoint(Transform engaged)
    {
        Transform previousSticky = _stickyTarget;
        _stickyTarget = engaged;
        if (TryResolveStickyCollider(out Collider col))
        {
            Vector3 point = col.bounds.center;
            _stickyTarget = previousSticky;
            return point;
        }

        _stickyTarget = previousSticky;
        return engaged.position;
    }

    static Transform NormalizeEngagedTransform(Transform source)
    {
        if (source == null)
            return null;

        Transform best = source;
        Transform current = source;
        while (current != null)
        {
            if (HasDamageableOnTransform(current))
                best = current;
            current = current.parent;
        }

        return best;
    }

    static bool HasDamageableOnTransform(Transform tr)
    {
        MonoBehaviour[] components = tr.GetComponents<MonoBehaviour>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] is IDamageable)
                return true;
        }

        return false;
    }

    void ResolveInputProviders()
    {
        if (_moveProvider == null)
        {
            if (moveIntentProvider != null)
                _moveProvider = moveIntentProvider as IMoveIntentProvider;
            if (_moveProvider == null)
                _moveProvider = GetComponent<IMoveIntentProvider>();
        }

        if (_dashProvider == null)
        {
            if (dashIntentProvider != null)
                _dashProvider = dashIntentProvider as IDashIntentProvider;
            if (_dashProvider == null)
                _dashProvider = GetComponent<IDashIntentProvider>();
        }
    }

    bool UsesViewConeFilter() =>
        frontViewAngle < 360f &&
        (requireTargetsInViewCone || prioritizeFrontTargets);

    float GetEffectiveQueryRadius()
    {
        if (enableOmnidirectionalFallback && omnidirectionalRadius > radius)
            return omnidirectionalRadius;
        return radius;
    }

    bool TryFindHigherPriorityTarget(
        Transform current,
        Vector3 planarOrigin,
        bool viewConeOnly,
        out Transform higher)
    {
        higher = null;
        if (current == null || _candidates.Count == 0)
            return false;

        int currentTier = ResolveTagTierForTransform(current);
        if (currentTier <= 0)
            return false;

        for (int tier = 0; tier < currentTier; tier++)
        {
            if (!TryPickNearestInTier(tier, planarOrigin, viewConeOnly, out Transform pick))
                continue;
            if (IsSameTargetTransform(pick, current))
                continue;

            higher = pick;
            return true;
        }

        return false;
    }

    int ResolveTagTierForTransform(Transform target)
    {
        if (target == null || targetTags == null)
            return -1;

        int bestTier = -1;
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled)
                continue;

            int tier = ResolveTagTier(col);
            if (tier < 0)
                continue;
            if (bestTier < 0 || tier < bestTier)
                bestTier = tier;
        }

        if (bestTier >= 0)
            return bestTier;

        Transform current = target;
        while (current != null)
        {
            for (int i = 0; i < targetTags.Length; i++)
            {
                string tag = targetTags[i];
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                if (!current.CompareTag(tag))
                    continue;
                if (bestTier < 0 || i < bestTier)
                    bestTier = i;
            }

            current = current.parent;
        }

        return bestTier;
    }

    bool TryPickNearestByPriority(Vector3 planarOrigin, bool viewConeOnly, out Transform nearest)
    {
        nearest = null;
        int tierCount = targetTags != null ? targetTags.Length : 0;
        for (int tier = 0; tier < tierCount; tier++)
        {
            if (TryPickNearestInTier(tier, planarOrigin, viewConeOnly, out nearest))
                return true;
        }

        return false;
    }

    bool TryPickNearestInTier(int tier, Vector3 planarOrigin, bool viewConeOnly, out Transform nearest)
    {
        nearest = null;
        float bestDistSq = float.MaxValue;
        Collider bestCollider = null;

        for (int i = 0; i < _candidates.Count; i++)
        {
            Candidate candidate = _candidates[i];
            if (candidate.Tier != tier)
                continue;

            Collider c = candidate.Collider;
            Transform targetRoot = candidate.TargetRoot != null ? candidate.TargetRoot : c.transform;
            if (!IsCombatTargetAlive(targetRoot))
                continue;
            if (!HasLineOfSightForTier(c, tier))
                continue;
            if (viewConeOnly && !IsPointWithinFrontCone(planarOrigin, targetRoot.position))
                continue;
            if (candidate.DistSq >= bestDistSq)
                continue;

            bestDistSq = candidate.DistSq;
            bestCollider = c;
            nearest = targetRoot;
        }

        if (bestCollider == null)
            return false;

        _debugLosWinner = bestCollider;
        return true;
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with transforms in tag priority tier order, then increasing XZ distance.
    /// Within each tier, only targets with obstacle LOS (lower-priority targets do not block) are included.
    /// Does not apply target stickiness; use <see cref="TryGetNearestTransform"/> for locked combat targeting.
    /// </summary>
    public int CollectTargets(List<Transform> buffer, bool clearList = true)
    {
        if (buffer == null)
            throw new ArgumentNullException(nameof(buffer));
        _debugLosWinner = null;
        if (clearList)
            buffer.Clear();

        BuildSortedCandidates(GetEffectiveQueryRadius());
        Vector3 planar = PlanarOrigin;
        int added = 0;
        int tierCount = targetTags != null ? targetTags.Length : 0;
        for (int tier = 0; tier < tierCount; tier++)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate candidate = _candidates[i];
                if (candidate.Tier != tier)
                    continue;

                Collider c = candidate.Collider;
                Transform targetRoot = candidate.TargetRoot != null ? candidate.TargetRoot : c.transform;
                if (!HasLineOfSightForTier(c, tier))
                    continue;
                if (requireTargetsInViewCone && !IsPointWithinFrontCone(planar, targetRoot.position))
                    continue;
                buffer.Add(targetRoot);
                added++;
            }
        }

        return added;
    }

    void TryMigrateLegacyTargetTag()
    {
        if (string.IsNullOrEmpty(legacyTargetTag))
            return;
        if (HasAnyTargetTagConfigured())
        {
            legacyTargetTag = "";
            return;
        }

        targetTags = new[] { legacyTargetTag };
        legacyTargetTag = "";
    }

    bool HasAnyTargetTagConfigured()
    {
        if (targetTags == null || targetTags.Length == 0)
            return false;
        for (int i = 0; i < targetTags.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(targetTags[i]))
                return true;
        }

        return false;
    }

    bool MatchesTargetTags(Collider c)
    {
        return ResolveTagTier(c) >= 0;
    }

    int ResolveTagTier(Collider c)
    {
        if (c == null || targetTags == null)
            return -1;

        int bestTier = -1;
        Transform current = c.transform;
        while (current != null)
        {
            for (int i = 0; i < targetTags.Length; i++)
            {
                string tag = targetTags[i];
                if (string.IsNullOrWhiteSpace(tag))
                    continue;
                if (!current.CompareTag(tag))
                    continue;
                if (bestTier < 0 || i < bestTier)
                    bestTier = i;
            }

            current = current.parent;
        }

        return bestTier;
    }

    static Transform ResolveTargetRoot(Collider c)
    {
        if (c == null)
            return null;

        var health = c.GetComponentInParent<CombatEntityHealth>();
        if (health != null)
            return health.transform;

        var destructible = c.GetComponentInParent<BaseDestructibleObject>();
        if (destructible != null)
            return destructible.transform;

        return c.transform;
    }

    void BuildSortedCandidates(float searchRadius)
    {
        TryMigrateLegacyTargetTag();
        _candidates.Clear();
        if (searchRadius <= 0f || !HasAnyTargetTagConfigured())
            return;

        Vector3 center = PlanarOrigin;
        Vector3 halfExtents = new Vector3(searchRadius, verticalHalfExtent, searchRadius);
        int count = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _overlapBuffer,
            Quaternion.identity,
            discoveryLayers,
            overlapQueryTriggerInteraction);

        Vector3 planar = PlanarOrigin;
        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapBuffer[i];
            if (c == null)
                continue;
            if (colliderIgnoreRoot != null &&
                (c.transform == colliderIgnoreRoot || c.transform.IsChildOf(colliderIgnoreRoot)))
                continue;
            if (!MatchesTargetTags(c))
                continue;
            Transform targetRoot = ResolveTargetRoot(c);
            if (targetRoot == null)
                continue;
            if (!NavMeshChaseDriver.IsWithinXZRadius(planar, targetRoot.position, searchRadius))
                continue;
            if (!IsCombatTargetAlive(targetRoot))
                continue;

            int tier = ResolveTagTier(c);
            if (tier < 0)
                continue;

            float distSq = NavMeshChaseDriver.FlatDistanceSq(planar, targetRoot.position);
            UpsertCandidate(c, targetRoot, tier, distSq);
        }

        if (_candidates.Count > 1)
            _candidates.Sort();
    }

    void UpsertCandidate(Collider c, Transform targetRoot, int tier, float distSq)
    {
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].TargetRoot != targetRoot)
                continue;
            if (!DetectionTargetRank.IsBetter(tier, distSq, _candidates[i].Tier, _candidates[i].DistSq))
                return;
            _candidates[i] = new Candidate { Collider = c, TargetRoot = targetRoot, Tier = tier, DistSq = distSq };
            return;
        }

        _candidates.Add(new Candidate { Collider = c, TargetRoot = targetRoot, Tier = tier, DistSq = distSq });
    }

    bool HasLineOfSightForTier(Collider candidate, int candidateTier)
    {
        EnsureColliderIgnoreRoot();

        return LineOfSightProbe.HasLineOfSight(
            LosOriginWorld,
            candidate,
            colliderIgnoreRoot,
            losLayers,
            losQueryTriggerInteraction,
            entityLosPenetrateLayers,
            hit => IsLowerPriorityTargetHit(hit, candidateTier));
    }

    bool IsLowerPriorityTargetHit(Collider hit, int candidateTier)
    {
        int hitTier = ResolveTagTier(hit);
        return hitTier >= 0 && hitTier > candidateTier;
    }

    Transform FacingTransform => facingRoot != null ? facingRoot : OriginTransform;

    Vector3 GetForwardFlat()
    {
        Vector3 f = FacingTransform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f)
            f = Vector3.forward;
        return f.normalized;
    }

    bool IsPointWithinFrontCone(Vector3 origin, Vector3 worldPoint)
    {
        if (frontViewAngle >= 360f)
            return true;

        Vector3 to = worldPoint - origin;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return true;

        to.Normalize();
        float half = frontViewAngle * 0.5f;
        return Vector3.Angle(GetForwardFlat(), to) <= half + 1e-4f;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 planar = OriginTransform.position;
        NavMeshChaseDriver.DrawXZWireDisc(planar, radius, new Color(0.2f, 0.8f, 1f, 0.9f));

        if (enableOmnidirectionalFallback && omnidirectionalRadius > 0f)
            NavMeshChaseDriver.DrawXZWireDisc(planar, omnidirectionalRadius, new Color(1f, 0.55f, 0.15f, 0.85f));

        if (UsesViewConeFilter())
            DrawVisionConeWire(planar, GetForwardFlat(), radius, frontViewAngle, new Color(0.95f, 0.85f, 0.2f, 1f));

        if (!drawLosDebug && !drawDetectedTargetGizmo)
            return;

        if (!Application.isPlaying)
            return;

        if (_overlapBuffer == null || _overlapBuffer.Length != overlapMaxHits)
            _overlapBuffer = new Collider[Mathf.Max(1, overlapMaxHits)];

        TryGetNearestTransform(out _);

        if (_debugLosWinner == null)
            return;

        if (drawDetectedTargetGizmo)
        {
            Bounds b = _debugLosWinner.bounds;
            float indicatorRadius = Mathf.Max(Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)), 0.12f);
            Color previous = Gizmos.color;
            Gizmos.color = new Color(0.25f, 1f, 0.35f, 1f);
            Gizmos.DrawWireSphere(b.center, indicatorRadius);
            Gizmos.color = previous;
        }

        if (drawLosDebug)
        {
            Vector3 los = LosOriginWorld;
            Vector3 aim = _debugLosWinner.bounds.center;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(los, aim);
        }
    }

    static void DrawVisionConeWire(Vector3 origin, Vector3 forwardFlat, float radius, float totalAngleDeg, Color color)
    {
        if (radius <= 0f || totalAngleDeg <= 0f)
            return;

        forwardFlat.y = 0f;
        if (forwardFlat.sqrMagnitude < 1e-6f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float halfRad = totalAngleDeg * 0.5f * Mathf.Deg2Rad;
        float baseYaw = Mathf.Atan2(forwardFlat.x, forwardFlat.z);

        const int arcSegments = 48;
        Vector3 left = XZDirectionFromYaw(baseYaw - halfRad);
        Vector3 right = XZDirectionFromYaw(baseYaw + halfRad);

        Color prev = Gizmos.color;
        Gizmos.color = color;

        Gizmos.DrawLine(origin, origin + left * radius);
        Gizmos.DrawLine(origin, origin + right * radius);

        Vector3 prevPt = origin + left * radius;
        for (int i = 1; i <= arcSegments; i++)
        {
            float t = (float)i / arcSegments;
            float yaw = Mathf.Lerp(baseYaw - halfRad, baseYaw + halfRad, t);
            Vector3 p = origin + XZDirectionFromYaw(yaw) * radius;
            Gizmos.DrawLine(prevPt, p);
            prevPt = p;
        }

        Gizmos.color = prev;
    }

    static Vector3 XZDirectionFromYaw(float yaw)
    {
        return new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
    }
}
