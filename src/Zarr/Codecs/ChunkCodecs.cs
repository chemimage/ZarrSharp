using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using ZstdSharp;
using ZstdSharp.Unsafe;

namespace Zarr.Codecs;

public enum ChunkCodecStage
{
    ArrayArray,
    ArrayBytes,
    BytesBytes,
}

public enum ByteEndian
{
    Little,
    Big,
}

public interface IChunkCodec
{
    string Id { get; }

    ChunkCodecStage Stage { get; }

    ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C);

    Dictionary<string, object?> ToMetadata();
}

internal interface IDeterministicSizeChunkCodec
{
    int ComputeEncodedSize(int inputLength);
}

public sealed class ChunkCodecPipeline : IChunkCodec
{
    private readonly IChunkCodec[] _codecs;

    public ChunkCodecPipeline(params IChunkCodec[] codecs)
        : this((IEnumerable<IChunkCodec>)codecs)
    {
    }

    public ChunkCodecPipeline(IEnumerable<IChunkCodec> codecs)
    {
        _codecs = Flatten(codecs).ToArray();
        if (_codecs.Length == 0)
        {
            throw new ArgumentException("A chunk codec pipeline must contain at least one codec.", nameof(codecs));
        }

        ValidateStages(_codecs);
    }

    public string Id => "pipeline";

    public ChunkCodecStage Stage => _codecs.Select(codec => codec.Stage).DefaultIfEmpty(ChunkCodecStage.BytesBytes).Max();

    public IReadOnlyList<IChunkCodec> Codecs => _codecs;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        var payload = input;
        foreach (var codec in _codecs)
        {
            payload = codec.Encode(payload, dataType, chunkShape, order);
        }

        return payload;
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        var payload = input;
        for (var index = _codecs.Length - 1; index >= 0; index--)
        {
            payload = _codecs[index].Decode(payload, dataType, chunkShape, order);
        }

        return payload;
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = Id,
            ["codecs"] = _codecs.Select(codec => codec.ToMetadata()).Cast<object?>().ToArray(),
        };
    }

    private static IEnumerable<IChunkCodec> Flatten(IEnumerable<IChunkCodec> codecs)
    {
        foreach (var codec in codecs)
        {
            if (codec is ChunkCodecPipeline pipeline)
            {
                foreach (var nested in pipeline.Codecs)
                {
                    yield return nested;
                }

                continue;
            }

            yield return codec;
        }
    }

    private static void ValidateStages(IEnumerable<IChunkCodec> codecs)
    {
        var previousStage = ChunkCodecStage.ArrayArray;
        var arrayBytesSeen = 0;

        foreach (var codec in codecs)
        {
            if (codec.Stage < previousStage)
            {
                throw new ArgumentException($"Invalid codec order for '{codec.Id}'. Codec stages must flow from array-array to array-bytes to bytes-bytes.");
            }

            if (codec.Stage == ChunkCodecStage.ArrayBytes)
            {
                arrayBytesSeen++;
            }

            previousStage = codec.Stage;
        }

        if (arrayBytesSeen > 1)
        {
            throw new ArgumentException("Only one array-bytes codec is allowed in a chunk codec pipeline.");
        }
    }
}

public sealed class BytesChunkCodec : IChunkCodec, IArrayBytesCodec, IDeterministicSizeChunkCodec
{
    public BytesChunkCodec(ByteEndian? endian = null)
    {
        Endian = endian;
    }

    public ByteEndian? Endian { get; }

    public string Id => "bytes";

    public ChunkCodecStage Stage => ChunkCodecStage.ArrayBytes;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        return Transform(input, dataType);
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        return Transform(input, dataType);
    }

    public Dictionary<string, object?> ToMetadata()
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Id,
        };

        if (Endian is not null)
        {
            result["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["endian"] = Endian == ByteEndian.Little ? "little" : "big",
            };
        }

        return result;
    }

    internal bool IsDefaultFor(DType dataType)
    {
        var defaultEndian = ChunkCodecFactory.DefaultEndianFor(dataType);
        return Endian == defaultEndian;
    }

    public int ComputeEncodedSize(int inputLength)
    {
        return inputLength;
    }

    private ReadOnlyMemory<byte> Transform(ReadOnlyMemory<byte> input, DType dataType)
    {
        var segmentSize = GetEndianSegmentSize(dataType);
        if (segmentSize is null ||
            Endian is null ||
            Endian == ChunkCodecFactory.DefaultEndianFor(dataType))
        {
            return input.ToArray();
        }

        if (input.Length % segmentSize.Value != 0)
        {
            throw new MetadataException($"The bytes codec cannot apply endian swapping to payloads of length {input.Length} for dtype '{dataType.Name}'.");
        }

        var output = input.ToArray();
        for (var offset = 0; offset < output.Length; offset += segmentSize.Value)
        {
            Array.Reverse(output, offset, segmentSize.Value);
        }

        return output;
    }

    private static int? GetEndianSegmentSize(DType dataType)
    {
        return dataType.Name switch
        {
            "uint16" or "int16" => 2,
            "uint32" or "int32" or "float32" => 4,
            "uint64" or "int64" or "float64" or "datetime" or "timespan" => 8,
            "complex128" => 8,
            _ => null,
        };
    }
}

