using UnityEngine;

/// <summary>
/// Hold-driven camera yaw pivot around the follow target (player stays centered).
/// Pair with <see cref="UiLevelRotateHoldButton"/> on left/right UI buttons.
/// </summary>
public sealed class UiLevelRotator : MonoBehaviour
{
    [SerializeField] ArcheroStyleCameraFollow cameraFollow;
    [Tooltip("Degrees per second applied while a rotate button is held.")]
    [SerializeField] float rotationSpeedDegreesPerSecond = 90f;

    float _holdDirection;

    void Awake()
    {
        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<ArcheroStyleCameraFollow>();
    }

    void Update()
    {
        if (cameraFollow == null || Mathf.Approximately(_holdDirection, 0f))
            return;

        cameraFollow.AddYawDegrees(_holdDirection * rotationSpeedDegreesPerSecond * Time.deltaTime);
    }

    public void BeginHold(float direction)
    {
        if (Mathf.Approximately(direction, 0f))
            return;
        _holdDirection = Mathf.Sign(direction);
    }

    public void EndHold(float direction)
    {
        if (Mathf.Approximately(direction, 0f))
            return;

        // Only clear if this release matches the active hold direction.
        if (Mathf.Sign(direction) == Mathf.Sign(_holdDirection))
            _holdDirection = 0f;
    }
}
