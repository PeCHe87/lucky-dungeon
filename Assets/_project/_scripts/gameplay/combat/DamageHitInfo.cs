using UnityEngine;

/// <summary>Optional attacker metadata forwarded with <see cref="IDamageable.TakeDamage"/>.</summary>
public readonly struct DamageHitInfo
{
    public readonly Transform attacker;

    public bool HasAttacker => attacker != null;

    public DamageHitInfo(Transform attacker) => this.attacker = attacker;
}
