using UnityEngine;

/// <summary>
/// Subscribes to <see cref="BaseDestructibleObject.Damaged"/> and applies a short local-space shake on a target transform.
/// </summary>
public sealed class DestructibleHitAnimation : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField, Min(0.01f)] float shakeDuration = 0.15f;
    [SerializeField, Min(0f)] float shakeAmplitude = 0.04f;
    [Tooltip("Extra amplitude added per damage point, clamped by Amplitude Bonus Max.")]
    [SerializeField, Min(0f)] float amplitudeBonusPerDamage;
    [SerializeField, Min(0f)] float amplitudeBonusMax = 0.04f;

    BaseDestructibleObject _destructible;
    Vector3 _baselineLocalPosition;
    float _shakeTimeRemaining;
    float _activeAmplitude;

    void Awake()
    {
        _destructible = GetComponent<BaseDestructibleObject>();
        if (target == null)
            target = transform;
        _baselineLocalPosition = target.localPosition;
    }

    void OnEnable()
    {
        if (_destructible != null)
            _destructible.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (_destructible != null)
            _destructible.Damaged -= OnDamaged;
        if (target != null)
            target.localPosition = _baselineLocalPosition;
        _shakeTimeRemaining = 0f;
    }

    void OnDamaged(float damageAmount)
    {
        float bonus = Mathf.Min(damageAmount * amplitudeBonusPerDamage, amplitudeBonusMax);
        _activeAmplitude = shakeAmplitude + bonus;
        _shakeTimeRemaining = shakeDuration;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        if (_shakeTimeRemaining <= 0f)
        {
            target.localPosition = _baselineLocalPosition;
            return;
        }

        _shakeTimeRemaining -= Time.deltaTime;
        float envelope = shakeDuration > 0f ? Mathf.Clamp01(_shakeTimeRemaining / shakeDuration) : 0f;
        Vector3 offset = Random.insideUnitSphere * (_activeAmplitude * envelope);
        target.localPosition = _baselineLocalPosition + offset;
    }
}

