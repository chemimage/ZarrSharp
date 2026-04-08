namespace Zarr.Registry;

using Zarr.Buffer;

public static class BufferPrototypeRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, BufferPrototype> Prototypes = new(StringComparer.Ordinal)
    {
        ["shared"] = BufferPrototype.Shared,
    };

    public static IReadOnlyList<string> RegisteredNames
    {
        get
        {
            lock (Gate)
            {
                return Prototypes.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }
        }
    }

    public static void Register(string name, BufferPrototype prototype)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Buffer prototype names must be non-empty.", nameof(name));
        }

        if (prototype is null)
        {
            throw new ArgumentNullException(nameof(prototype));
        }

        lock (Gate)
        {
            Prototypes[name] = prototype;
        }
    }

    public static BufferPrototype Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Buffer prototype names must be non-empty.", nameof(name));
        }

        lock (Gate)
        {
            if (Prototypes.TryGetValue(name, out var prototype))
            {
                return prototype;
            }
        }

        throw new NotSupportedException($"Buffer prototype '{name}' is not registered.");
    }

    public static BufferPrototype ResolveConfigured()
    {
        return Get(Configuration.ZarrConfiguration.Current.Registry.BufferPrototypeName);
    }
}
