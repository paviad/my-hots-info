namespace CascLibCore;

public class MultiDictionary<TKey, TValue> : Dictionary<TKey, List<TValue>> where TKey : notnull {
    public void Add(TKey key, TValue value) {
        if (TryGetValue(key, out var hset)) {
            hset.Add(value);
        }
        else {
            hset = [value];
            base[key] = hset;
        }
    }

    public new void Clear() {
        foreach (var kv in this) {
            kv.Value.Clear();
        }

        base.Clear();
    }
}
