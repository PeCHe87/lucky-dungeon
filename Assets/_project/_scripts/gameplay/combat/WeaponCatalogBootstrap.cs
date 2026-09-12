using UnityEngine;

/// <summary>
/// Loads a <see cref="WeaponCatalog"/> into <see cref="WeaponCatalog.Current"/> at Awake.
/// Place on a root in the init scene with the catalog asset assigned.
/// </summary>
public sealed class WeaponCatalogBootstrap : MonoBehaviour
{
    [SerializeField] WeaponCatalog catalog;

    void Awake()
    {
        if (catalog == null)
        {
            Debug.LogWarning($"[WeaponCatalogBootstrap] No catalog assigned on '{name}'.", this);
            return;
        }

        WeaponCatalog.SetCurrent(catalog);
    }
}
