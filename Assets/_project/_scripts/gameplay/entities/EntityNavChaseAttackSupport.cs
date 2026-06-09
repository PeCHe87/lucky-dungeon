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

    public static void ResetToChaseAfterDamageInterrupt(NavMeshAgent agent, ref bool hasChaseSample)
    {
        hasChaseSample = false;
        if (agent != null)
            agent.isStopped = false;
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

        if (attackController.EnablePreAttackTelegraph)
        {
            if (attackController.TryBeginPreAttack(target))
                return AttackPhaseBeginResult.PreAttacking;

            if (attackController.IsAttackBlocked)
                return AttackPhaseBeginResult.Failed;
        }

        return AttackPhaseBeginResult.Attacking;
    }

    public static AttackTickResult TickPreAttackPhase(
        EntityAttackController attackController,
        FieldOfViewComponent fieldOfView,
        NavMeshAgent agent,
        Vector3 origin)
    {
        if (attackController == null)
            return AttackTickResult.ResumeChasing;

        if (attackController.IsOnlyDamageBlocked)
        {
            if (agent != null)
                agent.isStopped = false;
            return AttackTickResult.ResumeChasing;
        }

        if (agent != null)
            agent.isStopped = true;

        Transform target = fieldOfView.HasTarget ? fieldOfView.Target : null;
        if (target != null)
            attackController.FaceTarget(target);

        if (attackController.IsTelegraphing)
            return AttackTickResult.StayPreAttacking;

        attackController.TryCommitStrike(target);
        return AttackTickResult.StayAttacking;
    }

    public static AttackTickResult TickAttackPhase(
        EntityAttackController attackController,
        FieldOfViewComponent fieldOfView,
        NavMeshAgent agent,
        Vector3 origin)
    {
        if (attackController == null)
            return AttackTickResult.ResumeChasing;

        if (attackController.IsOnlyDamageBlocked)
        {
            if (agent != null)
                agent.isStopped = false;
            return AttackTickResult.ResumeChasing;
        }

        if (agent != null)
            agent.isStopped = true;

        Transform target = fieldOfView.HasTarget ? fieldOfView.Target : null;
        if (target != null)
            attackController.FaceTarget(target);

        if (attackController.IsAttackCommitActive)
        {
            if (attackController.IsBusy)
                return AttackTickResult.StayAttacking;

            attackController.CompleteAttackCommit();
            return EvaluatePostAttackEngagement(attackController, fieldOfView, agent, target);
        }

        if (attackController.IsBusy)
            return AttackTickResult.StayAttacking;

        if (!fieldOfView.HasTarget)
            return AttackTickResult.ResumeSearching;

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

    static AttackTickResult EvaluatePostAttackEngagement(
        EntityAttackController attackController,
        FieldOfViewComponent fieldOfView,
        NavMeshAgent agent,
        Transform target)
    {
        if (!fieldOfView.HasTarget)
            return AttackTickResult.ResumeSearching;

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
