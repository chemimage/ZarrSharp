using Zarr.Buffer;

namespace Zarr.Configuration;

public sealed class ZarrOptions
{
    public ZarrFormat DefaultFormat { get; set; } = ZarrFormat.V3;

    public ArrayDefaults Array { get; } = new();

    public AsyncDefaults Async { get; } = new();

    public JsonDefaults Json { get; } = new();

    public MetadataDefaults Metadata { get; } = new();

    public RegistryDefaults Registry { get; } = new();

    public StorageDefaults Storage { get; } = new();
}

public sealed class ArrayDefaults
{
    public ArrayOrder Order { get; set; } = ArrayOrder.C;

    public bool WriteEmptyChunks { get; set; }

    public bool ReadMissingChunks { get; set; } = true;
}

public sealed class AsyncDefaults
{
    public int Concurrency { get; set; } = 10;

    public TimeSpan? Timeout { get; set; }
}

public sealed class JsonDefaults
{
    public int Indent { get; set; } = 2;
}

public sealed class MetadataDefaults
{
    public bool PreferConsolidatedMetadata { get; set; }

    public bool UseConsolidatedMetadataForChildren { get; set; } = true;

    public string PolicyName { get; set; } = "strict_reference";
}

public sealed class RegistryDefaults
{
    public string BufferPrototypeName { get; set; } = "shared";
}

public sealed class StorageDefaults
{
    public bool PreferReadOnlyWrapping { get; set; } = true;
}

public static class ZarrConfiguration
{
    public static ZarrOptions Current { get; } = new();
}
