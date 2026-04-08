namespace Zarr.Types;

public sealed class CustomDTypeRuntime
{
    public Func<object> DefaultValueFactory { get; set; } = null!;

    public Func<object?, object> Normalize { get; set; } = null!;

    public Func<Array, ReadOnlyMemory<byte>> Serialize { get; set; } = null!;

    public Func<ReadOnlyMemory<byte>, int, Array> Deserialize { get; set; } = null!;

    public Func<ZarrFormat, object>? MetadataFactory { get; set; }

    public Func<object?, object?>? SerializeFillValue { get; set; }

    public Func<object?, object?>? DeserializeFillValue { get; set; }

    public Func<object?, Type, object?>? ConvertForRead { get; set; }

    public Func<int, object?, Array>? CreateFilledArray { get; set; }

    public Type? PreferredReadType { get; set; }
}
