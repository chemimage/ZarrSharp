namespace Zarr.Metadata;

internal abstract class GroupMetadata
{
    protected GroupMetadata(
        Dictionary<string, object?> attributes,
        ConsolidatedMetadata? consolidatedMetadata = null,
        Dictionary<string, Dictionary<string, object?>>? extraFields = null,
        Dictionary<string, IMetadataExtension>? parsedExtensions = null)
    {
        Attributes = attributes;
        ConsolidatedMetadata = consolidatedMetadata;
        ExtraFields = extraFields?.ToDictionary(
            item => item.Key,
            item => JsonUtilities.CloneDictionary(item.Value),
            StringComparer.Ordinal)
            ?? new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        ParsedExtensions = parsedExtensions?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal)
            ?? new Dictionary<string, IMetadataExtension>(StringComparer.Ordinal);
    }

    public abstract ZarrFormat Format { get; }

    public Dictionary<string, object?> Attributes { get; }

    public ConsolidatedMetadata? ConsolidatedMetadata { get; set; }

    public Dictionary<string, Dictionary<string, object?>> ExtraFields { get; }

    public Dictionary<string, IMetadataExtension> ParsedExtensions { get; }

    public GroupMetadata Clone()
    {
        return Format switch
        {
            ZarrFormat.V2 => new GroupV2Metadata(
                JsonUtilities.CloneDictionary(Attributes),
                ConsolidatedMetadata?.Clone()),
            ZarrFormat.V3 => new GroupV3Metadata(
                JsonUtilities.CloneDictionary(Attributes),
                ConsolidatedMetadata?.Clone(),
                ExtraFields.ToDictionary(
                    item => item.Key,
                    item => JsonUtilities.CloneDictionary(item.Value),
                    StringComparer.Ordinal),
                ParsedExtensions.ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal)),
            _ => throw new MetadataException($"Unsupported group metadata format '{Format}'."),
        };
    }
}

internal sealed class GroupV2Metadata : GroupMetadata
{
    public GroupV2Metadata(Dictionary<string, object?> attributes, ConsolidatedMetadata? consolidatedMetadata = null)
        : base(attributes, consolidatedMetadata)
    {
    }

    public override ZarrFormat Format => ZarrFormat.V2;
}

internal sealed class GroupV3Metadata : GroupMetadata
{
    public GroupV3Metadata(
        Dictionary<string, object?> attributes,
        ConsolidatedMetadata? consolidatedMetadata = null,
        Dictionary<string, Dictionary<string, object?>>? extraFields = null,
        Dictionary<string, IMetadataExtension>? parsedExtensions = null)
        : base(attributes, consolidatedMetadata, extraFields, parsedExtensions)
    {
    }

    public override ZarrFormat Format => ZarrFormat.V3;
}

internal abstract class ArrayMetadata
{
    protected ArrayMetadata(
        int[] shape,
        int[] chunks,
        ChunkGrid chunkGrid,
        DType dataType,
        object? fillValue,
        Dictionary<string, object?> attributes,
        ArrayOrder order,
        IChunkCodec? chunkCodec,
        ChunkKeyEncoding chunkKeyEncoding,
        string?[]? dimensionNames = null,
        Dictionary<string, object?>[]? storageTransformers = null,
        Dictionary<string, Dictionary<string, object?>>? extraFields = null,
        IStorageTransformer[]? parsedStorageTransformers = null,
        Dictionary<string, IMetadataExtension>? parsedExtensions = null)
    {
        Shape = shape.ToArray();
        Chunks = chunks.ToArray();
        ChunkGrid = chunkGrid;
        DataType = dataType;
        FillValue = dataType.Normalize(fillValue);
        Attributes = attributes;
        Order = order;
        ChunkCodec = chunkCodec;
        ChunkKeyEncoding = chunkKeyEncoding;
        DimensionNames = dimensionNames?.ToArray();
        StorageTransformers = storageTransformers?.Select(JsonUtilities.CloneDictionary).ToArray() ?? Array.Empty<Dictionary<string, object?>>();
        ParsedStorageTransformers = parsedStorageTransformers?.ToArray() ?? Array.Empty<IStorageTransformer>();
        ExtraFields = extraFields?.ToDictionary(
            item => item.Key,
            item => JsonUtilities.CloneDictionary(item.Value),
            StringComparer.Ordinal)
            ?? new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        ParsedExtensions = parsedExtensions?.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal)
            ?? new Dictionary<string, IMetadataExtension>(StringComparer.Ordinal);
    }

    public abstract ZarrFormat Format { get; }

    public int[] Shape { get; }

    public int[] Chunks { get; }

    public ChunkGrid ChunkGrid { get; }

    public DType DataType { get; }

    public object FillValue { get; }

    public Dictionary<string, object?> Attributes { get; }

    public ArrayOrder Order { get; }

    public IChunkCodec? ChunkCodec { get; }

    public ChunkKeyEncoding ChunkKeyEncoding { get; }

    public string?[]? DimensionNames { get; }

    public Dictionary<string, object?>[] StorageTransformers { get; protected set; }

    public IStorageTransformer[] ParsedStorageTransformers { get; protected set; }

    public Dictionary<string, Dictionary<string, object?>> ExtraFields { get; protected set; }

    public Dictionary<string, IMetadataExtension> ParsedExtensions { get; protected set; }
}

internal sealed class ArrayV2Metadata : ArrayMetadata
{
    public ArrayV2Metadata(
        int[] shape,
        int[] chunks,
        DType dataType,
        object? fillValue,
        Dictionary<string, object?> attributes,
        ArrayOrder order = ArrayOrder.C,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null)
        : base(
            shape,
            chunks,
            new RegularChunkGrid(chunks),
            dataType,
            fillValue,
            attributes,
            order,
            chunkCodec,
            chunkKeyEncoding ?? new V2ChunkKeyEncoding())
    {
        if (ChunkKeyEncoding.Name != "v2")
        {
            throw new MetadataException("Zarr v2 arrays require the v2 chunk key encoding.");
        }
    }

    public override ZarrFormat Format => ZarrFormat.V2;
}

internal sealed class ArrayV3Metadata : ArrayMetadata
{
    public ArrayV3Metadata(
        int[] shape,
        int[] chunks,
        ChunkGrid? chunkGrid,
        DType dataType,
        object? fillValue,
        Dictionary<string, object?> attributes,
        ArrayOrder order = ArrayOrder.C,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        string?[]? dimensionNames = null,
        Dictionary<string, object?>[]? storageTransformers = null,
        Dictionary<string, Dictionary<string, object?>>? extraFields = null,
        IStorageTransformer[]? parsedStorageTransformers = null,
        Dictionary<string, IMetadataExtension>? parsedExtensions = null)
        : base(
            shape,
            NormalizeChunkShape(chunks, chunkGrid),
            NormalizeChunkGrid(chunks, chunkGrid),
            dataType,
            fillValue,
            attributes,
            order,
            chunkCodec,
            chunkKeyEncoding ?? new DefaultChunkKeyEncoding(),
            NormalizeDimensionNames(shape, dimensionNames),
            storageTransformers ?? Array.Empty<Dictionary<string, object?>>(),
            extraFields,
            parsedStorageTransformers,
            parsedExtensions)
    {
        if (storageTransformers is not null && parsedStorageTransformers is null)
        {
            var normalizedStorageTransformers = NormalizeStorageTransformers(storageTransformers);
            StorageTransformers = normalizedStorageTransformers.Raw;
            ParsedStorageTransformers = normalizedStorageTransformers.Parsed;
        }

        if (extraFields is not null && parsedExtensions is null)
        {
            var normalizedExtraFields = NormalizeExtraFields(extraFields);
            ExtraFields = normalizedExtraFields.Raw;
            ParsedExtensions = normalizedExtraFields.Parsed;
        }
    }

