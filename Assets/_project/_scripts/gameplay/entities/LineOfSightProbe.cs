using System;
using UnityEngine;

/// <summary>Shared raycast line-of-sight checks for entity and player target discovery.</summary>
public static class LineOfSightProbe
{
    const float LosEpsilon = 1e-4f;
    const float RaycastSlop = 0.02f;
    const float LosPenetrationStep = 0.01f;
    const int MaxLosPenetrationSteps = 8;

    /// <summary>
    /// True when a ray from <paramref name="from"/> reaches <paramref name="candidate"/> without a blocking hit.
    /// Hits on <paramref name="penetrateLayers"/> or matching <paramref name="shouldPenetrateHit"/> advance the ray.
    /// </summary>
    public static bool HasLineOfSight(
        Vector3 from,
        Collider candidate,
        Transform ignoreRoot,
        LayerMask layers,
        QueryTriggerInteraction triggers,
        LayerMask penetrateLayers = default,
        Func<Collider, bool> shouldPenetrateHit = null)
    {
        if (candidate == null)
            return false;

        Vector3 aim = candidate.bounds.center;
        Vector3 to = aim - from;
        float dist = to.magnitude;
        if (dist <= LosEpsilon)
            return true;

        Vector3 dir = to / dist;
        float remaining = dist + RaycastSlop;
        Vector3 origin = from;

        for (int step = 0; step < MaxLosPenetrationSteps && remaining > LosEpsilon; step++)
        {
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, remaining, layers, triggers))
                return true;

            if (ShouldAdvanceThroughHit(hit.collider, ignoreRoot, penetrateLayers, shouldPenetrateHit))
            {
                float advance = Mathf.Max(hit.distance + LosPenetrationStep, LosPenetrationStep);
                origin += dir * advance;
                remaining -= advance;
                continue;
            }

            return IsHitFromCandidate(hit, candidate);
        }

        return remaining <= LosEpsilon;
    }

    /// <summary>
    /// True when a ray from <paramref name="from"/> reaches <paramref name="worldPoint"/> without a blocking hit.
    /// </summary>
    public static bool HasClearLineToPoint(
        Vector3 from,
        Vector3 worldPoint,
        Transform ignoreRoot,
        LayerMask layers,
        QueryTriggerInteraction triggers,
        LayerMask penetrateLayers = default,
        Func<Collider, bool> shouldPenetrateHit = null)
    {
        Vector3 to = worldPoint - from;
        float dist = to.magnitude;
        if (dist <= LosEpsilon)
            return true;

        Vector3 dir = to / dist;
        float remaining = dist + RaycastSlop;
        Vector3 origin = from;

        for (int step = 0; step < MaxLosPenetrationSteps && remaining > LosEpsilon; step++)
        {
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, remaining, layers, triggers))
                return true;

            if (ShouldAdvanceThroughHit(hit.collider, ignoreRoot, penetrateLayers, shouldPenetrateHit))
            {
                float advance = Mathf.Max(hit.distance + LosPenetrationStep, LosPenetrationStep);
                origin += dir * advance;
                remaining -= advance;
                continue;
            }

            return false;
        }

        return remaining <= LosEpsilon;
    }

    static bool ShouldAdvanceThroughHit(
        Collider c,
        Transform ignoreRoot,
        LayerMask penetrateLayers,
        Func<Collider, bool> shouldPenetrateHit)
    {
        if (ShouldIgnoreForLos(c, ignoreRoot))
            return true;
        if (ShouldPenetrateForLos(c, penetrateLayers))
            return true;
        return shouldPenetrateHit != null && shouldPenetrateHit(c);
    }

    static bool ShouldIgnoreForLos(Collider c, Transform ignoreRoot)
    {
        if (c == null || ignoreRoot == null)
            return false;

        return c.transform == ignoreRoot || c.transform.IsChildOf(ignoreRoot);
    }

    static bool ShouldPenetrateForLos(Collider c, LayerMask penetrateLayers)
    {
        if (c == null || penetrateLayers.value == 0)
            return false;

        return (penetrateLayers.value & (1 << c.gameObject.layer)) != 0;
    }

    static bool IsHitFromCandidate(RaycastHit hit, Collider candidate)
    {
        if (hit.collider == candidate)
            return true;
        if (hit.transform.IsChildOf(candidate.transform))
            return true;
        if (candidate.transform.IsChildOf(hit.transform))
            return true;
        return false;
    }
}
