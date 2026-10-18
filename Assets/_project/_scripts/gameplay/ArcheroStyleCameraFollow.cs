using UnityEngine;

/// <summary>
/// Archero-style follow: smooth position only; fixed yaw/pitch. Zoom scales perspective FOV
/// or orthographic size (higher zoom = larger subject on screen).
/// Yaw rotates the camera around the follow target so the player stays centered.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ArcheroStyleCameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;

    [Header("Offset")]
    [Tooltip("Height above the target pivot (world up).")]
    [SerializeField] float verticalOffset = 10f;
    [Tooltip("Distance from the target on the horizontal plane, opposite to the camera look direction (from yaw).")]
    [SerializeField] float forwardOffset = 8f;

    [Header("Zoom")]
    [Tooltip("1 = base lens. Higher = zoom in (bigger subject). Lower = zoom out. Applies to FOV or orthographic size.")]
    [SerializeField, Min(0.01f)] float zoom = 1f;

    [Header("Zoom — perspective (field of view)")]
    [Tooltip("FOV when zoom = 1. Used when the camera is not orthographic.")]
    [SerializeField, Range(15f, 120f)] float baseFieldOfView = 55f;
    [SerializeField, Range(10f, 120f)] float minFieldOfView = 20f;
    [SerializeField, Range(10f, 120f)] float maxFieldOfView = 75f;

    [Header("Zoom — orthographic")]
    [Tooltip("Orthographic size when zoom = 1. Used when the camera is orthographic.")]
    [SerializeField, Min(0.01f)] float baseOrthographicSize = 8f;
    [Tooltip("Clamp for orthographic size after zoom (smaller = more zoomed in).")]
    [SerializeField, Min(0.01f)] float minOrthographicSize = 2f;
    [SerializeField, Min(0.01f)] float maxOrthographicSize = 24f;

    [Header("Fixed orientation")]
    [Tooltip("World Y rotation of the camera (pivots around the follow target). Does not follow the target's facing.")]
    [SerializeField] float yawDegrees;
    [Tooltip("Tilt down over the arena (typical Archero-style angle ~35–55).")]
    [SerializeField] float pitchDegrees = 48f;

    [Header("Smoothing")]
    [Tooltip("Lower = snappier position follow. Applied when the target moves; yaw pivots snap immediately.")]
    [SerializeField] float positionSmoothTime = 0.15f;
    [Tooltip("Smooth time for forward offset and pitch when SetOrbitOffsets is used.")]
    [SerializeField] float orbitSmoothTime = 0.35f;

    Vector3 _positionVelocity;
    Camera _camera;
    float _targetForwardOffset;
    float _targetPitchDegrees;
    float _forwardOffsetVelocity;
    float _pitchDegreesVelocity;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        _targetForwardOffset = forwardOffset;
        _targetPitchDegrees = pitchDegrees;
    }

    /// <summary>Higher = zoom in (smaller orthographic size or narrower FOV). Lower = zoom out.</summary>
    public float Zoom
    {
        get => zoom;
        set => zoom = Mathf.Max(0.01f, value);
    }

    /// <summary>World Y rotation of the camera around the follow target.</summary>
    public float YawDegrees
    {
        get => yawDegrees;
        set => yawDegrees = value;
    }

    /// <summary>Current horizontal orbit distance (may be mid-tween).</summary>
    public float ForwardOffset => forwardOffset;

    /// <summary>Current pitch in degrees (may be mid-tween).</summary>
    public float PitchDegrees => pitchDegrees;

    /// <summary>
    /// Smoothly eases forward offset and pitch toward the given values.
    /// </summary>
    public void SetOrbitOffsets(float newForwardOffset, float newPitchDegrees)
    {
        _targetForwardOffset = newForwardOffset;
        _targetPitchDegrees = newPitchDegrees;
    }

    /// <summary>
    /// Pivots look yaw around the follow target and snaps position onto the orbit
    /// so the player stays centered (no SmoothDamp slide between orbit points).
    /// </summary>
    public void AddYawDegrees(float deltaDegrees)
    {
        if (Mathf.Approximately(deltaDegrees, 0f))
            return;

        yawDegrees += deltaDegrees;
        ApplyOrientation();
        SnapPositionToOrbit();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        ApplyOrbitSmoothing();
        ApplyOrientation();
        ApplyLens();

        Vector3 desiredPosition = GetDesiredOrbitPosition();
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _positionVelocity,
            Mathf.Max(0.0001f, positionSmoothTime));
    }

    void ApplyOrbitSmoothing()
    {
        float smooth = Mathf.Max(0.0001f, orbitSmoothTime);
        forwardOffset = Mathf.SmoothDamp(
            forwardOffset,
            _targetForwardOffset,
            ref _forwardOffsetVelocity,
            smooth);
        pitchDegrees = Mathf.SmoothDamp(
            pitchDegrees,
            _targetPitchDegrees,
            ref _pitchDegreesVelocity,
            smooth);
    }

    void ApplyOrientation()
    {
        transform.rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
    }

    void ApplyLens()
    {
        if (_camera == null)
            return;

        if (_camera.orthographic)
        {
            float size = baseOrthographicSize / zoom;
            float lo = Mathf.Min(minOrthographicSize, maxOrthographicSize);
            float hi = Mathf.Max(minOrthographicSize, maxOrthographicSize);
            _camera.orthographicSize = Mathf.Clamp(size, lo, hi);
        }
        else
        {
            float fov = baseFieldOfView / zoom;
            float lo = Mathf.Min(minFieldOfView, maxFieldOfView);
            float hi = Mathf.Max(minFieldOfView, maxFieldOfView);
            _camera.fieldOfView = Mathf.Clamp(fov, lo, hi);
        }
    }

    void SnapPositionToOrbit()
    {
        if (target == null)
            return;

        transform.position = GetDesiredOrbitPosition();
        _positionVelocity = Vector3.zero;
    }

    Vector3 GetDesiredOrbitPosition()
    {
        Vector3 flatView = GetFlatViewForward();
        return target.position
            + Vector3.up * verticalOffset
            - flatView * forwardOffset;
    }

    Vector3 GetFlatViewForward()
    {
        Vector3 flatView = Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
        flatView.y = 0f;
        if (flatView.sqrMagnitude < 1e-6f)
            flatView = Vector3.forward;
        return flatView.normalized;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (target == null)
            return;

        Vector3 flatView = GetFlatViewForward();
        Vector3 desired = target.position + Vector3.up * verticalOffset - flatView * forwardOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(desired, 0.35f);
        Vector3 forward = Quaternion.Euler(pitchDegrees, yawDegrees, 0f) * Vector3.forward;
        Gizmos.DrawLine(desired, desired + forward * 3f);
    }
#endif
}
