using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

static class FSMNavMesh
{
    private const string AgentKey = "fsm.navAgent";

    public static NavMeshAgent GetAgent(EntityBlackboard bb)
    {
        var agent = bb.Get<NavMeshAgent>(AgentKey);
        if (agent != null) return agent;

        agent = bb.Self.GetComponent<NavMeshAgent>();
        if (agent != null) bb.Set(AgentKey, agent);
        return agent;
    }

    /// <summary>True when pathing APIs (<see cref="NavMeshAgent.isStopped"/>, Move, SetDestination) are safe.</summary>
    public static bool IsUsable(NavMeshAgent agent) =>
        agent != null && agent.enabled && agent.isOnNavMesh;

    public static void Stop(NavMeshAgent agent)
    {
        if (!IsUsable(agent))
            return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    public static void Resume(NavMeshAgent agent)
    {
        if (!IsUsable(agent))
            return;
        agent.isStopped = false;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// IDLE STATE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The entity stands still for 'duration' seconds.
/// Writes "fsm.timer" to the blackboard — use TimerExpired condition to exit.
/// </summary>
public class IdleStateHandler : IStateHandler
{
    public void OnEnter(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        bb.Set("fsm.timer", 0f); // Reset the shared run timer

        var agent = FSMNavMesh.GetAgent(bb);
        FSMNavMesh.Stop(agent);
        if (agent != null)
            EntityNavChaseAttackSupport.InvalidateChaseDestinationCaches(bb.Self.gameObject);
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        var timer = bb.Get<float>("fsm.timer") + dt;
        bb.Set("fsm.timer", timer);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        bb.Set("fsm.timer", 0f);
        FSMNavMesh.Resume(FSMNavMesh.GetAgent(bb));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PATROL STATE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The entity picks random waypoints inside a radius and walks between them.
/// PatrolCenter is locked to the entity's position when it first enters this state.
///
/// NOTE: Movement is driven via NavMeshAgent when present.
/// </summary>
public class PatrolStateHandler : IStateHandler
{
    public void OnEnter(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        var radius = FSMParamUtils.GetFloat(@params, "radius", 5f);
        var speed = FSMParamUtils.GetFloat(@params, "speed", 2f);
        var waypointThreshold = FSMParamUtils.GetFloat(@params, "waypointThreshold", 0.3f);
        bb.PatrolCenter = bb.Self.position; // Anchor to entry position

        var agent = FSMNavMesh.GetAgent(bb);

        if (FSMNavMesh.IsUsable(agent))
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = 0f;
        }

        PickNewWaypoint(bb, agent, radius);
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        var radius = FSMParamUtils.GetFloat(@params, "radius", 5f);
        var speed = FSMParamUtils.GetFloat(@params, "speed", 2f);
        var waypointThreshold = FSMParamUtils.GetFloat(@params, "waypointThreshold", 0.3f);
        var agent = FSMNavMesh.GetAgent(bb);

        if (agent == null)
        {
            // Fallback (shouldn't happen on fsm_patrol_entity, which has a NavMeshAgent)
            var dir = bb.PatrolTarget - bb.Self.position;
            dir.y = 0f;

            if (dir.magnitude < waypointThreshold)
            {
                PickNewWaypoint(bb, null, radius);
                return;
            }

            bb.Self.position += dir.normalized * (speed * dt);
            bb.Self.rotation = Quaternion.LookRotation(dir.normalized);
            return;
        }

        if (!FSMNavMesh.IsUsable(agent))
            return;

        agent.speed = speed;

        if (!agent.pathPending && agent.remainingDistance <= waypointThreshold)
            PickNewWaypoint(bb, agent, radius);

        EntityNavChaseAttackSupport.SyncLocomotionFacing(agent, bb.Self.gameObject);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params) { }

    private static void PickNewWaypoint(EntityBlackboard bb, NavMeshAgent agent, float radius)
    {
        bb.PatrolTarget = NavMeshIdlePatrolCycle.SampleRandomPointInRadius(bb.PatrolCenter, radius);
        if (FSMNavMesh.IsUsable(agent))
            agent.SetDestination(bb.PatrolTarget);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// CHASE STATE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The entity moves toward bb.Target.
/// Stops moving once within stoppingDistance — exits via TargetReached condition.
///
/// NOTE: Movement is driven via NavMeshAgent when present.
/// </summary>
public class ChaseStateHandler : IStateHandler
{
    public void OnEnter(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        var agent = FSMNavMesh.GetAgent(bb);
        if (agent != null)
            EntityNavChaseAttackSupport.TryEnableNavLocomotion(agent, bb.Self.gameObject);
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        if (bb.Target == null) return;

        var agent = FSMNavMesh.GetAgent(bb);

        var attack = bb.Self.GetComponent<EntityAttackController>();
        if (attack != null && attack.IsTargetInAttackRange(bb.Target))
        {
            FSMNavMesh.Stop(agent);
            return;
        }

        var speed = FSMParamUtils.GetFloat(@params, "speed", 4f);
        var stoppingDistance = FSMParamUtils.GetFloat(@params, "stoppingDistance", 1f);

        if (!FSMNavMesh.IsUsable(agent))
        {
            // Fallback (shouldn't happen on fsm_patrol_entity, which has a NavMeshAgent)
            var dir = bb.Target.position - bb.Self.position;
            dir.y = 0f;
            if (dir.magnitude <= stoppingDistance) return;
            bb.Self.position += dir.normalized * (speed * dt);
            bb.Self.rotation = Quaternion.LookRotation(dir.normalized);
            return;
        }

        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
        if (!EntityNavChaseAttackSupport.TryEnableNavLocomotion(agent, bb.Self.gameObject))
            return;

        agent.SetDestination(bb.Target.position);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params) { }
}

// ─────────────────────────────────────────────────────────────────────────────
// ATTACK STATE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Stands still and attacks bb.Target via <see cref="EntityAttackController"/>.
/// Exits via FSM transitions (TargetLost, TargetBeyondAttackRange, etc.).
/// </summary>
public class AttackStateHandler : IStateHandler
{
    EntityAttackController GetAttackController(EntityBlackboard bb) =>
        bb.Self.GetComponent<EntityAttackController>();

    public void OnEnter(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        var agent = FSMNavMesh.GetAgent(bb);
        FSMNavMesh.Stop(agent);

        var attack = GetAttackController(bb);
        if (attack != null && bb.Target != null)
        {
            EntityNavChaseAttackSupport.UpdateMeleeAttackApproach(attack, bb.Target, agent);
            EntityNavChaseAttackSupport.UpdateRangedEngagementMovement(attack, bb.Target, agent);
            if (attack.CanStrikeTarget(bb.Target))
                attack.TryAttackTarget(bb.Target);
        }
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        if (bb.Target == null)
            return;

        var attack = GetAttackController(bb);
        if (attack == null)
            return;

        var agent = FSMNavMesh.GetAgent(bb);
        attack.SyncNavAgentFacingLock(agent);
        attack.FaceTarget(bb.Target);

        if (attack.IsBusy || attack.IsWaitingForNextAttack)
        {
            FSMNavMesh.Stop(agent);
            return;
        }

        EntityNavChaseAttackSupport.UpdateMeleeAttackApproach(attack, bb.Target, agent);
        EntityNavChaseAttackSupport.UpdateRangedEngagementMovement(attack, bb.Target, agent);

        if (attack.CanStrikeTarget(bb.Target))
            attack.TryAttackTarget(bb.Target);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params) { }
}

// ─────────────────────────────────────────────────────────────────────────────
// TAKE DAMAGE STATE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Brief hit-react interrupt: entity stands still for 'duration' seconds, then returns to the prior FSM state.
/// Exit is handler-driven via <see cref="AIStateMachine.ForceReturnFromInterrupt"/>.
/// </summary>
public class TakeDamageStateHandler : IStateHandler
{
    const string HitReactTimerKey = "fsm.hitReactTimer";

    public void OnEnter(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        bb.Set(HitReactTimerKey, 0f);

        var agent = FSMNavMesh.GetAgent(bb);
        FSMNavMesh.Stop(agent);
        if (agent != null)
            EntityNavChaseAttackSupport.InvalidateChaseDestinationCaches(bb.Self.gameObject);
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        var duration = FSMParamUtils.GetFloat(@params, "duration", 0.3f);
        var timer = bb.Get<float>(HitReactTimerKey) + dt;
        bb.Set(HitReactTimerKey, timer);

        if (timer < duration)
            return;

        var fsm = bb.Self.GetComponent<AIStateMachine>();
        if (fsm != null)
            fsm.ForceReturnFromInterrupt();
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        bb.Set(HitReactTimerKey, 0f);

        var agent = FSMNavMesh.GetAgent(bb);
        if (FSMNavMesh.IsUsable(agent))
            EntityNavChaseAttackSupport.TryEnableNavLocomotion(agent, bb.Self.gameObject);

        EntityNavChaseAttackSupport.InvalidateChaseDestinationCaches(bb.Self.gameObject);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DIE STATE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Terminal death state: disables colliders, waits destroyDelay, then destroys the GameObject.
/// When <see cref="EntityRagdoll"/> is active, bone colliders stay enabled for physics.
/// </summary>
public class DieStateHandler : IStateHandler
{
    const string DieTimerKey = "fsm.dieTimer";

    public void OnEnter(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        bb.Set(DieTimerKey, 0f);

        var agent = FSMNavMesh.GetAgent(bb);
        if (agent != null)
        {
            if (agent.enabled)
            {
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.ResetPath();
                }
                agent.enabled = false;
            }
        }

        bool ragdollActive = bb.Self.TryGetComponent(out EntityRagdoll ragdoll) && ragdoll.IsActive;
        if (ragdollActive)
            return;

        var colliders = bb.Self.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        var destroyDelay = FSMParamUtils.GetFloat(@params, "destroyDelay", 1f);
        var timer = bb.Get<float>(DieTimerKey) + dt;
        bb.Set(DieTimerKey, timer);

        if (timer < destroyDelay)
            return;

        if (FSMParamUtils.GetBool(@params, "deactivateOnly", false))
            bb.Self.gameObject.SetActive(false);
        else
            Object.Destroy(bb.Self.gameObject);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params) { }
}
