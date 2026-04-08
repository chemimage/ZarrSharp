using System.Collections.Concurrent;

namespace Zarr.Storage;

public interface IStore
{
    ReadOnlyMemory<byte>? Get(string key);

    void Set(string key, ReadOnlyMemory<byte> value);

    bool Exists(string key);

    void Delete(string key);

    void DeletePrefix(string prefix);

    IEnumerable<string> ListKeys(string prefix = "");
}

public interface IAsyncStore
{
    ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default);

    ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default);

    ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default);
}

public interface IReadOnlyAwareStore
{
    bool IsReadOnly { get; }
}

internal static class StoreAsyncExtensions
{
    public static async ValueTask<ReadOnlyMemory<byte>?> GetAsync(this IStore store, string key, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateConfiguredTimeoutSource(cancellationToken);
        var effectiveToken = timeoutSource?.Token ?? cancellationToken;
        return store is IAsyncStore asyncStore
            ? await asyncStore.GetAsync(key, effectiveToken).ConfigureAwait(false)
            : await Task.Run(() => store.Get(key), effectiveToken).ConfigureAwait(false);
    }

    public static async ValueTask SetAsync(this IStore store, string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateConfiguredTimeoutSource(cancellationToken);
        var effectiveToken = timeoutSource?.Token ?? cancellationToken;
        if (store is IAsyncStore asyncStore)
        {
            await asyncStore.SetAsync(key, value, effectiveToken).ConfigureAwait(false);
            return;
        }

        await Task.Run(() => store.Set(key, value), effectiveToken).ConfigureAwait(false);
    }

    public static async ValueTask<bool> ExistsAsync(this IStore store, string key, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateConfiguredTimeoutSource(cancellationToken);
        var effectiveToken = timeoutSource?.Token ?? cancellationToken;
        return store is IAsyncStore asyncStore
            ? await asyncStore.ExistsAsync(key, effectiveToken).ConfigureAwait(false)
            : await Task.Run(() => store.Exists(key), effectiveToken).ConfigureAwait(false);
    }

    public static async ValueTask DeleteAsync(this IStore store, string key, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateConfiguredTimeoutSource(cancellationToken);
        var effectiveToken = timeoutSource?.Token ?? cancellationToken;
        if (store is IAsyncStore asyncStore)
        {
            await asyncStore.DeleteAsync(key, effectiveToken).ConfigureAwait(false);
            return;
        }

        await Task.Run(() => store.Delete(key), effectiveToken).ConfigureAwait(false);
    }

    public static async ValueTask DeletePrefixAsync(this IStore store, string prefix, CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateConfiguredTimeoutSource(cancellationToken);
        var effectiveToken = timeoutSource?.Token ?? cancellationToken;
        if (store is IAsyncStore asyncStore)
        {
            await asyncStore.DeletePrefixAsync(prefix, effectiveToken).ConfigureAwait(false);
            return;
        }

        await Task.Run(() => store.DeletePrefix(prefix), effectiveToken).ConfigureAwait(false);
    }

    public static async ValueTask<IReadOnlyList<string>> ListKeysAsync(this IStore store, string prefix = "", CancellationToken cancellationToken = default)
    {
        using var timeoutSource = CreateConfiguredTimeoutSource(cancellationToken);
        var effectiveToken = timeoutSource?.Token ?? cancellationToken;
        return store is IAsyncStore asyncStore
            ? await asyncStore.ListKeysAsync(prefix, effectiveToken).ConfigureAwait(false)
            : await Task.Run<IReadOnlyList<string>>(() => store.ListKeys(prefix).ToArray(), effectiveToken).ConfigureAwait(false);
    }

    private static CancellationTokenSource? CreateConfiguredTimeoutSource(CancellationToken cancellationToken)
    {
        var timeout = Configuration.ZarrConfiguration.Current.Async.Timeout;
        if (!timeout.HasValue)
        {
            return null;
        }

        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout.Value);
        return source;
    }
}

internal sealed class ReadOnlyStore : IStore, IAsyncStore, IReadOnlyAwareStore
{
    private readonly IStore _inner;

    public ReadOnlyStore(IStore inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public bool IsReadOnly => true;

    public ReadOnlyMemory<byte>? Get(string key) => _inner.Get(key);

    public void Set(string key, ReadOnlyMemory<byte> value) => throw CreateReadOnlyError();

    public bool Exists(string key) => _inner.Exists(key);

    public void Delete(string key) => throw CreateReadOnlyError();

    public void DeletePrefix(string prefix) => throw CreateReadOnlyError();

    public IEnumerable<string> ListKeys(string prefix = "") => _inner.ListKeys(prefix);

    public ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default) => _inner.GetAsync(key, cancellationToken);

    public ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) => new(Task.FromException(CreateReadOnlyError()));

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => _inner.ExistsAsync(key, cancellationToken);

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default) => new(Task.FromException(CreateReadOnlyError()));

    public ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default) => new(Task.FromException(CreateReadOnlyError()));

    public ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default) => _inner.ListKeysAsync(prefix, cancellationToken);

    private static InvalidOperationException CreateReadOnlyError()
    {
        return new InvalidOperationException("The store is read-only.");
    }
}

