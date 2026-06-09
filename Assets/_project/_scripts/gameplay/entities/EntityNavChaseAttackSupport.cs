using UnityEngine;
using UnityEngine.AI;

/// <summary>Shared attack-phase helpers for nav detect/chase behaviors.</summary>
public static class EntityNavChaseAttackSupport
{
    public enum AttackPhaseBeginResult
    {
        Failed,
        PreAttacking,
        Attacking,
    }

    public enum AttackTickResult
    {
        StayAttacking,
        StayPreAttacking,
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

    public static AttackPhaseBeginResult TryBeginPreAttackPhase(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        if (!TryBeginAttackPhase(attackController, target, agent))
            return AttackPhaseBeginResult.Failed;

        if (attackController.EnablePreAttackTelegraph && attackController.TryBeginPreAttack(target))
            return AttackPhaseBeginResult.PreAttacking;

        return AttackPhaseBeginResult.Attacking;
    }

    public static AttackTickResult TickPreAttackPhase(
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

        if (ShouldAbortAttackEngagement(attackController, target, agent, out AttackTickResult abortResult))
            return abortResult;

        if (attackController.IsTelegraphing)
            return AttackTickResult.StayPreAttacking;

        attackController.TryAttackTarget(target);
        return AttackTickResult.StayAttacking;
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

        if (ShouldAbortAttackEngagement(attackController, target, agent, out AttackTickResult abortResult))
            return abortResult;

        if (attackController.IsTargetInAttackRange(target))
        {
            if (attackController.EnablePreAttackTelegraph && attackController.TryBeginPreAttack(target))
                return AttackTickResult.StayPreAttacking;

            attackController.TryAttackTarget(target);
        }

        return AttackTickResult.StayAttacking;
    }

    static bool ShouldAbortAttackEngagement(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent,
        out AttackTickResult result)
    {
        result = AttackTickResult.StayAttacking;

        if (!attackController.IsTargetBeyondAttackEngagement(target))
            return false;

        attackController.CancelActiveAttack();
        if (agent != null)
            agent.isStopped = false;
        result = AttackTickResult.ResumeChasing;
        return true;
    }
}
