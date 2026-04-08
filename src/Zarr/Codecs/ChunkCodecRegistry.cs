namespace Zarr.Codecs;

public static class ChunkCodecRegistry
{
    public static IReadOnlyList<string> RegisteredIds => ChunkCodecFactory.RegisteredIds;

    public static void Register(string id, Func<IReadOnlyDictionary<string, object?>, IChunkCodec> factory)
    {
        ChunkCodecFactory.Register(id, factory);
    }
}
