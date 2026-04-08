namespace Zarr.Api;

public abstract class HierarchyNodeDefinition
{
    protected HierarchyNodeDefinition(IReadOnlyDictionary<string, object?>? attributes = null)
    {
        Attributes = JsonUtilities.CloneDictionary(attributes);
    }

    public IReadOnlyDictionary<string, object?> Attributes { get; }

    public static HierarchyGroupDefinition Group(IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return new HierarchyGroupDefinition(attributes);
    }

    public static HierarchyArrayDefinition Array<T>(
        int[] shape,
        int[]? chunks = null,
        T? fillValue = default,
        IReadOnlyDictionary<string, object?>? attributes = null,
        ArrayOrder order = ArrayOrder.C,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return HierarchyArrayDefinition.Create(shape, chunks, fillValue, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }
}

public sealed class HierarchyGroupDefinition : HierarchyNodeDefinition
{
    public HierarchyGroupDefinition(IReadOnlyDictionary<string, object?>? attributes = null)
        : base(attributes)
    {
    }
}

public sealed class HierarchyArrayDefinition : HierarchyNodeDefinition
{
    public HierarchyArrayDefinition(
        DType dataType,
        int[] shape,
        int[]? chunks = null,
        object? fillValue = null,
        IReadOnlyDictionary<string, object?>? attributes = null,
        ArrayOrder order = ArrayOrder.C,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
        : base(attributes)
    {
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
        Shape = shape?.ToArray() ?? throw new ArgumentNullException(nameof(shape));
        if (Shape.Length == 0)
        {
            throw new ArgumentException("shape must contain at least one dimension.", nameof(shape));
        }

        if (Shape.Any(length => length <= 0))
        {
            throw new ArgumentException("shape values must be positive.", nameof(shape));
        }

        Chunks = chunks?.ToArray() ?? Shape.ToArray();
        if (Chunks.Length != Shape.Length)
        {
            throw new ArgumentException("chunks must have the same rank as shape.", nameof(chunks));
        }

        if (Chunks.Any(length => length <= 0))
        {
            throw new ArgumentException("chunk sizes must be positive.", nameof(chunks));
        }

        FillValue = fillValue;
        Order = order;
        ChunkCodec = chunkCodec;
        ChunkKeyEncoding = chunkKeyEncoding;
        V3Metadata = CloneV3Metadata(v3Metadata);
    }

    public DType DataType { get; }

    public int[] Shape { get; }

    public int[] Chunks { get; }

    public object? FillValue { get; }

    public ArrayOrder Order { get; }

    public IChunkCodec? ChunkCodec { get; }

    public ChunkKeyEncoding? ChunkKeyEncoding { get; }

    public V3ArrayMetadataOptions? V3Metadata { get; }

    public static HierarchyArrayDefinition Create<T>(
        int[] shape,
        int[]? chunks = null,
        T? fillValue = default,
        IReadOnlyDictionary<string, object?>? attributes = null,
        ArrayOrder order = ArrayOrder.C,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return new HierarchyArrayDefinition(
            DType.FromType(typeof(T)),
            shape,
            chunks,
            fillValue,
            attributes,
            order,
            chunkCodec,
            chunkKeyEncoding,
            v3Metadata);
    }

    internal V3ArrayMetadataOptions? CloneOptions()
    {
        return CloneV3Metadata(V3Metadata);
    }

    private static V3ArrayMetadataOptions? CloneV3Metadata(V3ArrayMetadataOptions? value)
    {
        if (value is null)
        {
            return null;
        }

        return new V3ArrayMetadataOptions
        {
            ChunkGrid = value.ChunkGrid is null ? null : new RegularChunkGrid(value.ChunkGrid.ChunkShape),
            DimensionNames = value.DimensionNames?.ToArray(),
            StorageTransformers = value.StorageTransformers?.Select(JsonUtilities.CloneDictionary).ToArray(),
            ExtraFields = value.ExtraFields?.ToDictionary(
                item => item.Key,
                item => (IReadOnlyDictionary<string, object?>)JsonUtilities.CloneDictionary(item.Value),
                StringComparer.Ordinal),
        };
    }
}
