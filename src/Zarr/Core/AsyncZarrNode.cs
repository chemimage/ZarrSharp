namespace Zarr.Core;

public abstract class AsyncZarrNode
{
    protected AsyncZarrNode(ZarrNode node)
    {
        Node = node;
    }

    protected ZarrNode Node { get; }

    public string Path => Node.Path;

    public string Name => Node.Name;

    public string Basename => Node.Basename;

    public ZarrFormat Format => Node.Format;

    public Attributes Attrs => Node.Attrs;

    public Task UpdateAttributesAsync(IReadOnlyDictionary<string, object?> values)
    {
        Node.UpdateAttributes(values);
        return Task.CompletedTask;
    }
}
