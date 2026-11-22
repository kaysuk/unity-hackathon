using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    // The key stays recorded after use so a spent item cannot be collected twice.
    private readonly Dictionary<string, bool> items = new();

    [System.Serializable]
    internal struct Entry
    {
        public string id;
        public bool held;
    }

    internal Entry[] Capture()
    {
        var snapshot = new Entry[items.Count];
        int index = 0;
        foreach (var item in items)
            snapshot[index++] = new Entry { id = item.Key, held = item.Value };
        return snapshot;
    }

    internal void Restore(Entry[] snapshot)
    {
        items.Clear();
        if (snapshot == null)
            return;
        foreach (Entry item in snapshot)
            if (!string.IsNullOrWhiteSpace(item.id))
                items.TryAdd(item.id, item.held);
    }

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

    public bool TryUse(string itemId)
    {
        if (!HasItem(itemId))
            return false;

        items[itemId] = false;
        return true;
    }

    internal bool TryReturn(string itemId)
    {
        if (itemId == null || !items.TryGetValue(itemId, out bool held) || held)
            return false;

        items[itemId] = true;
        return true;
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
