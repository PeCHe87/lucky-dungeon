using UnityEngine;
using UnityEngine.AI;

/// <summary>Nav behaviors that cache chase destinations implement this so FSM hit-react can invalidate after <see cref="NavMeshAgent.ResetPath"/>.</summary>
public interface IEntityNavChaseDestinationCache
{
    void InvalidateChaseDestinationCache();
}

/// <summary>Shared attack-phase helpers for nav detect/chase behaviors.</summary>
public static class EntityNavChaseAttackSupport
{
    public static void InvalidateChaseDestinationCaches(GameObject entityRoot)
    {
        if (entityRoot == null)
            return;

        var caches = entityRoot.GetComponents<IEntityNavChaseDestinationCache>();
        for (int i = 0; i < caches.Length; i++)
            caches[i].InvalidateChaseDestinationCache();
    }

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

    public static void UpdateMeleeAttackApproach(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        if (attackController == null || target == null || agent == null)
            return;

        if (!attackController.IsMeleeWeaponEquipped)
            return;

        if (attackController.IsBusy)
        {
            agent.isStopped = true;
            return;
        }

        if (attackController.CanStrikeMeleeTarget(target))
        {
            agent.isStopped = true;
            return;
        }

        if (!attackController.CanMaintainMeleeEngagement(target))
            return;

        float stopDistance = attackController.MeleeApproachStopDistance;
        agent.isStopped = false;
        agent.stoppingDistance = stopDistance;
        agent.SetDestination(target.position);
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

        Transform target = fieldOfView.HasTarget ? fieldOfView.Target : null;
        if (target != null)
            attackController.FaceTarget(target);

        if (attackController.IsTelegraphing)
        {
            if (agent != null)
                agent.isStopped = true;
            return AttackTickResult.StayPreAttacking;
        }

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
        {
            if (agent != null)
                agent.isStopped = true;
            return AttackTickResult.StayAttacking;
        }

        if (!fieldOfView.HasTarget)
            return AttackTickResult.ResumeSearching;

        if (ShouldAbortAttackEngagement(attackController, target, agent, out AttackTickResult abortResult))
            return abortResult;

        UpdateMeleeAttackApproach(attackController, target, agent);
        UpdateRangedEngagementMovement(attackController, target, agent);

        if (attackController.CanStrikeTarget(target))
        {
            if (attackController.EnablePreAttackTelegraph && attackController.TryBeginPreAttack(target))
                return AttackTickResult.StayPreAttacking;

            attackController.TryAttackTarget(target);
        }

        return AttackTickResult.StayAttacking;
    }

    public static void UpdateRangedAttackRetreat(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        if (attackController == null || target == null || agent == null)
            return;

        if (!attackController.IsRangedWeaponEquipped)
            return;

        if (!attackController.IsTargetTooCloseForRanged(target))
            return;

        if (attackController.IsBusy)
        {
            agent.isStopped = true;
            return;
        }

        attackController.FaceTarget(target);

        float retreatStopDistance = attackController.RangedRetreatStopDistanceFromTarget;
        if (retreatStopDistance <= 0f)
            return;

        Vector3 origin = attackController.AttackOriginTransform.position;
        Vector3 targetPos = target.position;
        Vector3 away = origin - targetPos;
        away.y = 0f;
        if (away.sqrMagnitude < 1e-8f)
        {
            away = attackController.AttackFacingTransform.forward;
            away.y = 0f;
        }

        if (away.sqrMagnitude < 1e-8f)
            away = Vector3.back;
        else
            away.Normalize();

        Vector3 desiredPos = targetPos + away * retreatStopDistance;
        agent.isStopped = false;
        agent.stoppingDistance = 0.25f;

        if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(origin + away * 2f);
    }

    public static void UpdateRangedEngagementMovement(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        if (attackController == null || agent == null || !attackController.IsRangedWeaponEquipped)
            return;

        if (target != null && attackController.IsTargetTooCloseForRanged(target))
        {
            UpdateRangedAttackRetreat(attackController, target, agent);
            return;
        }

        HoldPositionIfNotMelee(attackController, agent);
    }

    public static void HoldPositionIfNotMelee(EntityAttackController attackController, NavMeshAgent agent)
    {
        if (attackController == null || agent == null || attackController.IsMeleeWeaponEquipped)
            return;

        agent.isStopped = true;
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

        UpdateMeleeAttackApproach(attackController, target, agent);
        UpdateRangedEngagementMovement(attackController, target, agent);

        if (attackController.CanStrikeTarget(target))
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

    public enum TargetDetectedTickResult
    {
        Continue,
        Complete,
        Cancelled,
    }

    public static void NotifyAggroLost(EntityTargetDetectedTelegraph telegraph)
        => telegraph?.NotifyAggroLost();

    public static void BeginTargetDetected(
        EntityTargetDetectedTelegraph telegraph,
        Transform target,
        ref float fallbackRemaining)
    {
        if (telegraph != null)
            telegraph.Begin(target);
        else
            fallbackRemaining = EntityTargetDetectedTelegraph.FallbackDetectionDuration;
    }

    public static TargetDetectedTickResult TickTargetDetectedPhase(
        NavMeshAgent agent,
        FieldOfViewComponent fieldOfView,
        EntityTargetDetectedTelegraph telegraph,
        ref float fallbackRemaining)
    {
        if (agent != null)
            agent.isStopped = true;

        if (fieldOfView == null || !fieldOfView.HasTarget)
        {
            telegraph?.Cancel();
            return TargetDetectedTickResult.Cancelled;
        }

        if (telegraph != null)
        {
            if (telegraph.Tick(Time.deltaTime))
            {
                telegraph.FinishPhase();
                return TargetDetectedTickResult.Complete;
            }

            return TargetDetectedTickResult.Continue;
        }

        fallbackRemaining -= Time.deltaTime;
        return fallbackRemaining <= 0f
            ? TargetDetectedTickResult.Complete
            : TargetDetectedTickResult.Continue;
    }
}
