using UnityEngine;

/// <summary>Horizontal push applied to a victim along the attacker's forward at hit time.</summary>
public struct PushbackContext
{
    public Vector3 direction;
    public float distance;
    public float duration;
}
