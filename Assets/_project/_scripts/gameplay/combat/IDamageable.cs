/// <summary>Implement on a <see cref="UnityEngine.MonoBehaviour"/> to receive damage from weapons.</summary>
public interface IDamageable
{
    /// <summary>Applies damage with default physical, non-critical presentation.</summary>
    void TakeDamage(float amount)
    {
        TakeDamage(amount, new DamageNumberStyle(DamageElement.Physical, false));
    }

    void TakeDamage(float amount, DamageNumberStyle style);
}
