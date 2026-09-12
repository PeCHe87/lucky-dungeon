using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-wide list of <see cref="WeaponData"/> assets for id-based lookup.
/// Register via <see cref="WeaponCatalogBootstrap"/> so <see cref="Current"/> is available at runtime.
/// Prefer a direct serialized ref when a MonoBehaviour can take an Inspector reference.
/// </summary>
[CreateAssetMenu(menuName = "Combat/Weapon Catalog", fileName = "WeaponCatalog")]
public sealed class WeaponCatalog : ScriptableObject
{
    [SerializeField] WeaponData[] weapons;

    Dictionary<string, WeaponData> _byId;
    bool _loggedDuplicateIds;

    public static WeaponCatalog Current { get; private set; }

    public IReadOnlyList<WeaponData> All => weapons ?? System.Array.Empty<WeaponData>();

    public static void SetCurrent(WeaponCatalog catalog)
    {
        Current = catalog;
        if (catalog != null)
            catalog.InvalidateLookup();
    }

    public bool TryGetById(string weaponId, out WeaponData data)
    {
        data = null;
        if (string.IsNullOrEmpty(weaponId))
            return false;

        EnsureLookup();
        return _byId.TryGetValue(weaponId, out data);
    }

    public WeaponData GetById(string weaponId)
    {
        if (TryGetById(weaponId, out WeaponData data))
            return data;

        Debug.LogWarning($"[WeaponCatalog] No weapon with id '{weaponId}'.", this);
        return null;
    }

    void OnEnable() => InvalidateLookup();

    void InvalidateLookup()
    {
        _byId = null;
        _loggedDuplicateIds = false;
    }

    void EnsureLookup()
    {
        if (_byId != null)
            return;

        _byId = new Dictionary<string, WeaponData>();
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Length; i++)
        {
            WeaponData weapon = weapons[i];
            if (weapon == null)
                continue;

            string id = weapon.WeaponId;
            if (string.IsNullOrEmpty(id))
                continue;

            if (_byId.ContainsKey(id))
            {
                if (!_loggedDuplicateIds)
                {
                    Debug.LogWarning(
                        $"[WeaponCatalog] Duplicate weaponId '{id}'. Keeping the first entry.",
                        this);
                    _loggedDuplicateIds = true;
                }
                continue;
            }

            _byId.Add(id, weapon);
        }
    }
}
