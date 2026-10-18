using UnityEngine;

/// <summary>
/// Portal trigger for building occupancy. Place on a trigger collider child of the building.
/// Enter: touching marks the player inside. Exit: touching marks the player outside.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class BuildingOccupancyTrigger : MonoBehaviour
{
    public enum Kind
    {
        Enter = 0,
        Exit = 1,
    }

    [SerializeField] BuildingOccupancy occupancy;
    [SerializeField] Kind kind = Kind.Enter;

    void Awake()
    {
        if (occupancy == null)
            occupancy = GetComponentInParent<BuildingOccupancy>();

        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[{nameof(BuildingOccupancyTrigger)}] Collider on '{name}' should be a trigger.", this);
    }

    void OnTriggerEnter(Collider other)
    {
        if (occupancy == null)
            return;

        if (kind == Kind.Enter)
            occupancy.NotifyEnter(other);
        else
            occupancy.NotifyExit(other);
    }
}
