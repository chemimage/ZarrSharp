namespace Zarr.Registry;

public static class StorageTransformerRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Func<IReadOnlyDictionary<string, object?>, IStorageTransformer>> Factories = new(StringComparer.Ordinal);

    public static IReadOnlyList<string> RegisteredNames
    {
        get
        {
            lock (Gate)
            {
                return Factories.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }
        }
    }

    public static void Register(string name, Func<IReadOnlyDictionary<string, object?>, IStorageTransformer> factory)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Storage transformer names must be non-empty.", nameof(name));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        lock (Gate)
        {
            Factories[name] = factory;
        }
    }

    public static bool IsRegistered(string name)
    {
        lock (Gate)
        {
            return Factories.ContainsKey(name);
        }
    }

    public static bool TryCreate(string name, IReadOnlyDictionary<string, object?> configuration, out IStorageTransformer transformer)
    {
        lock (Gate)
        {
            if (Factories.TryGetValue(name, out var factory))
            {
                transformer = factory(configuration);
                return true;
            }
        }

        transformer = null!;
        return false;
    }
}
