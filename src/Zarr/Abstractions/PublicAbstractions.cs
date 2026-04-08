namespace Zarr.Abstractions;

public enum MetadataExtensionNodeKind
{
    Array,
    Group,
}

public interface IArrayArrayCodec
{
    string Id { get; }

    ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    Dictionary<string, object?> ToMetadata();
}

public interface IArrayBytesCodec
{
    string Id { get; }

    ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    Dictionary<string, object?> ToMetadata();
}

public interface IBytesBytesCodec
{
    string Id { get; }

    ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    Dictionary<string, object?> ToMetadata();
}

public interface IChunkGrid
{
    string Name { get; }

    bool SupportsDataAccess { get; }

    int[] ChunkShape { get; }

    int[] GridShape(int[] arrayShape);

    IEnumerable<int[]> AllChunkCoordinates(int[] arrayShape);

    Dictionary<string, object?> ToMetadata();
}

public interface IStorageTransformer
{
    string Name { get; }

    IReadOnlyDictionary<string, object?> Configuration { get; }

    Dictionary<string, object?> ToMetadata();
}

public interface IMetadataExtension
{
    string Name { get; }

    bool MustUnderstand { get; }

    MetadataExtensionNodeKind NodeKind { get; }

    IReadOnlyDictionary<string, object?> Configuration { get; }

    Dictionary<string, object?> ToMetadata();
}

public interface IMetadataExtensionProvider
{
    string Name { get; }

    MetadataExtensionNodeKind NodeKind { get; }

    IMetadataExtension Parse(IReadOnlyDictionary<string, object?> metadata);
}

public interface IMetadataPolicy
{
    string Name { get; }

    void ValidateExtension(string name, MetadataExtensionNodeKind nodeKind, IReadOnlyDictionary<string, object?> metadata);

    void ValidateStorageTransformer(IReadOnlyDictionary<string, object?> metadata);

    void ValidateConsolidatedMetadataEnvelope(IReadOnlyDictionary<string, object?> metadata, string path);
}

public interface IDTypeProvider
{
    bool TryFromType(Type type, out DType dataType);

    bool TryFromName(string name, out DType dataType);

    bool TryFromMetadata(object? metadata, ZarrFormat format, out DType dataType);
}

public interface IStoreProvider
{
    IReadOnlyCollection<string> UriSchemes { get; }

    Type? OptionsType { get; }

    bool CanOpen(object specification);

    IStore Open(object specification, ZarrOpenMode? mode = null, IReadOnlyDictionary<string, string>? storageOptions = null);
}
