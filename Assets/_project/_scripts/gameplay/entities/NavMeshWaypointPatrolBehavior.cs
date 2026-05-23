using UnityEngine;
using UnityEngine.AI;

public class NavMeshWaypointPatrolBehavior : MonoBehaviour, IEntityNavBehavior
{
    [SerializeField] Transform[] waypoints;
    [SerializeField] float arrivalRadius = 0.5f;
    [Tooltip("How far to search for a valid NavMesh point around each waypoint.")]
    [SerializeField] float samplePositionRadius = 2f;
    [SerializeField] WaypointPatrolMode mode = WaypointPatrolMode.Loop;

    int _index;
    int _direction = 1;
    bool _stopped;
    int _lastSetIndex = -1;

    public void Tick(NavMeshAgent agent)
    {
        if (agent == null)
            return;

        agent.stoppingDistance = arrivalRadius;

        if (_stopped || waypoints == null || waypoints.Length == 0)
        {
            agent.isStopped = true;
            return;
        }

        agent.isStopped = false;

        Transform target = waypoints[_index];
        if (target == null)
            return;

        Vector3 pos = agent.transform.position;
        if (FlatDistanceSq(pos, target.position) <= arrivalRadius * arrivalRadius)
        {
            AdvanceIndex();
            if (_stopped)
            {
                agent.isStopped = true;
                agent.ResetPath();
                return;
            }

            target = waypoints[_index];
            if (target == null)
                return;

            _lastSetIndex = -1;
        }

        if (_lastSetIndex != _index)
            TrySetDestination(agent, target.position);
    }

    static float FlatDistanceSq(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    void TrySetDestination(NavMeshAgent agent, Vector3 worldTarget)
    {
        if (NavMesh.SamplePosition(worldTarget, out NavMeshHit hit, samplePositionRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
        else
            agent.SetDestination(worldTarget);

        _lastSetIndex = _index;
    }

    void AdvanceIndex()
    {
        int n = waypoints.Length;
        if (n == 0)
            return;

        switch (mode)
        {
            case WaypointPatrolMode.Loop:
                _index = (_index + 1) % n;
                break;

            case WaypointPatrolMode.PingPong:
                if (n <= 1)
                    return;

                _index += _direction;
                if (_index >= n)
                {
                    _index = n - 2;
                    if (_index < 0)
                        _index = 0;
                    _direction = -1;
                }
                else if (_index < 0)
                {
                    _index = n > 1 ? 1 : 0;
                    _direction = 1;
                }

                break;

            case WaypointPatrolMode.StopAtEnd:
                if (_index >= n - 1)
                    _stopped = true;
                else
                    _index++;
                break;
        }
    }

    public void ResetPatrol()
    {
        _stopped = false;
        _index = 0;
        _direction = 1;
        _lastSetIndex = -1;
    }
}
