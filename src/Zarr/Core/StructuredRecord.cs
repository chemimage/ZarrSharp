namespace Zarr.Core;

public sealed class StructuredRecord : IReadOnlyDictionary<string, object?>, IEquatable<StructuredRecord>
{
    private readonly Dictionary<string, object?> _values;

    public StructuredRecord(IReadOnlyDictionary<string, object?> values)
    {
        _values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    public object? this[string key] => _values[key];

    public IEnumerable<string> Keys => _values.Keys;

    public IEnumerable<object?> Values => _values.Values;

    public int Count => _values.Count;

    public bool ContainsKey(string key)
    {
        return _values.ContainsKey(key);
    }

    public bool TryGetValue(string key, out object? value)
    {
        return _values.TryGetValue(key, out value);
    }

    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>(_values, StringComparer.Ordinal);
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _values.GetEnumerator();
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as StructuredRecord);
    }

    public bool Equals(StructuredRecord? other)
    {
        if (other is null || Count != other.Count)
        {
            return false;
        }

        foreach (var item in _values)
        {
            if (!other._values.TryGetValue(item.Key, out var otherValue))
            {
                return false;
            }

            if (!Equals(item.Value, otherValue))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var item in _values.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            hash.Add(item.Key, StringComparer.Ordinal);
            hash.Add(item.Value);
        }

        return hash.ToHashCode();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
