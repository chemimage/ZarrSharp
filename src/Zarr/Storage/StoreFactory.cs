namespace Zarr.Storage;

public static class StoreFactory
{
    public static void RegisterUriScheme(string scheme, Func<Uri, IReadOnlyDictionary<string, string>?, ZarrOpenMode?, IStore> factory)
    {
        StoreProviderRegistry.RegisterUriScheme(scheme, factory);
    }

    public static bool UnregisterUriScheme(string scheme)
    {
        return StoreProviderRegistry.UnregisterUriScheme(scheme);
    }

    public static IStore MakeStore(ZarrOpenMode? mode = null)
    {
        return ApplyReadMode(new MemoryStore(), mode);
    }

    public static IStore MakeStore(IStore store, ZarrOpenMode? mode = null)
    {
        return ApplyReadMode(store ?? throw new ArgumentNullException(nameof(store)), mode);
    }

    public static IStore MakeStore(IReadOnlyDictionary<string, byte[]> initialData, ZarrOpenMode? mode = null)
    {
        return ApplyReadMode(new MemoryStore(initialData ?? throw new ArgumentNullException(nameof(initialData))), mode);
    }

    public static IStore MakeStore(string rootPathOrUri, ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null)
    {
        if (string.IsNullOrWhiteSpace(rootPathOrUri))
        {
            if (storageOptions is { Count: > 0 })
            {
                throw new ArgumentException("Storage options can only be used with URI-backed stores.", nameof(storageOptions));
            }

            return MakeStore(mode);
        }

        if (StorageUtilities.TryParseAbsoluteUri(rootPathOrUri, out var uri))
        {
            return MakeStore(uri, mode, storageOptions);
        }

        if (storageOptions is { Count: > 0 })
        {
            throw new ArgumentException("Storage options can only be used with URI-backed stores.", nameof(storageOptions));
        }

        return ApplyReadMode(new LocalStore(rootPathOrUri), mode);
    }

    public static IStore MakeStore(object specification, ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null)
    {
        if (specification is null)
        {
            return MakeStore(mode);
        }

        return specification switch
        {
            IStore store => MakeStore(store, mode),
            IReadOnlyDictionary<string, byte[]> initialData => MakeStore(initialData, mode),
            string rootPathOrUri => MakeStore(rootPathOrUri, mode, storageOptions),
            Uri uri => MakeStore(uri, mode, storageOptions),
            _ when StoreProviderRegistry.TryOpen(specification, mode, storageOptions, out var store) => ApplyReadMode(store, mode),
            _ => throw new NotSupportedException($"Store specification '{specification.GetType().FullName}' is not supported."),
        };
    }

    public static IStore MakeStore(Uri uri, ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null)
    {
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        if (uri.IsFile)
        {
            if (storageOptions is { Count: > 0 })
            {
                throw new ArgumentException("Storage options are not used for local file stores.", nameof(storageOptions));
            }

            return ApplyReadMode(new LocalStore(uri.LocalPath), mode);
        }

        if (!StoreProviderRegistry.TryOpen(uri, mode, storageOptions, out var store))
        {
            throw new NotSupportedException($"No store factory is registered for URI scheme '{uri.Scheme}'.");
        }

        return ApplyReadMode(store, mode);
    }

    public static StorePath MakeStorePath(IStore store, string path = "", ZarrOpenMode? mode = null)
    {
        return StorePath.Open(MakeStore(store, mode), path, mode);
    }

    public static Task<StorePath> MakeStorePathAsync(IStore store, string path = "", ZarrOpenMode? mode = null, CancellationToken cancellationToken = default)
    {
        return StorePath.OpenAsync(MakeStore(store, mode), path, mode, cancellationToken);
    }

    public static StorePath MakeStorePath(IReadOnlyDictionary<string, byte[]> initialData, string path = "", ZarrOpenMode? mode = null)
    {
        return StorePath.Open(MakeStore(initialData, mode), path, mode);
    }

    public static Task<StorePath> MakeStorePathAsync(IReadOnlyDictionary<string, byte[]> initialData, string path = "", ZarrOpenMode? mode = null, CancellationToken cancellationToken = default)
    {
        return StorePath.OpenAsync(MakeStore(initialData, mode), path, mode, cancellationToken);
    }

    public static StorePath MakeStorePath(string rootPathOrUri, string path = "", ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null)
    {
        return StorePath.Open(MakeStore(rootPathOrUri, mode, storageOptions), path, mode);
    }

    public static StorePath MakeStorePath(object specification, string path = "", ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null)
    {
        return StorePath.Open(MakeStore(specification, mode, storageOptions), path, mode);
    }

    public static Task<StorePath> MakeStorePathAsync(string rootPathOrUri, string path = "", ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null, CancellationToken cancellationToken = default)
    {
        return StorePath.OpenAsync(MakeStore(rootPathOrUri, mode, storageOptions), path, mode, cancellationToken);
    }

    public static Task<StorePath> MakeStorePathAsync(object specification, string path = "", ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null, CancellationToken cancellationToken = default)
    {
        return StorePath.OpenAsync(MakeStore(specification, mode, storageOptions), path, mode, cancellationToken);
    }

    public static StorePath MakeStorePath(Uri uri, string path = "", ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null)
    {
        return StorePath.Open(MakeStore(uri, mode, storageOptions), path, mode);
    }

    public static Task<StorePath> MakeStorePathAsync(Uri uri, string path = "", ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null, CancellationToken cancellationToken = default)
    {
        return StorePath.OpenAsync(MakeStore(uri, mode, storageOptions), path, mode, cancellationToken);
    }

    private static IStore ApplyReadMode(IStore store, ZarrOpenMode? mode)
    {
        return mode == ZarrOpenMode.Read &&
               Configuration.ZarrConfiguration.Current.Storage.PreferReadOnlyWrapping &&
               store is not IReadOnlyAwareStore { IsReadOnly: true }
            ? new ReadOnlyStore(store)
            : store;
    }
}
