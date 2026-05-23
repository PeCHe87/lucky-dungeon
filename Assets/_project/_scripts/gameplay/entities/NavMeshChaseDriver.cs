using UnityEngine;
using UnityEngine.AI;

public static class NavMeshChaseDriver
{
    public static float FlatDistanceSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    public static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt(FlatDistanceSq(a, b));
    }

    public static bool IsWithinXZRadius(Vector3 from, Vector3 targetPosition, float radius)
    {
        return FlatDistanceSq(from, targetPosition) <= radius * radius;
    }

    public static void SetDestinationSampled(NavMeshAgent agent, Vector3 worldTarget, float samplePositionRadius)
    {
        if (NavMesh.SamplePosition(worldTarget, out NavMeshHit hit, samplePositionRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(worldTarget);
    }

    public static void RefreshChaseDestinationIfNeeded(
        NavMeshAgent agent,
        Vector3 targetPosition,
        float samplePositionRadius,
        ref Vector3 lastSampledGoal,
        ref bool hasSampledGoal,
        float resampleMinFlatDistanceSq = 0.01f)
    {
        if (!hasSampledGoal || FlatDistanceSq(lastSampledGoal, targetPosition) > resampleMinFlatDistanceSq)
        {
            SetDestinationSampled(agent, targetPosition, samplePositionRadius);
            lastSampledGoal = targetPosition;
            hasSampledGoal = true;
        }
    }

    /// <summary>
    /// Horizontal (XZ) wire circle for Scene view gizmos. Call from OnDrawGizmosSelected.
    /// </summary>
    public static void DrawXZWireDisc(Vector3 center, float radius, Color color, int segments = 48)
    {
        if (radius <= 0f || segments < 3)
            return;

        Color previous = Gizmos.color;
        Gizmos.color = color;

        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = (float)i / segments * (Mathf.PI * 2f);
            Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        Gizmos.color = previous;
    }
}