public sealed class GzipChunkCodec : IChunkCodec, IBytesBytesCodec
{
    public GzipChunkCodec(int level = 1)
    {
        Level = level;
    }

    public int Level { get; }

    public string Id => "gzip";

    public ChunkCodecStage Stage => ChunkCodecStage.BytesBytes;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, MapLevel(Level), leaveOpen: true))
        {
            gzip.Write(input.Span);
        }

        return output.ToArray();
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        using var source = new MemoryStream(input.ToArray(), writable: false);
        using var gzip = new GZipStream(source, CompressionMode.Decompress, leaveOpen: false);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = Id,
            ["level"] = Level,
        };
    }

    private static CompressionLevel MapLevel(int level)
    {
        return level <= 1 ? CompressionLevel.Fastest : CompressionLevel.Optimal;
    }
}

public sealed class Crc32cChunkCodec : IChunkCodec, IBytesBytesCodec, IDeterministicSizeChunkCodec
{
    private static readonly uint[] LookupTable = BuildLookupTable();

    public string Id => "crc32c";

    public ChunkCodecStage Stage => ChunkCodecStage.BytesBytes;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        var checksum = Compute(input.Span);
        var output = new byte[input.Length + 4];
        input.Span.CopyTo(output);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(input.Length, 4), checksum);
        return output;
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        if (input.Length < 4)
        {
            throw new MetadataException("CRC32C chunk payloads must contain at least four bytes.");
        }

        var data = input.Span.Slice(0, input.Length - 4);
        var storedChecksum = BinaryPrimitives.ReadUInt32LittleEndian(input.Span.Slice(input.Length - 4, 4));
        var computedChecksum = Compute(data);

        if (storedChecksum != computedChecksum)
        {
            throw new MetadataException($"Stored CRC32C checksum '{storedChecksum}' did not match computed checksum '{computedChecksum}'.");
        }

        return data.ToArray();
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Id,
        };
    }

    public int ComputeEncodedSize(int inputLength)
    {
        return inputLength + 4;
    }

    private static uint Compute(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xffffffffu;
        foreach (var value in bytes)
        {
            crc = LookupTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return ~crc;
    }

    private static uint[] BuildLookupTable()
    {
        const uint Polynomial = 0x82F63B78u;
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            var crc = index;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ Polynomial;
            }

            table[index] = crc;
        }

        return table;
    }
}

public sealed class ZstdChunkCodec : IChunkCodec, IBytesBytesCodec
{
    public ZstdChunkCodec(int level = 0, bool checksum = false)
    {
        if (level > 22)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "The zstd codec level must be less than or equal to 22.");
        }

        Level = level;
        Checksum = checksum;
    }

    public int Level { get; }

    public bool Checksum { get; }

    public string Id => "zstd";

    public ChunkCodecStage Stage => ChunkCodecStage.BytesBytes;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        using var output = new MemoryStream();
        using (var stream = new CompressionStream(output, Level, 131072, leaveOpen: true))
        {
            if (Checksum)
            {
                stream.SetParameter(ZSTD_cParameter.ZSTD_c_checksumFlag, 1);
            }

            stream.Write(input.Span);
        }

        return output.ToArray();
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        using var source = new MemoryStream(input.ToArray(), writable: false);
        using var stream = new DecompressionStream(source, 131072, false, false);
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = Id,
            ["level"] = Level,
            ["checksum"] = Checksum,
        };
    }
}

public sealed class TransposeChunkCodec : IChunkCodec, IArrayArrayCodec
{
    private readonly int[] _transposeOrder;
    private readonly int[] _inverseOrder;

