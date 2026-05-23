using UnityEngine;

/// <summary>
/// Example sensor component that detects the player (or any tagged target)
/// and writes it to the EntityBlackboard.
///
/// This is intentionally separate from the FSM — sensors feed the blackboard,
/// FSM conditions read the blackboard. They never need to know about each other.
///
/// Attach alongside AIStateMachine + EntityBlackboard.
/// </summary>
public class EnemyTargetSensor : MonoBehaviour
{
    [Tooltip("The layer(s) to scan for targets")]
    public LayerMask targetLayer;

    [Tooltip("Max detection radius. Should be >= the largest TargetInRange condition value.")]
    public float sensorRadius = 10f;

    [Tooltip("How many times per second to run the overlap scan (lower = cheaper)")]
    public float scanRate = 5f;

    private EntityBlackboard _bb;
    private float            _scanTimer;

    void Awake() => _bb = GetComponent<EntityBlackboard>();

    void Update()
    {
        _scanTimer += Time.deltaTime;
        if (_scanTimer < 1f / scanRate) return;
        _scanTimer = 0f;

        var hits = Physics.OverlapSphere(transform.position, sensorRadius, targetLayer);
        _bb.Target = hits.Length > 0 ? hits[0].transform : null;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sensorRadius);
    }
}