    public override ZarrFormat Format => ZarrFormat.V3;

    private static int[] NormalizeChunkShape(int[] chunks, ChunkGrid? chunkGrid)
    {
        if (chunkGrid is null)
        {
            return chunks;
        }

        if (!chunkGrid.SupportsDataAccess)
        {
            return chunks;
        }

        var chunkShape = chunkGrid.ChunkShape;
        if (chunks.Length != chunkShape.Length || !chunks.SequenceEqual(chunkShape))
        {
            throw new MetadataException("The provided chunk grid is incompatible with the requested chunk shape.");
        }

        return chunkShape;
    }

    private static ChunkGrid NormalizeChunkGrid(int[] chunks, ChunkGrid? chunkGrid)
    {
        return chunkGrid ?? new RegularChunkGrid(chunks);
    }

    private static string?[]? NormalizeDimensionNames(int[] shape, string?[]? dimensionNames)
    {
        if (dimensionNames is null)
        {
            return null;
        }

        if (dimensionNames.Length != shape.Length)
        {
            throw new MetadataException("dimension_names must have the same rank as shape.");
        }

        return dimensionNames.ToArray();
    }

    private static (Dictionary<string, Dictionary<string, object?>> Raw, Dictionary<string, IMetadataExtension> Parsed) NormalizeExtraFields(Dictionary<string, Dictionary<string, object?>>? extraFields)
    {
        if (extraFields is null)
        {
            return (
                new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal),
                new Dictionary<string, IMetadataExtension>(StringComparer.Ordinal));
        }

        return MetadataStore.ParseExtraFields(extraFields, MetadataExtensionNodeKind.Array, MetadataStore.ReservedV3ArrayKeys);
    }

    private static (Dictionary<string, object?>[] Raw, IStorageTransformer[] Parsed) NormalizeStorageTransformers(Dictionary<string, object?>[]? storageTransformers)
    {
        if (storageTransformers is null)
        {
            return (Array.Empty<Dictionary<string, object?>>(), Array.Empty<IStorageTransformer>());
        }

        return MetadataStore.ParseStorageTransformers(storageTransformers);
    }
}

internal enum NodeKind
{
    None,
    Group,
    Array,
}

internal static class MetadataStore
{
    internal static readonly HashSet<string> ReservedV3GroupKeys = new(StringComparer.Ordinal)
    {
        "zarr_format",
        "node_type",
        "attributes",
        "consolidated_metadata",
    };

    internal static readonly HashSet<string> ReservedV3ArrayKeys = new(StringComparer.Ordinal)
    {
        "zarr_format",
        "node_type",
        "data_type",
        "shape",
        "chunk_grid",
        "chunk_key_encoding",
        "fill_value",
        "codecs",
        "attributes",
        "order",
        "storage_transformers",
        "dimension_names",
    };

    public static GroupMetadata CreateGroupMetadata(
        ZarrFormat format,
        Dictionary<string, object?> attributes,
        ConsolidatedMetadata? consolidatedMetadata = null,
        Dictionary<string, Dictionary<string, object?>>? extraFields = null)
    {
        return format switch
        {
            ZarrFormat.V2 when extraFields is { Count: > 0 } => throw new MetadataException("V3 extra fields can only be used with Zarr format V3 groups."),
            ZarrFormat.V2 => new GroupV2Metadata(attributes, consolidatedMetadata),
            ZarrFormat.V3 => CreateGroupV3Metadata(attributes, consolidatedMetadata, extraFields),
            _ => throw new MetadataException($"Unsupported Zarr format '{format}'."),
        };
    }

    public static ArrayMetadata CreateArrayMetadata(
        ZarrFormat format,
        int[] shape,
        int[] chunks,
        DType dataType,
        object? fillValue,
        Dictionary<string, object?> attributes,
        ArrayOrder order = ArrayOrder.C,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return format switch
        {
            ZarrFormat.V2 when v3Metadata is not null => throw new MetadataException("V3 metadata options can only be used with Zarr format V3 arrays."),
            ZarrFormat.V2 when dataType.IsStructured => throw new MetadataException("Structured record dtypes are only supported for Zarr format V3 arrays."),
            ZarrFormat.V2 => new ArrayV2Metadata(shape, chunks, dataType, fillValue, attributes, order, chunkCodec, chunkKeyEncoding),
            ZarrFormat.V3 => CreateArrayV3Metadata(
                shape,
                chunks,
                v3Metadata?.ChunkGrid,
                dataType,
                fillValue,
                attributes,
                order,
                chunkCodec,
                chunkKeyEncoding,
                v3Metadata?.DimensionNames,
                CloneStorageTransformers(v3Metadata?.StorageTransformers),
                CloneExtraFields(v3Metadata?.ExtraFields)),
            _ => throw new MetadataException($"Unsupported Zarr format '{format}'."),
        };
    }

    public static string ConsolidatedMetadataKey(ZarrFormat format, string path = "")
    {
        var normalized = PathUtilities.Normalize(path);
        var name = format == ZarrFormat.V2 ? ".zmetadata" : "zarr.consolidated.json";
        return normalized.Length == 0 ? name : $"{normalized}/{name}";
    }

    public static (NodeKind Kind, ZarrFormat? Format) DetectNode(IStore store, string path)
    {
        var normalized = PathUtilities.Normalize(path);
        if (store.Exists(V3MetadataKey(normalized)))
        {
            var payload = store.Get(V3MetadataKey(normalized)) ?? throw new MetadataException($"Missing metadata at '{normalized}'.");
            var metadata = JsonUtilities.DeserializeDictionary(payload);
            var nodeType = JsonUtilities.ConvertValue<string>(metadata.TryGetValue("node_type", out var value) ? value : null);
            return nodeType switch
            {
                "group" => (NodeKind.Group, ZarrFormat.V3),
                "array" => (NodeKind.Array, ZarrFormat.V3),
                _ => throw new MetadataException($"Unsupported node_type '{nodeType}'."),
            };
        }

        if (store.Exists(V2ArrayMetadataKey(normalized)))
        {
            return (NodeKind.Array, ZarrFormat.V2);
        }

        if (store.Exists(V2GroupMetadataKey(normalized)))
        {
            return (NodeKind.Group, ZarrFormat.V2);
        }

        return (NodeKind.None, null);
    }

    public static async Task<(NodeKind Kind, ZarrFormat? Format)> DetectNodeAsync(IStore store, string path, CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.Normalize(path);
        if (await store.ExistsAsync(V3MetadataKey(normalized), cancellationToken).ConfigureAwait(false))
        {
            var payload = await store.GetAsync(V3MetadataKey(normalized), cancellationToken).ConfigureAwait(false)
                ?? throw new MetadataException($"Missing metadata at '{normalized}'.");
            var metadata = JsonUtilities.DeserializeDictionary(payload);
            var nodeType = JsonUtilities.ConvertValue<string>(metadata.TryGetValue("node_type", out var value) ? value : null);
            return nodeType switch
            {
                "group" => (NodeKind.Group, ZarrFormat.V3),
                "array" => (NodeKind.Array, ZarrFormat.V3),
                _ => throw new MetadataException($"Unsupported node_type '{nodeType}'."),
            };
        }

        if (await store.ExistsAsync(V2ArrayMetadataKey(normalized), cancellationToken).ConfigureAwait(false))
        {
            return (NodeKind.Array, ZarrFormat.V2);
        }

        if (await store.ExistsAsync(V2GroupMetadataKey(normalized), cancellationToken).ConfigureAwait(false))
        {
            return (NodeKind.Group, ZarrFormat.V2);
        }

        return (NodeKind.None, null);
    }

