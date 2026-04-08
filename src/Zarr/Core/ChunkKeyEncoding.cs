namespace Zarr.Core;

public abstract class ChunkKeyEncoding
{
    protected ChunkKeyEncoding(string name, string separator)
    {
        Name = name;
        Separator = ValidateSeparator(separator);
    }

    public string Name { get; }

    public string Separator { get; }

    public abstract string EncodeChunkKey(IReadOnlyList<int> chunkCoords);

    public abstract int[] DecodeChunkKey(string chunkKey);

    public virtual Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Name,
            ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["separator"] = Separator,
            },
        };
    }

    internal static ChunkKeyEncoding CreateDefaultFor(ZarrFormat format)
    {
        return format switch
        {
            ZarrFormat.V2 => new V2ChunkKeyEncoding(),
            ZarrFormat.V3 => new DefaultChunkKeyEncoding(),
            _ => throw new MetadataException($"Unsupported Zarr format '{format}'."),
        };
    }

    internal static string ValidateSeparator(string separator)
    {
        return separator switch
        {
            "." or "/" => separator,
            _ => throw new ArgumentOutOfRangeException(nameof(separator), separator, "Chunk key separator must be '.' or '/'."),
        };
    }
}

public sealed class DefaultChunkKeyEncoding : ChunkKeyEncoding
{
    public DefaultChunkKeyEncoding(string separator = "/")
        : base("default", separator)
    {
    }

    public override string EncodeChunkKey(IReadOnlyList<int> chunkCoords)
    {
        return chunkCoords.Count == 0
            ? "c"
            : string.Join(Separator, new[] { "c" }.Concat(chunkCoords.Select(value => value.ToString())));
    }

    public override int[] DecodeChunkKey(string chunkKey)
    {
        if (string.Equals(chunkKey, "c", StringComparison.Ordinal))
        {
            return Array.Empty<int>();
        }

        var prefix = "c" + Separator;
        if (!chunkKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new MetadataException($"Chunk key '{chunkKey}' is not valid for default chunk key encoding.");
        }

        return chunkKey[prefix.Length..]
            .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
    }
}

public sealed class V2ChunkKeyEncoding : ChunkKeyEncoding
{
    public V2ChunkKeyEncoding(string separator = ".")
        : base("v2", separator)
    {
    }

    public override string EncodeChunkKey(IReadOnlyList<int> chunkCoords)
    {
        if (chunkCoords.Count == 0)
        {
            return "0";
        }

        return string.Join(Separator, chunkCoords);
    }

    public override int[] DecodeChunkKey(string chunkKey)
    {
        if (string.Equals(chunkKey, "0", StringComparison.Ordinal))
        {
            return Array.Empty<int>();
        }

        return chunkKey
            .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.Parse(value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
    }
}

internal static class ChunkKeyEncodingFactory
{
    public static ChunkKeyEncoding FromV2Metadata(object? dimensionSeparator)
    {
        var separator = JsonUtilities.ConvertValue<string>(dimensionSeparator) ?? ".";
        return new V2ChunkKeyEncoding(separator);
    }

    public static ChunkKeyEncoding FromV3Metadata(object? metadata)
    {
        if (metadata is null)
        {
            return new DefaultChunkKeyEncoding();
        }

        if (metadata is string name)
        {
            return Create(name, null);
        }

        var dictionary = JsonUtilities.ToDictionary(metadata, "chunk_key_encoding");
        var encodingName = JsonUtilities.ConvertValue<string>(dictionary.TryGetValue("name", out var rawName) ? rawName : null)
            ?? "default";

        string? separator = null;
        if (dictionary.TryGetValue("configuration", out var configuration))
        {
            var config = JsonUtilities.ToDictionary(configuration, "chunk_key_encoding.configuration");
            separator = JsonUtilities.ConvertValue<string>(config.TryGetValue("separator", out var rawSeparator) ? rawSeparator : null);
        }
        else if (dictionary.TryGetValue("separator", out var rawSeparator))
        {
            separator = JsonUtilities.ConvertValue<string>(rawSeparator);
        }

        return Create(encodingName, separator);
    }

    private static ChunkKeyEncoding Create(string name, string? separator)
    {
        return name switch
        {
            "default" => new DefaultChunkKeyEncoding(separator ?? "/"),
            "v2" => new V2ChunkKeyEncoding(separator ?? "."),
            _ => throw new MetadataException($"Unsupported chunk key encoding '{name}'."),
        };
    }
}
