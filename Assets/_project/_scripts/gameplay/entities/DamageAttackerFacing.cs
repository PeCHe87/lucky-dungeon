using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Subscribes to <see cref="CombatEntityHealth.DamagedWithHitInfo"/> and instantly snaps yaw toward the attacker
/// during <see cref="CombatEntityHealth.TakeDamage"/> (before weapon pushback runs).
/// </summary>
public sealed class DamageAttackerFacing : MonoBehaviour
{
    [SerializeField] bool faceAttackerOnDamage;
    [Tooltip("Optional visual child reset to local identity after agent root yaw. Auto-finds a child named \"body\" if unset.")]
    [SerializeField] Transform visualPivot;

    CombatEntityHealth _health;
    TopDownCharacterMovement _movement;
    NavMeshAgent _navMeshAgent;

    void Awake()
    {
        _health = GetComponentInParent<CombatEntityHealth>();
        _movement = GetComponentInParent<TopDownCharacterMovement>();
        _navMeshAgent = GetComponentInParent<NavMeshAgent>();
        ResolveVisualPivotIfNeeded();
    }

    void OnEnable()
    {
        if (_health != null)
            _health.DamagedWithHitInfo += OnDamagedWithHitInfo;
    }

    void OnDisable()
    {
        if (_health != null)
            _health.DamagedWithHitInfo -= OnDamagedWithHitInfo;
    }

    void OnDamagedWithHitInfo(float _, DamageHitInfo hitInfo)
    {
        if (EntityAttackController.ShouldSuppressDamageInterruptOn(_health.gameObject))
            return;

        if (!faceAttackerOnDamage || !hitInfo.HasAttacker)
            return;

        Vector3 attackerPosition = hitInfo.attacker.position;

        if (_movement != null)
        {
            _movement.SnapHorizontalFacingTowardWorldPosition(attackerPosition);
            return;
        }

        if (_navMeshAgent != null && _navMeshAgent.isActiveAndEnabled)
        {
            SnapAgentRootYawToward(_navMeshAgent, attackerPosition, ResolveVisualPivot());
            return;
        }

        SnapTransformYawToward(transform, attackerPosition);
    }

    static void SnapAgentRootYawToward(NavMeshAgent agent, Vector3 worldPosition, Transform visualChild)
    {
        Transform root = agent.transform;
        Vector3 to = worldPosition - root.position;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return;

        bool restoreUpdateRotation = agent.updateRotation;
        agent.updateRotation = false;
        root.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

        if (visualChild != null && visualChild != root)
            visualChild.localRotation = Quaternion.identity;

        agent.updateRotation = restoreUpdateRotation;
    }

    static void SnapTransformYawToward(Transform pivot, Vector3 worldPosition)
    {
        Vector3 to = worldPosition - pivot.position;
        to.y = 0f;
        if (to.sqrMagnitude < 1e-8f)
            return;

        pivot.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
    }

    void ResolveVisualPivotIfNeeded()
    {
        if (visualPivot != null)
            return;

        Transform searchRoot = _navMeshAgent != null
            ? _navMeshAgent.transform
            : _health != null
                ? _health.transform
                : transform;

        Transform body = searchRoot.Find("body");
        if (body != null)
            visualPivot = body;
    }

    Transform ResolveVisualPivot()
    {
        if (visualPivot == null)
            ResolveVisualPivotIfNeeded();
        return visualPivot;
    }
}
