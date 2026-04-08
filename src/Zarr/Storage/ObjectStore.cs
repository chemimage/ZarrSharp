using System.Collections.Concurrent;

namespace Zarr.Storage;

public interface IObjectStoreBackend
{
    ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default);

    ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default);
}

public sealed class InMemoryObjectStoreBackend : IObjectStoreBackend
{
    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

    public InMemoryObjectStoreBackend()
    {
    }

    public InMemoryObjectStoreBackend(IReadOnlyDictionary<string, byte[]> initialData)
    {
        if (initialData is null)
        {
            throw new ArgumentNullException(nameof(initialData));
        }

        foreach (var item in initialData)
        {
            _entries[PathUtilities.Normalize(item.Key)] = (byte[])item.Value.Clone();
        }
    }

    public ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.Normalize(key);
        return new ValueTask<ReadOnlyMemory<byte>?>(
            _entries.TryGetValue(normalized, out var value)
                ? (ReadOnlyMemory<byte>?)new ReadOnlyMemory<byte>((byte[])value.Clone())
                : null);
    }

    public ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        _entries[PathUtilities.Normalize(key)] = value.ToArray();
        return default;
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(_entries.ContainsKey(PathUtilities.Normalize(key)));
    }

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(PathUtilities.Normalize(key), out _);
        return default;
    }

    public ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        var normalizedPrefix = PathUtilities.Normalize(prefix);
        var keys = _entries.Keys
            .Where(key => normalizedPrefix.Length == 0 ||
                          key.Equals(normalizedPrefix, StringComparison.Ordinal) ||
                          key.StartsWith(normalizedPrefix + "/", StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        return new ValueTask<IReadOnlyList<string>>(keys);
    }
}

public sealed class ObjectStore : IStore, IAsyncStore, IReadOnlyAwareStore
{
    private readonly IObjectStoreBackend _backend;
    private readonly string _prefix;

    public ObjectStore(IObjectStoreBackend backend, string prefix = "", bool readOnly = false)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _prefix = PathUtilities.Normalize(prefix);
        IsReadOnly = readOnly;
    }

    public bool IsReadOnly { get; }

    public ReadOnlyMemory<byte>? Get(string key)
    {
        return RunSync(cancellationToken => _backend.GetAsync(ToBackendKey(key), cancellationToken));
    }

    public void Set(string key, ReadOnlyMemory<byte> value)
    {
        EnsureWritable();
        RunSync(cancellationToken => _backend.SetAsync(ToBackendKey(key), value, cancellationToken));
    }

    public bool Exists(string key)
    {
        return RunSync(cancellationToken => _backend.ExistsAsync(ToBackendKey(key), cancellationToken));
    }

    public void Delete(string key)
    {
        EnsureWritable();
        RunSync(cancellationToken => _backend.DeleteAsync(ToBackendKey(key), cancellationToken));
    }

    public void DeletePrefix(string prefix)
    {
        EnsureWritable();
        foreach (var key in ListKeys(prefix).ToArray())
        {
            Delete(key);
        }
    }

    public IEnumerable<string> ListKeys(string prefix = "")
    {
        var fullPrefix = ToBackendKey(prefix);
        return RunSync(cancellationToken => _backend.ListKeysAsync(fullPrefix, cancellationToken))
            .Select(ToStoreKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    public ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return _backend.GetAsync(ToBackendKey(key), cancellationToken);
    }

    public ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        return _backend.SetAsync(ToBackendKey(key), value, cancellationToken);
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return _backend.ExistsAsync(ToBackendKey(key), cancellationToken);
    }

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        return _backend.DeleteAsync(ToBackendKey(key), cancellationToken);
    }

    public async ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        var keys = await ListKeysAsync(prefix, cancellationToken).ConfigureAwait(false);
        foreach (var key in keys)
        {
            await DeleteAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        var fullPrefix = ToBackendKey(prefix);
        return (await _backend.ListKeysAsync(fullPrefix, cancellationToken).ConfigureAwait(false))
            .Select(ToStoreKey)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    public override string ToString()
    {
        return _prefix.Length == 0 ? "object://" : $"object://{_prefix}";
    }

    private string ToBackendKey(string key)
    {
        return PathUtilities.Combine(_prefix, key);
    }

    private string ToStoreKey(string backendKey)
    {
        var normalized = PathUtilities.Normalize(backendKey);
        if (_prefix.Length == 0)
        {
            return normalized;
        }

        if (normalized.Equals(_prefix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (!normalized.StartsWith(_prefix + "/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Backend key '{backendKey}' does not belong to object-store prefix '{_prefix}'.");
        }

        return normalized[(_prefix.Length + 1)..];
    }

    private void EnsureWritable()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException("The object store is read-only.");
        }
    }

    private static T RunSync<T>(Func<CancellationToken, ValueTask<T>> operation)
    {
        return operation(default).AsTask().GetAwaiter().GetResult();
    }

    private static void RunSync(Func<CancellationToken, ValueTask> operation)
    {
        operation(default).AsTask().GetAwaiter().GetResult();
    }
}
