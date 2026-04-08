namespace Zarr.Registry;

public static class ChunkGridRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, Func<IReadOnlyDictionary<string, object?>, ChunkGrid>> Factories = new(StringComparer.Ordinal);

    static ChunkGridRegistry()
    {
        Register(
            "regular",
            configuration => new RegularChunkGrid(JsonUtilities.ToIntArray(
                configuration.TryGetValue("chunk_shape", out var chunkShape) ? chunkShape : null,
                "chunk_grid.configuration.chunk_shape")));
    }

    public static IReadOnlyList<string> RegisteredNames
    {
        get
        {
            lock (Gate)
            {
                return Factories.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            }
        }
    }

    public static void Register(string name, Func<IReadOnlyDictionary<string, object?>, ChunkGrid> factory)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Chunk grid names must be non-empty.", nameof(name));
        }

        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        lock (Gate)
        {
            Factories[name] = factory;
        }
    }

    public static bool TryCreate(string name, IReadOnlyDictionary<string, object?> configuration, out ChunkGrid chunkGrid)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            chunkGrid = null!;
            return false;
        }

        lock (Gate)
        {
            if (Factories.TryGetValue(name, out var factory))
            {
                chunkGrid = factory(configuration);
                return true;
            }
        }

        chunkGrid = null!;
        return false;
    }
}