    public TransposeChunkCodec(IEnumerable<int> order)
    {
        if (order is null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        _transposeOrder = order.ToArray();
        if (_transposeOrder.Length == 0)
        {
            throw new ArgumentException("The transpose order must contain at least one axis.", nameof(order));
        }

        if (_transposeOrder.Length != _transposeOrder.Distinct().Count())
        {
            throw new ArgumentException($"The transpose order contains duplicate axes: [{string.Join(", ", _transposeOrder)}].", nameof(order));
        }

        if (_transposeOrder.Any(axis => axis < 0))
        {
            throw new ArgumentException("Transpose axes must be non-negative.", nameof(order));
        }

        _inverseOrder = BuildInverseOrder(_transposeOrder);
    }

    public IReadOnlyList<int> Order => _transposeOrder;

    public string Id => "transpose";

    public ChunkCodecStage Stage => ChunkCodecStage.ArrayArray;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        ValidateAgainstChunkShape(chunkShape);
        var sourceShape = chunkShape!.ToArray();
        var targetShape = PermuteShape(sourceShape, _transposeOrder);
        return Transform(input, dataType, sourceShape, targetShape, _transposeOrder, order);
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        ValidateAgainstChunkShape(chunkShape);
        var targetShape = chunkShape!.ToArray();
        var sourceShape = PermuteShape(targetShape, _transposeOrder);
        return Transform(input, dataType, sourceShape, targetShape, _inverseOrder, order);
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Id,
            ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["order"] = _transposeOrder.Cast<object?>().ToArray(),
            },
        };
    }

    private void ValidateAgainstChunkShape(IReadOnlyList<int>? chunkShape)
    {
        if (chunkShape is null)
        {
            throw new MetadataException("The transpose codec requires a chunk shape at runtime.");
        }

        if (chunkShape.Count != _transposeOrder.Length)
        {
            throw new MetadataException($"The transpose codec expects {_transposeOrder.Length} dimensions but received {chunkShape.Count}.");
        }

        if (_transposeOrder.Any(axis => axis >= chunkShape.Count))
        {
            throw new MetadataException($"The transpose order [{string.Join(", ", _transposeOrder)}] is incompatible with chunk rank {chunkShape.Count}.");
        }
    }

    private static ReadOnlyMemory<byte> Transform(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int> sourceShape, IReadOnlyList<int> targetShape, IReadOnlyList<int> axisOrder, ArrayOrder arrayOrder)
    {
        var length = sourceShape.Aggregate(1, (current, value) => current * value);
        var sourceValues = dataType.Deserialize(input, length);
        var targetValues = dataType.CreateFilledArray(length);
        var sourceCoords = new int[sourceShape.Count];
        var targetCoords = new int[targetShape.Count];

        for (var flatIndex = 0; flatIndex < length; flatIndex++)
        {
            FromFlatIndex(flatIndex, sourceShape, arrayOrder, sourceCoords);
            for (var dimension = 0; dimension < axisOrder.Count; dimension++)
            {
                targetCoords[dimension] = sourceCoords[axisOrder[dimension]];
            }

            var targetFlatIndex = ToFlatIndex(targetCoords, targetShape, arrayOrder);
            targetValues.SetValue(sourceValues.GetValue(flatIndex), targetFlatIndex);
        }

        return dataType.Serialize(targetValues);
    }

    private static int[] PermuteShape(IReadOnlyList<int> shape, IReadOnlyList<int> order)
    {
        var result = new int[shape.Count];
        for (var index = 0; index < order.Count; index++)
        {
            result[index] = shape[order[index]];
        }

        return result;
    }

    private static int[] BuildInverseOrder(IReadOnlyList<int> order)
    {
        var inverse = new int[order.Count];
        for (var index = 0; index < order.Count; index++)
        {
            inverse[order[index]] = index;
        }

        return inverse;
    }

    private static void FromFlatIndex(int flatIndex, IReadOnlyList<int> shape, ArrayOrder order, int[] destination)
    {
        var remainder = flatIndex;
        if (order == ArrayOrder.C)
        {
            for (var dimension = shape.Count - 1; dimension >= 0; dimension--)
            {
                destination[dimension] = remainder % shape[dimension];
                remainder /= shape[dimension];
            }

            return;
        }

        for (var dimension = 0; dimension < shape.Count; dimension++)
        {
            destination[dimension] = remainder % shape[dimension];
            remainder /= shape[dimension];
        }
    }

    private static int ToFlatIndex(IReadOnlyList<int> indices, IReadOnlyList<int> shape, ArrayOrder order)
    {
        var result = 0;
        if (order == ArrayOrder.C)
        {
            for (var dimension = 0; dimension < indices.Count; dimension++)
            {
                result *= shape[dimension];
                result += indices[dimension];
            }

            return result;
        }

        var stride = 1;
        for (var dimension = 0; dimension < indices.Count; dimension++)
        {
            result += indices[dimension] * stride;
            stride *= shape[dimension];
        }

        return result;
    }
}

