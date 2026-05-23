using UnityEngine;

public enum WaypointPatrolMode
{
    Loop,
    PingPong,
    StopAtEnd
}

public class WaypointPatrolBehavior : MonoBehaviour, IEntityMoveBehavior
{
    [Tooltip("Position used for distance to waypoints. Defaults to this transform.")]
    [SerializeField] Transform moveRoot;
    [SerializeField] Transform[] waypoints;
    [SerializeField] float arrivalRadius = 0.5f;
    [SerializeField] WaypointPatrolMode mode = WaypointPatrolMode.Loop;

    int _index;
    int _direction = 1;
    bool _stopped;

    void Awake()
    {
        if (moveRoot == null)
            moveRoot = transform;
    }

    public Vector2 GetMoveIntent()
    {
        if (_stopped || waypoints == null || waypoints.Length == 0)
            return Vector2.zero;

        Transform target = waypoints[_index];
        if (target == null)
            return Vector2.zero;

        Vector3 pos = moveRoot.position;
        Vector3 delta = FlatDeltaTo(target.position, pos);
        float rSq = arrivalRadius * arrivalRadius;
        if (delta.sqrMagnitude <= rSq)
        {
            AdvanceIndex();
            if (_stopped)
                return Vector2.zero;

            target = waypoints[_index];
            if (target == null)
                return Vector2.zero;

            delta = FlatDeltaTo(target.position, pos);
        }

        if (delta.sqrMagnitude < 1e-8f)
            return Vector2.zero;

        delta.Normalize();
        return new Vector2(delta.x, delta.z);
    }

    static Vector3 FlatDeltaTo(Vector3 target, Vector3 from)
    {
        return new Vector3(target.x - from.x, 0f, target.z - from.z);
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
    }
}
