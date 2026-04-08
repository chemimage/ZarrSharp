namespace Zarr.Types;

public enum ZarrNodeType
{
    None,
    Group,
    Array,
}

public readonly struct ZarrNodeDetection
{
    public ZarrNodeDetection(ZarrNodeType nodeType, ZarrFormat? format)
    {
        NodeType = nodeType;
        Format = format;
    }

    public ZarrNodeType NodeType { get; }

    public ZarrFormat? Format { get; }
}
