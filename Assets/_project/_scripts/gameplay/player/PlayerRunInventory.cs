using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Item stacks collected during a run. Owned by <see cref="PlayerRunState"/>.
/// New unique items are inserted at the front; stacking an existing id does not reorder.
/// </summary>
public sealed class PlayerRunInventory
{
    readonly Dictionary<string, int> _amounts = new Dictionary<string, int>();
    readonly List<string> _order = new List<string>();

    public void BeginRun()
    {
        _amounts.Clear();
        _order.Clear();
        GameEvents.RaisePlayerRunInventoryChanged();
    }

    public void Clear()
    {
        _amounts.Clear();
        _order.Clear();
        GameEvents.RaisePlayerRunInventoryChanged();
    }

    public int GetAmount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0;

        return _amounts.TryGetValue(itemId, out int amount) ? amount : 0;
    }

    public bool Add(string itemId, int amount = 1)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0)
            return false;

        int previous = GetAmount(itemId);
        int next = previous + amount;
        _amounts[itemId] = next;
        if (previous == 0)
            _order.Insert(0, itemId);

        Debug.Log($"[PlayerRunInventory] itemId='{itemId}' amount {previous} → {next}");
        GameEvents.RaisePlayerRunInventoryChanged();
        return true;
    }

    /// <summary>
    /// Fills <paramref name="buffer"/> with inventory-ordered stacks (newest unique first), up to <paramref name="maxCount"/>.
    /// </summary>
    public void CopyOrderedEntries(List<(string itemId, int amount)> buffer, int maxCount)
    {
        if (buffer == null)
            return;

        buffer.Clear();
        if (maxCount <= 0)
            return;

        int count = Mathf.Min(maxCount, _order.Count);
        for (int i = 0; i < count; i++)
        {
            string id = _order[i];
            if (!_amounts.TryGetValue(id, out int amount) || amount <= 0)
                continue;
            buffer.Add((id, amount));
        }
    }
}
