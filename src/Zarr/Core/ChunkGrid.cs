namespace Zarr.Core;

public abstract class ChunkGrid : IChunkGrid
{
    protected ChunkGrid(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public virtual bool SupportsDataAccess => false;

    public abstract int[] ChunkShape { get; }

    public abstract int[] GridShape(int[] arrayShape);

    public abstract IEnumerable<int[]> AllChunkCoordinates(int[] arrayShape);

    public abstract Dictionary<string, object?> ToMetadata();
}

public sealed class RegularChunkGrid : ChunkGrid
{
    private readonly int[] _chunkShape;

    public RegularChunkGrid(IEnumerable<int> chunkShape)
        : base("regular")
    {
        _chunkShape = chunkShape?.ToArray() ?? throw new ArgumentNullException(nameof(chunkShape));
        if (_chunkShape.Length == 0)
        {
            throw new ArgumentException("chunkShape must contain at least one dimension.", nameof(chunkShape));
        }

        if (_chunkShape.Any(length => length <= 0))
        {
            throw new ArgumentException("chunkShape values must be positive.", nameof(chunkShape));
        }
    }

    public override int[] ChunkShape => _chunkShape.ToArray();

    public override bool SupportsDataAccess => true;

    public override int[] GridShape(int[] arrayShape)
    {
        var normalizedShape = arrayShape?.ToArray() ?? throw new ArgumentNullException(nameof(arrayShape));
        if (normalizedShape.Length != _chunkShape.Length)
        {
            throw new ArgumentException("arrayShape must have the same rank as chunkShape.", nameof(arrayShape));
        }

        return normalizedShape
            .Select((length, dimension) => (length + _chunkShape[dimension] - 1) / _chunkShape[dimension])
            .ToArray();
    }

    public override IEnumerable<int[]> AllChunkCoordinates(int[] arrayShape)
    {
        var gridShape = GridShape(arrayShape);
        var current = new int[gridShape.Length];

        while (true)
        {
            yield return current.ToArray();

            var dimension = gridShape.Length - 1;
            while (dimension >= 0)
            {
                current[dimension]++;
                if (current[dimension] < gridShape[dimension])
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

    public override Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Name,
            ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["chunk_shape"] = ChunkShape,
            },
        };
    }
}

public sealed class NamedChunkGrid : ChunkGrid
{
    private readonly Dictionary<string, object?> _configuration;
    private readonly int[]? _chunkShape;

    public NamedChunkGrid(string name, IReadOnlyDictionary<string, object?>? configuration = null)
        : base(string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Chunk grid name must be non-empty.", nameof(name)) : name)
    {
        _configuration = JsonUtilities.CloneDictionary(configuration);
        _chunkShape = TryParseChunkShape(_configuration);
    }

    public IReadOnlyDictionary<string, object?> Configuration => JsonUtilities.CloneDictionary(_configuration);

    public override int[] ChunkShape => _chunkShape?.ToArray()
        ?? throw new NotSupportedException($"Chunk grid '{Name}' does not expose a regular chunk shape.");

    public override bool SupportsDataAccess => _chunkShape is not null;

    public override int[] GridShape(int[] arrayShape)
    {
        if (_chunkShape is null)
        {
            throw new NotSupportedException($"Chunk grid '{Name}' does not support grid-shape calculation in this implementation.");
        }

        return ComputeGridShape(arrayShape, _chunkShape);
    }

    public override IEnumerable<int[]> AllChunkCoordinates(int[] arrayShape)
    {
        if (_chunkShape is null)
        {
            throw new NotSupportedException($"Chunk grid '{Name}' does not support chunk enumeration in this implementation.");
        }

        return EnumerateChunkCoordinates(arrayShape, _chunkShape);
    }

    public override Dictionary<string, object?> ToMetadata()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = Name,
            ["configuration"] = JsonUtilities.CloneDictionary(_configuration),
        };
    }

    private static int[]? TryParseChunkShape(IReadOnlyDictionary<string, object?> configuration)
    {
        if (!configuration.TryGetValue("chunk_shape", out var rawChunkShape) ||
            rawChunkShape is null)
        {
            return null;
        }

        try
        {
            return JsonUtilities.ToIntArray(rawChunkShape, "chunk_grid.configuration.chunk_shape");
        }
        catch (MetadataException)
        {
            return null;
        }
    }

    private static int[] ComputeGridShape(int[] arrayShape, int[] chunkShape)
    {
        var normalizedShape = arrayShape?.ToArray() ?? throw new ArgumentNullException(nameof(arrayShape));
        if (normalizedShape.Length != chunkShape.Length)
        {
            throw new ArgumentException("arrayShape must have the same rank as chunkShape.", nameof(arrayShape));
        }

        return normalizedShape
            .Select((length, dimension) => (length + chunkShape[dimension] - 1) / chunkShape[dimension])
            .ToArray();
    }

    private static IEnumerable<int[]> EnumerateChunkCoordinates(int[] arrayShape, int[] chunkShape)
    {
        var gridShape = ComputeGridShape(arrayShape, chunkShape);
        var current = new int[gridShape.Length];

        while (true)
        {
            yield return current.ToArray();

            var dimension = gridShape.Length - 1;
            while (dimension >= 0)
            {
                current[dimension]++;
                if (current[dimension] < gridShape[dimension])
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
}

internal static class ChunkGridFactory
{
    public static ChunkGrid FromMetadata(object? value)
    {
        var chunkGrid = JsonUtilities.ToDictionary(value, "chunk_grid");
        var name = JsonUtilities.ConvertValue<string>(chunkGrid.TryGetValue("name", out var rawName) ? rawName : null) ?? "regular";
        var configuration = JsonUtilities.ToDictionary(
            chunkGrid.TryGetValue("configuration", out var rawConfiguration) ? rawConfiguration : null,
            "chunk_grid.configuration");

        return ChunkGridRegistry.TryCreate(name, configuration, out var registered)
            ? registered
            : new NamedChunkGrid(name, configuration);
    }
}
