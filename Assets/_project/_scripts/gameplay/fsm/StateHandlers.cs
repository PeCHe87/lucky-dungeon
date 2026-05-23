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
        if (agent != null)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        var timer = bb.Get<float>("fsm.timer") + dt;
        bb.Set("fsm.timer", timer);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params)
    {
        bb.Set("fsm.timer", 0f);

        var agent = FSMNavMesh.GetAgent(bb);
        if (agent != null) agent.isStopped = false;
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

        if (agent != null)
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

        agent.speed = speed;

        if (!agent.pathPending && agent.remainingDistance <= waypointThreshold)
            PickNewWaypoint(bb, agent, radius);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params) { }

    private static void PickNewWaypoint(EntityBlackboard bb, NavMeshAgent agent, float radius)
    {
        var angle          = Random.Range(0f, Mathf.PI * 2f);
        var r              = Random.Range(0f, radius);
        bb.PatrolTarget    = bb.PatrolCenter + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

        if (agent != null) agent.SetDestination(bb.PatrolTarget);
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
        {
            agent.isStopped = false;
        }
    }

    public void OnTick(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params, float dt)
    {
        if (bb.Target == null) return;

        var speed = FSMParamUtils.GetFloat(@params, "speed", 4f);
        var stoppingDistance = FSMParamUtils.GetFloat(@params, "stoppingDistance", 1f);

        var agent = FSMNavMesh.GetAgent(bb);

        if (agent == null)
        {
            // Fallback (shouldn't happen on fsm_patrol_entity, which has a NavMeshAgent)
            var dir = bb.Target.position - bb.Self.position;
            dir.y = 0f;
            if (dir.magnitude <= stoppingDistance) return;
            bb.Self.position += dir.normalized * (speed * dt);
            bb.Self.rotation = Quaternion.LookRotation(dir.normalized);
            return;
        }

        agent.isStopped = false;
        agent.speed = speed;
        agent.stoppingDistance = stoppingDistance;
        agent.SetDestination(bb.Target.position);
    }

    public void OnExit(EntityBlackboard bb, System.Collections.Generic.IReadOnlyList<FSMParam> @params) { }
}
