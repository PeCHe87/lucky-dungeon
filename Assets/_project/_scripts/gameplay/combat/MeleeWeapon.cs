using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Simple melee swing: sphere overlap, cone filter, optional <see cref="IDamageable"/>.</summary>
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

    float _cooldownRemaining;
    float _attackActiveTimer;
    Collider[] _overlapBuffer;
    TopDownCharacterMovement _cachedMovement;
    bool _movementResolved;

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

    void Update()
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining -= Time.deltaTime;
        if (_attackActiveTimer > 0f)
            _attackActiveTimer -= Time.deltaTime;
    }

    public bool TryAttack(in AttackContext ctx)
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

        TryMoveAttackerForward(ctx.attacker, forward);
        Vector3 origin = ctx.attacker.position;

        float halfCone = coneAngle >= 360f ? 180f : coneAngle * 0.5f;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            range,
            _overlapBuffer,
            hitLayers,
            overlapQueryTriggerInteraction);

        var damagedComponents = new HashSet<int>();
        int applied = 0;

        for (int i = 0; i < count; i++)
        {
            if (applied >= maxTargetsPerSwing)
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

            TryDamageFirstOnHierarchy(col.gameObject, damage, damagedComponents, in ctx);
            applied++;
        }

        _attackActiveTimer = attackActiveDuration;
        onAttackPerformed?.Invoke();
        return true;
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
}
