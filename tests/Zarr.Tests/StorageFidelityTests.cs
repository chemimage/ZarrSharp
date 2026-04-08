using System.Text.Json;

namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_v2.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_v3.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codec_pipeline.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_gzip.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_zstd.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_blosc.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_crc32c.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_endian.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_transpose.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_sharding.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_codecs/test_vlen.py")]
public sealed class StorageFidelityTests
{
    [Fact]
    public void V2MetadataIncludesOrderDimensionSeparatorAndCompressorFields()
    {
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<double>(
            store,
            new[] { 3 },
            new[] { 3 },
            "foo",
            ZarrFormat.V2,
            order: ArrayOrder.F,
            chunkCodec: new GzipChunkCodec(level: 1));

        var payload = store.Get("foo/.zarray");
        Assert.NotNull(payload);

        using var document = JsonDocument.Parse(payload.RequiredArray());
        var root = document.RootElement;

        Assert.Equal("F", root.GetProperty("order").GetString());
        Assert.Equal(".", root.GetProperty("dimension_separator").GetString());
        Assert.Equal("gzip", root.GetProperty("compressor").GetProperty("id").GetString());
        Assert.Equal(1, root.GetProperty("compressor").GetProperty("level").GetInt32());
    }

    [Fact]
    public void V2ChunkKeyEncodingSupportsSlashSeparatedChunkPaths()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 2, 2 },
            new[] { 2, 2 },
            "matrix",
            ZarrFormat.V2,
            chunkKeyEncoding: new V2ChunkKeyEncoding("/"));

        array.Write(new[,] { { 1, 2 }, { 3, 4 } });
        var reopened = ZarrApi.OpenArray(store, "matrix", ZarrFormat.V2);

        Assert.NotNull(store.Get("matrix/0/0"));
        Assert.Null(store.Get("matrix/0.0"));
        Assert.Equal("v2", reopened.ChunkKeyEncoding.Name);
        Assert.Equal("/", reopened.ChunkKeyEncoding.Separator);
        Assert.Equal(new[] { 1, 2, 3, 4 }, reopened.ReadFlat<int>());
    }

    [Fact]
    public void V3MetadataIncludesChunkGridChunkKeyEncodingAndCodecs()
    {
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            new[] { 2 },
            "vector",
            ZarrFormat.V3,
            chunkCodec: new GzipChunkCodec(level: 1));

        var payload = store.Get("vector/zarr.json");
        Assert.NotNull(payload);

        using var document = JsonDocument.Parse(payload.RequiredArray());
        var root = document.RootElement;

        Assert.Equal("array", root.GetProperty("node_type").GetString());
        Assert.Equal("regular", root.GetProperty("chunk_grid").GetProperty("name").GetString());
        Assert.Equal(2, root.GetProperty("chunk_grid").GetProperty("configuration").GetProperty("chunk_shape")[0].GetInt32());
        Assert.Equal("default", root.GetProperty("chunk_key_encoding").GetProperty("name").GetString());
        Assert.Equal("/", root.GetProperty("chunk_key_encoding").GetProperty("configuration").GetProperty("separator").GetString());
        Assert.Equal("bytes", root.GetProperty("codecs")[0].GetProperty("name").GetString());
        Assert.Equal("gzip", root.GetProperty("codecs")[1].GetProperty("name").GetString());
        Assert.Equal(1, root.GetProperty("codecs")[1].GetProperty("configuration").GetProperty("level").GetInt32());
    }

    [Fact]
    public void V3ChunkKeyEncodingCanUseV2StyleKeys()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 2, 2 },
            new[] { 2, 2 },
            "matrix",
            ZarrFormat.V3,
            chunkKeyEncoding: new V2ChunkKeyEncoding("."));

        array.Write(new[,] { { 1, 2 }, { 3, 4 } });
        var reopened = ZarrApi.OpenArray(store, "matrix", ZarrFormat.V3);

        Assert.NotNull(store.Get("matrix/0.0"));
        Assert.Null(store.Get("matrix/c/0/0"));
        Assert.Equal("v2", reopened.ChunkKeyEncoding.Name);
        Assert.Equal(".", reopened.ChunkKeyEncoding.Separator);
        Assert.Equal(new[] { 1, 2, 3, 4 }, reopened.ReadFlat<int>());
    }

    [Fact]
    public void V2CodecPipelinePersistsFiltersAndCompressor()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            new[] { 4 },
            "vector",
            ZarrFormat.V2,
            chunkCodec: new ChunkCodecPipeline(new GzipChunkCodec(level: 1), new GzipChunkCodec(level: 9)));

        array.WriteFlat(new[] { 1, 2, 3, 4 });
        var reopened = ZarrApi.OpenArray(store, "vector", ZarrFormat.V2);
        var payload = store.Get("vector/.zarray");
        Assert.NotNull(payload);

        using var document = JsonDocument.Parse(payload.RequiredArray());
        var root = document.RootElement;

        Assert.Equal(new[] { 1, 2, 3, 4 }, reopened.ReadFlat<int>());
        Assert.IsType<ChunkCodecPipeline>(reopened.ChunkCodec);
        Assert.Equal(2, reopened.ChunkCodecs.Count);
        Assert.Equal("gzip", root.GetProperty("compressor").GetProperty("id").GetString());
        Assert.Equal(9, root.GetProperty("compressor").GetProperty("level").GetInt32());
        Assert.Equal(1, root.GetProperty("filters").GetArrayLength());
        Assert.Equal("gzip", root.GetProperty("filters")[0].GetProperty("id").GetString());
        Assert.Equal(1, root.GetProperty("filters")[0].GetProperty("level").GetInt32());
    }

    [Fact]
    public void V3CodecPipelinePersistsOrderedCodecs()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            new[] { 4 },
            "vector",
            ZarrFormat.V3,
            chunkCodec: new ChunkCodecPipeline(new GzipChunkCodec(level: 1), new GzipChunkCodec(level: 9)));

        array.WriteFlat(new[] { 1, 2, 3, 4 });
        var reopened = ZarrApi.OpenArray(store, "vector", ZarrFormat.V3);
        var payload = store.Get("vector/zarr.json");
        Assert.NotNull(payload);

        using var document = JsonDocument.Parse(payload.RequiredArray());
        var root = document.RootElement;

        Assert.Equal(new[] { 1, 2, 3, 4 }, reopened.ReadFlat<int>());
        Assert.IsType<ChunkCodecPipeline>(reopened.ChunkCodec);
        Assert.Equal(3, reopened.ChunkCodecs.Count);
        Assert.Equal(3, root.GetProperty("codecs").GetArrayLength());
        Assert.Equal("bytes", root.GetProperty("codecs")[0].GetProperty("name").GetString());
        Assert.Equal(1, root.GetProperty("codecs")[1].GetProperty("configuration").GetProperty("level").GetInt32());
        Assert.Equal(9, root.GetProperty("codecs")[2].GetProperty("configuration").GetProperty("level").GetInt32());
    }

    [Fact]
    public void V3MetadataPersistsDimensionNamesStorageTransformersAndExtraFields()
    {
        var store = new MemoryStore();
        var options = new V3ArrayMetadataOptions
        {
            ChunkGrid = new RegularChunkGrid(new[] { 2, 2 }),
            DimensionNames = new string?[] { "row", null },
            StorageTransformers = new[]
            {
                (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = "demo-transformer",
                    ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["scale"] = 2,
                    },
                },
            },
            ExtraFields = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal)
            {
                ["demo_extension"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["must_understand"] = false,
                    ["note"] = "kept",
                },
            },
        };

        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4, 4 },
            chunks: null,
            path: "matrix",
            format: ZarrFormat.V3,
            v3Metadata: options);

        array.Attrs["owner"] = "test";
        var reopened = ZarrApi.OpenArray(store, "matrix", ZarrFormat.V3);
        var payload = store.Get("matrix/zarr.json");
        Assert.NotNull(payload);

        using var document = JsonDocument.Parse(payload.RequiredArray());
        var root = document.RootElement;

        Assert.IsType<RegularChunkGrid>(reopened.ChunkGrid);
        Assert.Equal(new[] { 2, 2 }, reopened.ChunkGrid.ChunkShape);
        Assert.Equal(new string?[] { "row", null }, reopened.DimensionNames);
        Assert.Single(reopened.StorageTransformers);
        Assert.Single(reopened.ExtraFields);
        Assert.True(reopened.ExtraFields.ContainsKey("demo_extension"));
        Assert.Equal("regular", root.GetProperty("chunk_grid").GetProperty("name").GetString());
        Assert.Equal(2, root.GetProperty("chunk_grid").GetProperty("configuration").GetProperty("chunk_shape")[0].GetInt32());
        Assert.Equal("row", root.GetProperty("dimension_names")[0].GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("dimension_names")[1].ValueKind);
        Assert.Equal("demo-transformer", root.GetProperty("storage_transformers")[0].GetProperty("name").GetString());
        Assert.Equal(2, root.GetProperty("storage_transformers")[0].GetProperty("configuration").GetProperty("scale").GetInt32());
        Assert.False(root.GetProperty("demo_extension").GetProperty("must_understand").GetBoolean());
        Assert.Equal("kept", root.GetProperty("demo_extension").GetProperty("note").GetString());
        Assert.Equal("test", root.GetProperty("attributes").GetProperty("owner").GetString());
    }

    [Fact]
    public void V3DimensionNamesMustMatchShapeRank()
    {
        var store = new MemoryStore();

        var exception = Assert.Throws<MetadataException>(() => ZarrApi.CreateArray<int>(
            store,
            new[] { 2, 2 },
            new[] { 2, 2 },
            "matrix",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                DimensionNames = new string?[] { "only-one" },
            }));

        Assert.Contains("dimension_names", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void V3ExtraFieldsMustDeclareMustUnderstandFalse()
    {
        var store = new MemoryStore();

        var exception = Assert.Throws<MetadataException>(() => ZarrApi.CreateArray<int>(
            store,
            new[] { 2 },
            new[] { 2 },
            "vector",
            ZarrFormat.V3,
            v3Metadata: new V3ArrayMetadataOptions
            {
                ExtraFields = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal)
                {
                    ["broken_extension"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["must_understand"] = true,
                    },
                },
            }));

        Assert.Contains("must_understand", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void UInt16RoundTripWorksWithGzip(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var data = Enumerable.Range(0, 256).Select(value => (ushort)value).ToArray();
        var array = ZarrApi.CreateArray<ushort>(
            context.Store,
            new[] { 256 },
            new[] { 256 },
            "numbers",
            format,
            chunkCodec: new GzipChunkCodec(level: 1));

        array.WriteFlat(data);
        var reopened = ZarrApi.OpenArray(context.Store, "numbers", format);

        Assert.Equal(data, reopened.ReadFlat<ushort>());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void GzipCodecCompressesChunkPayloadAndPreservesData(ZarrFormat format)
    {
        var store = new MemoryStore();
        var data = Enumerable.Repeat((ushort)7, 1024).ToArray();
        var array = ZarrApi.CreateArray<ushort>(
            store,
            new[] { 1024 },
            new[] { 1024 },
            "compressed",
            format,
            chunkCodec: new GzipChunkCodec(level: 1));

        array.WriteFlat(data);
        var key = format == ZarrFormat.V2 ? "compressed/0" : "compressed/c/0";
        var payload = store.Get(key);

        Assert.NotNull(payload);
        Assert.True(payload.Required().Length < data.Length * sizeof(ushort));
        Assert.Equal(data, array.ReadFlat<ushort>());
    }

    [Fact]
    public void V3BytesCodecCanPersistBigEndianNumericChunks()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<ushort>(
            store,
            new[] { 2 },
            new[] { 2 },
            "numbers",
            ZarrFormat.V3,
            chunkCodec: new ChunkCodecPipeline(new BytesChunkCodec(ByteEndian.Big), new Crc32cChunkCodec()));

        array.WriteFlat(new ushort[] { 0x0102, 0x0304 });
        var payload = store.Get("numbers/c/0");
        Assert.NotNull(payload);

        Assert.Equal(8, payload.Required().Length);
        Assert.Equal(0x01, payload.Required().Span[0]);
        Assert.Equal(0x02, payload.Required().Span[1]);
        Assert.Equal(0x03, payload.Required().Span[2]);
        Assert.Equal(0x04, payload.Required().Span[3]);
        Assert.Equal(new ushort[] { 0x0102, 0x0304 }, array.ReadFlat<ushort>());
    }

    [Fact]
    public void V3TransposeCodecPreservesLogicalValuesAndTransposesStoredChunkLayout()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 2, 3 },
            new[] { 2, 3 },
            "matrix",
            ZarrFormat.V3,
            chunkCodec: new TransposeChunkCodec(new[] { 1, 0 }));

        array.Write(new[,] { { 1, 2, 3 }, { 4, 5, 6 } });
        var reopened = ZarrApi.OpenArray(store, "matrix", ZarrFormat.V3);
        var payload = store.Get("matrix/c/0/0");
        var metadata = store.Get("matrix/zarr.json");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);

        var observed = new int[6];
        var payloadBytes = payload.RequiredArray();
        System.Buffer.BlockCopy(payloadBytes, 0, observed, 0, payloadBytes.Length);

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;

        Assert.Equal(new[] { 1, 4, 2, 5, 3, 6 }, observed);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, reopened.ReadFlat<int>());
        Assert.Equal(2, reopened.ChunkCodecs.Count);
        Assert.IsType<TransposeChunkCodec>(reopened.ChunkCodecs[0]);
        Assert.IsType<BytesChunkCodec>(reopened.ChunkCodecs[1]);
        Assert.Equal("transpose", root.GetProperty("codecs")[0].GetProperty("name").GetString());
        Assert.Equal(1, root.GetProperty("codecs")[0].GetProperty("configuration").GetProperty("order")[0].GetInt32());
        Assert.Equal(0, root.GetProperty("codecs")[0].GetProperty("configuration").GetProperty("order")[1].GetInt32());
        Assert.Equal("bytes", root.GetProperty("codecs")[1].GetProperty("name").GetString());
    }

    [Fact]
    public void PublicCodecRegistryCanReopenArraysUsingCustomRegisteredCodecs()
    {
        const string CodecId = "reverse-bytes-test";
        ChunkCodecRegistry.Register(CodecId, _ => new ReverseBytesTestCodec());

        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            new[] { 4 },
            "vector",
            ZarrFormat.V3,
            chunkCodec: new ReverseBytesTestCodec());

        array.WriteFlat(new[] { 1, 2, 3, 4 });
        var reopened = ZarrApi.OpenArray(store, "vector", ZarrFormat.V3);
        var metadata = store.Get("vector/zarr.json");

        Assert.Contains(CodecId, ChunkCodecRegistry.RegisteredIds);
        Assert.Equal(new[] { 1, 2, 3, 4 }, reopened.ReadFlat<int>());

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.Equal("bytes", root.GetProperty("codecs")[0].GetProperty("name").GetString());
        Assert.Equal(CodecId, root.GetProperty("codecs")[1].GetProperty("name").GetString());
    }

    [Fact]
    public void V2BloscCodecRoundTripsAndPersistsCompressorMetadata()
    {
        var store = new MemoryStore();
        var data = Enumerable.Repeat(17, 128).ToArray();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 128 },
            new[] { 128 },
            "numbers",
            ZarrFormat.V2,
            chunkCodec: new BloscChunkCodec(
                compressor: BloscCompressor.Zstd,
                level: 5,
                shuffle: BloscShuffle.Shuffle,
                typeSize: sizeof(int)));

        array.WriteFlat(data);
        var reopened = ZarrApi.OpenArray(store, "numbers", ZarrFormat.V2);
        var payload = store.Get("numbers/0");
        var metadata = store.Get("numbers/.zarray");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);
        Assert.True(payload.Required().Length < data.Length * sizeof(int));
        Assert.Equal(data, reopened.ReadFlat<int>());

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.IsType<BloscChunkCodec>(reopened.ChunkCodec);
        Assert.Equal("blosc", root.GetProperty("compressor").GetProperty("id").GetString());
        Assert.Equal("zstd", root.GetProperty("compressor").GetProperty("cname").GetString());
        Assert.Equal(5, root.GetProperty("compressor").GetProperty("clevel").GetInt32());
        Assert.Equal(1, root.GetProperty("compressor").GetProperty("shuffle").GetInt32());
        Assert.Equal(sizeof(int), root.GetProperty("compressor").GetProperty("typesize").GetInt32());
    }

    [Fact]
    public void V3BloscCodecRoundTripsAndPersistsCodecMetadata()
    {
        var store = new MemoryStore();
        var data = Enumerable.Range(0, 128).ToArray();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 128 },
            new[] { 128 },
            "numbers",
            ZarrFormat.V3,
            chunkCodec: new BloscChunkCodec(
                compressor: BloscCompressor.Lz4,
                level: 7,
                shuffle: BloscShuffle.BitShuffle,
                typeSize: sizeof(int)));

        array.WriteFlat(data);
        var reopened = ZarrApi.OpenArray(store, "numbers", ZarrFormat.V3);
        var payload = store.Get("numbers/c/0");
        var metadata = store.Get("numbers/zarr.json");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);
        Assert.True(payload.Required().Length < data.Length * sizeof(int));
        Assert.Equal(data, reopened.ReadFlat<int>());

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.Equal(2, reopened.ChunkCodecs.Count);
        Assert.IsType<BytesChunkCodec>(reopened.ChunkCodecs[0]);
        Assert.IsType<BloscChunkCodec>(reopened.ChunkCodecs[1]);
        Assert.Equal("blosc", root.GetProperty("codecs")[1].GetProperty("name").GetString());
        Assert.Equal("lz4", root.GetProperty("codecs")[1].GetProperty("configuration").GetProperty("cname").GetString());
        Assert.Equal(7, root.GetProperty("codecs")[1].GetProperty("configuration").GetProperty("clevel").GetInt32());
        Assert.Equal("bitshuffle", root.GetProperty("codecs")[1].GetProperty("configuration").GetProperty("shuffle").GetString());
    }

    [Fact]
    public void V3BloscMetadataParserAcceptsStringShuffleConfiguration()
    {
        var store = new MemoryStore();
        store.Set("custom/zarr.json", System.Text.Encoding.UTF8.GetBytes(@"
{
  ""zarr_format"": 3,
  ""node_type"": ""array"",
  ""shape"": [4],
  ""data_type"": ""int32"",
  ""chunk_grid"": {
    ""name"": ""regular"",
    ""configuration"": {
      ""chunk_shape"": [4]
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
  ""codecs"": [
    {
      ""name"": ""blosc"",
      ""configuration"": {
        ""cname"": ""zstd"",
        ""clevel"": 5,
        ""shuffle"": ""shuffle"",
        ""blocksize"": 0,
        ""typesize"": 4
      }
    }
  ]
}"));

        var array = ZarrApi.OpenArray(store, "custom", ZarrFormat.V3);
        var codec = Assert.IsType<BloscChunkCodec>(array.ChunkCodec);
        Assert.Equal(BloscCompressor.Zstd, codec.Compressor);
        Assert.Equal(BloscShuffle.Shuffle, codec.Shuffle);
    }

    [Fact]
    public void V3ShardingCodecRoundTripsShardPayloadsWithIndexAtEnd()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4, 4 },
            new[] { 4, 4 },
            "matrix",
            ZarrFormat.V3,
            chunkCodec: new ShardingChunkCodec(
                new[] { 2, 2 },
                codecs: new ChunkCodecPipeline(new BytesChunkCodec(ByteEndian.Big), new GzipChunkCodec(level: 1))));

        array.Write(new[,] { { 0, 1, 2, 3 }, { 4, 5, 6, 7 }, { 8, 9, 10, 11 }, { 12, 13, 14, 15 } });
        var reopened = ZarrApi.OpenArray(store, "matrix", ZarrFormat.V3);
        var payload = store.Get("matrix/c/0/0");
        var metadata = store.Get("matrix/zarr.json");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 }, reopened.ReadFlat<int>());
        Assert.Equal(1, reopened.ChunkCodecs.Count);
        var codec = Assert.IsType<ShardingChunkCodec>(reopened.ChunkCodecs[0]);
        Assert.Equal(new[] { 2, 2 }, codec.ChunkShape);
        Assert.Equal(ShardingCodecIndexLocation.End, codec.IndexLocation);
        Assert.Equal(2, codec.Codecs.Count);
        Assert.Equal(2, codec.IndexCodecs.Count);

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.Equal("sharding_indexed", root.GetProperty("codecs")[0].GetProperty("name").GetString());
        Assert.Equal(2, root.GetProperty("codecs")[0].GetProperty("configuration").GetProperty("chunk_shape")[0].GetInt32());
        Assert.Equal("end", root.GetProperty("codecs")[0].GetProperty("configuration").GetProperty("index_location").GetString());
        Assert.Equal("gzip", root.GetProperty("codecs")[0].GetProperty("configuration").GetProperty("codecs")[1].GetProperty("name").GetString());
    }

    [Fact]
    public void V3ShardingCodecSupportsIndexAtStart()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4, 4 },
            new[] { 4, 4 },
            "matrix",
            ZarrFormat.V3,
            chunkCodec: new ShardingChunkCodec(
                new[] { 2, 2 },
                indexLocation: ShardingCodecIndexLocation.Start));

        array.Write(new[,] { { 1, 2, 3, 4 }, { 5, 6, 7, 8 }, { 9, 10, 11, 12 }, { 13, 14, 15, 16 } });
        var reopened = ZarrApi.OpenArray(store, "matrix", ZarrFormat.V3);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }, reopened.ReadFlat<int>());
        var codec = Assert.IsType<ShardingChunkCodec>(reopened.ChunkCodecs[0]);
        Assert.Equal(ShardingCodecIndexLocation.Start, codec.IndexLocation);
    }

    [Fact]
    public void V3ShardingMetadataParserAcceptsPythonStyleConfiguration()
    {
        var store = new MemoryStore();
        store.Set("custom/zarr.json", System.Text.Encoding.UTF8.GetBytes(@"
{
  ""zarr_format"": 3,
  ""node_type"": ""array"",
  ""shape"": [4, 4],
  ""data_type"": ""int32"",
  ""chunk_grid"": {
    ""name"": ""regular"",
    ""configuration"": {
      ""chunk_shape"": [4, 4]
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
  ""codecs"": [
    {
      ""name"": ""sharding_indexed"",
      ""configuration"": {
        ""chunk_shape"": [2, 2],
        ""codecs"": [
          { ""name"": ""bytes"", ""configuration"": { ""endian"": ""big"" } },
          { ""id"": ""gzip"", ""level"": 1 }
        ],
        ""index_codecs"": [
          { ""name"": ""bytes"", ""configuration"": { ""endian"": ""little"" } }
        ],
        ""index_location"": ""start""
      }
    }
  ]
}"));

        var array = ZarrApi.OpenArray(store, "custom", ZarrFormat.V3);
        var codec = Assert.IsType<ShardingChunkCodec>(array.ChunkCodec);
        Assert.Equal(new[] { 2, 2 }, codec.ChunkShape);
        Assert.Equal(ShardingCodecIndexLocation.Start, codec.IndexLocation);
        Assert.Equal(2, codec.Codecs.Count);
        Assert.Single(codec.IndexCodecs);
        Assert.IsType<BytesChunkCodec>(codec.Codecs[0]);
        Assert.IsType<GzipChunkCodec>(codec.Codecs[1]);
    }

    [Fact]
    public void V3VLenUtf8CodecRoundTripsStringsAndPersistsCodecMetadata()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<string>(
            store,
            new[] { 3 },
            new[] { 3 },
            "strings",
            ZarrFormat.V3,
            chunkCodec: new ChunkCodecPipeline(new VLenUtf8ChunkCodec(), new GzipChunkCodec(level: 1)));

        array.WriteFlat(new[] { "alpha", string.Empty, "beta" });
        var reopened = ZarrApi.OpenArray(store, "strings", ZarrFormat.V3);
        var payload = store.Get("strings/c/0");
        var metadata = store.Get("strings/zarr.json");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);
        Assert.Equal(0x1f, payload.Required().Span[0]);
        Assert.Equal(0x8b, payload.Required().Span[1]);

        var encoded = new GzipChunkCodec().Decode(payload.Required(), DType.String, new[] { 3 }, ArrayOrder.C);
        Assert.Equal(new byte[]
        {
            5, 0, 0, 0, (byte)'a', (byte)'l', (byte)'p', (byte)'h', (byte)'a',
            0, 0, 0, 0,
            4, 0, 0, 0, (byte)'b', (byte)'e', (byte)'t', (byte)'a',
        }, encoded.ToArray());
        Assert.Equal(new[] { "alpha", string.Empty, "beta" }, reopened.ReadFlat<string>());

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.Equal(2, reopened.ChunkCodecs.Count);
        Assert.IsType<VLenUtf8ChunkCodec>(reopened.ChunkCodecs[0]);
        Assert.IsType<GzipChunkCodec>(reopened.ChunkCodecs[1]);
        Assert.Equal("vlen-utf8", root.GetProperty("codecs")[0].GetProperty("name").GetString());
        Assert.Equal("gzip", root.GetProperty("codecs")[1].GetProperty("name").GetString());
    }

    [Fact]
    public void V3VLenBytesCodecRoundTripsByteArraysAndUsesLengthPrefixedPayloads()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<byte[]>(
            store,
            new[] { 3 },
            new[] { 3 },
            "blobs",
            ZarrFormat.V3,
            chunkCodec: new VLenBytesChunkCodec());

        array.WriteFlat(new byte[][]
        {
            new byte[] { 1, 2 },
            Array.Empty<byte>(),
            new byte[] { 3, 4, 5 },
        });

        var reopened = ZarrApi.OpenArray(store, "blobs", ZarrFormat.V3);
        var payload = store.Get("blobs/c/0");
        var metadata = store.Get("blobs/zarr.json");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);
        Assert.Equal(new byte[]
        {
            2, 0, 0, 0, 1, 2,
            0, 0, 0, 0,
            3, 0, 0, 0, 3, 4, 5,
        }, payload.RequiredArray());
        Assert.Equal(new byte[][]
        {
            new byte[] { 1, 2 },
            Array.Empty<byte>(),
            new byte[] { 3, 4, 5 },
        }, reopened.ReadFlat<byte[]>());

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.Single(reopened.ChunkCodecs);
        Assert.IsType<VLenBytesChunkCodec>(reopened.ChunkCodecs[0]);
        Assert.Equal("vlen-bytes", root.GetProperty("codecs")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void V2ZstdCodecRoundTripsAndPersistsCompressorMetadata()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 128 },
            new[] { 128 },
            "numbers",
            ZarrFormat.V2,
            chunkCodec: new ZstdChunkCodec(level: 3, checksum: true));

        array.WriteFlat(Enumerable.Repeat(42, 128).ToArray());
        var reopened = ZarrApi.OpenArray(store, "numbers", ZarrFormat.V2);
        var payload = store.Get("numbers/0");
        var metadata = store.Get("numbers/.zarray");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);
        Assert.Equal(new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, payload.RequiredArray().Take(4).ToArray());
        Assert.Equal(Enumerable.Repeat(42, 128).ToArray(), reopened.ReadFlat<int>());

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.IsType<ZstdChunkCodec>(reopened.ChunkCodec);
        Assert.Equal("zstd", root.GetProperty("compressor").GetProperty("id").GetString());
        Assert.Equal(3, root.GetProperty("compressor").GetProperty("level").GetInt32());
        Assert.True(root.GetProperty("compressor").GetProperty("checksum").GetBoolean());
    }

    [Fact]
    public void V3ZstdCodecRoundTripsAndPersistsCodecMetadata()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 128 },
            new[] { 128 },
            "numbers",
            ZarrFormat.V3,
            chunkCodec: new ZstdChunkCodec(level: 7, checksum: false));

        array.WriteFlat(Enumerable.Repeat(7, 128).ToArray());
        var reopened = ZarrApi.OpenArray(store, "numbers", ZarrFormat.V3);
        var payload = store.Get("numbers/c/0");
        var metadata = store.Get("numbers/zarr.json");

        Assert.NotNull(payload);
        Assert.NotNull(metadata);
        Assert.Equal(new byte[] { 0x28, 0xB5, 0x2F, 0xFD }, payload.RequiredArray().Take(4).ToArray());
        Assert.Equal(Enumerable.Repeat(7, 128).ToArray(), reopened.ReadFlat<int>());

        using var document = JsonDocument.Parse(metadata.RequiredArray());
        var root = document.RootElement;
        Assert.Equal(2, reopened.ChunkCodecs.Count);
        Assert.IsType<BytesChunkCodec>(reopened.ChunkCodecs[0]);
        Assert.IsType<ZstdChunkCodec>(reopened.ChunkCodecs[1]);
        Assert.Equal("zstd", root.GetProperty("codecs")[1].GetProperty("name").GetString());
        Assert.Equal(7, root.GetProperty("codecs")[1].GetProperty("configuration").GetProperty("level").GetInt32());
        Assert.False(root.GetProperty("codecs")[1].GetProperty("configuration").GetProperty("checksum").GetBoolean());
    }

    [Fact]
    public void Crc32cCodecRejectsCorruptedPayloads()
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            new[] { 4 },
            "checksummed",
            ZarrFormat.V3,
            chunkCodec: new Crc32cChunkCodec());

        array.WriteFlat(new[] { 1, 2, 3, 4 });
        var payload = store.Get("checksummed/c/0").RequiredArray();
        payload[0] ^= 0xff;
        store.Set("checksummed/c/0", payload);

        var exception = Assert.Throws<MetadataException>(() => array.ReadFlat<int>());
        Assert.Contains("CRC32C", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ArrayOrder.C, new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 })]
    [InlineData(ArrayOrder.F, new double[] { 0, 3, 6, 1, 4, 7, 2, 5, 8 })]
    public void V2OrderControlsStoredChunkLayout(ArrayOrder order, double[] expected)
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<double>(
            store,
            new[] { 3, 3 },
            new[] { 3, 3 },
            "matrix",
            ZarrFormat.V2,
            order: order);

        array.Write(new[,] { { 0d, 1d, 2d }, { 3d, 4d, 5d }, { 6d, 7d, 8d } });

        var payload = store.Get("matrix/0.0");
        Assert.NotNull(payload);

        var observed = new double[9];
        var payloadBytes = payload.RequiredArray();
        System.Buffer.BlockCopy(payloadBytes, 0, observed, 0, payloadBytes.Length);
        Assert.Equal(expected, observed);

        var roundTrip = (double[,])array.Read<double>();
        Assert.Equal(0d, roundTrip[0, 0]);
        Assert.Equal(1d, roundTrip[0, 1]);
        Assert.Equal(2d, roundTrip[0, 2]);
        Assert.Equal(3d, roundTrip[1, 0]);
        Assert.Equal(4d, roundTrip[1, 1]);
        Assert.Equal(5d, roundTrip[1, 2]);
        Assert.Equal(6d, roundTrip[2, 0]);
        Assert.Equal(7d, roundTrip[2, 1]);
        Assert.Equal(8d, roundTrip[2, 2]);
    }

    private sealed class ReverseBytesTestCodec : IChunkCodec
    {
        public string Id => "reverse-bytes-test";

        public ChunkCodecStage Stage => ChunkCodecStage.BytesBytes;

        public ReadOnlyMemory<byte> Encode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
        {
            return Reverse(input.ToArray());
        }

        public ReadOnlyMemory<byte> Decode(ReadOnlyMemory<byte> input, DType dataType, IReadOnlyList<int>? chunkShape = null, ArrayOrder order = ArrayOrder.C)
        {
            return Reverse(input.ToArray());
        }

        public Dictionary<string, object?> ToMetadata()
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = Id,
            };
        }

        private static byte[] Reverse(byte[] input)
        {
            var output = (byte[])input.Clone();
            Array.Reverse(output);
            return output;
        }
    }
}
