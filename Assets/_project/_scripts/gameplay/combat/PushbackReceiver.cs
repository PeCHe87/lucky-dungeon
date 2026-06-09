using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Receives attack pushback. Resistance reduces effective travel distance.
/// Player displacement uses <see cref="TopDownCharacterMovement.StartKnockback"/>.
/// </summary>
public sealed class PushbackReceiver : MonoBehaviour
{
    const float CollisionSkinWidth = 0.02f;

    [SerializeField, Range(0f, 1f)] float pushbackResistance;
    [Tooltip("Layers that block horizontal pushback (e.g. walls).")]
    [SerializeField] LayerMask pushbackBlockLayers;

    Coroutine _activeKnockback;
    Collider[] _selfColliders;

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
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
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
            step = ClampStepByCollision(direction, step, capsule, agent);
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

    float ClampStepByCollision(Vector3 direction, float step, CapsuleCollider capsule, NavMeshAgent agent)
    {
        if (step <= 0.001f || pushbackBlockLayers.value == 0)
            return step;

        if (!TryGetWorldCapsule(capsule, agent, out Vector3 point1, out Vector3 point2, out float radius))
            return step;

        if (!Physics.CapsuleCast(
                point1,
                point2,
                radius,
                direction,
                out RaycastHit hit,
                step,
                pushbackBlockLayers,
                QueryTriggerInteraction.Ignore)
            || IsSelfCollider(hit.collider))
        {
            return step;
        }

        return Mathf.Max(0f, hit.distance - CollisionSkinWidth);
    }

    bool IsSelfCollider(Collider collider)
    {
        if (collider == null || _selfColliders == null)
            return false;

        for (int i = 0; i < _selfColliders.Length; i++)
        {
            if (_selfColliders[i] == collider)
                return true;
        }

        return false;
    }

    static bool TryGetWorldCapsule(
        CapsuleCollider capsule,
        NavMeshAgent agent,
        out Vector3 point1,
        out Vector3 point2,
        out float radius)
    {
        if (capsule != null)
        {
            GetCapsuleWorldEndPoints(capsule, out point1, out point2, out radius);
            return true;
        }

        if (agent != null)
        {
            GetAgentWorldCapsule(agent, out point1, out point2, out radius);
            return true;
        }

        point1 = default;
        point2 = default;
        radius = 0f;
        return false;
    }

    static void GetCapsuleWorldEndPoints(CapsuleCollider capsule, out Vector3 point1, out Vector3 point2, out float radius)
    {
        Transform t = capsule.transform;
        Vector3 lossy = t.lossyScale;
        radius = capsule.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
        float height = capsule.height * Mathf.Abs(lossy.y);
        float cylinderHalf = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 axis = capsule.direction switch
        {
            0 => t.right,
            2 => t.forward,
            _ => t.up,
        };

        Vector3 center = t.TransformPoint(capsule.center);
        point1 = center + axis * cylinderHalf;
        point2 = center - axis * cylinderHalf;
    }

    static void GetAgentWorldCapsule(NavMeshAgent agent, out Vector3 point1, out Vector3 point2, out float radius)
    {
        radius = agent.radius;
        float baseY = agent.transform.position.y + agent.baseOffset;
        float centerX = agent.transform.position.x;
        float centerZ = agent.transform.position.z;

        point1 = new Vector3(centerX, baseY + agent.height - radius, centerZ);
        point2 = new Vector3(centerX, baseY + radius, centerZ);
    }
}
