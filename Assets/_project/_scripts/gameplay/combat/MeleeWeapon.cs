using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Melee swing: overlap on animation hit event, cone filter, optional <see cref="IDamageable"/>.
/// <see cref="TryAttack"/> begins the swing (cooldown, lunge); damage runs when
/// <see cref="ApplyPendingDamage"/> is called from an animation event.
/// Balance/config comes from <see cref="MeleeWeaponData"/>.
/// </summary>
public sealed class MeleeWeapon : MonoBehaviour, IWeapon, IWeaponDataSource, IAttackActivity, IWeaponAttackRange, IWeaponAnimationBinding, IWeaponTargetDetection, IWeaponAttackReadiness
{
    [SerializeField] MeleeWeaponData data;
    [SerializeField] UnityEvent onAttackPerformed;

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
        if (data == null)
            Debug.LogWarning($"{nameof(MeleeWeapon)} on {name}: {nameof(data)} is not assigned.", this);

        int maxTargets = data != null ? data.MaxTargetsPerSwing : 8;
        _overlapBuffer = new Collider[Mathf.Max(32, maxTargets * 4)];
    }

    public WeaponData Data => data;
    public RuntimeAnimatorController AnimatorController => data != null ? data.AnimatorController : null;
    public PlayerEntityStateAnimationProfile AnimationProfile => data != null ? data.AnimationProfile : null;
    public float TargetDetectionRadius => data != null ? data.TargetDetectionRadius : 6f;
    public float OmnidirectionalDetectionRadius => data != null ? data.OmnidirectionalDetectionRadius : 8f;

    public bool IsAttackActive => _attackActiveTimer > 0f;

    public bool IsAttackReady => _cooldownRemaining <= 0f;

    public void CancelAttack()
    {
        _attackActiveTimer = 0f;
        _hasArmedContext = false;
        _hasPendingHitContext = false;
    }

    public bool EnableApproachLunge => data != null && data.EnableApproachLunge;

    /// <summary>Horizontal distance from target at which approach stops (matches in-range check).</summary>
    public float ApproachStopDistanceFromTarget =>
        data == null ? 0f : Mathf.Max(0f, data.Range - data.ApproachStopBuffer);

    /// <summary>Radius used by damage overlap and range gizmo (<see cref="ApplyDamage"/>).</summary>
    public float DamageOverlapRadius => data != null ? data.Range : 0f;

    public float PushbackDistance => data != null ? data.PushbackDistance : 0f;

    /// <summary>True when target is within approach-stop distance (range minus buffer); ignores cone.</summary>
    public bool IsTargetWithinDamageRadius(Vector3 origin, Vector3 targetWorldPos)
    {
        return NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos) <= ApproachStopDistanceFromTarget;
    }

    /// <inheritdoc cref="IWeaponAttackRange"/>
    public bool IsTargetWithinAttackRange(Vector3 origin, Vector3 facingFlat, Vector3 targetWorldPos)
    {
        return IsTargetWithinDamageRadius(origin, targetWorldPos);
    }

    /// <summary>True when target is inside the damage overlap sphere radius; ignores cone.</summary>
    public bool IsTargetWithinOverlapRadius(Vector3 origin, Vector3 targetWorldPos)
    {
        return NavMeshChaseDriver.HorizontalDistance(origin, targetWorldPos) <= DamageOverlapRadius;
    }

    /// <summary>True when a melee lunge (approach or per-swing forward) should run toward <paramref name="target"/>.</summary>
    public bool ShouldApplyMeleeLunge(Vector3 origin, Transform target)
    {
        return target != null && !IsTargetWithinOverlapRadius(origin, target.position);
    }

    public bool IsTargetWithinDamageRange(Vector3 origin, Vector3 facingFlat, Vector3 targetWorldPos)
    {
        if (data == null || !IsTargetWithinDamageRadius(origin, targetWorldPos))
            return false;

        float coneAngle = data.ConeAngle;
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
        stopDistanceFromTarget = DamageOverlapRadius;
        maxTravel = data != null ? data.MaxApproachLungeDistance : 0f;
        speed = data != null ? data.ApproachLungeSpeed : 0f;

        if (data == null || !data.EnableApproachLunge)
            return false;

        if (IsTargetWithinOverlapRadius(origin, targetWorldPos))
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
        if (data == null || ctx.attacker == null)
            return false;
        if (_cooldownRemaining > 0f)
            return false;

        _cooldownRemaining = data.Cooldown;

        Vector3 forward = ctx.facing;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        if (ctx.optionalTarget == null
            || ShouldApplyMeleeLunge(ctx.attacker.position, ctx.optionalTarget))
            TryMoveAttackerForward(ctx.attacker, forward);

        _armedContext = ctx;
        _hasArmedContext = true;
        _attackActiveTimer = data.AttackActiveDuration;
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
        if (data == null || ctx.attacker == null)
            return;

        Vector3 forward = ctx.facing;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Vector3 origin = ctx.attacker.position;
        float coneAngle = data.ConeAngle;
        float range = data.Range;
        float halfCone = coneAngle >= 360f ? 180f : coneAngle * 0.5f;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            range,
            _overlapBuffer,
            data.HitLayers,
            data.OverlapQueryTriggerInteraction);

        var damagedComponents = new HashSet<int>();
        int candidatesInCone = 0;
        int damagedCount = 0;
        int maxTargets = data.MaxTargetsPerSwing;

        for (int i = 0; i < count; i++)
        {
            if (damagedCount >= maxTargets)
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
            if (TryDamageFirstOnHierarchy(col.gameObject, data.Damage, damagedComponents, in ctx, forward))
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
    bool TryDamageFirstOnHierarchy(
        GameObject hitObject,
        float amount,
        HashSet<int> damagedComponents,
        in AttackContext ctx,
        Vector3 pushbackDirection)
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
                if (mb is IDefeatable defeated && defeated.IsDefeated)
                    return false;
                int id = mb.GetInstanceID();
                if (!damagedComponents.Add(id))
                    return false;
                DamageElement element = ctx.damageElementOverride ?? data.DamageElement;
                bool isCrit = ctx.forceCritical || (data.CriticalStrikeChance > 0f && Random.value < data.CriticalStrikeChance);
                dmg.TakeDamage(amount, new DamageNumberStyle(element, isCrit), new DamageHitInfo(ctx.attacker));

                if (data.PushbackDistance > 0f && ShouldApplyPushback(tr, pushbackDirection)
                    && !EntityAttackController.ShouldSuppressPushbackOn(tr.gameObject))
                {
                    PushbackUtility.TryApplyOnHierarchy(hitObject, new PushbackContext
                    {
                        direction = pushbackDirection,
                        distance = data.PushbackDistance,
                        duration = data.PushbackDuration,
                    });
                }

                return true;
            }
            tr = tr.parent;
        }

        return false;
    }

    bool ShouldApplyPushback(Transform victimRoot, Vector3 pushDirection)
    {
        float resistance = PushbackGeometryProbe.ResolvePushbackResistance(victimRoot);
        float probeDist = data.PushbackDistance * (1f - resistance);
        LayerMask blockLayers = PushbackGeometryProbe.ResolveBlockLayers(victimRoot);

        return PushbackGeometryProbe.ShouldApplyPushbackForce(
            victimRoot,
            pushDirection,
            probeDist,
            blockLayers,
            data.MinPushTravelFraction);
    }

    void TryMoveAttackerForward(Transform attacker, Vector3 forward)
    {
        if (data == null || data.MoveForwardDistance <= 0f || attacker == null)
            return;

        if (!_movementResolved)
        {
            _cachedMovement = attacker.GetComponent<TopDownCharacterMovement>();
            _movementResolved = true;
        }

        if (_cachedMovement != null)
        {
            _cachedMovement.StartAttackLunge(
                forward,
                data.MoveForwardDistance / data.MoveForwardDuration,
                data.MoveForwardDuration);
            return;
        }

        CharacterController controller = attacker.GetComponent<CharacterController>();
        if (controller != null && controller.enabled)
        {
            controller.Move(forward * data.MoveForwardDistance);
            return;
        }

        attacker.position += forward * data.MoveForwardDistance;
    }

    void OnDrawGizmos()
    {
        float range = data != null ? data.Range : 0f;
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

        float coneAngle = data.ConeAngle;
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
