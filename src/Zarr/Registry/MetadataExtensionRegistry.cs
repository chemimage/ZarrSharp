namespace Zarr.Registry;

public static class MetadataExtensionRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<(string Name, MetadataExtensionNodeKind NodeKind), IMetadataExtensionProvider> Providers = new();

    public static IReadOnlyList<string> RegisteredNames
    {
        get
        {
            lock (Gate)
            {
                return Providers.Keys
                    .Select(key => key.Name)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }

    public static void Register(IMetadataExtensionProvider provider)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (Gate)
        {
            Providers[(provider.Name, provider.NodeKind)] = provider;
        }
    }

    public static bool TryCreate(string name, MetadataExtensionNodeKind nodeKind, IReadOnlyDictionary<string, object?> metadata, out IMetadataExtension extension)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            extension = null!;
            return false;
        }

        lock (Gate)
        {
            if (Providers.TryGetValue((name, nodeKind), out var provider))
            {
                extension = provider.Parse(metadata);
                return true;
            }
        }

        extension = null!;
        return false;
    }
}
