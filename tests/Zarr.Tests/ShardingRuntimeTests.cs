namespace Zarr.Tests;

[Trait("TestBasis", "DotNetImplementation")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_sharding.py")]
public sealed class ShardingRuntimeTests
{
    [Fact]
    public void ScalarReadsOnShardedArraysDecodeOnlyTheTouchedInnerChunk()
    {
        var store = new MemoryStore();
        var countingCodec = new CountingPassthroughChunkCodec();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4, 4 },
            new[] { 4, 4 },
            "matrix",
            ZarrFormat.V3,
            chunkCodec: new ChunkCodecPipeline(
                new ShardingChunkCodec(new[] { 2, 2 }, codecs: countingCodec),
                new Crc32cChunkCodec()));

        array.Write(new[,]
        {
            { 0, 1, 2, 3 },
            { 4, 5, 6, 7 },
            { 8, 9, 10, 11 },
            { 12, 13, 14, 15 },
        });

        countingCodec.Reset();
        var value = array.GetValue<int>(3, 2);

        Assert.Equal(14, value);
        Assert.Equal(1, countingCodec.DecodeCalls);
        Assert.Equal(0, countingCodec.EncodeCalls);
    }

    [Fact]
    public void ScalarWritesOnShardedArraysReencodeOnlyTheTouchedInnerChunk()
    {
        var store = new MemoryStore();
        var countingCodec = new CountingPassthroughChunkCodec();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4, 4 },
            new[] { 4, 4 },
            "matrix",
            ZarrFormat.V3,
            chunkCodec: new ChunkCodecPipeline(
                new ShardingChunkCodec(new[] { 2, 2 }, codecs: countingCodec),
                new Crc32cChunkCodec()));

        array.Write(new[,]
        {
            { 0, 1, 2, 3 },
            { 4, 5, 6, 7 },
            { 8, 9, 10, 11 },
            { 12, 13, 14, 15 },
        });

        countingCodec.Reset();
        array.SetValue(99, 1, 1);

        Assert.Equal(1, countingCodec.DecodeCalls);
        Assert.Equal(1, countingCodec.EncodeCalls);

        countingCodec.Reset();
        Assert.Equal(99, array.GetValue<int>(1, 1));
        Assert.Equal(1, countingCodec.DecodeCalls);
    }

    private sealed class CountingPassthroughChunkCodec : IChunkCodec, IBytesBytesCodec
    {
        public int EncodeCalls { get; private set; }

        public int DecodeCalls { get; private set; }

        public string Id => "counting";

        public ChunkCodecStage Stage => ChunkCodecStage.BytesBytes;

        public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
        {
            EncodeCalls++;
            return input.ToArray();
        }

        public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
        {
            DecodeCalls++;
            return input.ToArray();
        }

        public Dictionary<string, object?> ToMetadata()
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = Id,
            };
        }

        public void Reset()
        {
            EncodeCalls = 0;
            DecodeCalls = 0;
        }
    }
}
