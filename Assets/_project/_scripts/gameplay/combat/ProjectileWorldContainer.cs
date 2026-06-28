using UnityEngine;

/// <summary>Scene singleton parent for all pooled and active projectiles.</summary>
[DisallowMultipleComponent]
public sealed class ProjectileWorldContainer : MonoBehaviour
{
    public static ProjectileWorldContainer Instance { get; private set; }

    public Transform Container => transform;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureComponentOnSceneObject()
    {
        if (Instance != null)
            return;

        var existing = GameObject.Find("projectilesContainer");
        if (existing != null && existing.GetComponent<ProjectileWorldContainer>() == null)
            existing.AddComponent<ProjectileWorldContainer>();
    }
}
