using UnityEngine;

/// <summary>Pooled projectile: XZ movement, trigger/cast hit, first <see cref="IDamageable"/> on hierarchy.</summary>
[DisallowMultipleComponent]
public sealed class DamageProjectile : MonoBehaviour
{
    const float MinCastRadius = 0.05f;

    ProjectilePool _pool;
    Transform _owner;
    int _ownerId;
    Vector3 _direction;
    float _speed;
    float _damage;
    float _lifetimeRemaining;
    float _maxTravelDistance;
    float _traveled;
    LayerMask _hitLayers;
    LayerMask _obstacleLayers;
    DamageNumberStyle _style;
    float _pushbackDistance;
    float _pushbackDuration;
    float _deathPushForce;
    GameObject _impactVfxPrefab;
    float _impactVfxLifetime;
    float _castRadius;
    bool _released;

    public int OwnerId => _ownerId;

    public void Initialize(
        ProjectilePool pool,
        Transform owner,
        int ownerId,
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
        float pushbackDuration,
        float deathPushForce,
        LayerMask obstacleLayers,
        GameObject impactVfxPrefab,
        float impactVfxLifetime)
    {
        _pool = pool;
        _owner = owner;
        _ownerId = ownerId;
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
        _obstacleLayers = obstacleLayers;
        _style = style;
        _pushbackDistance = Mathf.Max(0f, pushbackDistance);
        _pushbackDuration = Mathf.Max(0.01f, pushbackDuration);
        _deathPushForce = Mathf.Max(0f, deathPushForce);
        _impactVfxPrefab = impactVfxPrefab;
        _impactVfxLifetime = Mathf.Max(0f, impactVfxLifetime);
        _released = false;

        transform.SetPositionAndRotation(worldPosition, worldRotation);

        EnsurePhysicsForTriggers();
        _castRadius = ResolveCastRadius();
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
            if (TryObstacleCast(delta, out RaycastHit hit))
            {
                transform.position = hit.point;
                HitObstacle(hit.point, hit.normal);
                return;
            }

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
        if (other.TryGetComponent(out DamageProjectile otherProjectile)
            && otherProjectile.OwnerId == _ownerId)
            return;
        if (_owner != null && (other.transform == _owner || other.transform.IsChildOf(_owner)))
            return;

        if (LayerMaskContains(_obstacleLayers, other.gameObject.layer))
        {
            Vector3 point = other.ClosestPoint(transform.position);
            Vector3 normal = -_direction;
            if (normal.sqrMagnitude < 1e-8f)
                normal = Vector3.up;
            HitObstacle(point, normal);
            return;
        }

        if (!LayerMaskContains(_hitLayers, other.gameObject.layer))
            return;

        TryDamageFirstOnHierarchy(other.gameObject);
        ReturnToPool();
    }

    bool TryObstacleCast(Vector3 delta, out RaycastHit hit)
    {
        hit = default;
        float distance = delta.magnitude;
        if (distance < 1e-8f || _obstacleLayers.value == 0)
            return false;

        Vector3 origin = transform.position;
        Vector3 dir = delta / distance;
        int count = Physics.SphereCastNonAlloc(
            origin,
            _castRadius,
            dir,
            _castHits,
            distance,
            _obstacleLayers,
            QueryTriggerInteraction.Ignore);

        RaycastHit best = default;
        float bestDist = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = _castHits[i];
            if (candidate.collider == null)
                continue;
            if (_owner != null
                && (candidate.collider.transform == _owner
                    || candidate.collider.transform.IsChildOf(_owner)))
                continue;
            if (candidate.distance < bestDist)
            {
                bestDist = candidate.distance;
                best = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        hit = best;
        return true;
    }

    void HitObstacle(Vector3 point, Vector3 normal)
    {
        SpawnImpactVfx(point, normal);
        ReturnToPool();
    }

    void SpawnImpactVfx(Vector3 point, Vector3 normal)
    {
        if (_impactVfxPrefab == null)
            return;

        Vector3 forward = normal;
        if (forward.sqrMagnitude < 1e-8f)
            forward = -_direction;
        if (forward.sqrMagnitude < 1e-8f)
            forward = Vector3.up;
        else
            forward.Normalize();

        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        GameObject instance = Instantiate(_impactVfxPrefab, point, rotation);

        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
            systems[i].Play(true);

        if (_impactVfxLifetime > 0f)
            Destroy(instance, _impactVfxLifetime);
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
                    dmg.TakeDamage(_damage, _style, new DamageHitInfo(_owner, _direction, _deathPushForce));

                    bool killed = mb is IDefeatable afterHit && afterHit.IsDefeated;
                    if (!killed
                        && _pushbackDistance > 0f
                        && ShouldApplyPushback(tr)
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

    float ResolveCastRadius()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
            return MinCastRadius;

        if (col is SphereCollider sphere)
            return Mathf.Max(MinCastRadius, sphere.radius * MaxAbsScale(transform));

        Bounds bounds = col.bounds;
        float r = Mathf.Max(bounds.extents.x, bounds.extents.z);
        return Mathf.Max(MinCastRadius, r);
    }

    static float MaxAbsScale(Transform t)
    {
        Vector3 s = t.lossyScale;
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z), 1e-4f);
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

    static readonly RaycastHit[] _castHits = new RaycastHit[8];
}
