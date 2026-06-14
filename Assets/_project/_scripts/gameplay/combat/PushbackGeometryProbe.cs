using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Shared horizontal capsule cast used by <see cref="PushbackReceiver"/> and melee engagement probes.
/// </summary>
public static class PushbackGeometryProbe
{
    public const float CollisionSkinWidth = 0.02f;
    public const float DefaultPinnedTravelThreshold = 0.05f;
    public const float DefaultMinPushTravelFraction = 0.9f;

    public static LayerMask DefaultBlockLayers =>
        LayerMask.GetMask("Obstacle", "Default");

    public static float ProbeMaxHorizontalTravel(
        Transform body,
        Vector3 flatDirection,
        float requestedDistance,
        LayerMask blockLayers,
        Collider[] ignoreColliders = null)
    {
        if (body == null || requestedDistance <= 0f)
            return 0f;

        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude < 1e-8f)
            return requestedDistance;
        flatDirection.Normalize();

        return ClampTravelByCollision(body, flatDirection, requestedDistance, blockLayers, ignoreColliders);
    }

    public static float ClampTravelByCollision(
        Transform body,
        Vector3 flatDirection,
        float requestedTravel,
        LayerMask blockLayers,
        Collider[] ignoreColliders = null)
    {
        if (requestedTravel <= 0.001f || blockLayers.value == 0 || body == null)
            return requestedTravel;

        if (!TryGetWorldCapsule(body, out Vector3 point1, out Vector3 point2, out float radius))
            return requestedTravel;

        if (!Physics.CapsuleCast(
                point1,
                point2,
                radius,
                flatDirection,
                out RaycastHit hit,
                requestedTravel,
                blockLayers,
                QueryTriggerInteraction.Ignore)
            || IsIgnoredCollider(hit.collider, ignoreColliders))
        {
            return requestedTravel;
        }

        return Mathf.Max(0f, hit.distance - CollisionSkinWidth);
    }

    public static bool IsPushPinned(
        Transform body,
        Vector3 flatPushDirection,
        float probeDistance,
        LayerMask blockLayers,
        float pinnedTravelThreshold = DefaultPinnedTravelThreshold,
        Collider[] ignoreColliders = null)
    {
        if (probeDistance <= 0f)
            return false;

        float allowed = ProbeMaxHorizontalTravel(
            body,
            flatPushDirection,
            probeDistance,
            blockLayers,
            ignoreColliders);

        return allowed < pinnedTravelThreshold;
    }

    /// <summary>
    /// True when a full push along <paramref name="flatPushDirection"/> is unobstructed enough to apply.
    /// </summary>
    public static bool ShouldApplyPushbackForce(
        Transform victimRoot,
        Vector3 flatPushDirection,
        float requestedDistance,
        LayerMask blockLayers,
        float minRequiredTravelFraction = DefaultMinPushTravelFraction,
        Collider[] ignoreColliders = null)
    {
        if (requestedDistance <= 0f)
            return false;

        float allowed = ProbeMaxHorizontalTravel(
            victimRoot,
            flatPushDirection,
            requestedDistance,
            blockLayers,
            ignoreColliders);

        return allowed >= requestedDistance * minRequiredTravelFraction;
    }

    public static LayerMask ResolveBlockLayers(Transform body)
    {
        if (body != null && body.TryGetComponent(out PushbackReceiver receiver))
            return receiver.PushbackBlockLayers;

        return DefaultBlockLayers;
    }

    public static float ResolvePushbackResistance(Transform body)
    {
        if (body != null && body.TryGetComponent(out PushbackReceiver receiver))
            return receiver.PushbackResistance;

        return 0f;
    }

    static bool IsIgnoredCollider(Collider collider, Collider[] ignoreColliders)
    {
        if (collider == null || ignoreColliders == null)
            return false;

        for (int i = 0; i < ignoreColliders.Length; i++)
        {
            if (ignoreColliders[i] == collider)
                return true;
        }

        return false;
    }

    static bool TryGetWorldCapsule(Transform body, out Vector3 point1, out Vector3 point2, out float radius)
    {
        CapsuleCollider capsule = body.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            GetCapsuleWorldEndPoints(capsule, out point1, out point2, out radius);
            return true;
        }

        CharacterController controller = body.GetComponent<CharacterController>();
        if (controller != null)
        {
            GetControllerWorldCapsule(body, controller, out point1, out point2, out radius);
            return true;
        }

        NavMeshAgent agent = body.GetComponent<NavMeshAgent>();
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

    static void GetControllerWorldCapsule(Transform body, CharacterController controller, out Vector3 point1, out Vector3 point2, out float radius)
    {
        radius = controller.radius;
        Vector3 worldCenter = body.TransformPoint(controller.center);
        float cylinderHalf = Mathf.Max(0f, controller.height * 0.5f - radius);
        point1 = worldCenter + Vector3.up * cylinderHalf;
        point2 = worldCenter - Vector3.up * cylinderHalf;
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
