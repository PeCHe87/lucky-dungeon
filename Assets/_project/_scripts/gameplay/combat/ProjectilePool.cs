using System.Collections.Generic;
using UnityEngine;

/// <summary>Pools a single projectile prefab: prewarms inactive instances, leases via <see cref="TryGet"/>, returns via <see cref="Release"/>.</summary>
public sealed class ProjectilePool : MonoBehaviour
{
    [Tooltip("Prefab root must include a DamageProjectile component (typically on the same GameObject).")]
    [SerializeField] GameObject prefab;
    [Tooltip("Inactive instances are parented here. Defaults to this transform.")]
    [SerializeField] Transform poolParent;
    [SerializeField, Min(0)] int prewarmCount = 8;
    [Tooltip("Hard cap on total instances (active + inactive). 0 = unlimited growth.")]
    [SerializeField, Min(0)] int maxPoolSize = 32;

    readonly Stack<GameObject> _inactive = new();
    int _totalCreated;

    void Awake()
    {
        if (poolParent == null)
            poolParent = transform;
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(ProjectilePool)} on {name}: prefab is not assigned.", this);
            return;
        }

        for (int i = 0; i < prewarmCount; i++)
        {
            if (maxPoolSize > 0 && _totalCreated >= maxPoolSize)
                break;
            GameObject instance = Instantiate(prefab, poolParent);
            instance.name = prefab.name;
            instance.SetActive(false);
            _inactive.Push(instance);
            _totalCreated++;
        }
    }

    /// <summary>Activates an instance from the pool, or creates one if allowed. Returns null if <see cref="maxPoolSize"/> is reached and the pool is empty.</summary>
    public GameObject TryGet()
    {
        if (prefab == null)
            return null;

        if (_inactive.Count > 0)
            return _inactive.Pop();

        if (maxPoolSize == 0 || _totalCreated < maxPoolSize)
        {
            _totalCreated++;
            GameObject created = Instantiate(prefab, poolParent);
            created.name = prefab.name;
            created.SetActive(false);
            return created;
        }

        Debug.LogWarning($"{nameof(ProjectilePool)} on {name}: pool exhausted (maxPoolSize={maxPoolSize}).", this);
        return null;
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
            return;
        instance.SetActive(false);
        instance.transform.SetParent(poolParent != null ? poolParent : transform, false);
        _inactive.Push(instance);
    }
}
