using System.Numerics;
using System.Text.Json;

namespace Zarr.Tests;

public sealed class StructuredMeasurement
{
    public int Id { get; set; }

    public double Value { get; set; }

    public string Label { get; set; } = string.Empty;
}

public sealed class NativeTypeTests
{
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_string.py")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void StringArraysUseNativeDotNetStrings(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var array = ZarrApi.CreateArray<string>(
            context.Store,
            new[] { 2, 2 },
            new[] { 2, 2 },
            "names",
            format,
            fillValue: "missing",
            chunkCodec: new GzipChunkCodec());

        Assert.Equal("missing", array.GetValue<string>(1, 1));

        array.Write(new[,] { { "alpha", "beta" }, { "gamma", "delta" } });
        var reopened = ZarrApi.OpenArray(context.Store, "names", format);
        var observed = (string[,])reopened.Read<string>();

        Assert.Equal("alpha", observed[0, 0]);
        Assert.Equal("beta", observed[0, 1]);
        Assert.Equal("gamma", observed[1, 0]);
        Assert.Equal("delta", observed[1, 1]);
        Assert.Equal("string", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void ByteArrayElementsUseNativeByteArrays(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = new byte[] { 9, 9 };
        var array = ZarrApi.CreateArray<byte[]>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "buffers",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill, array.GetValue<byte[]>(2));

        array.WriteFlat(new[]
        {
            new byte[] { 1, 2 },
            new byte[] { 3, 4, 5 },
            new byte[] { 6 },
        });

        var reopened = ZarrApi.OpenArray(context.Store, "buffers", format);
        var observed = reopened.ReadFlat<byte[]>();

        Assert.Equal(new byte[] { 1, 2 }, observed[0]);
        Assert.Equal(new byte[] { 3, 4, 5 }, observed[1]);
        Assert.Equal(new byte[] { 6 }, observed[2]);
        Assert.Equal("bytes", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void FixedStringDTypeCanBeCreatedExplicitlyAndReadBackAsString(ZarrFormat format)
    {
        var store = new MemoryStore();
        var dtype = DType.CreateFixedString(8);
        var array = ZarrApi.CreateArray(
            store,
            dtype,
            new[] { 3 },
            new[] { 3 },
            "labels",
            format,
            fillValue: "na");

        Assert.Equal("na", array.GetValue<string>(2));

        array.WriteFlat(new[] { "alpha", "beta", "gamma" });
        var reopened = ZarrApi.OpenArray(store, "labels", format);

        Assert.Equal("fixed_string[8]", reopened.DataType.Signature);
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, reopened.ReadFlat<string>());

        using var document = JsonDocument.Parse(store.Get(format == ZarrFormat.V2 ? "labels/.zarray" : "labels/zarr.json").RequiredArray());
        var dataType = format == ZarrFormat.V2
            ? document.RootElement.GetProperty("dtype")
            : document.RootElement.GetProperty("data_type");
        Assert.Equal("fixed_string", dataType.GetProperty("name").GetString());
        Assert.Equal(8, dataType.GetProperty("length").GetInt32());
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void FixedBytesDTypeCanBeCreatedExplicitlyAndReadBackAsBuffers(ZarrFormat format)
    {
        var store = new MemoryStore();
        var dtype = DType.CreateFixedBytes(4);
        var array = ZarrApi.CreateArray(
            store,
            dtype,
            new[] { 3 },
            new[] { 3 },
            "payloads",
            format,
            fillValue: new byte[] { 9 });

        Assert.Equal(new byte[] { 9, 0, 0, 0 }, array.GetValue<byte[]>(1));

        array.WriteFlat(new[]
        {
            new byte[] { 1, 2 },
            new byte[] { 3, 4, 5, 6 },
            new byte[] { 7 },
        });

        var reopened = ZarrApi.OpenArray(store, "payloads", format);
        var observed = reopened.ReadFlat<byte[]>();
        var selected = (ReadOnlyMemory<byte>[])reopened.GetSelection<ReadOnlyMemory<byte>>(ArraySelection.Slice(0, 2));

        Assert.Equal("fixed_bytes[4]", reopened.DataType.Signature);
        Assert.Equal(new byte[] { 1, 2, 0, 0 }, observed[0]);
        Assert.Equal(new byte[] { 3, 4, 5, 6 }, observed[1]);
        Assert.Equal(new byte[] { 7, 0, 0, 0 }, observed[2]);
        Assert.Equal(new byte[] { 1, 2, 0, 0 }, selected[0].ToArray());
        Assert.Equal(new byte[] { 3, 4, 5, 6 }, selected[1].ToArray());

        using var document = JsonDocument.Parse(store.Get(format == ZarrFormat.V2 ? "payloads/.zarray" : "payloads/zarr.json").RequiredArray());
        var dataType = format == ZarrFormat.V2
            ? document.RootElement.GetProperty("dtype")
            : document.RootElement.GetProperty("data_type");
        Assert.Equal("fixed_bytes", dataType.GetProperty("name").GetString());
        Assert.Equal(4, dataType.GetProperty("length").GetInt32());
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_bytes.py")]
    [Fact]
    public void V2ByteArrayFillValueSerializesAsBase64()
    {
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<byte[]>(
            store,
            new[] { 3 },
            new[] { 3 },
            "foo",
            ZarrFormat.V2,
            fillValue: new byte[] { 88 },
            chunkCodec: null);

        var payload = store.Get("foo/.zarray");
        Assert.NotNull(payload);

        using var document = JsonDocument.Parse(payload.RequiredArray());
        var root = document.RootElement;
        Assert.Equal("bytes", root.GetProperty("dtype").GetString());
        Assert.Equal("WA==", root.GetProperty("fill_value").GetString());
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_string.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void NativeReferenceTypesRoundTripWithGzip(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<string>(
            store,
            new[] { 4 },
            new[] { 4 },
            "labels",
            format,
            chunkCodec: new GzipChunkCodec(level: 1));

        array.WriteFlat(new[] { "north", "south", "east", "west" });
        var reopened = ZarrApi.OpenArray(store, "labels", format);

        Assert.Equal(new[] { "north", "south", "east", "west" }, reopened.ReadFlat<string>());
        var key = format == ZarrFormat.V2 ? "labels/0" : "labels/c/0";
        Assert.NotNull(store.Get(key));
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void DateTimeArraysUseNativeDotNetDateTimes(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = new DateTime(2024, 1, 1, 12, 30, 0, DateTimeKind.Utc);
        var array = ZarrApi.CreateArray<DateTime>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "timestamps",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill, array.GetValue<DateTime>(2));

        array.WriteFlat(new[]
        {
            new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            new DateTime(2025, 2, 4, 4, 5, 6, DateTimeKind.Utc),
            new DateTime(2025, 2, 5, 4, 5, 6, DateTimeKind.Utc),
        });

        var reopened = ZarrApi.OpenArray(context.Store, "timestamps", format);
        var observed = reopened.ReadFlat<DateTime>();

        Assert.Equal(DateTimeKind.Utc, observed[0].Kind);
        Assert.Equal(new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc), observed[0]);
        Assert.Equal(new DateTime(2025, 2, 4, 4, 5, 6, DateTimeKind.Utc), observed[1]);
        Assert.Equal(new DateTime(2025, 2, 5, 4, 5, 6, DateTimeKind.Utc), observed[2]);
        Assert.Equal("datetime", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void TimeSpanArraysUseNativeDotNetTimeSpans(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = TimeSpan.FromMinutes(15);
        var array = ZarrApi.CreateArray<TimeSpan>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "durations",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill, array.GetValue<TimeSpan>(1));

        array.WriteFlat(new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromHours(3),
        });

        var reopened = ZarrApi.OpenArray(context.Store, "durations", format);
        var observed = reopened.ReadFlat<TimeSpan>();

        Assert.Equal(TimeSpan.FromSeconds(1), observed[0]);
        Assert.Equal(TimeSpan.FromMinutes(2), observed[1]);
        Assert.Equal(TimeSpan.FromHours(3), observed[2]);
        Assert.Equal("timespan", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public void V2DateTimeAndTimeSpanFillValuesSerializeAsStrings()
    {
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<DateTime>(
            store,
            new[] { 1 },
            new[] { 1 },
            "dt",
            ZarrFormat.V2,
            fillValue: new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        _ = ZarrApi.CreateArray<TimeSpan>(
            store,
            new[] { 1 },
            new[] { 1 },
            "ts",
            ZarrFormat.V2,
            fillValue: TimeSpan.FromHours(12));

        using var dtDocument = JsonDocument.Parse(store.Get("dt/.zarray").RequiredArray());
        using var tsDocument = JsonDocument.Parse(store.Get("ts/.zarray").RequiredArray());

        Assert.Equal("datetime", dtDocument.RootElement.GetProperty("dtype").GetString());
        Assert.Equal("2024-06-01T00:00:00.0000000Z", dtDocument.RootElement.GetProperty("fill_value").GetString());
        Assert.Equal("timespan", tsDocument.RootElement.GetProperty("dtype").GetString());
        Assert.Equal("12:00:00", tsDocument.RootElement.GetProperty("fill_value").GetString());
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void DateTimeOffsetArraysUseNativeDotNetDateTimeOffsets(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = new DateTimeOffset(2024, 1, 1, 12, 30, 0, TimeSpan.FromHours(2));
        var array = ZarrApi.CreateArray<DateTimeOffset>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "offsets",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill, array.GetValue<DateTimeOffset>(2));

        var expected = new[]
        {
            new DateTimeOffset(2025, 2, 3, 4, 5, 6, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2025, 2, 4, 4, 5, 6, TimeSpan.Zero),
            new DateTimeOffset(2025, 2, 5, 4, 5, 6, TimeSpan.FromHours(9)),
        };

        array.WriteFlat(expected);
        var reopened = ZarrApi.OpenArray(context.Store, "offsets", format);

        Assert.Equal(expected, reopened.ReadFlat<DateTimeOffset>());
        Assert.Equal("datetimeoffset", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public void V2DateTimeOffsetFillValueSerializesAsString()
    {
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<DateTimeOffset>(
            store,
            new[] { 1 },
            new[] { 1 },
            "dto",
            ZarrFormat.V2,
            fillValue: new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.FromHours(2)));

        using var document = JsonDocument.Parse(store.Get("dto/.zarray").RequiredArray());

        Assert.Equal("datetimeoffset", document.RootElement.GetProperty("dtype").GetString());
        Assert.Equal("2024-06-01T00:00:00.0000000+02:00", document.RootElement.GetProperty("fill_value").GetString());
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void GuidArraysUseNativeDotNetGuids(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var array = ZarrApi.CreateArray<Guid>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "identifiers",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill, array.GetValue<Guid>(2));

        var expected = new[]
        {
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Guid.Parse("12345678-1234-5678-9abc-def012345678"),
            Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210"),
        };

        array.WriteFlat(expected);
        var reopened = ZarrApi.OpenArray(context.Store, "identifiers", format);

        Assert.Equal(expected, reopened.ReadFlat<Guid>());
        Assert.Equal("guid", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public void V2GuidFillValueSerializesAsString()
    {
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<Guid>(
            store,
            new[] { 1 },
            new[] { 1 },
            "guid-array",
            ZarrFormat.V2,
            fillValue: Guid.Parse("11111111-2222-3333-4444-555555555555"));

        using var document = JsonDocument.Parse(store.Get("guid-array/.zarray").RequiredArray());

        Assert.Equal("guid", document.RootElement.GetProperty("dtype").GetString());
        Assert.Equal("11111111-2222-3333-4444-555555555555", document.RootElement.GetProperty("fill_value").GetString());
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_complex.py")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void ComplexArraysUseNativeDotNetComplexNumbers(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = new Complex(1.5, -2.5);
        var array = ZarrApi.CreateArray<Complex>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "complex-values",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill, array.GetValue<Complex>(2));

        var expected = new[]
        {
            new Complex(0d, 1d),
            new Complex(2d, -3.5d),
            new Complex(4.25d, 0d),
        };

        array.WriteFlat(expected);
        var reopened = ZarrApi.OpenArray(context.Store, "complex-values", format);

        Assert.Equal(expected, reopened.ReadFlat<Complex>());
        Assert.Equal(expected[1], reopened.GetScalar<Complex>(ArraySelection.At(1)));
        Assert.Equal("complex128", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_structured.py")]
    [Theory]
    [InlineData("memory")]
    [InlineData("local")]
    public void StructuredArraysUseNativeDotNetTypesInV3(string storeKind)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = new StructuredMeasurement
        {
            Id = -1,
            Value = -0.5d,
            Label = "missing",
        };

        var array = ZarrApi.CreateArray<StructuredMeasurement>(
            context.Store,
            new[] { 2 },
            new[] { 2 },
            "measurements",
            ZarrFormat.V3,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        AssertMeasurement(fill, array.GetValue<StructuredMeasurement>(1));

        var expected = new[]
        {
            new StructuredMeasurement { Id = 1, Value = 1.25d, Label = "alpha" },
            new StructuredMeasurement { Id = 2, Value = 2.5d, Label = "beta" },
        };

        array.WriteFlat(expected);
        var reopened = ZarrApi.OpenArray(context.Store, "measurements", ZarrFormat.V3);
        var typed = reopened.ReadFlat<StructuredMeasurement>();
        var raw = reopened.ReadFlat<StructuredRecord>();
        var scalar = reopened.GetScalar<StructuredMeasurement>(ArraySelection.At(1));

        Assert.True(reopened.DataType.IsStructured);
        Assert.Equal("record", reopened.DataType.Name);
        AssertMeasurement(expected[0], typed[0]);
        AssertMeasurement(expected[1], typed[1]);
        AssertMeasurement(expected[1], scalar);
        Assert.Equal(expected[0].Id, Assert.IsType<int>(raw[0]["Id"]));
        Assert.Equal(expected[0].Value, Assert.IsType<double>(raw[0]["Value"]));
        Assert.Equal(expected[0].Label, Assert.IsType<string>(raw[0]["Label"]));
        Assert.Equal(expected[1].Id, Assert.IsType<int>(raw[1]["Id"]));
        Assert.Equal(expected[1].Value, Assert.IsType<double>(raw[1]["Value"]));
        Assert.Equal(expected[1].Label, Assert.IsType<string>(raw[1]["Label"]));
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_structured.py")]
    [Fact]
    public void V3StructuredDTypeMetadataUsesPythonStyleStructuredDefinition()
    {
        var store = new MemoryStore();
        _ = ZarrApi.CreateArray<StructuredMeasurement>(
            store,
            new[] { 1 },
            new[] { 1 },
            "structured",
            ZarrFormat.V3,
            fillValue: new StructuredMeasurement
            {
                Id = 10,
                Value = 2.5d,
                Label = "gamma",
            });

        using var document = JsonDocument.Parse(store.Get("structured/zarr.json").RequiredArray());
        var dataType = document.RootElement.GetProperty("data_type");
        var fields = dataType.GetProperty("configuration").GetProperty("fields").EnumerateArray().ToArray();

        Assert.Equal("structured", dataType.GetProperty("name").GetString());
        Assert.Equal(3, fields.Length);
        Assert.Equal("Id", fields[0][0].GetString());
        Assert.Equal("int32", fields[0][1].GetString());
        Assert.Equal("Value", fields[1][0].GetString());
        Assert.Equal("float64", fields[1][1].GetString());
        Assert.Equal("Label", fields[2][0].GetString());
        Assert.Equal("string", fields[2][1].GetString());
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_structured.py")]
    [Fact]
    public void V2StructuredArraysAreRejected()
    {
        var store = new MemoryStore();

        var exception = Assert.Throws<MetadataException>(() => ZarrApi.CreateArray<StructuredMeasurement>(
            store,
            new[] { 1 },
            new[] { 1 },
            "structured-v2",
            ZarrFormat.V2));

        Assert.Contains("V3", exception.Message);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_dtype/test_npy/test_structured.py")]
    [Fact]
    public void EmptyLikePreservesStructuredDTypeFromReopenedArray()
    {
        var sourceStore = new MemoryStore();
        _ = ZarrApi.CreateArray<StructuredMeasurement>(
            sourceStore,
            new[] { 2 },
            new[] { 2 },
            "source",
            ZarrFormat.V3,
            fillValue: new StructuredMeasurement
            {
                Id = -1,
                Value = -1d,
                Label = "source",
            });

        var reopened = ZarrApi.OpenArray(sourceStore, "source", ZarrFormat.V3);
        var targetStore = new MemoryStore();
        var clone = ZarrApi.EmptyLike(reopened, targetStore, "clone");

        clone.WriteFlat(new[]
        {
            new StructuredMeasurement { Id = 11, Value = 1.1d, Label = "first" },
            new StructuredMeasurement { Id = 12, Value = 1.2d, Label = "second" },
        });

        var observed = clone.ReadFlat<StructuredMeasurement>();

        Assert.True(clone.DataType.IsStructured);
        AssertMeasurement(new StructuredMeasurement { Id = 11, Value = 1.1d, Label = "first" }, observed[0]);
        AssertMeasurement(new StructuredMeasurement { Id = 12, Value = 1.2d, Label = "second" }, observed[1]);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void ReadOnlyMemoryByteArraysRoundTripAsNativeBuffers(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = new ReadOnlyMemory<byte>(new byte[] { 9, 9 });
        var array = ZarrApi.CreateArray<ReadOnlyMemory<byte>>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "readonly-buffers",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill.ToArray(), array.GetValue<ReadOnlyMemory<byte>>(2).ToArray());

        var expected = new[]
        {
            new ReadOnlyMemory<byte>(new byte[] { 1, 2 }),
            new ReadOnlyMemory<byte>(new byte[] { 3, 4, 5 }),
            new ReadOnlyMemory<byte>(new byte[] { 6 }),
        };

        array.WriteFlat(expected);
        var reopened = ZarrApi.OpenArray(context.Store, "readonly-buffers", format);
        var observed = reopened.ReadFlat<ReadOnlyMemory<byte>>();
        var rawObserved = (byte[][])reopened.Read();
        var selected = (ReadOnlyMemory<byte>[])reopened.GetSelection<ReadOnlyMemory<byte>>(ArraySelection.Slice(1, 3));

        Assert.Equal(expected[0].ToArray(), observed[0].ToArray());
        Assert.Equal(expected[1].ToArray(), observed[1].ToArray());
        Assert.Equal(expected[2].ToArray(), observed[2].ToArray());
        Assert.Equal(new byte[] { 1, 2 }, rawObserved[0]);
        Assert.Equal(new byte[] { 3, 4, 5 }, rawObserved[1]);
        Assert.Equal(new byte[] { 3, 4, 5 }, selected[0].ToArray());
        Assert.Equal(new byte[] { 6 }, selected[1].ToArray());
        Assert.Equal("bytes", reopened.DataType.Name);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void MemoryByteArraysRoundTripAsNativeBuffers(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var fill = new Memory<byte>(new byte[] { 8, 8 });
        var array = ZarrApi.CreateArray<Memory<byte>>(
            context.Store,
            new[] { 3 },
            new[] { 3 },
            "memory-buffers",
            format,
            fillValue: fill,
            chunkCodec: new GzipChunkCodec());

        Assert.Equal(fill.ToArray(), array.GetValue<Memory<byte>>(1).ToArray());

        var expected = new[]
        {
            new Memory<byte>(new byte[] { 10, 11 }),
            new Memory<byte>(new byte[] { 12, 13, 14 }),
            new Memory<byte>(new byte[] { 15 }),
        };

        array.WriteFlat(expected);
        var reopened = ZarrApi.OpenArray(context.Store, "memory-buffers", format);
        var observed = reopened.ReadFlat<Memory<byte>>();
        var selected = (Memory<byte>[])reopened.GetSelection<Memory<byte>>(ArraySelection.Slice(0, 2));

        Assert.Equal(expected[0].ToArray(), reopened.GetValue<Memory<byte>>(0).ToArray());
        Assert.Equal(expected[0].ToArray(), observed[0].ToArray());
        Assert.Equal(expected[1].ToArray(), observed[1].ToArray());
        Assert.Equal(expected[2].ToArray(), observed[2].ToArray());
        Assert.Equal(expected[0].ToArray(), selected[0].ToArray());
        Assert.Equal(expected[1].ToArray(), selected[1].ToArray());
        Assert.Equal("bytes", reopened.DataType.Name);
    }

    private static void AssertMeasurement(StructuredMeasurement expected, StructuredMeasurement actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Value, actual.Value);
        Assert.Equal(expected.Label, actual.Label);
    }
}
