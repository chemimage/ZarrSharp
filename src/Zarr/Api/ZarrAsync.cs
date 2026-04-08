namespace Zarr.Api;

public static class ZarrAsync
{
    public static async Task<AsyncZarrArray> Array(Array data, IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.FromArrayAsyncCore(data, store, path, format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> FromArray(Array data, IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.FromArrayAsyncCore(data, store, path, format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> Group(IStore store, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.CreateGroupAsyncCore(store, path, format, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> CreateGroup(IStore store, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.CreateGroupAsyncCore(store, path, format, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> OpenGroup(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.OpenGroupAsyncCore(store, path, format, useConsolidated, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> CreateArray<T>(IStore store, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, T? fillValue = default, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateArrayAsyncCore(store, DType.FromType(typeof(T)), shape, chunks, path, format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> CreateArray(IStore store, DType dataType, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, object? fillValue = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateArrayAsyncCore(store, dataType ?? throw new ArgumentNullException(nameof(dataType)), shape, chunks, path, format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> OpenArray(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.OpenArrayAsyncCore(store, path, format, useConsolidated, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrNode> Open(IStore store, string path, ZarrOpenMode mode, ZarrFormat? format = null, IReadOnlyDictionary<string, object?>? attributes = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        return Wrap(await ZarrApi.OpenAsyncCore(store, path, mode, format, attributes, useConsolidated, cancellationToken).ConfigureAwait(false));
    }

    public static Task<AsyncZarrArray> Empty<T>(IStore store, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return CreateArray<T>(store, shape, chunks, path, format, overwrite, attributes, default, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken);
    }

    public static async Task<AsyncZarrArray> Zeros<T>(IStore store, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateArrayAsyncCore(store, DType.FromType(typeof(T)), shape, chunks, path, format, overwrite, attributes, 0, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> Ones<T>(IStore store, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateArrayAsyncCore(store, DType.FromType(typeof(T)), shape, chunks, path, format, overwrite, attributes, 1, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public static Task<AsyncZarrArray> Full<T>(IStore store, int[] shape, T fillValue, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return CreateArray(store, shape, chunks, path, format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken);
    }

    public static async Task<AsyncZarrArray> EmptyLike(AsyncZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateLikeAsyncCore(reference.Array, store, path, shape, chunks, dtype, reference.FillValue, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> ZerosLike(AsyncZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateLikeAsyncCore(reference.Array, store, path, shape, chunks, dtype, 0, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> OnesLike(AsyncZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateLikeAsyncCore(reference.Array, store, path, shape, chunks, dtype, 1, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> FullLike(AsyncZarrArray reference, IStore store, object fillValue, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateLikeAsyncCore(reference.Array, store, path, shape, chunks, dtype, fillValue, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrArray> OpenLike(AsyncZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, object? fillValue = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var existing = await MetadataStore.DetectNodeAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);
        if (!overwrite && existing.Kind == NodeKind.Array)
        {
            return new AsyncZarrArray(await ZarrApi.OpenArrayAsyncCore(store, normalizedPath, existing.Format ?? reference.Format, cancellationToken: cancellationToken).ConfigureAwait(false));
        }

        if (existing.Kind == NodeKind.Group)
        {
            throw new InvalidOperationException("Incompatible object already exists at the requested array path.");
        }

        return new AsyncZarrArray(await ZarrApi.CreateLikeAsyncCore(reference.Array, store, path, shape, chunks, dtype, fillValue ?? reference.FillValue, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> ConsolidateMetadata(IStore store, string path = "", ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.ConsolidateMetadataAsyncCore(store, path, format, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> OpenConsolidated(IStore store, string path = "", ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.OpenConsolidatedAsyncCore(store, path, format, cancellationToken).ConfigureAwait(false));
    }

    public static Task<object> Load(IStore store, string path = "", ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        return ZarrApi.LoadAsyncCore(store, path, format, cancellationToken);
    }

    public static async Task<AsyncZarrArray> Save(IStore store, Array data, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.SaveArrayAsyncCore(data, store, path, format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> Save(IStore store, IReadOnlyDictionary<string, Array> arrays, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.SaveGroupAsyncCore(store, arrays, path, format, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrNode> Save(IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default, params Array[] arrays)
    {
        return arrays.Length switch
        {
            0 => throw new ArgumentException("At least one array must be provided.", nameof(arrays)),
            1 => new AsyncZarrArray(await ZarrApi.SaveArrayAsyncCore(arrays[0], store, path, format, overwrite, attributes: attributes, cancellationToken: cancellationToken).ConfigureAwait(false)),
            _ => new AsyncZarrGroup(await ZarrApi.SaveGroupAsyncCore(store, ZarrApi.ToPositionalArrayMap(arrays), path, format, overwrite, attributes, cancellationToken).ConfigureAwait(false)),
        };
    }

    public static async Task<AsyncZarrGroup> SaveGroup(IStore store, IReadOnlyDictionary<string, Array> arrays, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.SaveGroupAsyncCore(store, arrays, path, format, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> SaveGroup(IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default, params Array[] arrays)
    {
        return new AsyncZarrGroup(await ZarrApi.SaveGroupAsyncCore(store, ZarrApi.ToPositionalArrayMap(arrays), path, format, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public static Task<string> Tree(AsyncZarrGroup group, int? maxDepth = null, bool? useConsolidatedForChildren = null, CancellationToken cancellationToken = default)
    {
        return group.TreeAsync(maxDepth, useConsolidatedForChildren, cancellationToken);
    }

    public static async Task<CopySummary> Copy(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", ZarrFormat? format = null, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        return await ZarrApi.CopyAsyncCore(sourceStore, destinationStore, sourcePath, destinationPath, format, overwrite, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CopySummary> CopyAll(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", ZarrFormat? format = null, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        return await ZarrApi.CopyAllAsyncCore(sourceStore, destinationStore, sourcePath, destinationPath, format, overwrite, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<CopySummary> CopyStore(IStore sourceStore, IStore destinationStore, string sourcePrefix = "", string destinationPrefix = "", bool overwrite = false, CancellationToken cancellationToken = default)
    {
        return await ZarrApi.CopyStoreAsyncCore(sourceStore, destinationStore, sourcePrefix, destinationPrefix, overwrite, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyDictionary<string, AsyncZarrNode>> CreateHierarchy(IStore store, IReadOnlyDictionary<string, HierarchyNodeDefinition> nodes, ZarrFormat? format = null, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var created = (await ZarrApi.CreateHierarchyAsyncCore(store, nodes, format, overwrite, cancellationToken).ConfigureAwait(false))
            .ToDictionary(item => item.Key, item => Wrap(item.Value), StringComparer.Ordinal);
        return created;
    }

    public static async Task<AsyncZarrNode> MigrateV2ToV3(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", bool overwrite = false, CancellationToken cancellationToken = default)
    {
        return Wrap(await ZarrApi.MigrateAsyncCore(sourceStore, destinationStore, ZarrFormat.V3, sourcePath, destinationPath, overwrite, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrNode> MigrateV3ToV2(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", bool overwrite = false, CancellationToken cancellationToken = default)
    {
        return Wrap(await ZarrApi.MigrateAsyncCore(sourceStore, destinationStore, ZarrFormat.V2, sourcePath, destinationPath, overwrite, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrNode> Migrate(IStore sourceStore, IStore destinationStore, ZarrFormat targetFormat, string sourcePath = "", string destinationPath = "", bool overwrite = false, CancellationToken cancellationToken = default)
    {
        return Wrap(await ZarrApi.MigrateAsyncCore(sourceStore, destinationStore, targetFormat, sourcePath, destinationPath, overwrite, cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> OpenGroup(IStore store, string path, ZarrOpenMode mode, ZarrFormat? format = null, IReadOnlyDictionary<string, object?>? attributes = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        return mode switch
        {
            ZarrOpenMode.Read => await OpenGroup(StoreFactory.MakeStore(store, ZarrOpenMode.Read), path, format, useConsolidated, cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.ReadWrite => await OpenGroup(store, path, format, useConsolidated, cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.Append => (await MetadataStore.DetectNodeAsync(store, path, cancellationToken).ConfigureAwait(false)).Kind == NodeKind.None
                ? await CreateGroup(store, path, format, overwrite: false, attributes, cancellationToken).ConfigureAwait(false)
                : await OpenGroup(store, path, format, useConsolidated, cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.Create => await CreateGroup(store, path, format, overwrite: true, attributes, cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.CreateNew => await CreateGroup(store, path, format, overwrite: false, attributes, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
        };
    }

    public static async Task<AsyncZarrArray> OpenArray<T>(IStore store, string path = "", ZarrOpenMode mode = ZarrOpenMode.Append, int[]? shape = null, int[]? chunks = null, ZarrFormat? format = null, IReadOnlyDictionary<string, object?>? attributes = null, T? fillValue = default, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var existing = await MetadataStore.DetectNodeAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);

        return mode switch
        {
            ZarrOpenMode.Read => await OpenArray(StoreFactory.MakeStore(store, ZarrOpenMode.Read), normalizedPath, existing.Format ?? format, cancellationToken: cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.ReadWrite => await OpenArray(store, normalizedPath, existing.Format ?? format, cancellationToken: cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.Append when existing.Kind == NodeKind.None => await CreateArray(store, shape ?? throw new InvalidOperationException($"Opening an array with mode '{mode}' requires a shape when the array does not already exist."), chunks, normalizedPath, format, overwrite: false, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.Append => await OpenArray(store, normalizedPath, existing.Format ?? format, cancellationToken: cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.Create => await CreateArray(store, shape ?? throw new InvalidOperationException($"Opening an array with mode '{mode}' requires a shape when the array does not already exist."), chunks, normalizedPath, format, overwrite: true, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false),
            ZarrOpenMode.CreateNew => await CreateArray(store, shape ?? throw new InvalidOperationException($"Opening an array with mode '{mode}' requires a shape when the array does not already exist."), chunks, normalizedPath, format, overwrite: false, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false),
            _ => await OpenArray(store, normalizedPath, existing.Format ?? format, cancellationToken: cancellationToken).ConfigureAwait(false),
        };
    }

    private static AsyncZarrNode Wrap(ZarrNode node)
    {
        return node switch
        {
            ZarrGroup group => new AsyncZarrGroup(group),
            ZarrArray array => new AsyncZarrArray(array),
            _ => throw new NotSupportedException($"Unsupported node type '{node.GetType().Name}'."),
        };
    }
}