    public static GroupMetadata LoadGroup(IStore store, string path, ZarrFormat? format = null)
    {
        var normalized = PathUtilities.Normalize(path);
        var detected = DetectNode(store, normalized);
        var resolvedFormat = format ?? detected.Format ?? throw new NodeNotFoundException(normalized);
        if (detected.Kind != NodeKind.Group || detected.Format != resolvedFormat)
        {
            throw new NodeNotFoundException(normalized);
        }

        return resolvedFormat switch
        {
            ZarrFormat.V2 => LoadGroupV2(store, normalized),
            ZarrFormat.V3 => LoadGroupV3(store, normalized),
            _ => throw new MetadataException($"Unsupported Zarr format '{resolvedFormat}'."),
        };
    }

    public static async Task<GroupMetadata> LoadGroupAsync(IStore store, string path, ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.Normalize(path);
        var detected = await DetectNodeAsync(store, normalized, cancellationToken).ConfigureAwait(false);
        var resolvedFormat = format ?? detected.Format ?? throw new NodeNotFoundException(normalized);
        if (detected.Kind != NodeKind.Group || detected.Format != resolvedFormat)
        {
            throw new NodeNotFoundException(normalized);
        }

        return resolvedFormat switch
        {
            ZarrFormat.V2 => await LoadGroupV2Async(store, normalized, cancellationToken).ConfigureAwait(false),
            ZarrFormat.V3 => await LoadGroupV3Async(store, normalized, cancellationToken).ConfigureAwait(false),
            _ => throw new MetadataException($"Unsupported Zarr format '{resolvedFormat}'."),
        };
    }

    public static ArrayMetadata LoadArray(IStore store, string path, ZarrFormat? format = null)
    {
        var normalized = PathUtilities.Normalize(path);
        var detected = DetectNode(store, normalized);
        var resolvedFormat = format ?? detected.Format ?? throw new NodeNotFoundException(normalized);
        if (detected.Kind != NodeKind.Array || detected.Format != resolvedFormat)
        {
            throw new NodeNotFoundException(normalized);
        }

        return resolvedFormat switch
        {
            ZarrFormat.V2 => LoadArrayV2(store, normalized),
            ZarrFormat.V3 => LoadArrayV3(store, normalized),
            _ => throw new MetadataException($"Unsupported Zarr format '{resolvedFormat}'."),
        };
    }

    public static async Task<ArrayMetadata> LoadArrayAsync(IStore store, string path, ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.Normalize(path);
        var detected = await DetectNodeAsync(store, normalized, cancellationToken).ConfigureAwait(false);
        var resolvedFormat = format ?? detected.Format ?? throw new NodeNotFoundException(normalized);
        if (detected.Kind != NodeKind.Array || detected.Format != resolvedFormat)
        {
            throw new NodeNotFoundException(normalized);
        }

        return resolvedFormat switch
        {
            ZarrFormat.V2 => await LoadArrayV2Async(store, normalized, cancellationToken).ConfigureAwait(false),
            ZarrFormat.V3 => await LoadArrayV3Async(store, normalized, cancellationToken).ConfigureAwait(false),
            _ => throw new MetadataException($"Unsupported Zarr format '{resolvedFormat}'."),
        };
    }

    public static void SaveGroup(IStore store, string path, GroupMetadata metadata)
    {
        var normalized = PathUtilities.Normalize(path);
        switch (metadata)
        {
            case GroupV2Metadata groupV2:
                store.Set(V2GroupMetadataKey(normalized), JsonUtilities.Serialize(new Dictionary<string, object?>
                {
                    ["zarr_format"] = 2,
                }));
                store.Set(V2AttributesKey(normalized), JsonUtilities.Serialize(groupV2.Attributes));
                break;

            case GroupV3Metadata groupV3:
                store.Set(V3MetadataKey(normalized), JsonUtilities.Serialize(SerializeGroupMetadata(groupV3)));
                break;

            default:
                throw new MetadataException($"Unsupported group metadata type '{metadata.GetType().Name}'.");
        }
    }

    public static async Task SaveGroupAsync(IStore store, string path, GroupMetadata metadata, CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.Normalize(path);
        switch (metadata)
        {
            case GroupV2Metadata groupV2:
                await store.SetAsync(V2GroupMetadataKey(normalized), JsonUtilities.Serialize(new Dictionary<string, object?>
                {
                    ["zarr_format"] = 2,
                }), cancellationToken).ConfigureAwait(false);
                await store.SetAsync(V2AttributesKey(normalized), JsonUtilities.Serialize(groupV2.Attributes), cancellationToken).ConfigureAwait(false);
                break;

            case GroupV3Metadata groupV3:
                await store.SetAsync(V3MetadataKey(normalized), JsonUtilities.Serialize(SerializeGroupMetadata(groupV3)), cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new MetadataException($"Unsupported group metadata type '{metadata.GetType().Name}'.");
        }
    }

    public static void SaveArray(IStore store, string path, ArrayMetadata metadata)
    {
        var normalized = PathUtilities.Normalize(path);
        switch (metadata)
        {
            case ArrayV2Metadata arrayV2:
                store.Set(V2ArrayMetadataKey(normalized), JsonUtilities.Serialize(SerializeArrayV2(arrayV2)));
                store.Set(V2AttributesKey(normalized), JsonUtilities.Serialize(arrayV2.Attributes));
                break;

            case ArrayV3Metadata arrayV3:
                store.Set(V3MetadataKey(normalized), JsonUtilities.Serialize(SerializeArrayV3(arrayV3)));
                break;

            default:
                throw new MetadataException($"Unsupported array metadata type '{metadata.GetType().Name}'.");
        }
    }

    public static async Task SaveArrayAsync(IStore store, string path, ArrayMetadata metadata, CancellationToken cancellationToken = default)
    {
        var normalized = PathUtilities.Normalize(path);
        switch (metadata)
        {
            case ArrayV2Metadata arrayV2:
                await store.SetAsync(V2ArrayMetadataKey(normalized), JsonUtilities.Serialize(SerializeArrayV2(arrayV2)), cancellationToken).ConfigureAwait(false);
                await store.SetAsync(V2AttributesKey(normalized), JsonUtilities.Serialize(arrayV2.Attributes), cancellationToken).ConfigureAwait(false);
                break;

            case ArrayV3Metadata arrayV3:
                await store.SetAsync(V3MetadataKey(normalized), JsonUtilities.Serialize(SerializeArrayV3(arrayV3)), cancellationToken).ConfigureAwait(false);
                break;

            default:
                throw new MetadataException($"Unsupported array metadata type '{metadata.GetType().Name}'.");
        }
    }

    public static IEnumerable<string> ListNodePaths(IStore store, ZarrFormat format)
    {
        foreach (var key in store.ListKeys())
        {
            if (format == ZarrFormat.V2 &&
                (key.Equals(".zgroup", StringComparison.Ordinal) || key.EndsWith("/.zgroup", StringComparison.Ordinal)))
            {
                yield return PathFromSuffix(key, ".zgroup");
            }
            else if (format == ZarrFormat.V2 &&
                     (key.Equals(".zarray", StringComparison.Ordinal) || key.EndsWith("/.zarray", StringComparison.Ordinal)))
            {
                yield return PathFromSuffix(key, ".zarray");
            }
            else if (format == ZarrFormat.V3 &&
                     (key.Equals("zarr.json", StringComparison.Ordinal) || key.EndsWith("/zarr.json", StringComparison.Ordinal)))
            {
                yield return PathFromSuffix(key, "zarr.json");
            }
        }
    }

    public static async Task<IReadOnlyList<string>> ListNodePathsAsync(IStore store, ZarrFormat format, CancellationToken cancellationToken = default)
    {
        var keys = await store.ListKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var result = new List<string>();
        foreach (var key in keys)
        {
            if (format == ZarrFormat.V2 &&
                (key.Equals(".zgroup", StringComparison.Ordinal) || key.EndsWith("/.zgroup", StringComparison.Ordinal)))
            {
                result.Add(PathFromSuffix(key, ".zgroup"));
            }
            else if (format == ZarrFormat.V2 &&
                     (key.Equals(".zarray", StringComparison.Ordinal) || key.EndsWith("/.zarray", StringComparison.Ordinal)))
            {
                result.Add(PathFromSuffix(key, ".zarray"));
            }
            else if (format == ZarrFormat.V3 &&
                     (key.Equals("zarr.json", StringComparison.Ordinal) || key.EndsWith("/zarr.json", StringComparison.Ordinal)))
            {
                result.Add(PathFromSuffix(key, "zarr.json"));
            }
        }

        return result;
    }

    public static void SaveConsolidatedMetadata(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var groupMetadata = LoadGroup(store, normalizedPath, format);
        var consolidated = BuildConsolidatedMetadata(store, normalizedPath, format);

        if (format == ZarrFormat.V3)
        {
            groupMetadata.ConsolidatedMetadata = consolidated.Clone();
            SaveGroup(store, normalizedPath, groupMetadata);
        }

        store.Set(
            ConsolidatedMetadataKey(format, normalizedPath),
            JsonUtilities.Serialize(new Dictionary<string, object?>
            {
                ["zarr_format"] = (int)format,
                ["metadata"] = SerializeFlattenedConsolidatedMetadata(groupMetadata, consolidated),
            }));
    }

    public static async Task SaveConsolidatedMetadataAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var groupMetadata = await LoadGroupAsync(store, normalizedPath, format, cancellationToken).ConfigureAwait(false);
        var consolidated = await BuildConsolidatedMetadataAsync(store, normalizedPath, format, cancellationToken).ConfigureAwait(false);

        if (format == ZarrFormat.V3)
        {
            groupMetadata.ConsolidatedMetadata = consolidated.Clone();
            await SaveGroupAsync(store, normalizedPath, groupMetadata, cancellationToken).ConfigureAwait(false);
        }

        await store.SetAsync(
            ConsolidatedMetadataKey(format, normalizedPath),
            JsonUtilities.Serialize(new Dictionary<string, object?>
            {
                ["zarr_format"] = (int)format,
                ["metadata"] = SerializeFlattenedConsolidatedMetadata(groupMetadata, consolidated),
            }),
            cancellationToken).ConfigureAwait(false);
    }

