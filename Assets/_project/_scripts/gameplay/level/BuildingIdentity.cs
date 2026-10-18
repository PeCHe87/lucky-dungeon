using UnityEngine;

/// <summary>
/// Identifies a building and reacts when the player enters or exits (via child <see cref="BuildingOccupancy"/>).
/// Place on the building root GameObject.
/// </summary>
public sealed class BuildingIdentity : MonoBehaviour
{
    const string DefaultCeilingName = "ceiling";

    [SerializeField] string buildingId = "building";
    [Tooltip("Hidden while the player is inside. Defaults to a child named 'ceiling'.")]
    [SerializeField] GameObject ceiling;

    [Header("Interior camera")]
    [SerializeField] ArcheroStyleCameraFollow cameraFollow;
    [Tooltip("Camera forward offset while the player is inside this building.")]
    [SerializeField] float insideForwardOffset = 5f;
    [Tooltip("Camera pitch (degrees) while the player is inside this building.")]
    [SerializeField] float insidePitchDegrees = 25f;

    BuildingOccupancy _occupancy;
    float _outdoorForwardOffset;
    float _outdoorPitchDegrees;
    bool _hasOutdoorOrbit;

    public string BuildingId => buildingId;

    void Awake()
    {
        if (ceiling == null)
        {
            Transform ceilingTransform = transform.Find(DefaultCeilingName);
            if (ceilingTransform != null)
                ceiling = ceilingTransform.gameObject;
        }

        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<ArcheroStyleCameraFollow>();

        _occupancy = GetComponent<BuildingOccupancy>();
        if (_occupancy == null)
            _occupancy = GetComponentInChildren<BuildingOccupancy>(true);
        if (_occupancy == null)
        {
            Debug.LogWarning(
                $"[{nameof(BuildingIdentity)}] No {nameof(BuildingOccupancy)} found on '{name}'.",
                this);
            return;
        }

        _occupancy.PlayerEntered += OnPlayerEntered;
        _occupancy.PlayerExited += OnPlayerExited;
    }

    void OnDestroy()
    {
        if (_occupancy == null)
            return;

        _occupancy.PlayerEntered -= OnPlayerEntered;
        _occupancy.PlayerExited -= OnPlayerExited;
    }

    void OnPlayerEntered()
    {
        Debug.Log($"[{nameof(BuildingIdentity)}] Player entered building '{buildingId}'.", this);
        SetCeilingVisible(false);
        ApplyInteriorCamera();
    }

    void OnPlayerExited()
    {
        Debug.Log($"[{nameof(BuildingIdentity)}] Player exited building '{buildingId}'.", this);
        SetCeilingVisible(true);
        RestoreOutdoorCamera();
    }

    void ApplyInteriorCamera()
    {
        if (cameraFollow == null)
            return;

        if (!_hasOutdoorOrbit)
        {
            _outdoorForwardOffset = cameraFollow.ForwardOffset;
            _outdoorPitchDegrees = cameraFollow.PitchDegrees;
            _hasOutdoorOrbit = true;
        }

        cameraFollow.SetOrbitOffsets(insideForwardOffset, insidePitchDegrees);
    }

    void RestoreOutdoorCamera()
    {
        if (cameraFollow == null || !_hasOutdoorOrbit)
            return;

        cameraFollow.SetOrbitOffsets(_outdoorForwardOffset, _outdoorPitchDegrees);
        _hasOutdoorOrbit = false;
    }

    void SetCeilingVisible(bool visible)
    {
        if (ceiling == null)
            return;

        ceiling.SetActive(visible);
    }
}
