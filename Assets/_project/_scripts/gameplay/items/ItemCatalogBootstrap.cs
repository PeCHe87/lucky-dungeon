using UnityEngine;

/// <summary>
/// Loads an <see cref="ItemCatalog"/> into <see cref="ItemCatalog.Current"/> at Awake.
/// Place on a root in the init scene with the catalog asset assigned.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class ItemCatalogBootstrap : MonoBehaviour
{
    [SerializeField] ItemCatalog catalog;

    void Awake()
    {
        if (catalog == null)
        {
            Debug.LogWarning($"[ItemCatalogBootstrap] No catalog assigned on '{name}'.", this);
            return;
        }

        ItemCatalog.SetCurrent(catalog);
    }
}
