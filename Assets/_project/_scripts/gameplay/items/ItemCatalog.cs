using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Project-wide list of <see cref="ItemData"/> assets for id-based lookup.
/// Register via <see cref="ItemCatalogBootstrap"/> so <see cref="Current"/> is available at runtime.
/// Prefer a direct serialized ref when a MonoBehaviour can take an Inspector reference.
/// </summary>
[CreateAssetMenu(menuName = "Items/Item Catalog", fileName = "ItemCatalog")]
public sealed class ItemCatalog : ScriptableObject
{
    [SerializeField] ItemData[] items;

    Dictionary<string, ItemData> _byId;
    bool _loggedDuplicateIds;

    public static ItemCatalog Current { get; private set; }

    public IReadOnlyList<ItemData> All => items ?? System.Array.Empty<ItemData>();

    public static void SetCurrent(ItemCatalog catalog)
    {
        Current = catalog;
        if (catalog != null)
            catalog.InvalidateLookup();
    }

    public bool TryGetById(string itemId, out ItemData data)
    {
        data = null;
        if (string.IsNullOrEmpty(itemId))
            return false;

        EnsureLookup();
        return _byId.TryGetValue(itemId, out data);
    }

    public ItemData GetById(string itemId)
    {
        if (TryGetById(itemId, out ItemData data))
            return data;

        Debug.LogWarning($"[ItemCatalog] No item with id '{itemId}'.", this);
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

        _byId = new Dictionary<string, ItemData>();
        if (items == null)
            return;

        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item == null)
                continue;

            string id = item.ItemId;
            if (string.IsNullOrEmpty(id))
                continue;

            if (_byId.ContainsKey(id))
            {
                if (!_loggedDuplicateIds)
                {
                    Debug.LogWarning(
                        $"[ItemCatalog] Duplicate itemId '{id}'. Keeping the first entry.",
                        this);
                    _loggedDuplicateIds = true;
                }
                continue;
            }

            _byId.Add(id, item);
        }
    }
}
