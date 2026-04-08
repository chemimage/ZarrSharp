namespace Zarr.Core;

public sealed class AsyncZarrArray : AsyncZarrNode
{
    internal AsyncZarrArray(ZarrArray array)
        : base(array)
    {
        Array = array;
    }

    public ZarrArray Array { get; }

    public int[] Shape => Array.Shape;

    public int[] Chunks => Array.Chunks;

    public DType DataType => Array.DataType;

    public object FillValue => Array.FillValue;

    public ArrayOrder Order => Array.Order;

    public IChunkCodec? ChunkCodec => Array.ChunkCodec;

    public IReadOnlyList<IChunkCodec> ChunkCodecs => Array.ChunkCodecs;

    public ChunkGrid ChunkGrid => Array.ChunkGrid;

    public ChunkKeyEncoding ChunkKeyEncoding => Array.ChunkKeyEncoding;

    public string?[]? DimensionNames => Array.DimensionNames;

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> StorageTransformers => Array.StorageTransformers;

    public IReadOnlyList<IStorageTransformer> ParsedStorageTransformers => Array.ParsedStorageTransformers;

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ExtraFields => Array.ExtraFields;

    public IReadOnlyDictionary<string, IMetadataExtension> Extensions => Array.Extensions;

    public int InitializedChunkCount => Array.InitializedChunkCount;

    public Task<ArrayInfo> InfoAsync(CancellationToken cancellationToken = default)
    {
        return Array.InfoAsyncCore(cancellationToken);
    }

    public Task<T> GetValueAsync<T>(params int[] indices)
    {
        return Array.GetValueAsyncCore<T>(indices);
    }

    public Task<Array> ReadAsync(CancellationToken cancellationToken = default)
    {
        return Array.ReadAsyncCore(cancellationToken);
    }

    public Task<Array> ReadAsync<T>(CancellationToken cancellationToken = default)
    {
        return Array.ReadAsyncCore<T>(cancellationToken);
    }

    public Task WriteAsync(Array values, CancellationToken cancellationToken = default)
    {
        return Array.WriteAsyncCore(values, cancellationToken);
    }

    public Task WriteFlatAsync<T>(IReadOnlyList<T> values, CancellationToken cancellationToken = default)
    {
        return Array.WriteFlatAsyncCore(values, cancellationToken);
    }

    public Task SetValueAsync<T>(T value, params int[] indices)
    {
        return Array.SetValueAsyncCore(value, indices);
    }

    public Task ResizeAsync(int[] newShape, bool deleteOutsideChunks = true, CancellationToken cancellationToken = default)
    {
        return Array.ResizeAsyncCore(newShape, deleteOutsideChunks, cancellationToken);
    }

    public Task<int[]> AppendAsync(Array values, int axis = 0, CancellationToken cancellationToken = default)
    {
        return Array.AppendAsyncCore(values, axis, cancellationToken);
    }
}
