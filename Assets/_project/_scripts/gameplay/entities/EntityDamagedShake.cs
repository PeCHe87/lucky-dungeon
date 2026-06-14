using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Subscribes to <see cref="CombatEntityHealth.Damaged"/> and applies a short local-space shake on a target transform.
/// Use a visual child — never the transform that owns <see cref="NavMeshAgent"/>.
/// </summary>
public sealed class EntityDamagedShake : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField, Min(0.01f)] float shakeDuration = 0.15f;
    [SerializeField, Min(0f)] float shakeAmplitude = 0.04f;
    [Tooltip("Extra amplitude added per damage point, clamped by Amplitude Bonus Max.")]
    [SerializeField, Min(0f)] float amplitudeBonusPerDamage;
    [SerializeField, Min(0f)] float amplitudeBonusMax = 0.04f;

    CombatEntityHealth _health;
    Vector3 _baselineLocalPosition;
    float _shakeTimeRemaining;
    float _activeAmplitude;
    bool _wasShaking;

    void Reset()
    {
        target = transform;
    }

    void Awake()
    {
        _health = GetComponentInParent<CombatEntityHealth>();
        ResolveShakeTarget();
        _baselineLocalPosition = target.localPosition;
    }

    void OnEnable()
    {
        if (_health != null)
            _health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (_health != null)
            _health.Damaged -= OnDamaged;
        ResetShakeOffset();
        _shakeTimeRemaining = 0f;
        _wasShaking = false;
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
            if (_wasShaking)
                ResetShakeOffset();
            return;
        }

        _wasShaking = true;
        _shakeTimeRemaining -= Time.deltaTime;
        float envelope = shakeDuration > 0f ? Mathf.Clamp01(_shakeTimeRemaining / shakeDuration) : 0f;
        Vector3 offset = Random.insideUnitSphere * (_activeAmplitude * envelope);
        target.localPosition = _baselineLocalPosition + offset;
    }

    void ResolveShakeTarget()
    {
        if (target == null)
            target = transform;

        if (target.GetComponent<NavMeshAgent>() != null)
        {
            Debug.LogWarning(
                $"{nameof(EntityDamagedShake)} on '{name}' targets a transform with NavMeshAgent — using this component's transform instead.",
                this);
            target = transform;
        }
    }

    void ResetShakeOffset()
    {
        if (target == null)
            return;

        target.localPosition = _baselineLocalPosition;
        _wasShaking = false;
    }
}
