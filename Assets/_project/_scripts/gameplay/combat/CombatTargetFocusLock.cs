using UnityEngine;

/// <summary>
/// Thin wrapper that delegates to <see cref="NearestTargetQuery"/> engaged combat targeting.
/// Kept for existing prefab references; logic lives on the query.
/// </summary>
public sealed class CombatTargetFocusLock : MonoBehaviour
{
    [SerializeField] NearestTargetQuery nearestTargetQuery;

    void Awake()
    {
        if (nearestTargetQuery == null)
            nearestTargetQuery = GetComponent<NearestTargetQuery>();
    }

    public bool IsActive => nearestTargetQuery != null && nearestTargetQuery.HasEngagedCombatTarget;

    public void Lock(Transform target)
    {
        nearestTargetQuery?.EngageCombatTarget(target);
    }

    public void NotifyMeleeCombatLockEnded() { }

    public void Clear()
    {
        nearestTargetQuery?.ClearEngagedCombatTarget();
    }

    public bool TryGetFocus(out Transform target)
    {
        target = null;
        return nearestTargetQuery != null && nearestTargetQuery.TryGetEngagedCombatTarget(out target);
    }
}
