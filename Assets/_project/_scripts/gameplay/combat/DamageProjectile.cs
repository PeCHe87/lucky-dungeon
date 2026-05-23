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
        in DamageNumberStyle style)
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
                if (mb is IDamageable dmg)
                {
                    dmg.TakeDamage(_damage, _style);
                    return;
                }
            }
            tr = tr.parent;
        }
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
