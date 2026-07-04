using System;
using UnityEngine;

/// <summary>
/// Shared tier-first target ranking: lower tier wins; distance breaks ties within the same tier.
/// </summary>
public static class DetectionTargetRank
{
    public static bool IsBetter(int tier, float distSq, int bestTier, float bestDistSq)
    {
        if (tier < bestTier)
            return true;
        if (tier > bestTier)
            return false;
        return distSq < bestDistSq;
    }

    public static int Compare(int tierA, float distSqA, int tierB, float distSqB)
    {
        if (tierA != tierB)
            return tierA.CompareTo(tierB);
        return distSqA.CompareTo(distSqB);
    }
}

[Serializable]
public struct EntityTargetPriorityRule
{
    [Tooltip("Lower values outrank higher values. Distance breaks ties within the same tier.")]
    public int tier;
    [Tooltip("Collider must be on one of these layers.")]
    public LayerMask layers;
    [Tooltip("When enabled, only entities with the configured alignment are eligible.")]
    public bool filterByAlignment;
    public EntityAlignment alignment;
}

public static class EntityTargetPriorityRules
{
    public static bool TryResolveRule(Collider col, EntityTargetPriorityRule[] rules, out int tier)
    {
        tier = int.MaxValue;
        if (col == null || rules == null || rules.Length == 0)
            return false;

        var health = col.GetComponentInParent<CombatEntityHealth>();
        if (health == null || health.IsDefeated)
            return false;

        bool found = false;
        int bestTier = int.MaxValue;
        for (int i = 0; i < rules.Length; i++)
        {
            EntityTargetPriorityRule rule = rules[i];
            if (!IsLayerMatch(col.gameObject.layer, rule.layers))
                continue;
            if (rule.filterByAlignment && health.Alignment != rule.alignment)
                continue;

            if (rule.tier < bestTier)
            {
                bestTier = rule.tier;
                found = true;
            }
        }

        if (!found)
            return false;

        tier = bestTier;
        return true;
    }

    public static bool TargetMatchesAnyRule(Transform target, EntityTargetPriorityRule[] rules)
    {
        if (target == null || rules == null || rules.Length == 0)
            return false;

        var health = target.GetComponent<CombatEntityHealth>();
        if (health == null || health.IsDefeated)
            return false;

        for (int i = 0; i < rules.Length; i++)
        {
            EntityTargetPriorityRule rule = rules[i];
            if (!IsLayerMatch(target.gameObject.layer, rule.layers))
                continue;
            if (rule.filterByAlignment && health.Alignment != rule.alignment)
                continue;
            return true;
        }

        return false;
    }

    public static EntityTargetPriorityRule[] CreateDefaultRules(EntityAlignment fallbackAlignment)
    {
        return new[]
        {
            new EntityTargetPriorityRule
            {
                tier = 0,
                layers = LayerMask.GetMask("Player"),
                filterByAlignment = false,
            },
            new EntityTargetPriorityRule
            {
                tier = 1,
                layers = LayerMask.GetMask("Entity"),
                filterByAlignment = true,
                alignment = fallbackAlignment,
            },
        };
    }

    static bool IsLayerMatch(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
}
