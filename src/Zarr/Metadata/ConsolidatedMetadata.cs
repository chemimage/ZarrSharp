namespace Zarr.Metadata;

internal sealed class ConsolidatedMetadata
{
    private readonly Dictionary<string, object> _metadata;

    public ConsolidatedMetadata()
        : this(new Dictionary<string, object>(StringComparer.Ordinal))
    {
    }

    public ConsolidatedMetadata(IDictionary<string, object> metadata)
    {
        _metadata = metadata is null
            ? throw new ArgumentNullException(nameof(metadata))
            : new Dictionary<string, object>(metadata, StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, object> Metadata => _metadata;

    public ConsolidatedMetadata Clone()
    {
        return new ConsolidatedMetadata(_metadata.ToDictionary(
            item => item.Key,
            item => CloneMetadata(item.Value),
            StringComparer.Ordinal));
    }

    public IEnumerable<KeyValuePair<string, object>> Flatten()
    {
        return Flatten(string.Empty, _metadata);
    }

    public bool TryResolve(string relativePath, out object metadata)
    {
        var normalized = PathUtilities.Normalize(relativePath);
        var segments = normalized.Length == 0
            ? Array.Empty<string>()
            : normalized.Split('/');

        var current = _metadata;
        metadata = default!;

        for (var index = 0; index < segments.Length; index++)
        {
            if (!current.TryGetValue(segments[index], out var nodeMetadata))
            {
                return false;
            }

            if (index == segments.Length - 1)
            {
                metadata = CloneMetadata(nodeMetadata);
                return true;
            }

            if (nodeMetadata is not GroupMetadata groupMetadata ||
                groupMetadata.ConsolidatedMetadata is null)
            {
                return false;
            }

            current = groupMetadata.ConsolidatedMetadata._metadata;
        }

        return false;
    }

    public bool Remove(string relativePath)
    {
        var normalized = PathUtilities.Normalize(relativePath);
        var segments = normalized.Length == 0
            ? Array.Empty<string>()
            : normalized.Split('/');

        if (segments.Length == 0)
        {
            return false;
        }

        var current = _metadata;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!current.TryGetValue(segments[index], out var nodeMetadata) ||
                nodeMetadata is not GroupMetadata groupMetadata ||
                groupMetadata.ConsolidatedMetadata is null)
            {
                return false;
            }

            current = groupMetadata.ConsolidatedMetadata._metadata;
        }

        return current.Remove(segments[^1]);
    }

    public void Set(string relativePath, object metadata)
    {
        var normalized = PathUtilities.Normalize(relativePath);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A relative path is required.", nameof(relativePath));
        }

        Add(_metadata, normalized, CloneMetadata(metadata));
    }

    public static ConsolidatedMetadata FromFlatMetadata(IEnumerable<KeyValuePair<string, object>> metadata)
    {
        if (metadata is null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        var root = new ConsolidatedMetadata();
        foreach (var entry in metadata.OrderBy(item => PathDepth(item.Key)).ThenBy(item => item.Key, StringComparer.Ordinal))
        {
            Add(root._metadata, PathUtilities.Normalize(entry.Key), CloneMetadata(entry.Value));
        }

        return root;
    }

    private static IEnumerable<KeyValuePair<string, object>> Flatten(string prefix, IReadOnlyDictionary<string, object> metadata)
    {
        foreach (var entry in metadata.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var path = prefix.Length == 0 ? entry.Key : $"{prefix}/{entry.Key}";
            yield return new KeyValuePair<string, object>(path, CloneMetadata(entry.Value));

            if (entry.Value is GroupMetadata groupMetadata &&
                groupMetadata.ConsolidatedMetadata is not null)
            {
                foreach (var child in Flatten(path, groupMetadata.ConsolidatedMetadata._metadata))
                {
                    yield return child;
                }
            }
        }
    }

    private static void Add(Dictionary<string, object> target, string path, object metadata)
    {
        var segments = path.Split('/');
        var current = target;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!current.TryGetValue(segments[index], out var existing) ||
                existing is not GroupMetadata existingGroup)
            {
                throw new MetadataException($"Missing parent group metadata for consolidated entry '{path}'.");
            }

            if (existingGroup.ConsolidatedMetadata is null)
            {
                existingGroup.ConsolidatedMetadata = new ConsolidatedMetadata();
            }

            current = existingGroup.ConsolidatedMetadata._metadata;
        }

        current[segments[^1]] = metadata;
    }

    private static object CloneMetadata(object value)
    {
        return value switch
        {
            GroupMetadata groupMetadata => groupMetadata.Clone(),
            ArrayMetadata arrayMetadata => arrayMetadata,
            _ => throw new MetadataException($"Unsupported consolidated metadata node '{value.GetType().Name}'."),
        };
    }

    private static int PathDepth(string path)
    {
        return string.IsNullOrEmpty(path)
            ? 0
            : path.Count(character => character == '/') + 1;
    }
}
