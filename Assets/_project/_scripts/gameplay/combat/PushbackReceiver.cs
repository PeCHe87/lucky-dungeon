using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Receives attack pushback. Resistance reduces effective travel distance.
/// Player displacement uses <see cref="TopDownCharacterMovement.StartKnockback"/>.
/// </summary>
public sealed class PushbackReceiver : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float pushbackResistance;
    [Tooltip("Layers that block horizontal pushback (e.g. walls).")]
    [SerializeField] LayerMask pushbackBlockLayers;
    [Tooltip("Seconds after knockback ends before NavMesh locomotion may resume.")]
    [SerializeField, Min(0f)] float movementResumeDelayAfterPushback = 0.2f;

    Coroutine _activeKnockback;
    float _movementResumeAllowedAt;
    Collider[] _selfColliders;
    int _agentKnockbackDepth;
    bool _agentWasStopped;

    public float PushbackResistance => pushbackResistance;

    public LayerMask PushbackBlockLayers => pushbackBlockLayers;

    public bool IsApplyingPushback => _activeKnockback != null;

    public bool IsNavLocomotionBlocked =>
        IsApplyingPushback || Time.time < _movementResumeAllowedAt;

    void Reset()
    {
        pushbackBlockLayers = LayerMask.GetMask("Obstacle", "Default");
    }

    void Awake()
    {
        if (pushbackBlockLayers.value == 0)
            pushbackBlockLayers = LayerMask.GetMask("Obstacle", "Default");

        _selfColliders = GetComponentsInChildren<Collider>();
    }

    void LateUpdate()
    {
        if (IsApplyingPushback || _agentKnockbackDepth > 0)
            return;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (!IsAgentUsable(agent) || agent.updatePosition)
            return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning(
            $"{nameof(PushbackReceiver)} on '{name}': NavMeshAgent.updatePosition was false outside knockback — restoring.",
            this);
#endif
        ReconcileAgentTransform(agent, transform);
    }

    void OnDisable()
    {
        CancelPushback();
        _movementResumeAllowedAt = 0f;
    }

    public void ApplyPushback(in PushbackContext ctx)
    {
        if (TryGetComponent(out IDefeatable defeatable) && defeatable.IsDefeated)
            return;

        float distance = ctx.distance * (1f - pushbackResistance);
        if (distance <= 0.01f || ctx.duration <= 0f)
            return;

        Vector3 direction = ctx.direction;
        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-8f)
            return;
        direction.Normalize();

        float speed = distance / ctx.duration;

        if (TryGetComponent(out TopDownCharacterMovement movement))
        {
            movement.StartKnockback(direction, speed, ctx.duration);
            return;
        }

        if (_activeKnockback != null)
            StopCoroutine(_activeKnockback);

        _activeKnockback = StartCoroutine(ApplyKnockbackRoutine(direction, distance, speed, ctx.duration));
    }

    /// <summary>Stops an in-flight knockback (e.g. when the entity dies / ragdolls).</summary>
    public void CancelPushback()
    {
        if (_activeKnockback != null)
        {
            StopCoroutine(_activeKnockback);
            _activeKnockback = null;
        }

        RestoreNavMeshAgentAfterKnockback();
    }

    IEnumerator ApplyKnockbackRoutine(Vector3 direction, float totalDistance, float speed, float duration)
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        CharacterController controller = GetComponent<CharacterController>();
        bool useAgent = IsAgentUsable(agent);

        BeginNavMeshAgentKnockback(agent, useAgent);

        float moved = 0f;
        float elapsed = 0f;

        try
        {
            while (elapsed < duration && moved < totalDistance - 0.001f)
            {
                if (TryGetComponent(out IDefeatable defeatable) && defeatable.IsDefeated)
                    break;

                float dt = Time.deltaTime;
                elapsed += dt;
                float step = Mathf.Min(speed * dt, totalDistance - moved);
                step = ClampStepByCollision(direction, step);
                if (step <= 0.001f)
                    break;

                // Agent may be disabled mid-knockback (ragdoll death) — fall back to transform.
                useAgent = IsAgentUsable(agent);
                Vector3 delta = direction * step;
                ApplyHorizontalDelta(delta, useAgent, agent, controller);

                moved += step;
                yield return null;
            }
        }
        finally
        {
            EndNavMeshAgentKnockback(agent, useAgent && IsAgentUsable(agent));
            _activeKnockback = null;
            _agentKnockbackDepth = 0;
            if (useAgent && IsAgentUsable(agent) && movementResumeDelayAfterPushback > 0f)
            {
                _movementResumeAllowedAt = Mathf.Max(
                    _movementResumeAllowedAt,
                    Time.time + movementResumeDelayAfterPushback);
            }
        }
    }

    void BeginNavMeshAgentKnockback(NavMeshAgent agent, bool useAgent)
    {
        if (!useAgent || !IsAgentUsable(agent))
            return;

        if (_agentKnockbackDepth == 0)
        {
            _agentWasStopped = agent.isStopped;
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.ResetPath();
        }

        _agentKnockbackDepth++;
    }

    void EndNavMeshAgentKnockback(NavMeshAgent agent, bool useAgent)
    {
        if (!useAgent || _agentKnockbackDepth <= 0)
            return;

        _agentKnockbackDepth--;
        if (_agentKnockbackDepth > 0)
            return;

        if (!IsAgentUsable(agent))
        {
            _agentKnockbackDepth = 0;
            return;
        }

        ReconcileAgentTransform(agent, transform);
        agent.isStopped = _agentWasStopped;
        ReconcileNavAgentRotation(agent);
        _agentKnockbackDepth = 0;
    }

    void RestoreNavMeshAgentAfterKnockback()
    {
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (!IsAgentUsable(agent) || _agentKnockbackDepth <= 0)
        {
            _agentKnockbackDepth = 0;
            return;
        }

        ReconcileAgentTransform(agent, transform);
        agent.isStopped = _agentWasStopped;
        ReconcileNavAgentRotation(agent);
        _agentKnockbackDepth = 0;
    }

    void ReconcileNavAgentRotation(NavMeshAgent agent)
    {
        if (!IsAgentUsable(agent))
            return;

        agent.updateRotation = true;
        if (TryGetComponent(out EntityAttackController attackController))
            attackController.SyncNavAgentFacingLock(agent);
    }

    static void ReconcileAgentTransform(NavMeshAgent agent, Transform root)
    {
        if (!IsAgentUsable(agent))
            return;

        Vector3 position = root.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            position = hit.position;

        root.position = position;
        agent.nextPosition = position;
        agent.Warp(position);
        agent.updatePosition = true;
    }

    void ApplyHorizontalDelta(Vector3 delta, bool useAgent, NavMeshAgent agent, CharacterController controller)
    {
        if (useAgent && IsAgentUsable(agent))
        {
            agent.Move(delta);
            transform.position = agent.nextPosition;
            return;
        }

        if (controller != null && controller.enabled)
            controller.Move(delta);
        else
            transform.position += delta;
    }

    float ClampStepByCollision(Vector3 direction, float step)
    {
        return PushbackGeometryProbe.ClampTravelByCollision(
            transform,
            direction,
            step,
            pushbackBlockLayers,
            _selfColliders);
    }

    static bool IsAgentUsable(NavMeshAgent agent) =>
        agent != null && agent.enabled && agent.isOnNavMesh;
}