internal static class VariableLengthChunkCodecUtilities
{
    public static int GetElementCount(IReadOnlyList<int>? chunkShape, string codecId)
    {
        if (chunkShape is null)
        {
            throw new MetadataException($"The '{codecId}' codec requires a chunk shape at runtime.");
        }

        return chunkShape.Aggregate(1, (current, value) => current * value);
    }

    public static void WriteLength(Stream output, int length)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, length);
        output.Write(bytes);
    }

    public static int ReadLength(ReadOnlySpan<byte> input, ref int offset, string codecId)
    {
        if (offset + sizeof(int) > input.Length)
        {
            throw new MetadataException($"The '{codecId}' codec payload ended before the next value length could be read.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(input.Slice(offset, sizeof(int)));
        offset += sizeof(int);
        if (length < 0)
        {
            throw new MetadataException($"The '{codecId}' codec encountered a negative element length '{length}'.");
        }

        return length;
    }

    public static void EnsureValueBytesAvailable(ReadOnlySpan<byte> input, int offset, int length, string codecId)
    {
        if (offset + length > input.Length)
        {
            throw new MetadataException($"The '{codecId}' codec payload ended before the current value was fully decoded.");
        }
    }

    public static void EnsureFullyConsumed(ReadOnlySpan<byte> input, int offset, string codecId)
    {
        if (offset != input.Length)
        {
            throw new MetadataException($"The '{codecId}' codec payload contained trailing data after decoding the expected values.");
        }
    }
}

public sealed class VLenUtf8ChunkCodec : IChunkCodec, IArrayBytesCodec
{
    public string Id => "vlen-utf8";

    public ChunkCodecStage Stage => ChunkCodecStage.ArrayBytes;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        EnsureDataType(dataType);
        var values = dataType.Deserialize(input, VariableLengthChunkCodecUtilities.GetElementCount(chunkShape, Id));
        if (values is not string[] strings)
        {
            throw new MetadataException($"The '{Id}' codec expected string chunk data.");
        }

        using var output = new MemoryStream();
        foreach (var value in strings)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            VariableLengthChunkCodecUtilities.WriteLength(output, bytes.Length);
            output.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        EnsureDataType(dataType);
        var values = new string[VariableLengthChunkCodecUtilities.GetElementCount(chunkShape, Id)];
        var offset = 0;
        var bytes = input.Span;

        for (var index = 0; index < values.Length; index++)
        {
            var length = VariableLengthChunkCodecUtilities.ReadLength(bytes, ref offset, Id);
            VariableLengthChunkCodecUtilities.EnsureValueBytesAvailable(bytes, offset, length, Id);
            values[index] = Encoding.UTF8.GetString(bytes.Slice(offset, length));
            offset += length;
        }

        VariableLengthChunkCodecUtilities.EnsureFullyConsumed(bytes, offset, Id);
        return dataType.Serialize(values);
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Id,
            ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static void EnsureDataType(DType dataType)
    {
        if (!string.Equals(dataType.Name, "string", StringComparison.Ordinal))
        {
            throw new MetadataException("The 'vlen-utf8' codec can only be used with the string dtype.");
        }
    }
}

public sealed class VLenBytesChunkCodec : IChunkCodec, IArrayBytesCodec
{
    public string Id => "vlen-bytes";

    public ChunkCodecStage Stage => ChunkCodecStage.ArrayBytes;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        EnsureDataType(dataType);
        var values = dataType.Deserialize(input, VariableLengthChunkCodecUtilities.GetElementCount(chunkShape, Id));
        if (values is not byte[][] arrays)
        {
            throw new MetadataException($"The '{Id}' codec expected byte[] chunk data.");
        }

        using var output = new MemoryStream();
        foreach (var value in arrays)
        {
            var bytes = value ?? Array.Empty<byte>();
            VariableLengthChunkCodecUtilities.WriteLength(output, bytes.Length);
            output.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        EnsureDataType(dataType);
        var values = new byte[VariableLengthChunkCodecUtilities.GetElementCount(chunkShape, Id)][];
        var offset = 0;
        var bytes = input.Span;

        for (var index = 0; index < values.Length; index++)
        {
            var length = VariableLengthChunkCodecUtilities.ReadLength(bytes, ref offset, Id);
            VariableLengthChunkCodecUtilities.EnsureValueBytesAvailable(bytes, offset, length, Id);
            var value = new byte[length];
            bytes.Slice(offset, length).CopyTo(value);
            values[index] = value;
            offset += length;
        }

        VariableLengthChunkCodecUtilities.EnsureFullyConsumed(bytes, offset, Id);
        return dataType.Serialize(values);
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Id,
            ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal),
        };
    }

    private static void EnsureDataType(DType dataType)
    {
        if (!string.Equals(dataType.Name, "bytes", StringComparison.Ordinal))
        {
            throw new MetadataException("The 'vlen-bytes' codec can only be used with the bytes dtype.");
        }
    }
}