    public static bool HasConsolidatedMetadata(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        return store.Exists(ConsolidatedMetadataKey(format, normalizedPath)) ||
               (format == ZarrFormat.V3 && TryLoadInlineConsolidatedMetadata(store, normalizedPath) is not null);
    }

    public static async Task<bool> HasConsolidatedMetadataAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        return await store.ExistsAsync(ConsolidatedMetadataKey(format, normalizedPath), cancellationToken).ConfigureAwait(false) ||
               (format == ZarrFormat.V3 && await TryLoadInlineConsolidatedMetadataAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false) is not null);
    }

    public static Dictionary<string, object?> LoadConsolidatedMetadata(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        if (store.Exists(ConsolidatedMetadataKey(format, normalizedPath)))
        {
            var payload = store.Get(ConsolidatedMetadataKey(format, normalizedPath)) ?? throw new NodeNotFoundException(ConsolidatedMetadataKey(format, normalizedPath));
            var content = JsonUtilities.DeserializeDictionary(payload);
            return JsonUtilities.ToDictionary(content["metadata"], "metadata");
        }

        var inline = TryLoadInlineConsolidatedMetadata(store, normalizedPath);
        if (inline is not null)
        {
            return SerializeFlattenedConsolidatedMetadata(LoadGroup(store, normalizedPath, format), inline);
        }

        throw new NodeNotFoundException(ConsolidatedMetadataKey(format, normalizedPath));
    }

    public static async Task<Dictionary<string, object?>> LoadConsolidatedMetadataAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        if (await store.ExistsAsync(ConsolidatedMetadataKey(format, normalizedPath), cancellationToken).ConfigureAwait(false))
        {
            var payload = await store.GetAsync(ConsolidatedMetadataKey(format, normalizedPath), cancellationToken).ConfigureAwait(false)
                ?? throw new NodeNotFoundException(ConsolidatedMetadataKey(format, normalizedPath));
            var content = JsonUtilities.DeserializeDictionary(payload);
            return JsonUtilities.ToDictionary(content["metadata"], "metadata");
        }

        var inline = await TryLoadInlineConsolidatedMetadataAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);
        if (inline is not null)
        {
            return SerializeFlattenedConsolidatedMetadata(await LoadGroupAsync(store, normalizedPath, format, cancellationToken).ConfigureAwait(false), inline);
        }

        throw new NodeNotFoundException(ConsolidatedMetadataKey(format, normalizedPath));
    }

    public static GroupMetadata LoadGroupWithConsolidatedMetadata(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var group = LoadGroup(store, normalizedPath, format);
        group.ConsolidatedMetadata ??= TryLoadConsolidatedHierarchy(store, normalizedPath, format);
        return group;
    }

    public static async Task<GroupMetadata> LoadGroupWithConsolidatedMetadataAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var group = await LoadGroupAsync(store, normalizedPath, format, cancellationToken).ConfigureAwait(false);
        group.ConsolidatedMetadata ??= await TryLoadConsolidatedHierarchyAsync(store, normalizedPath, format, cancellationToken).ConfigureAwait(false);
        return group;
    }

    public static bool TryLoadConsolidatedNodeMetadata(IStore store, string path, ZarrFormat format, out object metadata)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        foreach (var ancestor in EnumerateConsolidatedSearchPaths(normalizedPath))
        {
            var consolidated = TryLoadConsolidatedHierarchy(store, ancestor, format);
            if (consolidated is null)
            {
                continue;
            }

            var relativePath = ancestor.Length == 0
                ? normalizedPath
                : string.Equals(ancestor, normalizedPath, StringComparison.Ordinal)
                    ? string.Empty
                    : normalizedPath[(ancestor.Length + 1)..];

            if (relativePath.Length == 0)
            {
                metadata = LoadGroupWithConsolidatedMetadata(store, ancestor, format);
                return true;
            }

            if (consolidated.TryResolve(relativePath, out metadata))
            {
                return true;
            }
        }

        metadata = default!;
        return false;
    }

    public static async Task<object?> TryLoadConsolidatedNodeMetadataAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        foreach (var ancestor in EnumerateConsolidatedSearchPaths(normalizedPath))
        {
            var consolidated = await TryLoadConsolidatedHierarchyAsync(store, ancestor, format, cancellationToken).ConfigureAwait(false);
            if (consolidated is null)
            {
                continue;
            }

            var relativePath = ancestor.Length == 0
                ? normalizedPath
                : string.Equals(ancestor, normalizedPath, StringComparison.Ordinal)
                    ? string.Empty
                    : normalizedPath[(ancestor.Length + 1)..];

            if (relativePath.Length == 0)
            {
                return await LoadGroupWithConsolidatedMetadataAsync(store, ancestor, format, cancellationToken).ConfigureAwait(false);
            }

            if (consolidated.TryResolve(relativePath, out var metadata))
            {
                return metadata;
            }
        }

        return null;
    }

    public static string V2GroupMetadataKey(string path)
    {
        var normalized = PathUtilities.Normalize(path);
        return normalized.Length == 0 ? ".zgroup" : $"{normalized}/.zgroup";
    }

    public static string V2ArrayMetadataKey(string path)
    {
        var normalized = PathUtilities.Normalize(path);
        return normalized.Length == 0 ? ".zarray" : $"{normalized}/.zarray";
    }

    public static string V2AttributesKey(string path)
    {
        var normalized = PathUtilities.Normalize(path);
        return normalized.Length == 0 ? ".zattrs" : $"{normalized}/.zattrs";
    }

    public static string V3MetadataKey(string path)
    {
        var normalized = PathUtilities.Normalize(path);
        return normalized.Length == 0 ? "zarr.json" : $"{normalized}/zarr.json";
    }

    private static GroupMetadata LoadGroupV2(IStore store, string path)
    {
        _ = store.Get(V2GroupMetadataKey(path)) ?? throw new NodeNotFoundException(path);
        return new GroupV2Metadata(LoadV2Attributes(store, path));
    }

    private static async Task<GroupMetadata> LoadGroupV2Async(IStore store, string path, CancellationToken cancellationToken)
    {
        _ = await store.GetAsync(V2GroupMetadataKey(path), cancellationToken).ConfigureAwait(false) ?? throw new NodeNotFoundException(path);
        return new GroupV2Metadata(await LoadV2AttributesAsync(store, path, cancellationToken).ConfigureAwait(false));
    }

    private static GroupMetadata LoadGroupV3(IStore store, string path)
    {
        var payload = store.Get(V3MetadataKey(path)) ?? throw new NodeNotFoundException(path);
        return LoadGroupV3FromDictionary(JsonUtilities.DeserializeDictionary(payload));
    }

    private static async Task<GroupMetadata> LoadGroupV3Async(IStore store, string path, CancellationToken cancellationToken)
    {
        var payload = await store.GetAsync(V3MetadataKey(path), cancellationToken).ConfigureAwait(false) ?? throw new NodeNotFoundException(path);
        return LoadGroupV3FromDictionary(JsonUtilities.DeserializeDictionary(payload));
    }

    private static ArrayMetadata LoadArrayV2(IStore store, string path)
    {
        var payload = store.Get(V2ArrayMetadataKey(path)) ?? throw new NodeNotFoundException(path);
        var metadata = JsonUtilities.DeserializeDictionary(payload);
        metadata["attributes"] = LoadV2Attributes(store, path);
        return LoadArrayV2FromDictionary(metadata);
    }

    private static async Task<ArrayMetadata> LoadArrayV2Async(IStore store, string path, CancellationToken cancellationToken)
    {
        var payload = await store.GetAsync(V2ArrayMetadataKey(path), cancellationToken).ConfigureAwait(false) ?? throw new NodeNotFoundException(path);
        var metadata = JsonUtilities.DeserializeDictionary(payload);
        metadata["attributes"] = await LoadV2AttributesAsync(store, path, cancellationToken).ConfigureAwait(false);
        return LoadArrayV2FromDictionary(metadata);
    }

    private static ArrayMetadata LoadArrayV3(IStore store, string path)
    {
        var payload = store.Get(V3MetadataKey(path)) ?? throw new NodeNotFoundException(path);
        return LoadArrayV3FromDictionary(JsonUtilities.DeserializeDictionary(payload));
    }

    private static async Task<ArrayMetadata> LoadArrayV3Async(IStore store, string path, CancellationToken cancellationToken)
    {
        var payload = await store.GetAsync(V3MetadataKey(path), cancellationToken).ConfigureAwait(false) ?? throw new NodeNotFoundException(path);
        return LoadArrayV3FromDictionary(JsonUtilities.DeserializeDictionary(payload));
    }

    private static Dictionary<string, object?> LoadV2Attributes(IStore store, string path)
    {
        var payload = store.Get(V2AttributesKey(path));
        return payload is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : JsonUtilities.DeserializeDictionary(payload.Value);
    }

    private static async Task<Dictionary<string, object?>> LoadV2AttributesAsync(IStore store, string path, CancellationToken cancellationToken)
    {
        var payload = await store.GetAsync(V2AttributesKey(path), cancellationToken).ConfigureAwait(false);
        return payload is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : JsonUtilities.DeserializeDictionary(payload.Value);
    }

    private static string PathFromSuffix(string key, string suffix)
    {
        if (key.Equals(suffix, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return key[..^(suffix.Length + 1)];
    }

    private static Dictionary<string, object?> SerializeGroupMetadata(GroupMetadata metadata)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["node_type"] = "group",
            ["zarr_format"] = (int)metadata.Format,
            ["attributes"] = JsonUtilities.CloneDictionary(metadata.Attributes),
        };

        if (metadata.ConsolidatedMetadata is not null)
        {
            result["consolidated_metadata"] = SerializeConsolidatedMetadata(metadata.ConsolidatedMetadata, metadata.Format);
        }

        foreach (var extraField in metadata.ExtraFields.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            result[extraField.Key] = JsonUtilities.CloneDictionary(extraField.Value);
        }

        return result;
    }

    private static Dictionary<string, object?> SerializeArrayMetadata(ArrayMetadata metadata)
    {
        return metadata switch
        {
            ArrayV2Metadata arrayV2 => SerializeArrayV2(arrayV2),
            ArrayV3Metadata arrayV3 => SerializeArrayV3(arrayV3),
            _ => throw new MetadataException($"Unsupported array metadata type '{metadata.GetType().Name}'."),
        };
    }

    private static Dictionary<string, object?> SerializeArrayV2(ArrayV2Metadata metadata)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["zarr_format"] = 2,
            ["shape"] = metadata.Shape.ToArray(),
            ["chunks"] = metadata.Chunks.ToArray(),
            ["dtype"] = metadata.DataType.ToMetadata(ZarrFormat.V2),
            ["fill_value"] = metadata.DataType.SerializeFillValue(metadata.FillValue),
            ["order"] = metadata.Order.ToString(),
            ["compressor"] = ChunkCodecFactory.ToV2CompressorMetadata(metadata.ChunkCodec, metadata.DataType),
            ["filters"] = ChunkCodecFactory.ToV2FiltersMetadata(metadata.ChunkCodec, metadata.DataType),
            ["dimension_separator"] = metadata.ChunkKeyEncoding.Separator,
        };
    }

    private static Dictionary<string, object?> SerializeArrayV3(ArrayV3Metadata metadata)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["zarr_format"] = 3,
            ["node_type"] = "array",
            ["shape"] = metadata.Shape.ToArray(),
            ["data_type"] = metadata.DataType.ToMetadata(ZarrFormat.V3),
            ["chunk_grid"] = metadata.ChunkGrid.ToMetadata(),
            ["chunk_key_encoding"] = metadata.ChunkKeyEncoding.ToMetadata(),
            ["fill_value"] = metadata.DataType.SerializeFillValue(metadata.FillValue),
            ["attributes"] = JsonUtilities.CloneDictionary(metadata.Attributes),
            ["order"] = metadata.Order.ToString(),
            ["codecs"] = ChunkCodecFactory.ToV3CodecsMetadata(metadata.ChunkCodec, metadata.DataType),
        };

        if (metadata.DimensionNames is not null)
        {
            result["dimension_names"] = metadata.DimensionNames.ToArray();
        }

        if (metadata.StorageTransformers.Length > 0)
        {
            result["storage_transformers"] = metadata.StorageTransformers
                .Select(JsonUtilities.CloneDictionary)
                .Cast<object?>()
                .ToArray();
        }

        foreach (var extraField in metadata.ExtraFields.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            result[extraField.Key] = JsonUtilities.CloneDictionary(extraField.Value);
        }

        return result;
    }

    private static ArrayOrder ParseOrder(object? value)
    {
        var order = JsonUtilities.ConvertValue<string>(value) ?? "C";
        return order switch
        {
            "C" => ArrayOrder.C,
            "F" => ArrayOrder.F,
            _ => throw new MetadataException($"Unsupported array order '{order}'."),
        };
    }

    private static GroupV3Metadata CreateGroupV3Metadata(
        Dictionary<string, object?> attributes,
        ConsolidatedMetadata? consolidatedMetadata,
        Dictionary<string, Dictionary<string, object?>>? extraFields)
    {
        var parsedExtraFields = ParseExtraFields(extraFields, MetadataExtensionNodeKind.Group, ReservedV3GroupKeys);
        return new GroupV3Metadata(attributes, consolidatedMetadata, parsedExtraFields.Raw, parsedExtraFields.Parsed);
    }

    private static ArrayV3Metadata CreateArrayV3Metadata(
        int[] shape,
        int[] chunks,
        ChunkGrid? chunkGrid,
        DType dataType,
        object? fillValue,
        Dictionary<string, object?> attributes,
        ArrayOrder order,
        IChunkCodec? chunkCodec,
        ChunkKeyEncoding? chunkKeyEncoding,
        string?[]? dimensionNames,
        Dictionary<string, object?>[]? storageTransformers,
        Dictionary<string, Dictionary<string, object?>>? extraFields)
    {
        var parsedStorageTransformers = ParseStorageTransformers(storageTransformers);
        var parsedExtraFields = ParseExtraFields(extraFields, MetadataExtensionNodeKind.Array, ReservedV3ArrayKeys);
        return new ArrayV3Metadata(
            shape,
            chunks,
            chunkGrid,
            dataType,
            fillValue,
            attributes,
            order,
            chunkCodec,
            chunkKeyEncoding,
            dimensionNames,
            parsedStorageTransformers.Raw,
            parsedExtraFields.Raw,
            parsedStorageTransformers.Parsed,
            parsedExtraFields.Parsed);
    }

    internal static (Dictionary<string, Dictionary<string, object?>> Raw, Dictionary<string, IMetadataExtension> Parsed) ParseExtraFields(
        Dictionary<string, object?> metadata,
        MetadataExtensionNodeKind nodeKind,
        HashSet<string> reservedKeys)
    {
        var rawFields = new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal);
        var parsedFields = new Dictionary<string, IMetadataExtension>(StringComparer.Ordinal);
        foreach (var extraField in metadata.Where(item => !reservedKeys.Contains(item.Key)))
        {
            var value = JsonUtilities.ToDictionary(extraField.Value, extraField.Key);
            ValidateExtraField(extraField.Key, value, nodeKind, reservedKeys);
            rawFields[extraField.Key] = JsonUtilities.CloneDictionary(value);
            if (MetadataExtensionRegistry.TryCreate(extraField.Key, nodeKind, value, out var extension))
            {
                parsedFields[extraField.Key] = extension;
            }
        }

        return (rawFields, parsedFields);
    }

    internal static (Dictionary<string, Dictionary<string, object?>> Raw, Dictionary<string, IMetadataExtension> Parsed) ParseExtraFields(
        Dictionary<string, Dictionary<string, object?>>? extraFields,
        MetadataExtensionNodeKind nodeKind,
        HashSet<string> reservedKeys)
    {
        if (extraFields is null)
        {
            return (
                new Dictionary<string, Dictionary<string, object?>>(StringComparer.Ordinal),
                new Dictionary<string, IMetadataExtension>(StringComparer.Ordinal));
        }

        var metadata = extraFields.ToDictionary(
            item => item.Key,
            item => (object?)item.Value,
            StringComparer.Ordinal);
        return ParseExtraFields(metadata, nodeKind, reservedKeys);
    }

    internal static (Dictionary<string, object?>[] Raw, IStorageTransformer[] Parsed) ParseStorageTransformers(Dictionary<string, object?>[]? storageTransformers)
    {
        if (storageTransformers is null)
        {
            return (Array.Empty<Dictionary<string, object?>>(), Array.Empty<IStorageTransformer>());
        }

        var raw = new List<Dictionary<string, object?>>(storageTransformers.Length);
        var parsed = new List<IStorageTransformer>(storageTransformers.Length);
        foreach (var storageTransformer in storageTransformers)
        {
            ValidateStorageTransformer(storageTransformer);
            var cloned = JsonUtilities.CloneDictionary(storageTransformer);
            raw.Add(cloned);
            var name = JsonUtilities.ConvertValue<string>(cloned.TryGetValue("name", out var rawName) ? rawName : null)!;
            var configuration = JsonUtilities.ToDictionary(
                cloned.TryGetValue("configuration", out var rawConfiguration) ? rawConfiguration : new Dictionary<string, object?>(),
                $"{name}.configuration");
            if (StorageTransformerRegistry.TryCreate(name, configuration, out var transformer))
            {
                parsed.Add(transformer);
            }
        }

        return (raw.ToArray(), parsed.ToArray());
    }

    private static Dictionary<string, object?>[]? CloneStorageTransformers(IReadOnlyList<IReadOnlyDictionary<string, object?>>? storageTransformers)
    {
        return storageTransformers?.Select(JsonUtilities.CloneDictionary).ToArray();
    }

    private static Dictionary<string, Dictionary<string, object?>>? CloneExtraFields(IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? extraFields)
    {
        if (extraFields is null)
        {
            return null;
        }

        return extraFields.ToDictionary(
            item => item.Key,
            item => JsonUtilities.CloneDictionary(item.Value),
            StringComparer.Ordinal);
    }

    private static ConsolidatedMetadata BuildConsolidatedMetadata(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var prefix = normalizedPath.Length == 0 ? string.Empty : normalizedPath + "/";
        var flatEntries = ListNodePaths(store, format)
            .Distinct(StringComparer.Ordinal)
            .Where(nodePath => !string.Equals(nodePath, normalizedPath, StringComparison.Ordinal))
            .Where(nodePath => prefix.Length == 0 || nodePath.StartsWith(prefix, StringComparison.Ordinal))
            .Select(nodePath => new KeyValuePair<string, object>(
                prefix.Length == 0 ? nodePath : nodePath[prefix.Length..],
                DetectNode(store, nodePath).Kind switch
                {
                    NodeKind.Group => CreateFlattenedConsolidatedGroupMetadata(LoadGroup(store, nodePath, format)),
                    NodeKind.Array => LoadArray(store, nodePath, format),
                    _ => throw new MetadataException($"Unsupported node '{nodePath}' in consolidated metadata."),
                }))
            .ToArray();

        return ConsolidatedMetadata.FromFlatMetadata(flatEntries);
    }

    private static async Task<ConsolidatedMetadata> BuildConsolidatedMetadataAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var prefix = normalizedPath.Length == 0 ? string.Empty : normalizedPath + "/";
        var nodePaths = (await ListNodePathsAsync(store, format, cancellationToken).ConfigureAwait(false))
            .Distinct(StringComparer.Ordinal)
            .Where(nodePath => !string.Equals(nodePath, normalizedPath, StringComparison.Ordinal))
            .Where(nodePath => prefix.Length == 0 || nodePath.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        var flatEntries = new List<KeyValuePair<string, object>>(nodePaths.Length);
        foreach (var nodePath in nodePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = prefix.Length == 0 ? nodePath : nodePath[prefix.Length..];
            var detected = await DetectNodeAsync(store, nodePath, cancellationToken).ConfigureAwait(false);
            var metadata = detected.Kind switch
            {
                NodeKind.Group => (object)CreateFlattenedConsolidatedGroupMetadata(
                    await LoadGroupAsync(store, nodePath, format, cancellationToken).ConfigureAwait(false)),
                NodeKind.Array => await LoadArrayAsync(store, nodePath, format, cancellationToken).ConfigureAwait(false),
                _ => throw new MetadataException($"Unsupported node '{nodePath}' in consolidated metadata."),
            };

            flatEntries.Add(new KeyValuePair<string, object>(relativePath, metadata));
        }

        return ConsolidatedMetadata.FromFlatMetadata(flatEntries);
    }

    private static Dictionary<string, object?> SerializeFlattenedConsolidatedMetadata(GroupMetadata groupMetadata, ConsolidatedMetadata consolidatedMetadata)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [string.Empty] = SerializeGroupMetadata(CreateGroupMetadata(
                groupMetadata.Format,
                JsonUtilities.CloneDictionary(groupMetadata.Attributes),
                extraFields: groupMetadata.ExtraFields.ToDictionary(
                    item => item.Key,
                    item => JsonUtilities.CloneDictionary(item.Value),
                    StringComparer.Ordinal))),
        };

        foreach (var entry in consolidatedMetadata.Flatten())
        {
            result[entry.Key] = entry.Value switch
            {
                GroupMetadata childGroup => SerializeGroupMetadata(CreateFlattenedConsolidatedGroupMetadata(childGroup)),
                ArrayMetadata arrayMetadata => SerializeArrayMetadata(arrayMetadata),
                _ => throw new MetadataException($"Unsupported consolidated metadata node '{entry.Value.GetType().Name}'."),
            };
        }

        return result;
    }

    private static Dictionary<string, object?> SerializeConsolidatedMetadata(ConsolidatedMetadata metadata, ZarrFormat format)
    {
        var serializedChildren = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var entry in metadata.Flatten())
        {
            serializedChildren[entry.Key] = entry.Value switch
            {
                GroupMetadata groupMetadata => SerializeGroupMetadata(CreateFlattenedConsolidatedGroupMetadata(groupMetadata)),
                ArrayMetadata arrayMetadata => SerializeArrayMetadata(arrayMetadata),
                _ => throw new MetadataException($"Unsupported consolidated metadata node '{entry.Value.GetType().Name}'."),
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = "inline",
            ["must_understand"] = false,
            ["metadata"] = serializedChildren,
        };
    }

    private static ConsolidatedMetadata? ParseConsolidatedMetadata(object? value, ZarrFormat format)
    {
        if (value is null)
        {
            return null;
        }

        var document = JsonUtilities.ToDictionary(value, "consolidated_metadata");
        ValidateMustUnderstandFalse(document, "consolidated_metadata");
        var metadataDictionary = JsonUtilities.ToDictionary(
            document.TryGetValue("metadata", out var rawMetadata) ? rawMetadata : null,
            "consolidated_metadata.metadata");

        var entries = metadataDictionary.ToDictionary(
            item => item.Key,
            item => ParseNodeMetadata(item.Value),
            StringComparer.Ordinal);

        return ConsolidatedMetadata.FromFlatMetadata(entries);
    }

    private static object ParseNodeMetadata(object? value)
    {
        var metadata = JsonUtilities.ToDictionary(value, "metadata");
        if (!metadata.TryGetValue("zarr_format", out var rawFormat))
        {
            throw new MetadataException("Metadata is missing its zarr_format.");
        }

        var format = (ZarrFormat)JsonUtilities.ConvertValue<int>(rawFormat);

        if (format == ZarrFormat.V3)
        {
            var nodeType = JsonUtilities.ConvertValue<string>(metadata.TryGetValue("node_type", out var rawNodeType) ? rawNodeType : null);
            return nodeType switch
            {
                "group" => LoadGroupV3FromDictionary(metadata),
                "array" => LoadArrayV3FromDictionary(metadata),
                _ => throw new MetadataException($"Unsupported node_type '{nodeType}' in consolidated metadata."),
            };
        }

        return metadata.ContainsKey("shape")
            ? LoadArrayV2FromDictionary(metadata)
            : new GroupV2Metadata(
                JsonUtilities.ToDictionary(metadata.TryGetValue("attributes", out var rawV2Attributes) ? rawV2Attributes : new Dictionary<string, object?>(), "attributes"));
    }

    private static ConsolidatedMetadata? TryLoadInlineConsolidatedMetadata(IStore store, string path)
    {
        if (!store.Exists(V3MetadataKey(path)))
        {
            return null;
        }

        var payload = store.Get(V3MetadataKey(path)) ?? throw new NodeNotFoundException(path);
        var metadata = JsonUtilities.DeserializeDictionary(payload);
        var nodeType = JsonUtilities.ConvertValue<string>(metadata.TryGetValue("node_type", out var rawNodeType) ? rawNodeType : null);
        if (!string.Equals(nodeType, "group", StringComparison.Ordinal))
        {
            return null;
        }

        var groupMetadata = LoadGroupV3FromDictionary(metadata);
        return groupMetadata.ConsolidatedMetadata?.Clone();
    }

    private static async Task<ConsolidatedMetadata?> TryLoadInlineConsolidatedMetadataAsync(IStore store, string path, CancellationToken cancellationToken)
    {
        if (!await store.ExistsAsync(V3MetadataKey(path), cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var payload = await store.GetAsync(V3MetadataKey(path), cancellationToken).ConfigureAwait(false) ?? throw new NodeNotFoundException(path);
        var metadata = JsonUtilities.DeserializeDictionary(payload);
        var nodeType = JsonUtilities.ConvertValue<string>(metadata.TryGetValue("node_type", out var rawNodeType) ? rawNodeType : null);
        if (!string.Equals(nodeType, "group", StringComparison.Ordinal))
        {
            return null;
        }

        var groupMetadata = LoadGroupV3FromDictionary(metadata);
        return groupMetadata.ConsolidatedMetadata?.Clone();
    }

    private static ConsolidatedMetadata? TryLoadConsolidatedHierarchy(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        if (store.Exists(ConsolidatedMetadataKey(format, normalizedPath)))
        {
            var payload = store.Get(ConsolidatedMetadataKey(format, normalizedPath)) ?? throw new NodeNotFoundException(ConsolidatedMetadataKey(format, normalizedPath));
            var content = JsonUtilities.DeserializeDictionary(payload);
            var metadata = JsonUtilities.ToDictionary(content.TryGetValue("metadata", out var rawMetadata) ? rawMetadata : null, "metadata");
            var flatEntries = metadata
                .Where(item => item.Key.Length > 0)
                .Select(item => new KeyValuePair<string, object>(item.Key, ParseNodeMetadata(item.Value)));
            return ConsolidatedMetadata.FromFlatMetadata(flatEntries);
        }

        return format == ZarrFormat.V3
            ? TryLoadInlineConsolidatedMetadata(store, normalizedPath)
            : null;
    }

    private static async Task<ConsolidatedMetadata?> TryLoadConsolidatedHierarchyAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        if (await store.ExistsAsync(ConsolidatedMetadataKey(format, normalizedPath), cancellationToken).ConfigureAwait(false))
        {
            var payload = await store.GetAsync(ConsolidatedMetadataKey(format, normalizedPath), cancellationToken).ConfigureAwait(false)
                ?? throw new NodeNotFoundException(ConsolidatedMetadataKey(format, normalizedPath));
            var content = JsonUtilities.DeserializeDictionary(payload);
            var metadata = JsonUtilities.ToDictionary(content.TryGetValue("metadata", out var rawMetadata) ? rawMetadata : null, "metadata");
            var flatEntries = metadata
                .Where(item => item.Key.Length > 0)
                .Select(item => new KeyValuePair<string, object>(item.Key, ParseNodeMetadata(item.Value)));
            return ConsolidatedMetadata.FromFlatMetadata(flatEntries);
        }

        return format == ZarrFormat.V3
            ? await TryLoadInlineConsolidatedMetadataAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static IEnumerable<string> EnumerateConsolidatedSearchPaths(string path)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        yield return normalizedPath;

        foreach (var parent in PathUtilities.EnumerateParentPaths(normalizedPath).Reverse())
        {
            yield return parent;
        }
    }

    private static GroupMetadata CreateFlattenedConsolidatedGroupMetadata(GroupMetadata groupMetadata)
    {
        return CreateGroupMetadata(
            groupMetadata.Format,
            JsonUtilities.CloneDictionary(groupMetadata.Attributes),
            groupMetadata.Format == ZarrFormat.V3 ? new ConsolidatedMetadata() : null,
            groupMetadata.ExtraFields.ToDictionary(
                item => item.Key,
                item => JsonUtilities.CloneDictionary(item.Value),
                StringComparer.Ordinal));
    }

    private static ArrayMetadata LoadArrayV2FromDictionary(Dictionary<string, object?> metadata)
    {
        var dtype = DType.FromMetadata(metadata.TryGetValue("dtype", out var rawDType) ? rawDType : null, ZarrFormat.V2);
        return new ArrayV2Metadata(
            JsonUtilities.ToIntArray(metadata["shape"], "shape"),
            JsonUtilities.ToIntArray(metadata["chunks"], "chunks"),
            dtype,
            dtype.DeserializeFillValue(metadata.TryGetValue("fill_value", out var fillValue) ? fillValue : null),
            JsonUtilities.ToDictionary(metadata.TryGetValue("attributes", out var attributes) ? attributes : new Dictionary<string, object?>(), "attributes"),
            ParseOrder(metadata.TryGetValue("order", out var order) ? order : null),
            ChunkCodecFactory.FromV2Metadata(
                metadata.TryGetValue("filters", out var filters) ? filters : null,
                metadata.TryGetValue("compressor", out var compressor) ? compressor : null),
            ChunkKeyEncodingFactory.FromV2Metadata(metadata.TryGetValue("dimension_separator", out var separator) ? separator : null));
    }

    private static ArrayMetadata LoadArrayV3FromDictionary(Dictionary<string, object?> metadata)
    {
        var dtype = DType.FromMetadata(metadata.TryGetValue("data_type", out var rawDataType) ? rawDataType : null, ZarrFormat.V3);
        var shape = JsonUtilities.ToIntArray(metadata["shape"], "shape");
        var chunkGridModel = metadata.TryGetValue("chunk_grid", out var rawChunkGrid)
            ? ChunkGridFactory.FromMetadata(rawChunkGrid)
            : new RegularChunkGrid(JsonUtilities.ToIntArray(metadata["chunk_shape"], "chunk_shape"));
        var chunks = chunkGridModel.SupportsDataAccess ? chunkGridModel.ChunkShape : shape.ToArray();
        var codecs = metadata.TryGetValue("codecs", out var codecList)
            ? ChunkCodecFactory.FromV3Metadata(codecList)
            : ChunkCodecFactory.FromMetadata(metadata.TryGetValue("codec", out var codec) ? codec : null);

        return CreateArrayV3Metadata(
            shape,
            chunks,
            chunkGridModel,
            dtype,
            dtype.DeserializeFillValue(metadata.TryGetValue("fill_value", out var fillValue) ? fillValue : null),
            JsonUtilities.ToDictionary(metadata.TryGetValue("attributes", out var attributes) ? attributes : new Dictionary<string, object?>(), "attributes"),
            ParseOrder(metadata.TryGetValue("order", out var order) ? order : null),
            codecs,
            ChunkKeyEncodingFactory.FromV3Metadata(metadata.TryGetValue("chunk_key_encoding", out var chunkKeyEncoding) ? chunkKeyEncoding : null),
            metadata.TryGetValue("dimension_names", out var dimensionNames) ? JsonUtilities.ToNullableStringArray(dimensionNames, "dimension_names") : null,
            metadata.TryGetValue("storage_transformers", out var storageTransformers) ? JsonUtilities.ToDictionaryArray(storageTransformers, "storage_transformers") : Array.Empty<Dictionary<string, object?>>(),
            ParseExtraFields(metadata, MetadataExtensionNodeKind.Array, ReservedV3ArrayKeys).Raw);
    }

    private static GroupMetadata LoadGroupV3FromDictionary(Dictionary<string, object?> metadata)
    {
        var nodeType = JsonUtilities.ConvertValue<string>(metadata.TryGetValue("node_type", out var rawNodeType) ? rawNodeType : null);
        if (!string.Equals(nodeType, "group", StringComparison.Ordinal))
        {
            throw new MetadataException($"Unsupported node_type '{nodeType}' for group metadata.");
        }

        return CreateGroupV3Metadata(
            JsonUtilities.ToDictionary(metadata.TryGetValue("attributes", out var attributes) ? attributes : new Dictionary<string, object?>(), "attributes"),
            ParseConsolidatedMetadata(metadata.TryGetValue("consolidated_metadata", out var consolidatedMetadata) ? consolidatedMetadata : null, ZarrFormat.V3),
            ParseExtraFields(metadata, MetadataExtensionNodeKind.Group, ReservedV3GroupKeys).Raw);
    }

    internal static void ValidateExtraField(string key, Dictionary<string, object?> value, MetadataExtensionNodeKind nodeKind, HashSet<string> reservedKeys)
    {
        if (reservedKeys.Contains(key))
        {
            throw new MetadataException($"Extra field '{key}' conflicts with a reserved metadata key.");
        }

        MetadataPolicyRegistry.ResolveConfigured().ValidateExtension(key, nodeKind, value);

        if (value.TryGetValue("configuration", out var rawConfiguration) &&
            rawConfiguration is Dictionary<string, object?> configuration &&
            configuration.Keys.Any(reservedKeys.Contains))
        {
            throw new MetadataException($"Extension '{key}' configuration contains a reserved key.");
        }
    }

    private static void ValidateMustUnderstandFalse(Dictionary<string, object?> value, string path)
    {
        MetadataPolicyRegistry.ResolveConfigured().ValidateConsolidatedMetadataEnvelope(value, path);
    }

    internal static void ValidateStorageTransformer(Dictionary<string, object?> value)
    {
        MetadataPolicyRegistry.ResolveConfigured().ValidateStorageTransformer(value);
    }
}
