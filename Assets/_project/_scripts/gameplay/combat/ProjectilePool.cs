using System.Collections.Generic;
using UnityEngine;

/// <summary>Pools a single projectile prefab: prewarms inactive instances, leases via <see cref="TryGet"/>, returns via <see cref="Release"/>.</summary>
public sealed class ProjectilePool : MonoBehaviour
{
    [Tooltip("Prefab root must include a DamageProjectile component (typically on the same GameObject).")]
    [SerializeField] GameObject prefab;
    [Tooltip("Fallback parent when no ProjectileWorldContainer exists in the scene. Defaults to this transform.")]
    [SerializeField] Transform poolParent;
    [SerializeField, Min(0)] int prewarmCount = 8;
    [Tooltip("Hard cap on total instances (active + inactive). 0 = unlimited growth.")]
    [SerializeField, Min(0)] int maxPoolSize = 32;

    readonly Stack<GameObject> _inactive = new();
    int _totalCreated;

    void Awake()
    {
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(ProjectilePool)} on {name}: prefab is not assigned.", this);
            return;
        }

        Transform parent = ResolvePoolParent();
        for (int i = 0; i < prewarmCount; i++)
        {
            if (maxPoolSize > 0 && _totalCreated >= maxPoolSize)
                break;
            GameObject instance = Instantiate(prefab, parent);
            instance.name = prefab.name;
            instance.SetActive(false);
            _inactive.Push(instance);
            _totalCreated++;
        }
    }

    Transform ResolvePoolParent()
    {
        if (ProjectileWorldContainer.Instance != null)
            return ProjectileWorldContainer.Instance.Container;
        if (poolParent != null)
            return poolParent;
        return transform;
    }

    void EnsureParent(GameObject instance)
    {
        Transform parent = ResolvePoolParent();
        if (instance.transform.parent != parent)
            instance.transform.SetParent(parent, false);
    }

    /// <summary>Activates an instance from the pool, or creates one if allowed. Returns null if <see cref="maxPoolSize"/> is reached and the pool is empty.</summary>
    public GameObject TryGet()
    {
        if (prefab == null)
            return null;

        GameObject instance;
        if (_inactive.Count > 0)
        {
            instance = _inactive.Pop();
        }
        else if (maxPoolSize == 0 || _totalCreated < maxPoolSize)
        {
            _totalCreated++;
            instance = Instantiate(prefab, ResolvePoolParent());
            instance.name = prefab.name;
            instance.SetActive(false);
        }
        else
        {
            Debug.LogWarning($"{nameof(ProjectilePool)} on {name}: pool exhausted (maxPoolSize={maxPoolSize}).", this);
            return null;
        }

        EnsureParent(instance);
        return instance;
    }

    public void Release(GameObject instance)
    {
        if (instance == null)
            return;
        instance.SetActive(false);
        EnsureParent(instance);
        _inactive.Push(instance);
    }
}
