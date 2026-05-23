using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// World object with HP; implements <see cref="IDamageable"/> so <see cref="MeleeWeapon"/> can destroy it.
/// </summary>
public sealed class BaseDestructibleObject : MonoBehaviour, IDamageable
{
    [SerializeField, Min(0.01f)] float maxHitPoints = 30f;
    [Tooltip("Seconds before the GameObject is destroyed after HP reaches zero (0 = end of frame).")]
    [SerializeField, Min(0f)] float destroyDelay;
    [SerializeField] UnityEvent onDestroyed = new UnityEvent();
    [SerializeField] UnityEvent<float> onDamaged = new UnityEvent<float>();
    [Tooltip("World anchor for floating damage text. Defaults to this transform.")]
    [SerializeField] Transform damageNumberAnchor;
    [SerializeField] float damageNumberHeightOffset = 0.5f;

    float _currentHitPoints;
    bool _broken;

    /// <summary>Fired after HP is reduced by a valid hit; argument is damage amount applied this frame.</summary>
    public event Action<float> Damaged;

    void Awake()
    {
        _currentHitPoints = maxHitPoints;
        onDestroyed ??= new UnityEvent();
        onDamaged ??= new UnityEvent<float>();
    }

    public void TakeDamage(float amount, DamageNumberStyle style)
    {
        if (_broken || amount <= 0f)
            return;

        _currentHitPoints -= amount;
        Damaged?.Invoke(amount);
        onDamaged.Invoke(amount);
        FloatingDamageTextPresenter.Instance?.Spawn(GetDamageNumberWorldPosition(), amount, style);
        if (_currentHitPoints > 0f)
            return;

        _currentHitPoints = 0f;
        _broken = true;
        onDestroyed?.Invoke();
        Destroy(gameObject, destroyDelay);
    }

    Vector3 GetDamageNumberWorldPosition()
    {
        Vector3 p = damageNumberAnchor != null ? damageNumberAnchor.position : transform.position;
        p.y += damageNumberHeightOffset;
        return p;
    }
}
