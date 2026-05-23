using UnityEngine;

/// <summary>Orients this transform to face a camera (full billboard or Y-axis only for top-down).</summary>
public sealed class BillboardFacingCamera : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [Tooltip("When enabled, only rotates around world Y toward the camera (good for isometric / top-down).")]
    [SerializeField] bool lockYAxis = true;

    void LateUpdate()
    {
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        Vector3 toCamera = cam.transform.position - transform.position;
        if (toCamera.sqrMagnitude < 1e-8f)
            return;

        if (lockYAxis)
        {
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-8f)
                return;
            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(-toCamera.normalized, Vector3.up);
        }
    }
}
