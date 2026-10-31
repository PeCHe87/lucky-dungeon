using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Channels nearby <see cref="SurvivorAlly"/> units within a radius until they are marked connected.
/// Connected survivors follow authored <see cref="followSlots"/> Transforms around the player.
/// </summary>
public sealed class PlayerSurvivorConnector : MonoBehaviour
{
    [Header("Connect")]
    [SerializeField, Min(0.1f)] float connectRadius = 3.5f;
    [SerializeField, Min(0.01f)] float connectDuration = 1.75f;
    [SerializeField] LayerMask survivorLayerMask = ~0;

    [Header("Follow slots")]
    [Tooltip("Empty child Transforms around the player. Each connected survivor is assigned one.")]
    [SerializeField] Transform[] followSlots;

    readonly List<SurvivorAlly> _connectedSurvivors = new List<SurvivorAlly>(8);
    SurvivorAlly[] _slotOccupants;

    SurvivorAlly _channelTarget;
    float _channelElapsed;

    public float ConnectRadius => connectRadius;
    public float ConnectDuration => connectDuration;
    public IReadOnlyList<SurvivorAlly> ConnectedSurvivors => _connectedSurvivors;

    void Awake()
    {
        EnsureOccupantBuffer();
    }

    void Update()
    {
        PruneConnected();

        if (!TryGetFreeSlotIndex(out _))
        {
            ClearChannel();
            return;
        }

        SurvivorAlly nearest = FindNearestConnectable();
        if (nearest == null)
        {
            ClearChannel();
            return;
        }

        if (_channelTarget != nearest)
        {
            if (_channelTarget != null)
                _channelTarget.SetConnectProgress(0f, false);

            _channelTarget = nearest;
            _channelElapsed = 0f;
        }

        float duration = Mathf.Max(0.01f, connectDuration);
        _channelElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_channelElapsed / duration);
        _channelTarget.SetConnectProgress(t, true);

        if (_channelElapsed < duration)
            return;

        if (!TryGetFreeSlotIndex(out int slotIndex))
        {
            ClearChannel();
            return;
        }

        SurvivorAlly connected = _channelTarget;
        Transform slot = followSlots[slotIndex];
        ClearChannel();
        connected.MarkConnected(transform, slot);
        _slotOccupants[slotIndex] = connected;
        if (!_connectedSurvivors.Contains(connected))
            _connectedSurvivors.Add(connected);
    }

    void OnDisable()
    {
        ClearChannel();
    }

    void OnDrawGizmosSelected()
    {
        NavMeshChaseDriver.DrawXZWireDisc(transform.position, connectRadius, new Color(0.3f, 0.85f, 1f, 0.9f));

        if (followSlots == null)
            return;

        Color previous = Gizmos.color;
        for (int i = 0; i < followSlots.Length; i++)
        {
            Transform slot = followSlots[i];
            if (slot == null)
                continue;

            Gizmos.color = new Color(0.2f, 1f, 0.55f, 0.95f);
            Gizmos.DrawWireSphere(slot.position, 0.25f);
            Gizmos.DrawLine(transform.position, slot.position);
        }

        Gizmos.color = previous;
    }

    SurvivorAlly FindNearestConnectable()
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            connectRadius,
            survivorLayerMask,
            QueryTriggerInteraction.Ignore);

        float bestDistSq = float.MaxValue;
        SurvivorAlly best = null;
        float radiusSq = connectRadius * connectRadius;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            SurvivorAlly ally = hit.GetComponentInParent<SurvivorAlly>();
            if (ally == null || !ally.CanConnect || ally.transform == transform)
                continue;

            if (!NavMeshChaseDriver.IsWithinXZRadius(transform.position, ally.transform.position, connectRadius))
                continue;

            float distSq = NavMeshChaseDriver.FlatDistanceSq(transform.position, ally.transform.position);
            if (distSq > radiusSq || distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            best = ally;
        }

        return best;
    }

    void ClearChannel()
    {
        if (_channelTarget != null)
            _channelTarget.SetConnectProgress(0f, false);

        _channelTarget = null;
        _channelElapsed = 0f;
    }

    void PruneConnected()
    {
        EnsureOccupantBuffer();

        for (int i = _connectedSurvivors.Count - 1; i >= 0; i--)
        {
            SurvivorAlly ally = _connectedSurvivors[i];
            if (ally != null && ally.IsConnected)
                continue;

            FreeSlotFor(ally);
            _connectedSurvivors.RemoveAt(i);
        }

        if (_slotOccupants == null)
            return;

        for (int i = 0; i < _slotOccupants.Length; i++)
        {
            SurvivorAlly occupant = _slotOccupants[i];
            if (occupant != null && !occupant.IsConnected)
                _slotOccupants[i] = null;
        }
    }

    void FreeSlotFor(SurvivorAlly ally)
    {
        if (ally == null || _slotOccupants == null)
            return;

        for (int i = 0; i < _slotOccupants.Length; i++)
        {
            if (_slotOccupants[i] == ally)
                _slotOccupants[i] = null;
        }
    }

    bool TryGetFreeSlotIndex(out int index)
    {
        EnsureOccupantBuffer();
        index = -1;

        if (followSlots == null || followSlots.Length == 0)
            return false;

        for (int i = 0; i < followSlots.Length; i++)
        {
            if (followSlots[i] == null)
                continue;

            SurvivorAlly occupant = i < _slotOccupants.Length ? _slotOccupants[i] : null;
            if (occupant != null && occupant.IsConnected)
                continue;

            index = i;
            return true;
        }

        return false;
    }

    void EnsureOccupantBuffer()
    {
        int count = followSlots != null ? followSlots.Length : 0;
        if (_slotOccupants != null && _slotOccupants.Length == count)
            return;

        var next = count > 0 ? new SurvivorAlly[count] : null;
        if (_slotOccupants != null && next != null)
        {
            int copy = Mathf.Min(_slotOccupants.Length, next.Length);
            for (int i = 0; i < copy; i++)
                next[i] = _slotOccupants[i];
        }

        _slotOccupants = next;
    }
}
