using System.IO;

namespace Zarr.Storage;

public sealed class StorePath
{
    public StorePath(IStore store, string path = "")
    {
        Store = store ?? throw new ArgumentNullException(nameof(store));
        Path = PathUtilities.Normalize(path);
    }

    public IStore Store { get; }

    public string Path { get; }

    public bool IsReadOnly => Store is IReadOnlyAwareStore readOnlyAwareStore && readOnlyAwareStore.IsReadOnly;

    public StorePath Child(string relativePath)
    {
        return new StorePath(Store, PathUtilities.Combine(Path, relativePath));
    }

    public ReadOnlyMemory<byte>? Get() => Store.Get(Path);

    public ValueTask<ReadOnlyMemory<byte>?> GetAsync(CancellationToken cancellationToken = default) => Store.GetAsync(Path, cancellationToken);

    public void Set(ReadOnlyMemory<byte> value) => Store.Set(Path, value);

    public ValueTask SetAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) => Store.SetAsync(Path, value, cancellationToken);

    public bool Exists() => Store.Exists(Path);

    public ValueTask<bool> ExistsAsync(CancellationToken cancellationToken = default) => Store.ExistsAsync(Path, cancellationToken);

    public void Delete() => Store.Delete(Path);

    public ValueTask DeleteAsync(CancellationToken cancellationToken = default) => Store.DeleteAsync(Path, cancellationToken);

    public void DeletePrefix() => Store.DeletePrefix(Path);

    public ValueTask DeletePrefixAsync(CancellationToken cancellationToken = default) => Store.DeletePrefixAsync(Path, cancellationToken);

    public bool IsEmpty()
    {
        return !Store.ListKeys(Path).Any();
    }

    public async ValueTask<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        return !(await Store.ListKeysAsync(Path, cancellationToken).ConfigureAwait(false)).Any();
    }

    public static StorePath Open(IStore store, string path = "", ZarrOpenMode? mode = null)
    {
        var resolvedStore = PrepareStore(store, mode);
        var storePath = new StorePath(resolvedStore, path);

        switch (mode)
        {
            case ZarrOpenMode.Create:
                storePath.DeletePrefix();
                break;

            case ZarrOpenMode.CreateNew:
                if (!storePath.IsEmpty())
                {
                    throw new IOException($"Cannot create store path '{storePath.Path}' because it already contains data.");
                }
                break;
        }

        return storePath;
    }

    public static async Task<StorePath> OpenAsync(IStore store, string path = "", ZarrOpenMode? mode = null, CancellationToken cancellationToken = default)
    {
        var resolvedStore = PrepareStore(store, mode);
        var storePath = new StorePath(resolvedStore, path);

        switch (mode)
        {
            case ZarrOpenMode.Create:
                await storePath.DeletePrefixAsync(cancellationToken).ConfigureAwait(false);
                break;

            case ZarrOpenMode.CreateNew:
                if (!await storePath.IsEmptyAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new IOException($"Cannot create store path '{storePath.Path}' because it already contains data.");
                }
                break;
        }

        return storePath;
    }

    public override string ToString()
    {
        return Path.Length == 0 ? Store.ToString() ?? string.Empty : $"{Store}/{Path}";
    }

    private static IStore PrepareStore(IStore store, ZarrOpenMode? mode)
    {
        if (store is null)
        {
            throw new ArgumentNullException(nameof(store));
        }

        var isReadOnly = store is IReadOnlyAwareStore readOnlyAwareStore && readOnlyAwareStore.IsReadOnly;
        if (mode == ZarrOpenMode.Read)
        {
            return isReadOnly ? store : new ReadOnlyStore(store);
        }

        if (isReadOnly)
        {
            throw new InvalidOperationException($"Store '{store.GetType().Name}' is read-only and cannot be opened with mode '{mode}'.");
        }

        return store;
    }
}
