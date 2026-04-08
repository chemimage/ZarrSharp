namespace Zarr.Core;

public sealed class Attributes : IDictionary<string, object?>
{
    private readonly Dictionary<string, object?> _values;
    private readonly Action _persist;

    internal Attributes(Dictionary<string, object?> values, Action persist)
    {
        _values = values;
        _persist = persist;
    }

    public object? this[string key]
    {
        get => _values[key];
        set
        {
            _values[key] = value;
            _persist();
        }
    }

    public ICollection<string> Keys => _values.Keys;

    public ICollection<object?> Values => _values.Values;

    public int Count => _values.Count;

    public bool IsReadOnly => false;

    public void Add(string key, object? value)
    {
        _values.Add(key, value);
        _persist();
    }

    public void Add(KeyValuePair<string, object?> item)
    {
        Add(item.Key, item.Value);
    }

    public Dictionary<string, object?> AsDictionary()
    {
        return JsonUtilities.CloneDictionary(_values);
    }

    public void Clear()
    {
        _values.Clear();
        _persist();
    }

    public bool Contains(KeyValuePair<string, object?> item)
    {
        return ((ICollection<KeyValuePair<string, object?>>)_values).Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return _values.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, object?>>)_values).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _values.GetEnumerator();
    }

    public void Put(IReadOnlyDictionary<string, object?> values)
    {
        _values.Clear();
        foreach (var item in values)
        {
            _values[item.Key] = item.Value;
        }

        _persist();
    }

    public bool Remove(string key)
    {
        var removed = _values.Remove(key);
        if (removed)
        {
            _persist();
        }

        return removed;
    }

    public bool Remove(KeyValuePair<string, object?> item)
    {
        var removed = ((ICollection<KeyValuePair<string, object?>>)_values).Remove(item);
        if (removed)
        {
            _persist();
        }

        return removed;
    }

    public bool TryGetValue(string key, out object? value)
    {
        return _values.TryGetValue(key, out value);
    }

    public void Update(IReadOnlyDictionary<string, object?> values)
    {
        foreach (var item in values)
        {
            _values[item.Key] = item.Value;
        }

        _persist();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
