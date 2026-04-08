using System.Text;

namespace Zarr.Core;

public sealed class GroupInfo
{
    public GroupInfo(string name, ZarrFormat format, string storeType, int memberCount, int arrayCount, int groupCount)
    {
        Name = name;
        Format = format;
        StoreType = storeType;
        MemberCount = memberCount;
        ArrayCount = arrayCount;
        GroupCount = groupCount;
    }

    public string Name { get; }

    public ZarrFormat Format { get; }

    public string StoreType { get; }

    public int MemberCount { get; }

    public int ArrayCount { get; }

    public int GroupCount { get; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Name        : {Name}");
        builder.AppendLine("Type        : Group");
        builder.AppendLine($"Zarr format : {Format}");
        builder.AppendLine($"Store type  : {StoreType}");
        builder.AppendLine($"No. members : {MemberCount}");
        builder.AppendLine($"No. arrays  : {ArrayCount}");
        builder.Append($"No. groups  : {GroupCount}");
        return builder.ToString();
    }
}

public sealed class ArrayInfo
{
    public ArrayInfo(
        ZarrFormat format,
        string dataType,
        object fillValue,
        IReadOnlyList<int> shape,
        IReadOnlyList<int>? chunkShape,
        ArrayOrder order,
        string storeType,
        IReadOnlyList<string> codecs,
        long? logicalBytes,
        long storedBytes,
        int initializedChunkCount)
    {
        Format = format;
        DataType = dataType;
        FillValue = fillValue;
        Shape = shape.ToArray();
        ChunkShape = chunkShape?.ToArray();
        Order = order;
        StoreType = storeType;
        Codecs = codecs.ToArray();
        LogicalBytes = logicalBytes;
        StoredBytes = storedBytes;
        InitializedChunkCount = initializedChunkCount;
    }

    public ZarrFormat Format { get; }

    public string DataType { get; }

    public object FillValue { get; }

    public int[] Shape { get; }

    public int[]? ChunkShape { get; }

    public ArrayOrder Order { get; }

    public string StoreType { get; }

    public string[] Codecs { get; }

    public long? LogicalBytes { get; }

    public long StoredBytes { get; }

    public int InitializedChunkCount { get; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Type               : Array");
        builder.AppendLine($"Zarr format        : {Format}");
        builder.AppendLine($"Data type          : {DataType}");
        builder.AppendLine($"Fill value         : {FillValue}");
        builder.AppendLine($"Shape              : {FormatShape(Shape)}");
        builder.AppendLine($"Chunk shape        : {(ChunkShape is null ? "<variable>" : FormatShape(ChunkShape))}");
        builder.AppendLine($"Order              : {Order}");
        builder.AppendLine($"Store type         : {StoreType}");
        builder.AppendLine($"Codecs             : {(Codecs.Length == 0 ? "<none>" : string.Join(", ", Codecs))}");

        if (LogicalBytes.HasValue)
        {
            builder.AppendLine($"No. bytes          : {FormatByteSize(LogicalBytes.Value)}");
        }

        builder.AppendLine($"No. bytes stored   : {FormatByteSize(StoredBytes)}");
        builder.Append($"Chunks Initialized : {InitializedChunkCount}");
        return builder.ToString();
    }

    private static string FormatShape(IReadOnlyList<int> shape)
    {
        return "(" + string.Join(", ", shape) + ")";
    }

    private static string FormatByteSize(long size)
    {
        if (size < 1024)
        {
            return size.ToString();
        }

        var suffixes = new[] { "K", "M", "G", "T", "P" };
        double current = size;
        var suffixIndex = -1;
        while (current >= 1024d && suffixIndex < suffixes.Length - 1)
        {
            current /= 1024d;
            suffixIndex++;
        }

        return $"{size} ({current:0.0}{suffixes[suffixIndex]})";
    }
}
