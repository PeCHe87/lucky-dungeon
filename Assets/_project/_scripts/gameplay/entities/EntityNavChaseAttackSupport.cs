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

    public static bool IsNavLocomotionBlocked(GameObject entityRoot) =>
        entityRoot != null
        && entityRoot.TryGetComponent(out PushbackReceiver receiver)
        && receiver.IsNavLocomotionBlocked;

    /// <summary>Returns true when locomotion was enabled; false when blocked (agent left stopped).</summary>
    public static bool TryEnableNavLocomotion(NavMeshAgent agent, GameObject entityRoot)
    {
        if (agent == null)
            return false;

        if (IsNavLocomotionBlocked(entityRoot))
        {
            agent.isStopped = true;
            return false;
        }

        agent.isStopped = false;
        ReconcileNavAgentRotation(agent, entityRoot != null ? entityRoot.GetComponent<EntityAttackController>() : null);
        return true;
    }

    public static void SyncLocomotionFacing(NavMeshAgent agent, GameObject entityRoot)
    {
        if (agent == null || !IsNavAgentLocomoting(agent))
            return;

        EntityAttackController attackController =
            entityRoot != null ? entityRoot.GetComponent<EntityAttackController>() : null;

        ReconcileNavAgentRotation(agent, attackController);

        if (attackController != null && attackController.IsCombatFacingLocked)
            return;

        if (!agent.updateRotation)
        {
            Vector3 moveDir = agent.velocity;
            if (moveDir.sqrMagnitude < 1e-8f)
                moveDir = agent.desiredVelocity;

            Transform facingRoot = attackController != null
                ? attackController.AttackFacingTransform
                : agent.transform;

            RotateTowardFlatDirection(facingRoot, moveDir, agent.angularSpeed);
        }
    }

    static void ReconcileNavAgentRotation(NavMeshAgent agent, EntityAttackController attackController)
    {
        if (agent == null)
            return;

        agent.updateRotation = true;
        attackController?.SyncNavAgentFacingLock(agent);
    }

    public enum AttackPhaseBeginResult
    {
        Failed,
        PreAttacking,
        Attacking,
        AttackReady,
    }

    public enum AttackTickResult
    {
        StayAttacking,
        StayPreAttacking,
        StayAttackReady,
        ResumeChasing,
        ResumeSearching,
    }

    public static void ResetToChaseAfterDamageInterrupt(NavMeshAgent agent, GameObject entityRoot, ref bool hasChaseSample)
    {
        hasChaseSample = false;
        TryEnableNavLocomotion(agent, entityRoot);
    }

    public static void SyncChaseFacing(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        if (attackController == null || target == null)
            return;

        attackController.SyncNavAgentFacingLock(agent);

        if (attackController.IsCombatFacingLocked)
            return;

        if (IsNavAgentLocomoting(agent))
        {
            SyncLocomotionFacing(agent, attackController.gameObject);
            return;
        }

        attackController.FaceTarget(target);
    }

    static void RotateTowardFlatDirection(Transform facingRoot, Vector3 direction, float degreesPerSecond)
    {
        if (facingRoot == null)
            return;

        direction.y = 0f;
        if (direction.sqrMagnitude < 1e-8f)
            return;

        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        facingRoot.rotation = Quaternion.RotateTowards(
            facingRoot.rotation,
            target,
            degreesPerSecond * Time.deltaTime);
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
        if (!TryEnableNavLocomotion(agent, attackController.gameObject))
            return;

        agent.stoppingDistance = stopDistance;
        agent.SetDestination(target.position);
        if (!IsNavAgentLocomoting(agent))
            attackController.FaceTarget(target);
    }

    public static AttackPhaseBeginResult TryBeginPreAttackPhase(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        if (!TryBeginAttackPhase(attackController, target, agent))
            return AttackPhaseBeginResult.Failed;

        if (attackController.IsWaitingForNextAttack)
            return AttackPhaseBeginResult.AttackReady;

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

        attackController.SyncNavAgentFacingLock(agent);

        if (attackController.IsOnlyDamageBlocked)
        {
            if (agent != null)
                TryEnableNavLocomotion(agent, attackController.gameObject);
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

    public static AttackTickResult TickAttackReadyPhase(
        EntityAttackController attackController,
        FieldOfViewComponent fieldOfView,
        NavMeshAgent agent,
        Vector3 origin)
    {
        if (attackController == null)
            return AttackTickResult.ResumeChasing;

        attackController.SyncNavAgentFacingLock(agent);

        if (attackController.IsInTakeDamageFsm)
            return AttackTickResult.StayAttackReady;

        if (!fieldOfView.HasTarget)
            return AttackTickResult.ResumeSearching;

        if (attackController.IsOnlyDamageBlocked)
        {
            if (ShouldHoldAttackReadyDuringCooldown(attackController))
                return HoldAttackReadyDuringCooldown(attackController, fieldOfView.Target, agent);

            if (agent != null)
                TryEnableNavLocomotion(agent, attackController.gameObject);
            return AttackTickResult.ResumeChasing;
        }

        Transform target = fieldOfView.Target;
        attackController.FaceTarget(target);

        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        if (ShouldHoldAttackReadyDuringCooldown(attackController))
            return HoldAttackReadyDuringCooldown(attackController, target, agent);

        if (ShouldAbortAttackEngagement(attackController, target, agent, out AttackTickResult abortResult))
            return abortResult;

        UpdateMeleeAttackApproach(attackController, target, agent);
        UpdateRangedEngagementMovement(attackController, target, agent);

        if (!attackController.CanStrikeTarget(target))
            return StayAttackReadyWithSustain(attackController);

        if (attackController.EnablePreAttackTelegraph && attackController.TryBeginPreAttack(target))
            return AttackTickResult.StayPreAttacking;

        if (attackController.TryAttackTarget(target))
            return AttackTickResult.StayAttacking;

        return StayAttackReadyWithSustain(attackController);
    }

    static AttackTickResult StayAttackReadyWithSustain(EntityAttackController attackController)
    {
        if (attackController.IsInTakeDamageFsm)
            return AttackTickResult.StayAttackReady;

        if (!attackController.IsHoldingAttackReadyStance)
            attackController.EnterBetweenAttackRecovery();
        else
            attackController.SustainBetweenAttackRecovery();

        return AttackTickResult.StayAttackReady;
    }

    static bool ShouldHoldAttackReadyDuringCooldown(EntityAttackController attackController) =>
        attackController != null
        && attackController.EnableBetweenAttackRecovery
        && attackController.IsWaitingForNextAttack;

    static AttackTickResult HoldAttackReadyDuringCooldown(
        EntityAttackController attackController,
        Transform target,
        NavMeshAgent agent)
    {
        attackController.FaceTarget(target);
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }

        return StayAttackReadyWithSustain(attackController);
    }

    public static AttackTickResult TickAttackPhase(
        EntityAttackController attackController,
        FieldOfViewComponent fieldOfView,
        NavMeshAgent agent,
        Vector3 origin)
    {
        if (attackController == null)
            return AttackTickResult.ResumeChasing;

        attackController.SyncNavAgentFacingLock(agent);

        if (attackController.IsOnlyDamageBlocked)
        {
            if (agent != null)
                TryEnableNavLocomotion(agent, attackController.gameObject);
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

        if (ShouldHoldAttackReadyDuringCooldown(attackController))
            return HoldAttackReadyDuringCooldown(attackController, target, agent);

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
            attackController.SyncNavAgentFacingLock(agent);
            agent.isStopped = true;
            return;
        }

        attackController.SyncNavAgentFacingLock(agent);

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
        if (!TryEnableNavLocomotion(agent, attackController.gameObject))
            return;

        agent.stoppingDistance = 0.25f;

        if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(origin + away * 2f);

        if (!IsNavAgentLocomoting(agent))
            attackController.FaceTarget(target);
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

        if (ShouldHoldAttackReadyDuringCooldown(attackController))
            return HoldAttackReadyDuringCooldown(attackController, target, agent);

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
            TryEnableNavLocomotion(agent, attackController.gameObject);
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

    static bool IsNavAgentLocomoting(NavMeshAgent agent, float speedThreshold = 0.05f)
    {
        if (agent == null || !agent.isOnNavMesh || !agent.enabled || agent.isStopped)
            return false;

        float thresholdSq = speedThreshold * speedThreshold;
        if (agent.velocity.sqrMagnitude > thresholdSq)
            return true;
        if (agent.desiredVelocity.sqrMagnitude > thresholdSq)
            return true;

        return agent.hasPath
            && !agent.pathPending
            && agent.remainingDistance > agent.stoppingDistance + 0.05f;
    }
}
