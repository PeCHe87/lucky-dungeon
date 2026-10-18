using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks whether the player is inside a building.
/// Place on the building root. Wire child enter/exit portals with <see cref="BuildingOccupancyTrigger"/>.
/// Enter portal → inside; exit portal → outside (OnTriggerEnter only).
/// </summary>
public sealed class BuildingOccupancy : MonoBehaviour
{
    static int s_playerLayer = -1;

    [SerializeField] UnityEvent onPlayerEntered = new UnityEvent();
    [SerializeField] UnityEvent onPlayerExited = new UnityEvent();

    bool _isPlayerInside;

    public bool IsPlayerInside => _isPlayerInside;

    /// <summary>Fired when the player becomes inside (enter trigger).</summary>
    public event Action PlayerEntered;

    /// <summary>Fired when the player becomes outside (exit trigger).</summary>
    public event Action PlayerExited;

    void Awake()
    {
        onPlayerEntered ??= new UnityEvent();
        onPlayerExited ??= new UnityEvent();
    }

    void OnDisable()
    {
        if (!_isPlayerInside)
            return;

        _isPlayerInside = false;
        PlayerExited?.Invoke();
        onPlayerExited.Invoke();
    }

    /// <summary>Called by an enter <see cref="BuildingOccupancyTrigger"/>.</summary>
    public void NotifyEnter(Collider other)
    {
        if (!IsValidPlayerCollider(other) || _isPlayerInside)
            return;

        _isPlayerInside = true;
        PlayerEntered?.Invoke();
        onPlayerEntered.Invoke();
    }

    /// <summary>Called by an exit <see cref="BuildingOccupancyTrigger"/>.</summary>
    public void NotifyExit(Collider other)
    {
        if (!IsValidPlayerCollider(other) || !_isPlayerInside)
            return;

        _isPlayerInside = false;
        PlayerExited?.Invoke();
        onPlayerExited.Invoke();
    }

    internal static bool IsValidPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (s_playerLayer < 0)
            s_playerLayer = LayerMask.NameToLayer("Player");

        if (s_playerLayer < 0 || other.gameObject.layer != s_playerLayer)
            return false;

        var health = other.GetComponentInParent<CombatEntityHealth>();
        return health != null && health.Alignment == EntityAlignment.Ally && !health.IsDefeated;
    }
}
