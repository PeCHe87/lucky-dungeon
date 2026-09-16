using UnityEngine;

/// <summary>Horizontal push applied to a victim away from the attacker toward the hit (attacker → victim).</summary>
public struct PushbackContext
{
    public Vector3 direction;
    public float distance;
    public float duration;
}
