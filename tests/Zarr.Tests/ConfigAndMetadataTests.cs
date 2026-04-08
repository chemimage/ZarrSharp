using System.Text;
using System.Text.Json;

namespace Zarr.Tests;

public sealed class ConfigAndMetadataTests
{
    [Fact]
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_config.py")]
    public void ReadMissingChunksFalseThrowsForMissingChunk()
    {
        using var scope = new ConfigurationScope();
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<int>(store, new[] { 4, 4 }, chunks: new[] { 2, 2 }, fillValue: 42);

        ZarrConfiguration.ReadMissingChunks = false;
        var array = ZarrApi.OpenArray(store);

        Assert.Throws<ChunkNotFoundException>(() => array.Read());
    }

    [Fact]
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_config.py")]
    public void ReadMissingChunksFalseOnlyThrowsForMissingShard()
    {
        using var scope = new ConfigurationScope();
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 8, 4 },
            chunks: new[] { 4, 4 },
            fillValue: 42,
            chunkCodec: new ShardingChunkCodec(new[] { 2, 2 }));

        array.WriteSelection((Array)new int[,] { { 1, 1 }, { 1, 1 } }, ArraySelection.Slice(0, 2), ArraySelection.Slice(0, 2));

        ZarrConfiguration.ReadMissingChunks = false;
        var reopened = ZarrApi.OpenArray(store);

        var presentShard = (int[,])reopened.GetSelection<int>(ArraySelection.Slice(0, 4), ArraySelection.Slice(0, 4));
        var expected = new int[4, 4];
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                expected[row, column] = 42;
            }
        }

        expected[0, 0] = 1;
        expected[0, 1] = 1;
        expected[1, 0] = 1;
        expected[1, 1] = 1;
        AssertMatrixEqual(expected, presentShard);

        Assert.Throws<ChunkNotFoundException>(() => reopened.GetSelection(ArraySelection.Slice(4, 8), ArraySelection.Slice(0, 4)));
    }

    [Fact]
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_sharding.py")]
    public void PartialSelectionWriteAndReadRoundTripsThroughShards()
    {
        var store = new MemoryStore();
        var data = new int[16, 16];
        var value = 0;
        for (var row = 0; row < 16; row++)
        {
            for (var column = 0; column < 16; column++)
            {
                data[row, column] = value++;
            }
        }

        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 40, 40 },
            chunks: new[] { 20, 20 },
            fillValue: 1,
            chunkCodec: new ShardingChunkCodec(new[] { 10, 10 }));

        array.WriteSelection((Array)data, ArraySelection.Slice(0, 16), ArraySelection.Slice(0, 16));

        var actual = (int[,])array.GetSelection<int>(ArraySelection.Slice(0, 16), ArraySelection.Slice(0, 16));
        AssertMatrixEqual(data, actual);
    }

    [Fact]
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_consolidated.py")]
    public void ReconsolidationIgnoresStaleChildConsolidatedMetadata()
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store);
        var child = root.CreateGroup("child");
        child.CreateArray<int>("a", new[] { 4 }, chunks: new[] { 4 });

        _ = ZarrApi.ConsolidateMetadata(store);

        var mutableRoot = ZarrApi.OpenGroup(store, useConsolidated: false);
        var mutableChild = mutableRoot.OpenGroup("child");
        mutableChild.CreateArray<int>("b", new[] { 4 }, chunks: new[] { 4 });

        var reconsolidated = ZarrApi.ConsolidateMetadata(store);
        var reopenedChild = (ZarrGroup)reconsolidated["child"];

        Assert.True(reopenedChild.ContainsItem("a"));
        Assert.True(reopenedChild.ContainsItem("b"));
    }

    [Fact]
    [Trait("TestBasis", "DotNetImplementation")]
    public void DefaultFormatAndOrderComeFromConfiguration()
    {
        using var scope = new ConfigurationScope();
        var store = new MemoryStore();

        ZarrConfiguration.DefaultFormat = ZarrFormat.V2;
        ZarrConfiguration.DefaultArrayOrder = ArrayOrder.F;

        var group = ZarrApi.CreateGroup(store);
        var array = ZarrApi.CreateArray<int>(store, new[] { 4 }, chunks: new[] { 4 }, path: "array");

        Assert.Equal(ZarrFormat.V2, group.Format);
        Assert.Equal(ZarrFormat.V2, array.Format);
        Assert.Equal(ArrayOrder.F, array.Order);
    }

    [Fact]
    [Trait("TestBasis", "DotNetImplementation")]
    public void JsonIndentConfigurationControlsMetadataPrettyPrinting()
    {
        using var scope = new ConfigurationScope();
        var store = new MemoryStore();

        global::Zarr.Configuration.ZarrConfiguration.Current.Json.Indent = 0;
        _ = ZarrApi.CreateGroup(store);
        var compactJson = Encoding.UTF8.GetString(store.Get("zarr.json")!.Value.ToArray());
        Assert.DoesNotContain(Environment.NewLine, compactJson);

        global::Zarr.Configuration.ZarrConfiguration.Current.Json.Indent = 2;
        _ = ZarrApi.CreateGroup(store, "pretty", overwrite: true);
        var indentedJson = Encoding.UTF8.GetString(store.Get("pretty/zarr.json")!.Value.ToArray());
        Assert.Contains(Environment.NewLine, indentedJson);
    }

    [Fact]
    [Trait("TestBasis", "DotNetImplementation")]
    public void ReadOnlyWrappingPreferenceControlsReadModeMutability()
    {
        using var scope = new ConfigurationScope();
        var store = new MemoryStore();
        _ = ZarrApi.CreateGroup(store);

        ZarrConfiguration.PreferReadOnlyWrapping = true;
        var readOnly = ZarrApi.OpenGroup(store, string.Empty, ZarrOpenMode.Read);
        Assert.Throws<InvalidOperationException>(() => readOnly.UpdateAttributes(new Dictionary<string, object?> { ["a"] = 1 }));

        ZarrConfiguration.PreferReadOnlyWrapping = false;
        var writable = ZarrApi.OpenGroup(store, string.Empty, ZarrOpenMode.Read);
        writable.UpdateAttributes(new Dictionary<string, object?> { ["a"] = 1 });
        Assert.Equal(1, ZarrApi.OpenGroup(store).Attrs["a"]);
    }

    [Fact]
    [Trait("TestBasis", "DotNetImplementation")]
    public void RegisteredExtensionsAndStorageTransformersExposeTypedViews()
    {
        using var scope = new ConfigurationScope();
        MetadataExtensionRegistry.Register(new TestArrayExtensionProvider());
        MetadataExtensionRegistry.Register(new TestGroupExtensionProvider());
        StorageTransformerRegistry.Register("tests.storage", configuration => new TestStorageTransformer(configuration));

        var store = new MemoryStore();
        store.Set("group/zarr.json", JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["zarr_format"] = 3,
            ["node_type"] = "group",
            ["attributes"] = new Dictionary<string, object?>(),
            ["tests.group_ext"] = new Dictionary<string, object?>
            {
                ["must_understand"] = false,
                ["configuration"] = new Dictionary<string, object?> { ["value"] = "group" },
            },
        }));
        var group = ZarrApi.OpenGroup(store, "group");

        var array = group.CreateArray<int>(
            "values",
            new[] { 4 },
            chunks: new[] { 4 },
            v3Metadata: new V3ArrayMetadataOptions
            {
                StorageTransformers = new[]
                {
                    (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
                    {
                        ["name"] = "tests.storage",
                        ["configuration"] = new Dictionary<string, object?> { ["level"] = 3 },
                    },
                },
                ExtraFields = new Dictionary<string, IReadOnlyDictionary<string, object?>>
                {
                    ["tests.array_ext"] = new Dictionary<string, object?> { ["must_understand"] = false, ["configuration"] = new Dictionary<string, object?> { ["value"] = "array" } },
                },
            });

        Assert.Equal("group", ((TestMetadataExtension)group.Extensions["tests.group_ext"]).Value);
        Assert.Equal("array", ((TestMetadataExtension)array.Extensions["tests.array_ext"]).Value);
        Assert.Single(array.ParsedStorageTransformers);
        Assert.Equal(3, ((TestStorageTransformer)array.ParsedStorageTransformers[0]).Level);
    }

    [Fact]
    [Trait("TestBasis", "DotNetImplementation")]
    public void MetadataPolicySelectionUsesConfiguredRegistryName()
    {
        using var scope = new ConfigurationScope();
        MetadataPolicyRegistry.Register(new RejectingMetadataPolicy());
        ZarrConfiguration.MetadataPolicyName = "tests.rejecting";

        var store = new MemoryStore();
        var error = Assert.Throws<MetadataException>(() => ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            chunks: new[] { 4 },
            v3Metadata: new V3ArrayMetadataOptions
            {
                ExtraFields = new Dictionary<string, IReadOnlyDictionary<string, object?>>
                {
                    ["tests.reject_me"] = new Dictionary<string, object?> { ["must_understand"] = false },
                },
            }));
        Assert.Contains("tests.reject_me", error.Message);
    }

    [Fact]
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_v3.py")]
    public void UnknownRequiredExtensionIsRejected()
    {
        var store = new MemoryStore();
        Assert.Throws<MetadataException>(() => ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            chunks: new[] { 4 },
            v3Metadata: new V3ArrayMetadataOptions
            {
                ExtraFields = new Dictionary<string, IReadOnlyDictionary<string, object?>>
                {
                    ["tests.required"] = new Dictionary<string, object?> { ["must_understand"] = true },
                },
            }));
    }

    private sealed class ConfigurationScope : IDisposable
    {
        private readonly ZarrFormat _defaultFormat = ZarrConfiguration.DefaultFormat;
        private readonly ArrayOrder _defaultOrder = ZarrConfiguration.DefaultArrayOrder;
        private readonly bool _writeEmptyChunks = ZarrConfiguration.WriteEmptyChunks;
        private readonly bool _readMissingChunks = ZarrConfiguration.ReadMissingChunks;
        private readonly bool _preferConsolidated = ZarrConfiguration.PreferConsolidatedMetadata;
        private readonly bool _preferConsolidatedChildren = ZarrConfiguration.UseConsolidatedMetadataForChildren;
        private readonly string _metadataPolicyName = ZarrConfiguration.MetadataPolicyName;
        private readonly bool _preferReadOnlyWrapping = ZarrConfiguration.PreferReadOnlyWrapping;
        private readonly string _bufferPrototypeName = ZarrConfiguration.BufferPrototypeName;
        private readonly int _jsonIndent = global::Zarr.Configuration.ZarrConfiguration.Current.Json.Indent;

        public void Dispose()
        {
            ZarrConfiguration.DefaultFormat = _defaultFormat;
            ZarrConfiguration.DefaultArrayOrder = _defaultOrder;
            ZarrConfiguration.WriteEmptyChunks = _writeEmptyChunks;
            ZarrConfiguration.ReadMissingChunks = _readMissingChunks;
            ZarrConfiguration.PreferConsolidatedMetadata = _preferConsolidated;
            ZarrConfiguration.UseConsolidatedMetadataForChildren = _preferConsolidatedChildren;
            ZarrConfiguration.MetadataPolicyName = _metadataPolicyName;
            ZarrConfiguration.PreferReadOnlyWrapping = _preferReadOnlyWrapping;
            ZarrConfiguration.BufferPrototypeName = _bufferPrototypeName;
            global::Zarr.Configuration.ZarrConfiguration.Current.Json.Indent = _jsonIndent;
        }
    }

    private sealed class TestMetadataExtension : IMetadataExtension
    {
        public TestMetadataExtension(string name, MetadataExtensionNodeKind nodeKind, string value, bool mustUnderstand)
        {
            Name = name;
            NodeKind = nodeKind;
            Value = value;
            MustUnderstand = mustUnderstand;
            Configuration = new Dictionary<string, object?> { ["value"] = value };
        }

        public string Name { get; }

        public bool MustUnderstand { get; }

        public MetadataExtensionNodeKind NodeKind { get; }

        public string Value { get; }

        public IReadOnlyDictionary<string, object?> Configuration { get; }

        public Dictionary<string, object?> ToMetadata()
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["must_understand"] = MustUnderstand,
                ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["value"] = Value,
                },
            };
        }
    }

    private sealed class TestArrayExtensionProvider : IMetadataExtensionProvider
    {
        public string Name => "tests.array_ext";

        public MetadataExtensionNodeKind NodeKind => MetadataExtensionNodeKind.Array;

        public IMetadataExtension Parse(IReadOnlyDictionary<string, object?> metadata)
        {
            var configuration = (IReadOnlyDictionary<string, object?>)(metadata.TryGetValue("configuration", out var rawConfiguration) && rawConfiguration is IReadOnlyDictionary<string, object?> readOnlyConfiguration
                ? readOnlyConfiguration
                : new Dictionary<string, object?>());
            return new TestMetadataExtension(
                Name,
                NodeKind,
                Convert.ToString(configuration["value"]) ?? string.Empty,
                Convert.ToBoolean(metadata["must_understand"]));
        }
    }

    private sealed class TestGroupExtensionProvider : IMetadataExtensionProvider
    {
        public string Name => "tests.group_ext";

        public MetadataExtensionNodeKind NodeKind => MetadataExtensionNodeKind.Group;

        public IMetadataExtension Parse(IReadOnlyDictionary<string, object?> metadata)
        {
            var configuration = (IReadOnlyDictionary<string, object?>)(metadata.TryGetValue("configuration", out var rawConfiguration) && rawConfiguration is IReadOnlyDictionary<string, object?> readOnlyConfiguration
                ? readOnlyConfiguration
                : new Dictionary<string, object?>());
            return new TestMetadataExtension(
                Name,
                NodeKind,
                Convert.ToString(configuration["value"]) ?? string.Empty,
                Convert.ToBoolean(metadata["must_understand"]));
        }
    }

    private sealed class TestStorageTransformer : IStorageTransformer
    {
        public TestStorageTransformer(IReadOnlyDictionary<string, object?> configuration)
        {
            Configuration = new Dictionary<string, object?>(configuration, StringComparer.Ordinal);
            Level = Convert.ToInt32(configuration["level"]);
        }

        public string Name => "tests.storage";

        public int Level { get; }

        public IReadOnlyDictionary<string, object?> Configuration { get; }

        public Dictionary<string, object?> ToMetadata()
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = Name,
                ["configuration"] = new Dictionary<string, object?>(Configuration, StringComparer.Ordinal),
            };
        }
    }

    private sealed class RejectingMetadataPolicy : IMetadataPolicy
    {
        public string Name => "tests.rejecting";

        public void ValidateExtension(string name, MetadataExtensionNodeKind nodeKind, IReadOnlyDictionary<string, object?> metadata)
        {
            throw new MetadataException($"Rejected extension '{name}'.");
        }

        public void ValidateStorageTransformer(IReadOnlyDictionary<string, object?> metadata)
        {
        }

        public void ValidateConsolidatedMetadataEnvelope(IReadOnlyDictionary<string, object?> metadata, string path)
        {
        }
    }

    private static void AssertMatrixEqual<T>(T[,] expected, T[,] actual)
    {
        Assert.Equal(expected.GetLength(0), actual.GetLength(0));
        Assert.Equal(expected.GetLength(1), actual.GetLength(1));
        for (var row = 0; row < expected.GetLength(0); row++)
        {
            for (var column = 0; column < expected.GetLength(1); column++)
            {
                Assert.Equal(expected[row, column], actual[row, column]);
            }
        }
    }
}
