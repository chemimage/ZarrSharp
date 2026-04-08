namespace Zarr.Api;

public readonly struct CopySummary
{
    public CopySummary(int nodesCopied, int keysCopied, long bytesCopied)
    {
        NodesCopied = nodesCopied;
        KeysCopied = keysCopied;
        BytesCopied = bytesCopied;
    }

    public int NodesCopied { get; }

    public int KeysCopied { get; }

    public long BytesCopied { get; }

    public CopySummary Add(CopySummary other)
    {
        return new CopySummary(
            NodesCopied + other.NodesCopied,
            KeysCopied + other.KeysCopied,
            BytesCopied + other.BytesCopied);
    }
}
