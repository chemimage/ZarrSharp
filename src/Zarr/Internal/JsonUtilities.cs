using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zarr.Internal;

internal static class JsonUtilities
{
    public static byte[] Serialize(object value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = Configuration.ZarrConfiguration.Current.Json.Indent > 0,
        }))
        {
            JsonSerializer.Serialize(writer, value, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            });
        }

        return stream.ToArray();
    }

    public static Dictionary<string, object?> DeserializeDictionary(ReadOnlyMemory<byte> value)
    {
        using var document = JsonDocument.Parse(value);
        var plain = ToPlainObject(document.RootElement);
        return plain as Dictionary<string, object?> ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public static Dictionary<string, object?> CloneDictionary(IReadOnlyDictionary<string, object?>? source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (source is null)
        {
            return result;
        }

        foreach (var item in source)
        {
            result[item.Key] = CloneValue(item.Value);
        }

        return result;
    }

    public static int[] ToIntArray(object? value, string propertyName)
    {
        if (value is int[] ints)
        {
            return ints.ToArray();
        }

        if (value is IReadOnlyList<int> intList)
        {
            return intList.ToArray();
        }

        if (value is IEnumerable<object?> sequence)
        {
            return sequence.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToArray();
        }

        if (value is not List<object?> list)
        {
            throw new MetadataException($"Metadata property '{propertyName}' is missing or malformed.");
        }

        return list.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToArray();
    }

    public static Dictionary<string, object?> ToDictionary(object? value, string propertyName)
    {
        if (value is Dictionary<string, object?> dictionary)
        {
            return dictionary;
        }

        throw new MetadataException($"Metadata property '{propertyName}' is missing or malformed.");
    }

    public static Dictionary<string, object?>[] ToDictionaryArray(object? value, string propertyName)
    {
        if (value is not List<object?> list)
        {
            throw new MetadataException($"Metadata property '{propertyName}' is missing or malformed.");
        }

        return list.Select(item => ToDictionary(item, propertyName)).ToArray();
    }

    public static string?[] ToNullableStringArray(object? value, string propertyName)
    {
        if (value is not List<object?> list)
        {
            throw new MetadataException($"Metadata property '{propertyName}' is missing or malformed.");
        }

        return list.Select(item => ConvertValue<string>(item)).ToArray();
    }

    public static T? ConvertValue<T>(object? value)
    {
        if (value is null)
        {
            return default;
        }

        if (value is T match)
        {
            return match;
        }

        return (T?)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
    }

    private static object? CloneValue(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> dictionary => CloneDictionary(dictionary),
            List<object?> list => list.Select(CloneValue).ToList(),
            _ => value,
        };
    }

    internal static object? ToPlainObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    dictionary[property.Name] = ToPlainObject(property.Value);
                }

                return dictionary;

            case JsonValueKind.Array:
                return element.EnumerateArray().Select(ToPlainObject).ToList();

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt32(out var int32))
                {
                    return int32;
                }

                if (element.TryGetInt64(out var int64))
                {
                    return int64;
                }

                return element.GetDouble();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;

            default:
                throw new MetadataException($"Unsupported JSON value kind: {element.ValueKind}.");
        }
    }
}
