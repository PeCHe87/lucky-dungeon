using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trigger zone: while the player overlaps, connected survivors detach and escape toward <see cref="safeGoal"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class SurvivorsSafeArea : MonoBehaviour
{
    [Tooltip("World destination survivors path to when evacuated (e.g. child named goal).")]
    [SerializeField] Transform safeGoal;

    readonly List<SurvivorAlly> _evacuateScratch = new List<SurvivorAlly>(8);

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[{nameof(SurvivorsSafeArea)}] Collider on '{name}' should be a trigger.", this);

        if (safeGoal == null)
        {
            Transform childGoal = transform.Find("goal");
            if (childGoal != null)
                safeGoal = childGoal;
        }
    }

    void OnTriggerEnter(Collider other) => TryEvacuate(other);

    void OnTriggerStay(Collider other) => TryEvacuate(other);

    void TryEvacuate(Collider other)
    {
        if (safeGoal == null || other == null)
            return;

        PlayerSurvivorConnector connector = other.GetComponentInParent<PlayerSurvivorConnector>();
        if (connector == null)
            return;

        IReadOnlyList<SurvivorAlly> connected = connector.ConnectedSurvivors;
        if (connected == null || connected.Count == 0)
            return;

        _evacuateScratch.Clear();
        for (int i = 0; i < connected.Count; i++)
        {
            SurvivorAlly ally = connected[i];
            if (ally != null && ally.IsConnected)
                _evacuateScratch.Add(ally);
        }

        for (int i = 0; i < _evacuateScratch.Count; i++)
            _evacuateScratch[i].BeginEscape(safeGoal);
    }
}