internal sealed class ChunkCodecRuntimeStages
{
    public ChunkCodecRuntimeStages(
        IReadOnlyList<IChunkCodec> arrayArrayCodecs,
        IChunkCodec? arrayBytesCodec,
        IReadOnlyList<IChunkCodec> bytesBytesCodecs)
    {
        ArrayArrayCodecs = arrayArrayCodecs ?? throw new ArgumentNullException(nameof(arrayArrayCodecs));
        ArrayBytesCodec = arrayBytesCodec;
        BytesBytesCodecs = bytesBytesCodecs ?? throw new ArgumentNullException(nameof(bytesBytesCodecs));
    }

    public IReadOnlyList<IChunkCodec> ArrayArrayCodecs { get; }

    public IChunkCodec? ArrayBytesCodec { get; }

    public IReadOnlyList<IChunkCodec> BytesBytesCodecs { get; }
}

internal static class ChunkCodecFactory
{
    private static readonly object RegistryGate = new();
    private static readonly Dictionary<string, Func<IReadOnlyDictionary<string, object?>, IChunkCodec>> Registry = CreateRegistry();

    public static IReadOnlyList<string> RegisteredIds
    {
        get
        {
            lock (RegistryGate)
            {
                return Registry.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            }
        }
    }

    public static IReadOnlyList<IChunkCodec> Expand(IChunkCodec? value)
    {
        return value switch
        {
            null => Array.Empty<IChunkCodec>(),
            ChunkCodecPipeline pipeline => pipeline.Codecs,
            _ => new[] { value },
        };
    }

    public static ByteEndian? DefaultEndianFor(DType dataType)
    {
        return dataType.Name switch
        {
            "uint16" or "uint32" or "uint64" or "int16" or "int32" or "int64" or "float32" or "float64" or "complex128" or "datetime" or "timespan" => BitConverter.IsLittleEndian ? ByteEndian.Little : ByteEndian.Big,
            _ => null,
        };
    }

