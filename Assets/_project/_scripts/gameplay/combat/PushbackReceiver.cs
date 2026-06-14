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

    Coroutine _activeKnockback;
    Collider[] _selfColliders;

    public float PushbackResistance => pushbackResistance;

    public LayerMask PushbackBlockLayers => pushbackBlockLayers;

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

    public void ApplyPushback(in PushbackContext ctx)
    {
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

    IEnumerator ApplyKnockbackRoutine(Vector3 direction, float totalDistance, float speed, float duration)
    {
        float moved = 0f;
        float elapsed = 0f;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        CharacterController controller = GetComponent<CharacterController>();
        bool useAgent = agent != null && agent.enabled;
        bool agentWasStopped = false;
        bool agentUpdatedPosition = true;
        bool agentUpdatedRotation = true;

        if (useAgent)
        {
            agentWasStopped = agent.isStopped;
            agentUpdatedPosition = agent.updatePosition;
            agentUpdatedRotation = agent.updateRotation;
            agent.isStopped = true;
            agent.updatePosition = false;
            agent.updateRotation = false;
            agent.ResetPath();
        }

        while (elapsed < duration && moved < totalDistance - 0.001f)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float step = Mathf.Min(speed * dt, totalDistance - moved);
            step = ClampStepByCollision(direction, step);
            if (step <= 0.001f)
                break;

            Vector3 delta = direction * step;
            ApplyHorizontalDelta(delta, useAgent, agent, controller);

            moved += step;
            yield return null;
        }

        if (useAgent)
        {
            agent.nextPosition = transform.position;
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                agent.nextPosition = hit.position;
                agent.Warp(hit.position);
            }

            agent.updatePosition = agentUpdatedPosition;
            agent.updateRotation = agentUpdatedRotation;
            agent.isStopped = agentWasStopped;
        }

        _activeKnockback = null;
    }

    void ApplyHorizontalDelta(Vector3 delta, bool useAgent, NavMeshAgent agent, CharacterController controller)
    {
        if (useAgent)
        {
            transform.position += delta;
            agent.nextPosition = transform.position;
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
}
