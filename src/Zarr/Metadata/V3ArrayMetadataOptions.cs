namespace Zarr.Metadata;

public sealed class V3ArrayMetadataOptions
{
    public ChunkGrid? ChunkGrid { get; set; }

    public string?[]? DimensionNames { get; set; }

    public IReadOnlyList<IReadOnlyDictionary<string, object?>>? StorageTransformers { get; set; }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>>? ExtraFields { get; set; }
}
