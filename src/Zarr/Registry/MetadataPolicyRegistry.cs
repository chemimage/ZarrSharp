namespace Zarr.Registry;

public static class MetadataPolicyRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, IMetadataPolicy> Policies = new(StringComparer.Ordinal);

    static MetadataPolicyRegistry()
    {
        Register(new StrictReferenceMetadataPolicy());
    }

    public static IReadOnlyList<string> RegisteredNames
    {
        get
        {
            lock (Gate)
            {
                return Policies.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }
        }
    }

    public static void Register(IMetadataPolicy policy)
    {
        if (policy is null)
        {
            throw new ArgumentNullException(nameof(policy));
        }

        lock (Gate)
        {
            Policies[policy.Name] = policy;
        }
    }

    public static IMetadataPolicy Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Metadata policy names must be non-empty.", nameof(name));
        }

        lock (Gate)
        {
            if (Policies.TryGetValue(name, out var policy))
            {
                return policy;
            }
        }

        throw new NotSupportedException($"Metadata policy '{name}' is not registered.");
    }

    public static IMetadataPolicy ResolveConfigured()
    {
        return Get(Configuration.ZarrConfiguration.Current.Metadata.PolicyName);
    }

    private sealed class StrictReferenceMetadataPolicy : IMetadataPolicy
    {
        public string Name => "strict_reference";

        public void ValidateExtension(string name, MetadataExtensionNodeKind nodeKind, IReadOnlyDictionary<string, object?> metadata)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new MetadataException("Metadata extension names must be non-empty.");
            }

            if (!metadata.TryGetValue("must_understand", out var rawMustUnderstand))
            {
                throw new MetadataException($"Extra field '{name}' must include 'must_understand'.");
            }

            var mustUnderstand = JsonUtilities.ConvertValue<bool>(rawMustUnderstand);
            if (mustUnderstand && !MetadataExtensionRegistry.TryCreate(name, nodeKind, metadata, out _))
            {
                throw new MetadataException($"Extra field '{name}' must declare 'must_understand' as false unless a registered implementation is available.");
            }

            if (metadata.TryGetValue("configuration", out var rawConfiguration) &&
                rawConfiguration is not null &&
                rawConfiguration is not Dictionary<string, object?>)
            {
                throw new MetadataException($"Extension '{name}' has a malformed 'configuration' value.");
            }
        }

        public void ValidateStorageTransformer(IReadOnlyDictionary<string, object?> metadata)
        {
            var name = JsonUtilities.ConvertValue<string>(metadata.TryGetValue("name", out var rawName) ? rawName : null);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new MetadataException("Each storage transformer must define a non-empty 'name'.");
            }

            if (metadata.TryGetValue("configuration", out var rawConfiguration) &&
                rawConfiguration is not null &&
                rawConfiguration is not Dictionary<string, object?>)
            {
                throw new MetadataException($"Storage transformer '{name}' has a malformed 'configuration' value.");
            }
        }

        public void ValidateConsolidatedMetadataEnvelope(IReadOnlyDictionary<string, object?> metadata, string path)
        {
            if (!metadata.TryGetValue("must_understand", out var rawMustUnderstand))
            {
                return;
            }

            if (JsonUtilities.ConvertValue<bool>(rawMustUnderstand))
            {
                throw new MetadataException($"'{path}' must declare 'must_understand' as false.");
            }
        }
    }
}
