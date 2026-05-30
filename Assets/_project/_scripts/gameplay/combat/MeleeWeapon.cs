using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Melee swing: overlap on animation hit event, cone filter, optional <see cref="IDamageable"/>.
/// <see cref="TryAttack"/> begins the swing (cooldown, lunge); damage runs when
/// <see cref="ApplyPendingDamage"/> is called from an animation event.
/// </summary>
public sealed class MeleeWeapon : MonoBehaviour, IWeapon, IWeaponEquippedPresentation, IAttackActivity
{
    [SerializeField] float damage = 10f;
    [SerializeField] float cooldown = 0.35f;
    [SerializeField] float range = 2.5f;
    [Tooltip("Moves the attacker forward in facing direction when the attack is processed.")]
    [SerializeField, Min(0f)] float moveForwardDistance;
    [Tooltip("Duration in seconds of the forward lunge. Distance / duration = lunge speed.")]
    [SerializeField, Min(0.01f)] float moveForwardDuration = 0.15f;
    [Tooltip("Total angle in degrees in the horizontal plane around facing. 360 = no cone limit.")]
    [SerializeField, Range(1f, 360f)] float coneAngle = 120f;
    [SerializeField] LayerMask hitLayers = ~0;
    [SerializeField, Min(1)] int maxTargetsPerSwing = 8;
    [SerializeField] QueryTriggerInteraction overlapQueryTriggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] UnityEvent onAttackPerformed;
    [Tooltip("How long <see cref=\"IAttackActivity.IsAttackActive\"/> stays true after a successful attack.")]
    [SerializeField, Min(0.01f)] float attackActiveDuration = 0.15f;
    [Header("Damage presentation")]
    [SerializeField] DamageElement damageElement = DamageElement.Physical;
    [SerializeField, Range(0f, 1f)] float criticalStrikeChance;
    [Header("Equipped presentation")]
    [Tooltip("Child object(s) with meshes/VFX to show only when this weapon is equipped. Do not use the GameObject with this script if that would disable attack logic.")]
    [SerializeField] GameObject[] equippedVisualRoots;

    [Header("Approach lunge")]
    [Tooltip("Lunge toward the detected target before the attack clip when outside melee range.")]
    [SerializeField] bool enableApproachLunge = true;
    [Tooltip("Stops the approach this far inside damage range (horizontal distance from target).")]
    [SerializeField, Min(0f)] float approachStopBuffer = 0.3f;
    [Tooltip("Max horizontal travel per approach lunge.")]
    [SerializeField, Min(0.01f)] float maxApproachLungeDistance = 6f;
    [SerializeField, Min(0.01f)] float approachLungeSpeed = 12f;

    [Header("Debug")]
    [SerializeField] bool logDamagePipeline;
    [SerializeField] bool drawDamageRadiusGizmo = true;

    float _cooldownRemaining;
    float _attackActiveTimer;
    Collider[] _overlapBuffer;
    TopDownCharacterMovement _cachedMovement;
    bool _movementResolved;
    bool _hasArmedContext;
    AttackContext _armedContext;
    bool _hasPendingHitContext;
    AttackContext _pendingHitContext;

    void Awake()
    {
        _overlapBuffer = new Collider[Mathf.Max(32, maxTargetsPerSwing * 4)];
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

    public bool IsAttackActive => _attackActiveTimer > 0f;

    public bool EnableApproachLunge => enableApproachLunge;

    /// <summary>Horizontal distance from target at which approach stops (matches in-range check).</summary>
    public float ApproachStopDistanceFromTarget => Mathf.Max(0f, range - approachStopBuffer);

    /// <summary>True when target is within horizontal melee reach (overlap radius); ignores cone.</summary>
    public bool IsTargetWithinDamageRadius(Vector3 origin, Vector3 targetWorldPos)
    {
        return NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos) <= ApproachStopDistanceFromTarget;
    }

    public bool IsTargetWithinDamageRange(Vector3 origin, Vector3 facingFlat, Vector3 targetWorldPos)
    {
        if (!IsTargetWithinDamageRadius(origin, targetWorldPos))
            return false;

        if (coneAngle >= 360f)
            return true;

        Vector3 to = targetWorldPos - origin;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return true;
        to.Normalize();
        float halfCone = coneAngle * 0.5f;
        return Vector3.Angle(facingFlat, to) <= halfCone + 0.01f;
    }

    /// <summary>True when an approach lunge should run before <see cref="TryBeginAttack"/>.</summary>
    public bool TryComputeApproachLunge(
        Vector3 origin,
        Vector3 targetWorldPos,
        out float stopDistanceFromTarget,
        out float maxTravel,
        out float speed)
    {
        stopDistanceFromTarget = ApproachStopDistanceFromTarget;
        maxTravel = maxApproachLungeDistance;
        speed = approachLungeSpeed;

        if (!enableApproachLunge)
            return false;

        if (IsTargetWithinDamageRadius(origin, targetWorldPos))
            return false;

        float dist = NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos);
        if (dist <= stopDistanceFromTarget)
            return false;

        float travel = dist - stopDistanceFromTarget;
        if (travel > maxTravel)
            travel = maxTravel;
        if (travel < 0.01f)
            return false;

        return true;
    }

    /// <summary>Duration in seconds for a computed approach lunge (travel / speed).</summary>
    public bool TryGetApproachLungeDuration(
        Vector3 origin,
        Vector3 targetWorldPos,
        out float duration)
    {
        duration = 0f;
        if (!TryComputeApproachLunge(origin, targetWorldPos, out float stopDistance, out float maxTravel, out float speed))
            return false;

        float dist = NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos);
        float travel = dist - stopDistance;
        if (travel > maxTravel)
            travel = maxTravel;
        duration = travel / speed;
        return duration > 0f;
    }

    void Update()
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.deltaTime;
        if (_attackActiveTimer > 0f)
            _attackActiveTimer -= Time.deltaTime;
    }

    /// <summary>Begins a melee swing: cooldown, lunge, arms context for the next clip start. No overlap yet.</summary>
    public bool TryAttack(in AttackContext ctx) => TryBeginAttack(in ctx);

    public bool TryBeginAttack(in AttackContext ctx)
    {
        if (ctx.attacker == null)
            return false;
        if (_cooldownRemaining > 0f)
            return false;

        _cooldownRemaining = cooldown;

        Vector3 forward = ctx.facing;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        bool skipForwardLunge = ctx.optionalTarget != null
            && IsTargetWithinDamageRadius(ctx.attacker.position, ctx.optionalTarget.position);
        if (!skipForwardLunge)
            TryMoveAttackerForward(ctx.attacker, forward);

        _armedContext = ctx;
        _hasArmedContext = true;
        _attackActiveTimer = attackActiveDuration;
        return true;
    }

    /// <summary>Called when an attack animator state starts; links armed context to the clip's hit event.</summary>
    public void ArmHitForCurrentSwing()
    {
        if (!_hasArmedContext)
        {
            if (logDamagePipeline)
                Debug.LogWarning("[MeleeWeapon] ArmHitForCurrentSwing skipped: no armed context (TryBeginAttack did not run).", this);
            return;
        }

        _pendingHitContext = _armedContext;
        _hasPendingHitContext = true;
        _hasArmedContext = false;

        if (logDamagePipeline)
            Debug.Log("[MeleeWeapon] Pending hit context armed for animation event.", this);
    }

    /// <summary>Called from animation event <c>OnMeleeHitFrame</c> on the Animator object.</summary>
    public void ApplyPendingDamage()
    {
        if (!_hasPendingHitContext)
        {
            if (logDamagePipeline)
                Debug.LogWarning(
                    "[MeleeWeapon] ApplyPendingDamage skipped: no pending hit context (ArmHitForCurrentSwing did not run).",
                    this);
            return;
        }

        AttackContext ctx = _pendingHitContext;
        _hasPendingHitContext = false;

        if (logDamagePipeline)
        {
            string attackerName = ctx.attacker != null ? ctx.attacker.name : "<null>";
            Debug.Log($"[MeleeWeapon] Applying pending damage (attacker={attackerName}).", this);
        }

        ApplyDamage(in ctx);
        onAttackPerformed?.Invoke();
    }

    void ApplyDamage(in AttackContext ctx)
    {
        if (ctx.attacker == null)
            return;

        Vector3 forward = ctx.facing;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Vector3 origin = ctx.attacker.position;
        float halfCone = coneAngle >= 360f ? 180f : coneAngle * 0.5f;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            range,
            _overlapBuffer,
            hitLayers,
            overlapQueryTriggerInteraction);

        var damagedComponents = new HashSet<int>();
        int candidatesInCone = 0;
        int damagedCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (damagedCount >= maxTargetsPerSwing)
                break;

            Collider col = _overlapBuffer[i];
            if (col == null)
                continue;
            if (col.transform == ctx.attacker || col.transform.IsChildOf(ctx.attacker))
                continue;

            Vector3 to = col.bounds.center - origin;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-8f)
                continue;
            to.Normalize();
            if (coneAngle < 360f && Vector3.Angle(forward, to) > halfCone + 0.01f)
                continue;

            candidatesInCone++;
            if (TryDamageFirstOnHierarchy(col.gameObject, damage, damagedComponents, in ctx))
                damagedCount++;
        }

        if (logDamagePipeline)
        {
            Debug.Log(
                $"[MeleeWeapon] ApplyDamage overlap={count} inCone={candidatesInCone} damaged={damagedCount} " +
                $"(range={range}, origin={origin}).",
                this);
            if (candidatesInCone > 0 && damagedCount == 0)
                Debug.LogWarning("[MeleeWeapon] Hit colliders in cone but no IDamageable found on hierarchy.", this);
        }
    }

    /// <summary>Walks up from the hit object and applies damage to the first <see cref="IDamageable"/> found.</summary>
    bool TryDamageFirstOnHierarchy(GameObject hitObject, float amount, HashSet<int> damagedComponents, in AttackContext ctx)
    {
        Transform tr = hitObject.transform;
        while (tr != null)
        {
            MonoBehaviour[] components = tr.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour mb = components[i];
                if (mb == null)
                    continue;
                if (!(mb is IDamageable dmg))
                    continue;
                int id = mb.GetInstanceID();
                if (!damagedComponents.Add(id))
                    return false;
                DamageElement element = ctx.damageElementOverride ?? damageElement;
                bool isCrit = ctx.forceCritical || (criticalStrikeChance > 0f && Random.value < criticalStrikeChance);
                dmg.TakeDamage(amount, new DamageNumberStyle(element, isCrit));
                return true;
            }
            tr = tr.parent;
        }

        return false;
    }

    void TryMoveAttackerForward(Transform attacker, Vector3 forward)
    {
        if (moveForwardDistance <= 0f || attacker == null)
            return;

        if (!_movementResolved)
        {
            _cachedMovement = attacker.GetComponent<TopDownCharacterMovement>();
            _movementResolved = true;
        }

        if (_cachedMovement != null)
        {
            _cachedMovement.StartAttackLunge(forward, moveForwardDistance / moveForwardDuration, moveForwardDuration);
            return;
        }

        CharacterController controller = attacker.GetComponent<CharacterController>();
        if (controller != null && controller.enabled)
        {
            controller.Move(forward * moveForwardDistance);
            return;
        }

        attacker.position += forward * moveForwardDistance;
    }

    void OnDrawGizmos()
    {
        if (!drawDamageRadiusGizmo || range <= 0f)
            return;

        if (Application.isPlaying)
        {
            WeaponHolder holder = GetComponentInParent<WeaponHolder>();
            if (holder != null && !holder.IsMeleeEquipped())
                return;
        }

        if (!TryResolveDamageGizmoFrame(out Vector3 origin, out Vector3 forwardFlat))
            return;

        NavMeshChaseDriver.DrawXZWireDisc(origin, range, new Color(0.25f, 0.9f, 1f, 0.45f));

        if (coneAngle < 360f)
            DrawDamageConeWire(origin, forwardFlat, range, coneAngle, new Color(0.95f, 0.85f, 0.2f, 1f));

        Gizmos.color = new Color(0.4f, 1f, 0.5f, 1f);
        Gizmos.DrawLine(origin, origin + forwardFlat * Mathf.Min(range, 1.5f));
    }

    bool TryResolveDamageGizmoFrame(out Vector3 origin, out Vector3 forwardFlat)
    {
        PlayerAttackController attackController = GetComponentInParent<PlayerAttackController>();
        if (attackController != null)
        {
            origin = attackController.AttackOriginTransform.position;
            forwardFlat = FlattenForward(attackController.AttackFacingTransform.forward);
            return true;
        }

        WeaponHolder holder = GetComponentInParent<WeaponHolder>();
        if (holder != null)
        {
            origin = holder.transform.position;
            forwardFlat = FlattenForward(holder.transform.forward);
            return true;
        }

        origin = transform.position;
        forwardFlat = FlattenForward(transform.forward);
        return true;
    }

    static Vector3 FlattenForward(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            return Vector3.forward;
        forward.Normalize();
        return forward;
    }

    static void DrawDamageConeWire(Vector3 origin, Vector3 forwardFlat, float radius, float totalAngleDeg, Color color)
    {
        if (radius <= 0f || totalAngleDeg <= 0f)
            return;

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

    static Vector3 XZDirectionFromYaw(float yawRadians)
    {
        return new Vector3(Mathf.Sin(yawRadians), 0f, Mathf.Cos(yawRadians));
    }
}
