using UnityEngine;

/// <summary>Optional attacker metadata forwarded with <see cref="IDamageable.TakeDamage"/>.</summary>
public readonly struct DamageHitInfo
{
    public readonly Transform attacker;
    public readonly Vector3 pushDirection;
    public readonly float deathPushForce;

    public bool HasAttacker => attacker != null;
    public bool HasPushDirection => pushDirection.sqrMagnitude > 1e-8f;

    public DamageHitInfo(Transform attacker)
        : this(attacker, default, 0f)
    {
    }

    public DamageHitInfo(Transform attacker, Vector3 pushDirection, float deathPushForce)
    {
        this.attacker = attacker;
        this.pushDirection = pushDirection;
        this.deathPushForce = Mathf.Max(0f, deathPushForce);
    }
}
