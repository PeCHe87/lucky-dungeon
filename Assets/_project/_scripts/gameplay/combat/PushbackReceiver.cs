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

    Coroutine _activeKnockback;

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

        if (useAgent)
        {
            agentWasStopped = agent.isStopped;
            agent.isStopped = true;
            agent.ResetPath();
        }

        while (elapsed < duration && moved < totalDistance - 0.001f)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            float step = Mathf.Min(speed * dt, totalDistance - moved);
            Vector3 delta = direction * step;

            if (useAgent)
                agent.Move(delta);
            else if (controller != null && controller.enabled)
                controller.Move(delta);
            else
                transform.position += delta;

            moved += step;
            yield return null;
        }

        if (useAgent)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            agent.isStopped = agentWasStopped;
        }

        _activeKnockback = null;
    }
}