public class MemoryStore : IStore, IAsyncStore, IReadOnlyAwareStore
{
    private readonly ConcurrentDictionary<string, byte[]> _data;

    public MemoryStore()
        : this(initialData: null)
    {
    }

    public MemoryStore(IReadOnlyDictionary<string, byte[]>? initialData)
    {
        _data = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);
        if (initialData is null)
        {
            return;
        }

        foreach (var item in initialData)
        {
            _data[PathUtilities.Normalize(item.Key)] = (byte[])item.Value.Clone();
        }
    }

    public bool IsReadOnly => false;

    public ReadOnlyMemory<byte>? Get(string key)
    {
        var normalized = PathUtilities.Normalize(key);
        return _data.TryGetValue(normalized, out var value)
            ? (ReadOnlyMemory<byte>?)new ReadOnlyMemory<byte>((byte[])value.Clone())
            : null;
    }

    public void Set(string key, ReadOnlyMemory<byte> value)
    {
        var normalized = PathUtilities.Normalize(key);
        _data[normalized] = value.ToArray();
    }

    public bool Exists(string key)
    {
        return _data.ContainsKey(PathUtilities.Normalize(key));
    }

    public void Delete(string key)
    {
        _data.TryRemove(PathUtilities.Normalize(key), out _);
    }

    public void DeletePrefix(string prefix)
    {
        var normalized = PathUtilities.Normalize(prefix);
        foreach (var key in MatchingKeys(normalized))
        {
            _data.TryRemove(key, out _);
        }
    }

    public IEnumerable<string> ListKeys(string prefix = "")
    {
        var normalized = PathUtilities.Normalize(prefix);
        return MatchingKeys(normalized).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    public ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ReadOnlyMemory<byte>?>(Get(key));
    }

    public ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        Set(key, value);
        return default;
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(Exists(key));
    }

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Delete(key);
        return default;
    }

    public ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        DeletePrefix(prefix);
        return default;
    }

    public ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        return new ValueTask<IReadOnlyList<string>>(ListKeys(prefix).ToArray());
    }

    private IEnumerable<string> MatchingKeys(string prefix)
    {
        return _data.Keys.Where(key => MatchesPrefix(key, prefix));
    }

    private static bool MatchesPrefix(string key, string prefix)
    {
        if (prefix.Length == 0)
        {
            return true;
        }

        return key.Equals(prefix, StringComparison.Ordinal) || key.StartsWith(prefix + "/", StringComparison.Ordinal);
    }
}

public sealed class LocalStore : IStore, IAsyncStore, IReadOnlyAwareStore
{
    public LocalStore(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public bool IsReadOnly => false;

    public ReadOnlyMemory<byte>? Get(string key)
    {
        var fullPath = ToFullPath(key);
        return File.Exists(fullPath)
            ? (ReadOnlyMemory<byte>?)File.ReadAllBytes(fullPath)
            : null;
    }

    public void Set(string key, ReadOnlyMemory<byte> value)
    {
        var fullPath = ToFullPath(key);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(fullPath, value.ToArray());
    }

    public bool Exists(string key)
    {
        return File.Exists(ToFullPath(key));
    }

    public void Delete(string key)
    {
        var fullPath = ToFullPath(key);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            CleanupEmptyParents(Path.GetDirectoryName(fullPath));
        }
    }

    public void DeletePrefix(string prefix)
    {
        var normalized = PathUtilities.Normalize(prefix);
        foreach (var key in ListKeys(normalized).ToArray())
        {
            Delete(key);
        }
    }

    public IEnumerable<string> ListKeys(string prefix = "")
    {
        var normalizedPrefix = PathUtilities.Normalize(prefix);
        if (!Directory.Exists(RootPath))
        {
            return Array.Empty<string>();
        }

        return Directory
            .EnumerateFiles(RootPath, "*", SearchOption.AllDirectories)
            .Select(file => PathUtilities.Normalize(Path.GetRelativePath(RootPath, file)))
            .Where(key => normalizedPrefix.Length == 0 ||
                          key.Equals(normalizedPrefix, StringComparison.Ordinal) ||
                          key.StartsWith(normalizedPrefix + "/", StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var fullPath = ToFullPath(key);
        return File.Exists(fullPath)
            ? (ReadOnlyMemory<byte>?)await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public async ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        var fullPath = ToFullPath(key);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await stream.WriteAsync(value, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(Exists(key));
    }

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Delete(key);
        return default;
    }

    public async ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.Normalize(prefix);
        foreach (var key in (await ListKeysAsync(normalized, cancellationToken).ConfigureAwait(false)).ToArray())
        {
            Delete(key);
        }
    }

    public async ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        return await Task.Run<IReadOnlyList<string>>(() => ListKeys(prefix).ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private string ToFullPath(string key)
    {
        var normalized = PathUtilities.Normalize(key);
        return normalized.Length == 0
            ? RootPath
            : Path.Combine(RootPath, normalized.Replace('/', Path.DirectorySeparatorChar));
    }

    private void CleanupEmptyParents(string? path)
    {
        while (!string.IsNullOrEmpty(path) &&
               !string.Equals(path, RootPath, StringComparison.OrdinalIgnoreCase) &&
               Directory.Exists(path) &&
               !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
            path = Path.GetDirectoryName(path);
        }
    }
}
