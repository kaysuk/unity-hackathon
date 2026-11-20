using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    // The key stays recorded after use so a spent item cannot be collected twice.
    private readonly Dictionary<string, bool> items = new();

    public bool HasItem(string itemId)
    {
        return itemId != null && items.TryGetValue(itemId, out bool held) && held;
    }

    public bool HasCollected(string itemId)
    {
        return itemId != null && items.ContainsKey(itemId);
    }

    public bool TryCollect(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && items.TryAdd(itemId, true);
    }

    public bool TryUsePair(string firstItemId, string secondItemId)
    {
        if (firstItemId == secondItemId || !HasItem(firstItemId) || !HasItem(secondItemId))
            return false;

        items[firstItemId] = false;
        items[secondItemId] = false;
        return true;
    }
}
