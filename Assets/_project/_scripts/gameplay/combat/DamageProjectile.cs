using UnityEngine;

/// <summary>Pooled projectile: XZ movement, trigger hit, first <see cref="IDamageable"/> on hierarchy.</summary>
[DisallowMultipleComponent]
public sealed class DamageProjectile : MonoBehaviour
{
    ProjectilePool _pool;
    Transform _owner;
    Vector3 _direction;
    float _speed;
    float _damage;
    float _lifetimeRemaining;
    float _maxTravelDistance;
    float _traveled;
    LayerMask _hitLayers;
    DamageNumberStyle _style;
    float _pushbackDistance;
    float _pushbackDuration;
    bool _released;

    public void Initialize(
        ProjectilePool pool,
        Transform owner,
        Vector3 worldPosition,
        Quaternion worldRotation,
        Vector3 xzDirection,
        float speed,
        float damage,
        float maxLifetime,
        float maxTravelDistance,
        LayerMask hitLayers,
        in DamageNumberStyle style,
        float pushbackDistance,
        float pushbackDuration)
    {
        _pool = pool;
        _owner = owner;
        _direction = xzDirection;
        _direction.y = 0f;
        if (_direction.sqrMagnitude < 1e-8f)
            _direction = Vector3.forward;
        else
            _direction.Normalize();

        _speed = Mathf.Max(0f, speed);
        _damage = damage;
        _lifetimeRemaining = Mathf.Max(0f, maxLifetime);
        _maxTravelDistance = Mathf.Max(0f, maxTravelDistance);
        _traveled = 0f;
        _hitLayers = hitLayers;
        _style = style;
        _pushbackDistance = Mathf.Max(0f, pushbackDistance);
        _pushbackDuration = Mathf.Max(0.01f, pushbackDuration);
        _released = false;

        transform.SetPositionAndRotation(worldPosition, worldRotation);

        EnsurePhysicsForTriggers();
    }

    void Update()
    {
        if (_released || !isActiveAndEnabled)
            return;

        if (_lifetimeRemaining > 0f)
            _lifetimeRemaining -= Time.deltaTime;
        if (_lifetimeRemaining <= 0f)
        {
            ReturnToPool();
            return;
        }

        if (_speed > 0f)
        {
            Vector3 delta = _direction * (_speed * Time.deltaTime);
            transform.position += delta;
            _traveled += delta.magnitude;
        }

        if (_maxTravelDistance > 0f && _traveled >= _maxTravelDistance)
            ReturnToPool();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_released || other == null)
            return;
        if (_owner != null && (other.transform == _owner || other.transform.IsChildOf(_owner)))
            return;
        if (!LayerMaskContains(_hitLayers, other.gameObject.layer))
            return;

        TryDamageFirstOnHierarchy(other.gameObject);
        ReturnToPool();
    }

    static bool LayerMaskContains(LayerMask mask, int layer)
    {
        if (layer < 0 || layer > 31)
            return false;
        return (mask.value & (1 << layer)) != 0;
    }

    void TryDamageFirstOnHierarchy(GameObject hitObject)
    {
        Transform tr = hitObject.transform;
        while (tr != null)
        {
            MonoBehaviour[] components = tr.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour mb = components[i];
                if (mb == null)
                    continue;
                if (mb is IDefeatable defeated && defeated.IsDefeated)
                    return;
                if (mb is IDamageable dmg)
                {
                    dmg.TakeDamage(_damage, _style, new DamageHitInfo(_owner));

                    if (_pushbackDistance > 0f && ShouldApplyPushback(tr)
                        && !EntityAttackController.ShouldSuppressPushbackOn(tr.gameObject))
                    {
                        PushbackUtility.TryApplyOnHierarchy(hitObject, new PushbackContext
                        {
                            direction = _direction,
                            distance = _pushbackDistance,
                            duration = _pushbackDuration,
                        });
                    }

                    return;
                }
            }
            tr = tr.parent;
        }
    }

    bool ShouldApplyPushback(Transform victimRoot)
    {
        float resistance = PushbackGeometryProbe.ResolvePushbackResistance(victimRoot);
        float probeDist = _pushbackDistance * (1f - resistance);
        LayerMask blockLayers = PushbackGeometryProbe.ResolveBlockLayers(victimRoot);

        return PushbackGeometryProbe.ShouldApplyPushbackForce(
            victimRoot,
            _direction,
            probeDist,
            blockLayers);
    }

    void EnsurePhysicsForTriggers()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.interpolation = RigidbodyInterpolation.None;
    }

    void ReturnToPool()
    {
        if (_released)
            return;
        _released = true;
        if (_pool != null)
            _pool.Release(gameObject);
        else
            gameObject.SetActive(false);
    }
}
