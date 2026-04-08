namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_v2.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_v3.py")]
public sealed class MetadataBehaviorTests
{
    [Fact]
    public void StorageTransformersMustDeclareANameAndDictionaryConfiguration()
    {
        var store = new MemoryStore();

        var missingName = Assert.Throws<MetadataException>(() => ZarrApi.CreateArray<int>(
            store,
            new[] { 2 },
            new[] { 2 },
            "missing-name",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                StorageTransformers = new[]
                {
                    (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                    },
                },
            }));

        var malformedConfiguration = Assert.Throws<MetadataException>(() => ZarrApi.CreateArray<int>(
            store,
            new[] { 2 },
            new[] { 2 },
            "bad-config",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                StorageTransformers = new[]
                {
                    (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["name"] = "demo-transformer",
                        ["configuration"] = "not-a-dictionary",
                    },
                },
            }));

        Assert.Contains("name", missingName.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("configuration", malformedConfiguration.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArraysWithStorageTransformersRejectDataAccess()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 2 },
            new[] { 2 },
            "vector",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                StorageTransformers = new[]
                {
                    (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["name"] = "demo-transformer",
                        ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                    },
                },
            });

        Assert.Throws<NotSupportedException>(() => array.GetValue<int>(0));
        Assert.Throws<NotSupportedException>(() => array.WriteFlat(new[] { 1, 2 }));
    }

    [Fact]
    public void V3GroupExtraFieldsRoundTripAndPersistAcrossAttributeUpdates()
    {
        var store = new MemoryStore();
        store.Set("zarr.json", System.Text.Encoding.UTF8.GetBytes(@"
{
  ""zarr_format"": 3,
  ""node_type"": ""group"",
  ""attributes"": {
    ""owner"": ""chem""
  },
  ""demo_extension"": {
    ""must_understand"": false,
    ""kind"": ""lab""
  }
}"));

        var group = ZarrApi.OpenGroup(store, "", ZarrFormat.V3);
        group.Attrs["status"] = "active";

        var payload = store.Get("zarr.json");
        Assert.NotNull(payload);

        using var document = System.Text.Json.JsonDocument.Parse(payload.RequiredArray());
        Assert.True(group.ExtraFields.ContainsKey("demo_extension"));
        Assert.Equal("lab", group.ExtraFields["demo_extension"]["kind"]);
        Assert.Equal("active", document.RootElement.GetProperty("attributes").GetProperty("status").GetString());
        Assert.False(document.RootElement.GetProperty("demo_extension").GetProperty("must_understand").GetBoolean());
        Assert.Equal("lab", document.RootElement.GetProperty("demo_extension").GetProperty("kind").GetString());
    }

    [Fact]
    public void MigrateV2ToV3PreservesHierarchyDataAndAttributes()
    {
        var sourceStore = new MemoryStore();
        var sourceRoot = ZarrApi.Group(sourceStore, format: ZarrFormat.V2);
        var sourceGroup = sourceRoot.CreateGroup("experiments", attributes: new Dictionary<string, object?> { ["owner"] = "chem" });
        var sourceArray = sourceGroup.CreateArray<int>(
            "values",
            new[] { 4 },
            new[] { 2 },
            attributes: new Dictionary<string, object?> { ["kind"] = "signal" });
        sourceArray.WriteFlat(new[] { 10, 20, 30, 40 });

        var destinationStore = new MemoryStore();
        var migrated = ZarrApi.MigrateV2ToV3(sourceStore, destinationStore, sourcePath: "", destinationPath: "", overwrite: true);

        var migratedGroup = Assert.IsType<ZarrGroup>(migrated);
        var reopenedGroup = ZarrApi.OpenGroup(destinationStore, "experiments", ZarrFormat.V3);
        var reopenedArray = ZarrApi.OpenArray(destinationStore, "experiments/values", ZarrFormat.V3);

        Assert.Equal(ZarrFormat.V3, migratedGroup.Format);
        Assert.Equal("chem", reopenedGroup.Attrs["owner"]);
        Assert.Equal("signal", reopenedArray.Attrs["kind"]);
        Assert.Equal(new[] { 10, 20, 30, 40 }, reopenedArray.ReadFlat<int>());
        Assert.Equal("v2", reopenedArray.ChunkKeyEncoding.Name);
        Assert.NotNull(destinationStore.Get("experiments/values/zarr.json"));
    }

    [Fact]
    public void NamedChunkGridWithChunkShapeSupportsRuntimeDataAccess()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            new[] { 2 },
            "custom",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                ChunkGrid = new NamedChunkGrid(
                    "custom-grid",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["chunk_shape"] = new[] { 2 },
                        ["layout"] = "demo",
                    }),
            });

        array.WriteFlat(new[] { 1, 2, 3, 4 });
        var reopened = ZarrApi.OpenArray(store, "custom", ZarrFormat.V3);

        Assert.Equal("custom-grid", reopened.ChunkGrid.Name);
        Assert.Equal(new[] { 1, 2, 3, 4 }, reopened.ReadFlat<int>());
    }

    [Fact]
    public void UnknownV3ChunkGridWithoutChunkShapeCanBeOpenedForMetadataAccess()
    {
        var store = new MemoryStore();
        store.Set("custom/zarr.json", System.Text.Encoding.UTF8.GetBytes(@"
{
  ""zarr_format"": 3,
  ""node_type"": ""array"",
  ""shape"": [4],
  ""data_type"": ""int32"",
  ""chunk_grid"": {
    ""name"": ""custom-grid"",
    ""configuration"": {
      ""layout"": ""demo""
    }
  },
  ""chunk_key_encoding"": {
    ""name"": ""default"",
    ""configuration"": {
      ""separator"": ""/""
    }
  },
  ""fill_value"": 0,
  ""attributes"": {},
  ""order"": ""C"",
  ""codecs"": [
    {
      ""name"": ""bytes"",
      ""configuration"": {
        ""endian"": ""little""
      }
    }
  ]
}"));

        var array = ZarrApi.OpenArray(store, "custom", ZarrFormat.V3);

        Assert.Equal("custom-grid", array.ChunkGrid.Name);
        Assert.Throws<NotSupportedException>(() => array.ReadFlat<int>());
    }

    [Fact]
    public void MigrateV3ToV2PreservesCompatibleHierarchyDataAndAttributes()
    {
        var sourceStore = new MemoryStore();
        var sourceRoot = ZarrApi.Group(sourceStore, format: ZarrFormat.V3);
        var sourceGroup = sourceRoot.CreateGroup("runs", attributes: new Dictionary<string, object?> { ["owner"] = "chem" });
        var sourceArray = sourceGroup.CreateArray<int>(
            "values",
            new[] { 4 },
            new[] { 2 },
            attributes: new Dictionary<string, object?> { ["kind"] = "signal" },
            chunkCodec: new GzipChunkCodec(level: 1));
        sourceArray.WriteFlat(new[] { 10, 20, 30, 40 });

        var destinationStore = new MemoryStore();
        var migrated = ZarrApi.MigrateV3ToV2(sourceStore, destinationStore, sourcePath: "", destinationPath: "", overwrite: true);

        var migratedGroup = Assert.IsType<ZarrGroup>(migrated);
        var reopenedGroup = ZarrApi.OpenGroup(destinationStore, "runs", ZarrFormat.V2);
        var reopenedArray = ZarrApi.OpenArray(destinationStore, "runs/values", ZarrFormat.V2);

        Assert.Equal(ZarrFormat.V2, migratedGroup.Format);
        Assert.Equal("chem", reopenedGroup.Attrs["owner"]);
        Assert.Equal("signal", reopenedArray.Attrs["kind"]);
        Assert.Equal(new[] { 10, 20, 30, 40 }, reopenedArray.ReadFlat<int>());
        Assert.NotNull(destinationStore.Get("runs/values/.zarray"));
    }

    [Fact]
    public void MigrateV3ToV2RejectsV3OnlyArrayMetadata()
    {
        var sourceStore = new MemoryStore();
        var sourceArray = ZarrApi.CreateArray<int>(
            sourceStore,
            new[] { 4 },
            new[] { 2 },
            "signals",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                ChunkGrid = new NamedChunkGrid(
                    "custom-grid",
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["chunk_shape"] = new[] { 2 },
                    }),
                DimensionNames = new[] { "sample" },
                ExtraFields = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal)
                {
                    ["demo_extension"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["must_understand"] = false,
                    },
                },
            });
        sourceArray.WriteFlat(new[] { 10, 20, 30, 40 });

        var destinationStore = new MemoryStore();
        var exception = Assert.Throws<MetadataException>(() => ZarrApi.MigrateV3ToV2(sourceStore, destinationStore));

        Assert.Contains("chunk_grid", exception.Message, StringComparison.Ordinal);
        Assert.Contains("dimension_names", exception.Message, StringComparison.Ordinal);
        Assert.Contains("extra_fields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrateV3ToV2RejectsStorageTransformersBeforeDataRead()
    {
        var sourceStore = new MemoryStore();
        _ = ZarrApi.CreateArray<int>(
            sourceStore,
            new[] { 2 },
            new[] { 2 },
            "signals",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                StorageTransformers = new[]
                {
                    (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["name"] = "demo-transformer",
                        ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal),
                    },
                },
            });

        var destinationStore = new MemoryStore();
        var exception = Assert.Throws<MetadataException>(() => ZarrApi.MigrateV3ToV2(sourceStore, destinationStore));

        Assert.Contains("storage_transformers", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrateV3ToV2RejectsGroupExtraFields()
    {
        var sourceStore = new MemoryStore();
        sourceStore.Set("zarr.json", System.Text.Encoding.UTF8.GetBytes(@"
{
  ""zarr_format"": 3,
  ""node_type"": ""group"",
  ""attributes"": {},
  ""demo_extension"": {
    ""must_understand"": false,
    ""kind"": ""lab""
  }
}"));

        var destinationStore = new MemoryStore();
        var exception = Assert.Throws<MetadataException>(() => ZarrApi.MigrateV3ToV2(sourceStore, destinationStore));

        Assert.Contains("extra_fields", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsolidatedMetadataMustDeclareMustUnderstandFalse()
    {
        var store = new MemoryStore();
        store.Set("zarr.json", System.Text.Encoding.UTF8.GetBytes(@"
{
  ""zarr_format"": 3,
  ""node_type"": ""group"",
  ""attributes"": {},
  ""consolidated_metadata"": {
    ""kind"": ""inline"",
    ""must_understand"": true,
    ""metadata"": {}
  }
}"));

        var exception = Assert.Throws<MetadataException>(() => ZarrApi.OpenGroup(store, "", ZarrFormat.V3));

        Assert.Contains("must_understand", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
