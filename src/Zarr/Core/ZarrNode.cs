namespace Zarr.Core;

public abstract class ZarrNode
{
    protected ZarrNode(IStore store, string path, ZarrFormat format, Dictionary<string, object?> attributes)
    {
        Store = store;
        Path = PathUtilities.Normalize(path);
        Format = format;
        AttributeValues = attributes;
        Attrs = new Attributes(AttributeValues, PersistMetadata);
    }

    protected IStore Store { get; }

    protected Dictionary<string, object?> AttributeValues { get; }

    public string Path { get; }

    public string Name => "/" + Path;

    public string Basename => PathUtilities.Basename(Path);

    public ZarrFormat Format { get; }

    public Attributes Attrs { get; }

    public void UpdateAttributes(IReadOnlyDictionary<string, object?> values)
    {
        Attrs.Update(values);
    }

    internal IStore StoreForAsync()
    {
        return Store;
    }

    protected abstract void PersistMetadata();
}
