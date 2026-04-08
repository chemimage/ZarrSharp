namespace Zarr.Registry;

public static class StoreProviderRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Func<Uri, IReadOnlyDictionary<string, string>?, ZarrOpenMode?, IStore>> UriFactories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Type, Func<object, ZarrOpenMode?, IStore>> OptionFactories = new();
    private static readonly List<IStoreProvider> Providers = new();

    public static IReadOnlyList<string> RegisteredUriSchemes
    {
        get
        {
            lock (Gate)
            {
                return UriFactories.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }
    }

    public static void RegisterUriScheme(string scheme, Func<Uri, IReadOnlyDictionary<string, string>?, ZarrOpenMode?, IStore> factory)
    {
        if (string.IsNullOrWhiteSpace(scheme))
        {
            throw new ArgumentException("A URI scheme is required.", nameof(scheme));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        lock (Gate)
        {
            UriFactories[scheme] = factory;
        }
    }

    public static bool UnregisterUriScheme(string scheme)
    {
        if (scheme is null)
        {
            throw new ArgumentNullException(nameof(scheme));
        }

        lock (Gate)
        {
            return UriFactories.Remove(scheme);
        }
    }

    public static void RegisterOptions<TOptions>(Func<TOptions, ZarrOpenMode?, IStore> factory)
        where TOptions : class
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        lock (Gate)
        {
            OptionFactories[typeof(TOptions)] = (value, mode) => factory((TOptions)value, mode);
        }
    }

    public static void RegisterProvider(IStoreProvider provider)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (Gate)
        {
            Providers.Add(provider);
            foreach (var scheme in provider.UriSchemes)
            {
                UriFactories[scheme] = (uri, storageOptions, mode) => provider.Open(uri, mode, storageOptions);
            }

            if (provider.OptionsType is not null)
            {
                OptionFactories[provider.OptionsType] = (value, mode) => provider.Open(value, mode);
            }
        }
    }

    public static bool TryOpen(Uri uri, ZarrOpenMode? mode, IReadOnlyDictionary<string, string>? storageOptions, out IStore store)
    {
        if (uri is null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        lock (Gate)
        {
            if (UriFactories.TryGetValue(uri.Scheme, out var factory))
            {
                store = factory(uri, storageOptions, mode);
                return true;
            }
        }

        store = null!;
        return false;
    }

    public static bool TryOpen(object specification, ZarrOpenMode? mode, IReadOnlyDictionary<string, string>? storageOptions, out IStore store)
    {
        if (specification is null)
        {
            throw new ArgumentNullException(nameof(specification));
        }

        lock (Gate)
        {
            if (OptionFactories.TryGetValue(specification.GetType(), out var optionFactory))
            {
                store = optionFactory(specification, mode);
                return true;
            }

            foreach (var provider in Providers)
            {
                if (provider.CanOpen(specification))
                {
                    store = provider.Open(specification, mode, storageOptions);
                    return true;
                }
            }
        }

        store = null!;
        return false;
    }
}