    public static void Register(string id, Func<IReadOnlyDictionary<string, object?>, IChunkCodec> factory)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Codec ids must be non-empty.", nameof(id));
        }

        lock (RegistryGate)
        {
            Registry[id] = factory ?? throw new ArgumentNullException(nameof(factory));
        }
    }

    public static ReadOnlyMemory<byte> EncodePayload(IChunkCodec? value, ReadOnlyMemory<byte> payload, ZarrFormat format, DType dataType, IReadOnlyList<int> chunkShape, ArrayOrder order)
    {
        var runtimeStages = SplitForRuntime(value, format, dataType);
        return EncodeBytesBytesPayload(runtimeStages, EncodeArrayPayload(runtimeStages, payload, dataType, chunkShape, order), dataType, chunkShape, order);
    }

    public static ReadOnlyMemory<byte> DecodePayload(IChunkCodec? value, ReadOnlyMemory<byte> payload, ZarrFormat format, DType dataType, IReadOnlyList<int> chunkShape, ArrayOrder order)
    {
        var runtimeStages = SplitForRuntime(value, format, dataType);
        return DecodeArrayPayload(runtimeStages, DecodeBytesBytesPayload(runtimeStages, payload, dataType, chunkShape, order), dataType, chunkShape, order);
    }

    public static IChunkCodec? FromMetadata(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is not Dictionary<string, object?> dictionary)
        {
            throw new MetadataException("Chunk codec metadata is malformed.");
        }

        var id = JsonUtilities.ConvertValue<string>(dictionary.TryGetValue("id", out var codecId) ? codecId : null)
            ?? JsonUtilities.ConvertValue<string>(dictionary.TryGetValue("name", out var codecName) ? codecName : null);
        var configuration = TryGetConfiguration(dictionary);

        if (id is null)
        {
            return null;
        }

        if (string.Equals(id, "pipeline", StringComparison.Ordinal))
        {
            return FromV3Metadata(dictionary.TryGetValue("codecs", out var codecs) ? codecs : null);
        }

        Func<IReadOnlyDictionary<string, object?>, IChunkCodec>? factory;
        lock (RegistryGate)
        {
            Registry.TryGetValue(id, out factory);
        }

        if (factory is not null)
        {
            return factory(dictionary);
        }

        throw new MetadataException($"Unsupported chunk codec '{id}'.");
    }

    public static IChunkCodec? FromV2Metadata(object? filters, object? compressor)
    {
        var codecs = new List<IChunkCodec>();
        foreach (var filter in EnumerateMetadataEntries(filters))
        {
            var codec = FromMetadata(filter);
            if (codec is not null)
            {
                codecs.AddRange(Expand(codec));
            }
        }

        var compressorCodec = FromMetadata(compressor);
        if (compressorCodec is not null)
        {
            codecs.AddRange(Expand(compressorCodec));
        }

        return Create(codecs);
    }

    public static IChunkCodec? FromV3Metadata(object? value)
    {
        var codecs = EnumerateMetadataEntries(value)
            .Select(FromMetadata)
            .Where(codec => codec is not null)
            .SelectMany(codec => Expand(codec))
            .ToArray();

        return Create(codecs);
    }

    public static object? ToV2FiltersMetadata(IChunkCodec? value, DType dataType)
    {
        var codecs = ExpandV2Compatible(value, dataType);
        if (codecs.Count <= 1)
        {
            return null;
        }

        return codecs.Take(codecs.Count - 1)
            .Select(codec => codec.ToMetadata())
            .Cast<object?>()
            .ToArray();
    }

    public static object? ToV2CompressorMetadata(IChunkCodec? value, DType dataType)
    {
        var codecs = ExpandV2Compatible(value, dataType);
        return codecs.Count == 0 ? null : codecs[^1].ToMetadata();
    }

    public static object[] ToV3CodecsMetadata(IChunkCodec? value, DType dataType)
    {
        return ExpandForRuntime(value, ZarrFormat.V3, dataType)
            .Select(codec => NormalizeV3CodecMetadata(codec.ToMetadata()))
            .Cast<object>()
            .ToArray();
    }

    internal static ChunkCodecRuntimeStages SplitForRuntime(IChunkCodec? value, ZarrFormat format, DType dataType)
    {
        var arrayArrayCodecs = new List<IChunkCodec>();
        var bytesBytesCodecs = new List<IChunkCodec>();
        IChunkCodec? arrayBytesCodec = null;

        foreach (var codec in ExpandForRuntime(value, format, dataType))
        {
            switch (codec.Stage)
            {
                case ChunkCodecStage.ArrayArray:
                    arrayArrayCodecs.Add(codec);
                    break;

                case ChunkCodecStage.ArrayBytes:
                    arrayBytesCodec = codec;
                    break;

                case ChunkCodecStage.BytesBytes:
                    bytesBytesCodecs.Add(codec);
                    break;
            }
        }

        return new ChunkCodecRuntimeStages(arrayArrayCodecs, arrayBytesCodec, bytesBytesCodecs);
    }

    internal static ReadOnlyMemory<byte> EncodeArrayPayload(ChunkCodecRuntimeStages runtimeStages, ReadOnlyMemory<byte> payload, DType dataType, IReadOnlyList<int> chunkShape, ArrayOrder order)
    {
        var result = payload;
        foreach (var codec in runtimeStages.ArrayArrayCodecs)
        {
            result = codec.Encode(result, dataType, chunkShape, order);
        }

        if (runtimeStages.ArrayBytesCodec is not null)
        {
            result = runtimeStages.ArrayBytesCodec.Encode(result, dataType, chunkShape, order);
        }

        return result;
    }

    internal static ReadOnlyMemory<byte> DecodeArrayPayload(ChunkCodecRuntimeStages runtimeStages, ReadOnlyMemory<byte> payload, DType dataType, IReadOnlyList<int> chunkShape, ArrayOrder order)
    {
        var result = payload;
        if (runtimeStages.ArrayBytesCodec is not null)
        {
            result = runtimeStages.ArrayBytesCodec.Decode(result, dataType, chunkShape, order);
        }

        for (var index = runtimeStages.ArrayArrayCodecs.Count - 1; index >= 0; index--)
        {
            result = runtimeStages.ArrayArrayCodecs[index].Decode(result, dataType, chunkShape, order);
        }

        return result;
    }

    internal static ReadOnlyMemory<byte> EncodeBytesBytesPayload(ChunkCodecRuntimeStages runtimeStages, ReadOnlyMemory<byte> payload, DType dataType, IReadOnlyList<int> chunkShape, ArrayOrder order)
    {
        var result = payload;
        foreach (var codec in runtimeStages.BytesBytesCodecs)
        {
            result = codec.Encode(result, dataType, chunkShape, order);
        }

        return result;
    }

    internal static ReadOnlyMemory<byte> DecodeBytesBytesPayload(ChunkCodecRuntimeStages runtimeStages, ReadOnlyMemory<byte> payload, DType dataType, IReadOnlyList<int> chunkShape, ArrayOrder order)
    {
        var result = payload;
        for (var index = runtimeStages.BytesBytesCodecs.Count - 1; index >= 0; index--)
        {
            result = runtimeStages.BytesBytesCodecs[index].Decode(result, dataType, chunkShape, order);
        }

        return result;
    }

    internal static bool TryComputeEncodedSize(IChunkCodec? value, ZarrFormat format, DType dataType, int inputLength, out int encodedLength)
    {
        encodedLength = inputLength;
        foreach (var codec in ExpandForRuntime(value, format, dataType))
        {
            if (codec is not IDeterministicSizeChunkCodec deterministicSizeCodec)
            {
                encodedLength = 0;
                return false;
            }

            encodedLength = deterministicSizeCodec.ComputeEncodedSize(encodedLength);
        }

        return true;
    }

    private static IReadOnlyList<IChunkCodec> ExpandForRuntime(IChunkCodec? value, ZarrFormat format, DType dataType)
    {
        var codecs = Expand(value).ToList();
        if (format == ZarrFormat.V3 && !codecs.Any(codec => codec.Stage == ChunkCodecStage.ArrayBytes))
        {
            var insertAt = codecs.FindLastIndex(codec => codec.Stage == ChunkCodecStage.ArrayArray) + 1;
            codecs.Insert(insertAt, new BytesChunkCodec(DefaultEndianFor(dataType)));
        }

        return codecs;
    }

    private static IReadOnlyList<IChunkCodec> ExpandV2Compatible(IChunkCodec? value, DType dataType)
    {
        var codecs = new List<IChunkCodec>();
        foreach (var codec in Expand(value))
        {
            switch (codec.Stage)
            {
                case ChunkCodecStage.BytesBytes:
                    codecs.Add(codec);
                    break;

                case ChunkCodecStage.ArrayBytes when codec is BytesChunkCodec bytesCodec && bytesCodec.IsDefaultFor(dataType):
                    break;

                case ChunkCodecStage.ArrayBytes:
                    throw new MetadataException($"Zarr v2 metadata cannot represent the array-bytes codec '{codec.Id}'.");

                case ChunkCodecStage.ArrayArray:
                    throw new MetadataException($"Zarr v2 metadata cannot represent the array-array codec '{codec.Id}'.");
            }
        }

        return codecs;
    }

    private static IChunkCodec? Create(IEnumerable<IChunkCodec> codecs)
    {
        var array = codecs.ToArray();
        return array.Length switch
        {
            0 => null,
            1 => array[0],
            _ => new ChunkCodecPipeline(array),
        };
    }

    private static Dictionary<string, object?> NormalizeV3CodecMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        if (metadata.TryGetValue("id", out var rawId))
        {
            var name = JsonUtilities.ConvertValue<string>(rawId)
                ?? throw new MetadataException("V3 codec metadata contained a null or invalid codec id.");

            var configuration = metadata
                .Where(item => !string.Equals(item.Key, "id", StringComparison.Ordinal) &&
                               !string.Equals(item.Key, "name", StringComparison.Ordinal))
                .ToDictionary(
                    item => item.Key,
                    item => NormalizeV3CodecMetadataValue(item.Value),
                    StringComparer.Ordinal);

            var result = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
            };

            if (configuration.Count > 0)
            {
                NormalizeV3CodecConfiguration(name, configuration);
                result["configuration"] = configuration;
            }

            return result;
        }

        var normalized = metadata.ToDictionary(
            item => item.Key,
            item => NormalizeV3CodecMetadataValue(item.Value),
            StringComparer.Ordinal);
        if (normalized.TryGetValue("name", out var rawName) &&
            JsonUtilities.ConvertValue<string>(rawName) is string codecName &&
            TryGetConfiguration(normalized) is { } codecConfiguration)
        {
            NormalizeV3CodecConfiguration(codecName, codecConfiguration);
        }

        return normalized;
    }

    private static void NormalizeV3CodecConfiguration(string name, Dictionary<string, object?> configuration)
    {
        if (!string.Equals(name, "blosc", StringComparison.Ordinal) ||
            !configuration.TryGetValue("shuffle", out var rawShuffle))
        {
            return;
        }

        configuration["shuffle"] = rawShuffle switch
        {
            string text => text.ToLowerInvariant() switch
            {
                "noshuffle" or "shuffle" or "bitshuffle" => text.ToLowerInvariant(),
                _ => rawShuffle,
            },
            _ => JsonUtilities.ConvertValue<int>(rawShuffle) switch
            {
                0 => "noshuffle",
                1 => "shuffle",
                2 => "bitshuffle",
                _ => rawShuffle,
            },
        };
    }

    private static object? NormalizeV3CodecMetadataValue(object? value)
    {
        return value switch
        {
            Dictionary<string, object?> dictionary => NormalizeV3CodecMetadata(dictionary),
            IReadOnlyDictionary<string, object?> readOnlyDictionary => NormalizeV3CodecMetadata(
                readOnlyDictionary.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal)),
            IEnumerable<object?> list => list.Select(NormalizeV3CodecMetadataValue).ToArray(),
            _ => value,
        };
    }

    private static IEnumerable<object?> EnumerateMetadataEntries(object? value)
    {
        return value switch
        {
            null => Array.Empty<object?>(),
            List<object?> list => list,
            _ => new[] { value },
        };
    }

    private static Dictionary<string, object?>? TryGetConfiguration(IReadOnlyDictionary<string, object?> value)
    {
        if (!value.TryGetValue("configuration", out var rawConfiguration) ||
            rawConfiguration is null)
        {
            return null;
        }

        return JsonUtilities.ToDictionary(rawConfiguration, "codec.configuration");
    }

    private static ByteEndian? ParseEndian(IReadOnlyDictionary<string, object?>? configuration)
    {
        var rawEndian = JsonUtilities.ConvertValue<string>(configuration?.TryGetValue("endian", out var value) == true ? value : null);
        return rawEndian?.ToLowerInvariant() switch
        {
            null => null,
            "little" => ByteEndian.Little,
            "big" => ByteEndian.Big,
            _ => throw new MetadataException($"Unsupported bytes codec endian '{rawEndian}'."),
        };
    }

    private static Dictionary<string, object?> CollectMetadataConfiguration(IReadOnlyDictionary<string, object?> metadata)
    {
        var configuration = TryGetConfiguration(metadata);
        if (configuration is not null)
        {
            return configuration;
        }

        return metadata
            .Where(item => !string.Equals(item.Key, "id", StringComparison.Ordinal) &&
                           !string.Equals(item.Key, "name", StringComparison.Ordinal) &&
                           !string.Equals(item.Key, "configuration", StringComparison.Ordinal))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }

    private static GzipChunkCodec CreateGzipCodec(IReadOnlyDictionary<string, object?> metadata)
    {
        var configuration = CollectMetadataConfiguration(metadata);
        return new GzipChunkCodec(
            JsonUtilities.ConvertValue<int>(
                configuration.TryGetValue("level", out var level) ? level : 1));
    }

    private static ZstdChunkCodec CreateZstdCodec(IReadOnlyDictionary<string, object?> metadata)
    {
        var configuration = CollectMetadataConfiguration(metadata);
        return new ZstdChunkCodec(
            JsonUtilities.ConvertValue<int>(
                configuration.TryGetValue("level", out var level) ? level : 0),
            JsonUtilities.ConvertValue<bool>(
                configuration.TryGetValue("checksum", out var checksum) ? checksum : false));
    }

    private static Dictionary<string, Func<IReadOnlyDictionary<string, object?>, IChunkCodec>> CreateRegistry()
    {
        return new Dictionary<string, Func<IReadOnlyDictionary<string, object?>, IChunkCodec>>(StringComparer.Ordinal)
        {
            ["bytes"] = metadata => new BytesChunkCodec(ParseEndian(TryGetConfiguration(metadata))),
            ["endian"] = metadata => new BytesChunkCodec(ParseEndian(TryGetConfiguration(metadata))),
            ["blosc"] = metadata => BloscChunkCodec.FromMetadata(CollectMetadataConfiguration(metadata)),
            ["crc32c"] = _ => new Crc32cChunkCodec(),
            ["gzip"] = CreateGzipCodec,
            ["sharding_indexed"] = ShardingChunkCodec.FromMetadata,
            ["zstd"] = CreateZstdCodec,
            ["transpose"] = metadata =>
            {
                var configuration = TryGetConfiguration(metadata)
                    ?? throw new MetadataException("Transpose codec metadata must include a configuration object.");
                var rawOrder = configuration.TryGetValue("order", out var order) ? order : null;
                if (rawOrder is not IEnumerable<object?> values)
                {
                    throw new MetadataException("Transpose codec configuration is missing its order.");
                }

                return new TransposeChunkCodec(values.Select(value => JsonUtilities.ConvertValue<int>(value)));
            },
            ["vlen-utf8"] = _ => new VLenUtf8ChunkCodec(),
            ["vlen-bytes"] = _ => new VLenBytesChunkCodec(),
        };
    }

}
