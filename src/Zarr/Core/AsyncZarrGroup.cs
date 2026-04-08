namespace Zarr.Core;

public sealed class AsyncZarrGroup : AsyncZarrNode
{
    internal AsyncZarrGroup(ZarrGroup group)
        : base(group)
    {
        Group = group;
    }

    public ZarrGroup Group { get; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ExtraFields => Group.ExtraFields;

    public IReadOnlyDictionary<string, IMetadataExtension> Extensions => Group.Extensions;

    public static async Task<AsyncZarrGroup> FromStore(IStore store, ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.CreateGroupAsyncCore(store, format: format, overwrite: overwrite, attributes: attributes, cancellationToken: cancellationToken).ConfigureAwait(false));
    }

    public static async Task<AsyncZarrGroup> Open(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.OpenGroupAsyncCore(store, path, format, useConsolidated, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AsyncZarrGroup> CreateGroupAsync(string path, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await ZarrApi.CreateGroupAsyncCore(Group.StoreForAsync(), PathUtilities.Combine(Group.Path, path), Group.Format, overwrite, attributes, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AsyncZarrArray> CreateArrayAsync<T>(string path, int[] shape, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, T? fillValue = default, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateArrayAsyncCore(Group.StoreForAsync(), DType.FromType(typeof(T)), shape, chunks, PathUtilities.Combine(Group.Path, path), Group.Format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AsyncZarrArray> CreateArrayAsync(string path, DType dataType, int[] shape, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, object? fillValue = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.CreateArrayAsyncCore(Group.StoreForAsync(), dataType ?? throw new ArgumentNullException(nameof(dataType)), shape, chunks, PathUtilities.Combine(Group.Path, path), Group.Format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public Task<AsyncZarrArray> CreateDatasetAsync<T>(string path, int[] shape, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, T? fillValue = default, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return CreateArrayAsync(path, shape, chunks, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken);
    }

    public async Task<AsyncZarrArray> CreateDatasetAsync(string path, Array data, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await ZarrApi.FromArrayAsyncCore(data, Group.StoreForAsync(), PathUtilities.Combine(Group.Path, path), Group.Format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AsyncZarrNode> GetItemAsync(string path, CancellationToken cancellationToken = default)
    {
        return Wrap(await Group.GetItemAsyncCore(path, cancellationToken).ConfigureAwait(false));
    }

    public Task DeleteItemAsync(string path, CancellationToken cancellationToken = default)
    {
        return Group.DeleteItemAsyncCore(path, cancellationToken);
    }

    public async Task<AsyncZarrGroup> RequireGroupAsync(string path, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrGroup(await Group.RequireGroupAsyncCore(path, overwrite, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AsyncZarrArray> RequireArrayAsync<T>(string path, int[] shape, bool exact = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        return new AsyncZarrArray(await Group.RequireArrayAsyncCore<T>(path, shape, exact, attributes, cancellationToken).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<KeyValuePair<string, AsyncZarrNode>>> MembersAsync(int? maxDepth = 0, bool? useConsolidatedForChildren = null, CancellationToken cancellationToken = default)
    {
        var members = (await Group.MembersAsyncCore(maxDepth, useConsolidatedForChildren: useConsolidatedForChildren, cancellationToken: cancellationToken).ConfigureAwait(false))
            .Select(item => new KeyValuePair<string, AsyncZarrNode>(item.Key, Wrap(item.Value)))
            .ToArray();
        return members;
    }

    public Task<GroupInfo> InfoAsync(bool? useConsolidatedForChildren = null, CancellationToken cancellationToken = default)
    {
        return Group.InfoAsyncCore(useConsolidatedForChildren, cancellationToken: cancellationToken);
    }

    public Task<string> TreeAsync(int? maxDepth = null, bool? useConsolidatedForChildren = null, CancellationToken cancellationToken = default)
    {
        return Group.TreeAsyncCore(maxDepth, useConsolidatedForChildren, cancellationToken: cancellationToken);
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
