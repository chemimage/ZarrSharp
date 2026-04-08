namespace Zarr.Errors;

public class ZarrException : Exception
{
    public ZarrException(string message)
        : base(message)
    {
    }

    public ZarrException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ContainsArrayException : ZarrException
{
    public ContainsArrayException(string path)
        : base($"An array already exists at '{path}'.")
    {
    }
}

public sealed class ContainsGroupException : ZarrException
{
    public ContainsGroupException(string path)
        : base($"A group already exists at '{path}'.")
    {
    }
}

public sealed class NodeNotFoundException : ZarrException
{
    public NodeNotFoundException(string path)
        : base($"No Zarr node was found at '{path}'.")
    {
    }
}

public sealed class ChunkNotFoundException : ZarrException
{
    public ChunkNotFoundException(string path)
        : base($"No chunk payload was found at '{path}'.")
    {
    }
}

public sealed class MetadataException : ZarrException
{
    public MetadataException(string message)
        : base(message)
    {
    }

    public MetadataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
