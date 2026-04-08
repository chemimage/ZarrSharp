using System.Globalization;
using System.Text.Json;

namespace Zarr.Tests;

public readonly record struct Rgb24(byte R, byte G, byte B);

[Trait("TestBasis", "DotNetImplementation")]
public sealed class CustomDTypeTests
{
    [Fact]
    public void CustomRegisteredDTypeRoundTripsThroughRuntimeAndMetadata()
    {
        var rgb24 = DType.CreateCustom(
            "rgb24_runtime_test",
            typeof(Rgb24),
            3,
            new CustomDTypeRuntime
            {
                DefaultValueFactory = static () => new Rgb24(),
                Normalize = static value => value switch
                {
                    null => new Rgb24(),
                    Rgb24 rgb => rgb,
                    string text => ParseRgb24(text),
                    _ => throw new InvalidCastException($"Unable to convert '{value}' to rgb24_runtime_test."),
                },
                Serialize = static values =>
                {
                    var typed = values is Rgb24[] rgbValues
                        ? rgbValues
                        : values.Cast<Rgb24>().ToArray();
                    var bytes = new byte[typed.Length * 3];
                    for (var index = 0; index < typed.Length; index++)
                    {
                        bytes[index * 3] = typed[index].R;
                        bytes[(index * 3) + 1] = typed[index].G;
                        bytes[(index * 3) + 2] = typed[index].B;
                    }

                    return bytes;
                },
                Deserialize = static (bytes, length) =>
                {
                    if (bytes.Length != length * 3)
                    {
                        throw new MetadataException($"Expected {length * 3} bytes for rgb24 chunk data but found {bytes.Length}.");
                    }

                    var values = new Rgb24[length];
                    for (var index = 0; index < length; index++)
                    {
                        values[index] = new Rgb24(
                            bytes.Span[index * 3],
                            bytes.Span[(index * 3) + 1],
                            bytes.Span[(index * 3) + 2]);
                    }

                    return values;
                },
                SerializeFillValue = static value =>
                {
                    var rgb = (Rgb24)value!;
                    return $"{rgb.R},{rgb.G},{rgb.B}";
                },
                DeserializeFillValue = static value => value switch
                {
                    null => new Rgb24(),
                    string text => ParseRgb24(text),
                    Rgb24 rgb => rgb,
                    _ => throw new MetadataException("rgb24 fill values must be stored as 'R,G,B'."),
                },
            });

        DTypeRegistry.Register(rgb24, typeof(Rgb24), "rgb24_runtime_test");

        var store = new MemoryStore();
        var fill = new Rgb24(1, 2, 3);
        var array = ZarrApi.CreateArray<Rgb24>(
            store,
            new[] { 3 },
            new[] { 3 },
            "pixels",
            ZarrFormat.V3,
            fillValue: fill);

        Assert.Equal(fill, array.GetValue<Rgb24>(2));

        var expected = new[]
        {
            new Rgb24(10, 20, 30),
            new Rgb24(40, 50, 60),
            new Rgb24(70, 80, 90),
        };

        array.WriteFlat(expected);

        var reopened = ZarrApi.OpenArray(store, "pixels", ZarrFormat.V3);
        Assert.Equal("rgb24_runtime_test", reopened.DataType.Name);
        Assert.Equal(expected, reopened.ReadFlat<Rgb24>());
        Assert.Equal(expected, (Rgb24[])reopened.Read());
        Assert.Equal(expected[1], reopened.GetValue<Rgb24>(1));

        using var document = JsonDocument.Parse(store.Get("pixels/zarr.json").RequiredArray());
        Assert.Equal("rgb24_runtime_test", document.RootElement.GetProperty("data_type").GetString());
        Assert.Equal("1,2,3", document.RootElement.GetProperty("fill_value").GetString());
    }

    private static Rgb24 ParseRgb24(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new MetadataException("rgb24 values must use the 'R,G,B' format.");
        }

        return new Rgb24(
            byte.Parse(parts[0], CultureInfo.InvariantCulture),
            byte.Parse(parts[1], CultureInfo.InvariantCulture),
            byte.Parse(parts[2], CultureInfo.InvariantCulture));
    }
}
