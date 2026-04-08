namespace Zarr.Core.Config;

public static class ZarrConfiguration
{
    public static ZarrFormat DefaultFormat
    {
        get => Configuration.ZarrConfiguration.Current.DefaultFormat;
        set => Configuration.ZarrConfiguration.Current.DefaultFormat = value;
    }

    public static ArrayOrder DefaultArrayOrder
    {
        get => Configuration.ZarrConfiguration.Current.Array.Order;
        set => Configuration.ZarrConfiguration.Current.Array.Order = value;
    }

    public static bool WriteEmptyChunks
    {
        get => Configuration.ZarrConfiguration.Current.Array.WriteEmptyChunks;
        set => Configuration.ZarrConfiguration.Current.Array.WriteEmptyChunks = value;
    }

    public static bool ReadMissingChunks
    {
        get => Configuration.ZarrConfiguration.Current.Array.ReadMissingChunks;
        set => Configuration.ZarrConfiguration.Current.Array.ReadMissingChunks = value;
    }

    public static bool PreferConsolidatedMetadata
    {
        get => Configuration.ZarrConfiguration.Current.Metadata.PreferConsolidatedMetadata;
        set => Configuration.ZarrConfiguration.Current.Metadata.PreferConsolidatedMetadata = value;
    }

    public static bool UseConsolidatedMetadataForChildren
    {
        get => Configuration.ZarrConfiguration.Current.Metadata.UseConsolidatedMetadataForChildren;
        set => Configuration.ZarrConfiguration.Current.Metadata.UseConsolidatedMetadataForChildren = value;
    }

    public static string MetadataPolicyName
    {
        get => Configuration.ZarrConfiguration.Current.Metadata.PolicyName;
        set => Configuration.ZarrConfiguration.Current.Metadata.PolicyName = value;
    }

    public static bool PreferReadOnlyWrapping
    {
        get => Configuration.ZarrConfiguration.Current.Storage.PreferReadOnlyWrapping;
        set => Configuration.ZarrConfiguration.Current.Storage.PreferReadOnlyWrapping = value;
    }

    public static string BufferPrototypeName
    {
        get => Configuration.ZarrConfiguration.Current.Registry.BufferPrototypeName;
        set => Configuration.ZarrConfiguration.Current.Registry.BufferPrototypeName = value;
    }
}
