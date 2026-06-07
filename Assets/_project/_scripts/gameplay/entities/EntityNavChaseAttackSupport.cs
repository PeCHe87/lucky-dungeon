using UnityEngine;
using UnityEngine.AI;

/// <summary>Shared attack-phase helpers for nav detect/chase behaviors.</summary>
public static class EntityNavChaseAttackSupport
{
    public enum AttackTickResult
    {
        StayAttacking,
        ResumeChasing,
        ResumeSearching,
    }

    public static bool TryBeginAttackPhase(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        if (attackController == null || target == null || agent == null)
            return false;

        if (!attackController.IsTargetInAttackRange(target))
            return false;

        agent.isStopped = true;
        agent.ResetPath();
        return true;
    }

    public static AttackTickResult TickAttackPhase(
        EntityAttackController attackController,
        FieldOfViewComponent fieldOfView,
        NavMeshAgent agent,
        Vector3 origin)
    {
        if (agent != null)
            agent.isStopped = true;

        if (attackController == null)
            return AttackTickResult.ResumeChasing;

        if (!fieldOfView.HasTarget)
            return AttackTickResult.ResumeSearching;

        Transform target = fieldOfView.Target;
        attackController.FaceTarget(target);

        if (attackController.IsBusy)
            return AttackTickResult.StayAttacking;

        if (attackController.IsTargetBeyondAttackEngagement(target))
        {
            if (agent != null)
                agent.isStopped = false;
            return AttackTickResult.ResumeChasing;
        }

        if (attackController.IsTargetInAttackRange(target))
            attackController.TryAttackTarget(target);

        return AttackTickResult.StayAttacking;
    }
}
