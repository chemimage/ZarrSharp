using System.Buffers.Binary;

namespace Zarr.Codecs;

public enum ShardingCodecIndexLocation
{
    Start,
    End,
}

public sealed class ShardingChunkCodec : IChunkCodec, IArrayBytesCodec
{
    private const ulong EmptyValue = ulong.MaxValue;
    private readonly int[] _chunkShape;
    private readonly IChunkCodec _codecs;
    private readonly IChunkCodec _indexCodecs;

    public ShardingChunkCodec(
        IEnumerable<int> chunkShape,
        IChunkCodec? codecs = null,
        IChunkCodec? indexCodecs = null,
        ShardingCodecIndexLocation indexLocation = ShardingCodecIndexLocation.End)
    {
        _chunkShape = chunkShape?.ToArray() ?? throw new ArgumentNullException(nameof(chunkShape));
        if (_chunkShape.Length == 0)
        {
            throw new ArgumentException("The sharding codec chunk shape must contain at least one dimension.", nameof(chunkShape));
        }

        if (_chunkShape.Any(length => length <= 0))
        {
            throw new ArgumentException("The sharding codec chunk shape values must be positive.", nameof(chunkShape));
        }

        _codecs = codecs ?? new BytesChunkCodec();
        _indexCodecs = indexCodecs ?? new ChunkCodecPipeline(new BytesChunkCodec(ByteEndian.Little), new Crc32cChunkCodec());
        IndexLocation = indexLocation;

        ValidateStages(_codecs, nameof(codecs));
        ValidateIndexStages(_indexCodecs, nameof(indexCodecs));
    }

    public string Id => "sharding_indexed";

    public ChunkCodecStage Stage => ChunkCodecStage.ArrayBytes;

    public int[] ChunkShape => _chunkShape.ToArray();

    public ShardingCodecIndexLocation IndexLocation { get; }

    public IReadOnlyList<IChunkCodec> Codecs => ChunkCodecFactory.Expand(_codecs);

    public IReadOnlyList<IChunkCodec> IndexCodecs => ChunkCodecFactory.Expand(_indexCodecs);

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        var shardShape = ValidateShardShape(chunkShape);
        var chunksPerShard = GetChunksPerShard(shardShape);
        var shardLength = shardShape.Aggregate(1, (current, value) => current * value);
        var shardValues = dataType.Deserialize(input, shardLength);
        var shardBuffers = new List<ReadOnlyMemory<byte>>();
        var chunkEntries = new List<(ulong Offset, ulong Length)>();
        var payloadLength = 0;

        foreach (var chunkCoords in EnumerateCoordinates(chunksPerShard))
        {
            var innerChunk = ExtractInnerChunk(shardValues, shardShape, chunkCoords, dataType, order);
            var encodedChunk = ChunkCodecFactory.EncodePayload(_codecs, dataType.Serialize(innerChunk), ZarrFormat.V3, dataType, _chunkShape, order);
            shardBuffers.Add(encodedChunk);
            chunkEntries.Add(((ulong)payloadLength, (ulong)encodedChunk.Length));
            payloadLength += encodedChunk.Length;
        }

        var indexBytes = EncodeIndex(chunkEntries, chunksPerShard);
        if (IndexLocation == ShardingCodecIndexLocation.Start)
        {
            var shiftedEntries = chunkEntries
                .Select(entry => (entry.Offset + (ulong)indexBytes.Length, entry.Length))
                .ToList();
            indexBytes = EncodeIndex(shiftedEntries, chunksPerShard);
            return Combine(IndexLocation, indexBytes, shardBuffers);
        }

        return Combine(IndexLocation, indexBytes, shardBuffers);
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        var shardShape = ValidateShardShape(chunkShape);
        var chunksPerShard = GetChunksPerShard(shardShape);
        var shardValues = dataType.CreateFilledArray(shardShape.Aggregate(1, (current, value) => current * value));
        var decodedIndex = DecodeIndex(input, chunksPerShard);
        var chunkIndex = 0;

