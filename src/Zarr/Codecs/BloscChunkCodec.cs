using Zarr.Codecs.Blosc;

namespace Zarr.Codecs;

public enum BloscCompressor
{
    BloscLz,
    Lz4,
    Lz4Hc,
    Snappy,
    Zlib,
    Zstd,
}

public enum BloscShuffle
{
    NoShuffle = 0,
    Shuffle = 1,
    BitShuffle = 2,
}

public sealed class BloscChunkCodec : IChunkCodec, IBytesBytesCodec
{
    public BloscChunkCodec(
        BloscCompressor compressor = BloscCompressor.Zstd,
        int level = 5,
        BloscShuffle shuffle = BloscShuffle.BitShuffle,
        int blockSize = 0,
        int typeSize = 1)
    {
        if (level < 0 || level > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(level), "The blosc codec level must be between 0 and 9.");
        }

        if (blockSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockSize), "The blosc codec block size must be non-negative.");
        }

        if (typeSize <= 0 || typeSize > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(typeSize), "The blosc codec type size must be between 1 and 255.");
        }

        Compressor = compressor;
        Level = level;
        Shuffle = shuffle;
        BlockSize = blockSize;
        TypeSize = typeSize;
    }

    public BloscCompressor Compressor { get; }

    public int Level { get; }

    public BloscShuffle Shuffle { get; }

    public int BlockSize { get; }

    public int TypeSize { get; }

    public string Id => "blosc";

    public ChunkCodecStage Stage => ChunkCodecStage.BytesBytes;

    public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        try
        {
            return BloscNative.Compress(input.Span, MapCompressor(Compressor), Level, (int)Shuffle, TypeSize, BlockSize);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new MetadataException(exception.Message, exception);
        }
    }

    public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
    {
        try
        {
            return BloscNative.Decompress(input.Span);
        }
        catch (Exception exception) when (exception is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new MetadataException(exception.Message, exception);
        }
    }

    public Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = Id,
            ["cname"] = ToMetadataName(Compressor),
            ["clevel"] = Level,
            ["shuffle"] = (int)Shuffle,
            ["blocksize"] = BlockSize,
            ["typesize"] = TypeSize,
        };
    }

    internal static BloscChunkCodec FromMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        return new BloscChunkCodec(
            ParseCompressor(JsonUtilities.ConvertValue<string>(metadata.TryGetValue("cname", out var cname) ? cname : "zstd")),
            JsonUtilities.ConvertValue<int>(metadata.TryGetValue("clevel", out var level) ? level : 5),
            ParseShuffle(metadata.TryGetValue("shuffle", out var shuffle) ? shuffle : 2),
            JsonUtilities.ConvertValue<int>(metadata.TryGetValue("blocksize", out var blockSize) ? blockSize : 0),
            JsonUtilities.ConvertValue<int>(metadata.TryGetValue("typesize", out var typeSize) ? typeSize : 1));
    }

    private static string MapCompressor(BloscCompressor compressor)
    {
        return compressor switch
        {
            BloscCompressor.BloscLz => "blosclz",
            BloscCompressor.Lz4 => "lz4",
            BloscCompressor.Lz4Hc => "lz4hc",
            BloscCompressor.Snappy => "snappy",
            BloscCompressor.Zlib => "zlib",
            BloscCompressor.Zstd => "zstd",
            _ => throw new MetadataException($"Unsupported Blosc compressor '{compressor}'."),
        };
    }

    private static string ToMetadataName(BloscCompressor compressor)
    {
        return compressor switch
        {
            BloscCompressor.BloscLz => "blosclz",
            BloscCompressor.Lz4 => "lz4",
            BloscCompressor.Lz4Hc => "lz4hc",
            BloscCompressor.Snappy => "snappy",
            BloscCompressor.Zlib => "zlib",
            BloscCompressor.Zstd => "zstd",
            _ => throw new MetadataException($"Unsupported Blosc compressor '{compressor}'."),
        };
    }

    private static BloscCompressor ParseCompressor(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "blosclz" => BloscCompressor.BloscLz,
            "lz4" => BloscCompressor.Lz4,
            "lz4hc" => BloscCompressor.Lz4Hc,
            "snappy" => BloscCompressor.Snappy,
            "zlib" => BloscCompressor.Zlib,
            "zstd" or null => BloscCompressor.Zstd,
            _ => throw new MetadataException($"Unsupported Blosc compressor '{value}'."),
        };
    }

    private static BloscShuffle ParseShuffle(object? value)
    {
        return value switch
        {
            null => BloscShuffle.BitShuffle,
            string text => text.ToLowerInvariant() switch
            {
                "noshuffle" => BloscShuffle.NoShuffle,
                "shuffle" => BloscShuffle.Shuffle,
                "bitshuffle" => BloscShuffle.BitShuffle,
                _ => throw new MetadataException($"Unsupported Blosc shuffle '{text}'."),
            },
            _ => JsonUtilities.ConvertValue<int>(value) switch
            {
                0 => BloscShuffle.NoShuffle,
                1 => BloscShuffle.Shuffle,
                2 => BloscShuffle.BitShuffle,
                var raw => throw new MetadataException($"Unsupported Blosc shuffle '{raw}'."),
            },
        };
    }
}