        foreach (var chunkCoords in EnumerateCoordinates(chunksPerShard))
        {
            var (offset, length) = decodedIndex[chunkIndex++];
            if (offset == EmptyValue || length == EmptyValue)
            {
                continue;
            }

            var chunkBytes = SliceChunkBytes(input, offset, length);
            var decodedChunkBytes = ChunkCodecFactory.DecodePayload(_codecs, chunkBytes, ZarrFormat.V3, dataType, _chunkShape, order);
            var decodedChunk = dataType.Deserialize(decodedChunkBytes, _chunkShape.Aggregate(1, (current, value) => current * value));
            WriteInnerChunk(shardValues, shardShape, chunkCoords, decodedChunk, order);
        }

        return dataType.Serialize(shardValues);
    }

    internal Array? DecodePartialChunk(ReadOnlyMemory<byte>? payload, DType dataType, IReadOnlyList<int> shardShape, IReadOnlyList<int> chunkCoords, ArrayOrder order = ArrayOrder.C)
    {
        if (payload is null || payload.Value.Length == 0)
        {
            return null;
        }

        var normalizedShardShape = ValidateShardShape(shardShape);
        var chunksPerShard = GetChunksPerShard(normalizedShardShape);
        var chunkIndex = GetChunkIndex(chunksPerShard, chunkCoords);
        var (offset, length) = DecodeIndex(payload.Value, chunksPerShard)[chunkIndex];
        if (offset == EmptyValue || length == EmptyValue)
        {
            return null;
        }

        var chunkBytes = SliceChunkBytes(payload.Value, offset, length);
        var decodedChunkBytes = ChunkCodecFactory.DecodePayload(_codecs, chunkBytes, ZarrFormat.V3, dataType, _chunkShape, order);
        return dataType.Deserialize(decodedChunkBytes, _chunkShape.Aggregate(1, (current, value) => current * value));
    }

    internal ReadOnlyMemory<byte>? EncodePartialChunk(ReadOnlyMemory<byte>? existingPayload, DType dataType, IReadOnlyList<int> shardShape, IReadOnlyList<int> chunkCoords, Array? chunkValues, ArrayOrder order = ArrayOrder.C)
    {
        var normalizedShardShape = ValidateShardShape(shardShape);
        var chunksPerShard = GetChunksPerShard(normalizedShardShape);
        var chunkCount = chunksPerShard.Aggregate(1, (current, value) => current * value);
        var shardBuffers = new ReadOnlyMemory<byte>[chunkCount];

        if (existingPayload is not null && existingPayload.Value.Length > 0)
        {
            var existingEntries = DecodeIndex(existingPayload.Value, chunksPerShard);
            for (var index = 0; index < existingEntries.Length; index++)
            {
                var (offset, length) = existingEntries[index];
                if (offset == EmptyValue || length == EmptyValue)
                {
                    continue;
                }

                shardBuffers[index] = SliceChunkBytes(existingPayload.Value, offset, length);
            }
        }

        var targetIndex = GetChunkIndex(chunksPerShard, chunkCoords);
        if (chunkValues is not null)
        {
            shardBuffers[targetIndex] = ChunkCodecFactory.EncodePayload(_codecs, dataType.Serialize(chunkValues), ZarrFormat.V3, dataType, _chunkShape, order);
        }

        var entries = new (ulong Offset, ulong Length)[chunkCount];
        var payloadLength = 0UL;
        for (var index = 0; index < shardBuffers.Length; index++)
        {
            if (shardBuffers[index].Length == 0)
            {
                entries[index] = (EmptyValue, EmptyValue);
                continue;
            }

            entries[index] = (payloadLength, (ulong)shardBuffers[index].Length);
            payloadLength += (ulong)shardBuffers[index].Length;
        }

        if (payloadLength == 0)
        {
            return null;
        }

        var indexBytes = EncodeIndex(entries, chunksPerShard);
        if (IndexLocation == ShardingCodecIndexLocation.Start)
        {
            var shiftedEntries = entries
                .Select(entry => entry.Offset == EmptyValue || entry.Length == EmptyValue
                    ? entry
                    : (entry.Offset + (ulong)indexBytes.Length, entry.Length))
                .ToArray();
            indexBytes = EncodeIndex(shiftedEntries, chunksPerShard);
        }

        return Combine(IndexLocation, indexBytes, shardBuffers);
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Id,
            ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["chunk_shape"] = _chunkShape.Cast<object?>().ToArray(),
                ["codecs"] = Codecs.Select(codec => codec.ToMetadata()).Cast<object?>().ToArray(),
                ["index_codecs"] = IndexCodecs.Select(codec => codec.ToMetadata()).Cast<object?>().ToArray(),
                ["index_location"] = IndexLocation == ShardingCodecIndexLocation.Start ? "start" : "end",
            },
        };
    }

    internal static ShardingChunkCodec FromMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        var configuration = JsonUtilities.ToDictionary(
            metadata.TryGetValue("configuration", out var rawConfiguration) ? rawConfiguration : null,
            "codec.configuration");

        var chunkShape = JsonUtilities.ToIntArray(
            configuration.TryGetValue("chunk_shape", out var rawChunkShape) ? rawChunkShape : null,
            "codec.configuration.chunk_shape");
        var codecs = ChunkCodecFactory.FromV3Metadata(configuration.TryGetValue("codecs", out var rawCodecs) ? rawCodecs : null);
        var indexCodecs = ChunkCodecFactory.FromV3Metadata(configuration.TryGetValue("index_codecs", out var rawIndexCodecs) ? rawIndexCodecs : null);
        var indexLocation = JsonUtilities.ConvertValue<string>(configuration.TryGetValue("index_location", out var rawLocation) ? rawLocation : null)
            ?.ToLowerInvariant() switch
        {
            null or "end" => ShardingCodecIndexLocation.End,
            "start" => ShardingCodecIndexLocation.Start,
            var value => throw new MetadataException($"Unsupported sharding index location '{value}'."),
        };

        return new ShardingChunkCodec(chunkShape, codecs, indexCodecs, indexLocation);
    }

    private ReadOnlyMemory<byte> Combine(ShardingCodecIndexLocation indexLocation, ReadOnlyMemory<byte> indexBytes, IReadOnlyList<ReadOnlyMemory<byte>> shardBuffers)
    {
        var totalLength = indexBytes.Length + shardBuffers.Sum(buffer => buffer.Length);
        using var owner = BufferPrototypeRegistry.ResolveConfigured().Allocate(totalLength);
        var payload = owner.Memory.Slice(0, totalLength);
        var offset = 0;

        if (indexLocation == ShardingCodecIndexLocation.Start)
        {
            indexBytes.CopyTo(payload);
            offset = indexBytes.Length;
        }

        foreach (var shardBuffer in shardBuffers)
        {
            shardBuffer.CopyTo(payload[offset..]);
            offset += shardBuffer.Length;
        }

        if (indexLocation == ShardingCodecIndexLocation.End)
        {
            indexBytes.CopyTo(payload[offset..]);
        }

        return payload.ToArray();
    }

    private static ReadOnlyMemory<byte> SliceChunkBytes(ReadOnlyMemory<byte> payload, ulong offset, ulong length)
    {
        var chunkLength = checked((int)length);
        using var owner = BufferPrototypeRegistry.ResolveConfigured().Allocate(chunkLength);
        var result = owner.Memory.Slice(0, chunkLength);
        payload.Slice(checked((int)offset), chunkLength).CopyTo(result);
        return result.ToArray();
    }

    private (ulong Offset, ulong Length)[] DecodeIndex(ReadOnlyMemory<byte> payload, int[] chunksPerShard)
    {
        var rawIndexLength = chunksPerShard.Aggregate(1, (current, value) => current * value) * 16;
        var encodedIndexLength = ComputeIndexEncodedLength(rawIndexLength);
        var indexBytes = new byte[encodedIndexLength];

        if (IndexLocation == ShardingCodecIndexLocation.Start)
        {
            payload.Span.Slice(0, encodedIndexLength).CopyTo(indexBytes);
        }
        else
        {
            payload.Span.Slice(payload.Length - encodedIndexLength, encodedIndexLength).CopyTo(indexBytes);
        }

        var decodedIndex = ChunkCodecFactory.DecodePayload(
            _indexCodecs,
            indexBytes,
            ZarrFormat.V3,
            DType.UInt64,
            new[] { chunksPerShard.Aggregate(1, (current, value) => current * value), 2 },
            ArrayOrder.C);

        var result = new (ulong Offset, ulong Length)[decodedIndex.Length / 16];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = (
                BinaryPrimitives.ReadUInt64LittleEndian(decodedIndex.Span.Slice(index * 16, 8)),
                BinaryPrimitives.ReadUInt64LittleEndian(decodedIndex.Span.Slice((index * 16) + 8, 8)));
        }

        return result;
    }

    private ReadOnlyMemory<byte> EncodeIndex(IReadOnlyList<(ulong Offset, ulong Length)> entries, int[] chunksPerShard)
    {
        using var owner = BufferPrototypeRegistry.ResolveConfigured().Allocate(entries.Count * 16);
        var rawIndex = owner.Memory.Slice(0, entries.Count * 16);
        for (var index = 0; index < entries.Count; index++)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(rawIndex.Span.Slice(index * 16, 8), entries[index].Offset);
            BinaryPrimitives.WriteUInt64LittleEndian(rawIndex.Span.Slice((index * 16) + 8, 8), entries[index].Length);
        }

        return ChunkCodecFactory.EncodePayload(
            _indexCodecs,
            rawIndex,
            ZarrFormat.V3,
            DType.UInt64,
            new[] { chunksPerShard.Aggregate(1, (current, value) => current * value), 2 },
            ArrayOrder.C);
    }

    private int ComputeIndexEncodedLength(int rawIndexLength)
    {
        if (!ChunkCodecFactory.TryComputeEncodedSize(_indexCodecs, ZarrFormat.V3, DType.UInt64, rawIndexLength, out var encodedLength))
        {
            throw new NotSupportedException("This sharding implementation requires index_codecs with a deterministic encoded size.");
        }

        return encodedLength;
    }

    private Array ExtractInnerChunk(Array shardValues, IReadOnlyList<int> shardShape, IReadOnlyList<int> chunkCoords, DType dataType, ArrayOrder order)
    {
        var chunkValues = dataType.CreateFilledArray(_chunkShape.Aggregate(1, (current, value) => current * value));
        var localCoords = new int[_chunkShape.Length];
        var shardCoords = new int[shardShape.Count];

        for (var flatIndex = 0; flatIndex < chunkValues.Length; flatIndex++)
        {
            FromFlatIndex(flatIndex, _chunkShape, order, localCoords);
            for (var dimension = 0; dimension < shardCoords.Length; dimension++)
            {
                shardCoords[dimension] = (chunkCoords[dimension] * _chunkShape[dimension]) + localCoords[dimension];
            }

            var shardFlatIndex = ToFlatIndex(shardCoords, shardShape, order);
            chunkValues.SetValue(shardValues.GetValue(shardFlatIndex), flatIndex);
        }

        return chunkValues;
    }

    private void WriteInnerChunk(Array shardValues, IReadOnlyList<int> shardShape, IReadOnlyList<int> chunkCoords, Array chunkValues, ArrayOrder order)
    {
        var localCoords = new int[_chunkShape.Length];
        var shardCoords = new int[shardShape.Count];

        for (var flatIndex = 0; flatIndex < chunkValues.Length; flatIndex++)
        {
            FromFlatIndex(flatIndex, _chunkShape, order, localCoords);
            for (var dimension = 0; dimension < shardCoords.Length; dimension++)
            {
                shardCoords[dimension] = (chunkCoords[dimension] * _chunkShape[dimension]) + localCoords[dimension];
            }

            var shardFlatIndex = ToFlatIndex(shardCoords, shardShape, order);
            shardValues.SetValue(chunkValues.GetValue(flatIndex), shardFlatIndex);
        }
    }

    private int[] ValidateShardShape(IReadOnlyList<int>? chunkShape)
    {
        if (chunkShape is null)
        {
            throw new MetadataException("The sharding codec requires the outer shard shape at runtime.");
        }

        var shardShape = chunkShape.ToArray();
        if (shardShape.Length != _chunkShape.Length)
        {
            throw new MetadataException("The sharding codec chunk rank must match the shard rank.");
        }

        for (var dimension = 0; dimension < shardShape.Length; dimension++)
        {
            if (shardShape[dimension] % _chunkShape[dimension] != 0)
            {
                throw new MetadataException($"The shard shape [{string.Join(", ", shardShape)}] must be divisible by the sharding inner chunk shape [{string.Join(", ", _chunkShape)}].");
            }
        }

        return shardShape;
    }

    private int[] GetChunksPerShard(IReadOnlyList<int> shardShape)
    {
        var result = new int[shardShape.Count];
        for (var dimension = 0; dimension < shardShape.Count; dimension++)
        {
            result[dimension] = shardShape[dimension] / _chunkShape[dimension];
        }

        return result;
    }

    private static int GetChunkIndex(IReadOnlyList<int> chunksPerShard, IReadOnlyList<int> chunkCoords)
    {
        if (chunkCoords.Count != chunksPerShard.Count)
        {
            throw new MetadataException("The sharding codec chunk coordinate rank must match the shard rank.");
        }

        var result = 0;
        for (var dimension = 0; dimension < chunkCoords.Count; dimension++)
        {
            if (chunkCoords[dimension] < 0 || chunkCoords[dimension] >= chunksPerShard[dimension])
            {
                throw new MetadataException($"The sharding codec chunk coordinate [{string.Join(", ", chunkCoords)}] is outside the shard grid [{string.Join(", ", chunksPerShard)}].");
            }

            result *= chunksPerShard[dimension];
            result += chunkCoords[dimension];
        }

        return result;
    }

    private static IEnumerable<int[]> EnumerateCoordinates(IReadOnlyList<int> shape)
    {
        var current = new int[shape.Count];
        while (true)
        {
            yield return current.ToArray();

            var dimension = shape.Count - 1;
            while (dimension >= 0)
            {
                current[dimension]++;
                if (current[dimension] < shape[dimension])
                {
                    break;
                }

                current[dimension] = 0;
                dimension--;
            }

            if (dimension < 0)
            {
                yield break;
            }
        }
    }

    private static void ValidateStages(IChunkCodec codec, string parameterName)
    {
        _ = codec ?? throw new ArgumentNullException(parameterName);
        _ = new ChunkCodecPipeline(codec);
    }

    private static void ValidateIndexStages(IChunkCodec codec, string parameterName)
    {
        _ = codec ?? throw new ArgumentNullException(parameterName);
        var runtimeStages = ChunkCodecFactory.SplitForRuntime(codec, ZarrFormat.V3, DType.UInt64);
        if (runtimeStages.ArrayArrayCodecs.Count > 0)
        {
            throw new ArgumentException("Sharding index_codecs cannot include array-array codecs.", parameterName);
        }

        if (runtimeStages.ArrayBytesCodec is null)
        {
            throw new ArgumentException("Sharding index_codecs must include exactly one array-bytes codec.", parameterName);
        }

        if (!ChunkCodecFactory.TryComputeEncodedSize(codec, ZarrFormat.V3, DType.UInt64, 16, out _))
        {
            throw new ArgumentException("Sharding index_codecs must have a deterministic encoded size.", parameterName);
        }
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
