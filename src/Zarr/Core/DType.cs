using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Zarr.Core;

public sealed partial class DType
{
    private readonly RecordField[] _recordFields;
    private readonly int? _fixedLength;
    private readonly string _equivalenceFamily;

    private DType(string name, Type clrType, int byteSize, string? signature = null, IEnumerable<RecordField>? recordFields = null, int? fixedLength = null, string? equivalenceFamily = null)
    {
        Name = name;
        ClrType = clrType;
        ByteSize = byteSize;
        Signature = signature ?? name;
        _recordFields = recordFields?.ToArray() ?? Array.Empty<RecordField>();
        _fixedLength = fixedLength;
        _equivalenceFamily = equivalenceFamily ?? Signature;
    }

    public string Name { get; }

    public Type ClrType { get; }

    public int ByteSize { get; }

    public string Signature { get; }

    public bool IsStructured => _recordFields.Length > 0;

    public static DType Bool { get; } = new("bool", typeof(bool), 1);

    public static DType UInt8 { get; } = new("uint8", typeof(byte), 1);

    public static DType UInt16 { get; } = new("uint16", typeof(ushort), 2);

    public static DType UInt32 { get; } = new("uint32", typeof(uint), 4);

    public static DType UInt64 { get; } = new("uint64", typeof(ulong), 8);

    public static DType Int16 { get; } = new("int16", typeof(short), 2);

    public static DType Int32 { get; } = new("int32", typeof(int), 4);

    public static DType Int64 { get; } = new("int64", typeof(long), 8);

    public static DType Float32 { get; } = new("float32", typeof(float), 4);

    public static DType Float64 { get; } = new("float64", typeof(double), 8);

    public static DType Complex128 { get; } = new("complex128", typeof(Complex), 16);

    public static DType String { get; } = new("string", typeof(string), -1);

    public static DType Bytes { get; } = new("bytes", typeof(byte[]), -1);

    public static DType ReadOnlyMemoryBytes { get; } = new("bytes", typeof(ReadOnlyMemory<byte>), -1);

    public static DType MemoryBytes { get; } = new("bytes", typeof(Memory<byte>), -1);

    public static DType DateTime { get; } = new("datetime", typeof(DateTime), 8);

    public static DType TimeSpan { get; } = new("timespan", typeof(TimeSpan), 8);

    public static DType DateTimeOffset { get; } = new("datetimeoffset", typeof(DateTimeOffset), 16);

    public static DType Guid { get; } = new("guid", typeof(Guid), 16);

    public static DType Create(string name, Type clrType, int byteSize, string? signature = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A dtype name is required.", nameof(name));
        }

        return new DType(name, clrType ?? throw new ArgumentNullException(nameof(clrType)), byteSize, signature);
    }

    public static DType CreateFixedString(int byteLength)
    {
        ValidateFixedLength(byteLength);
        return new DType("fixed_string", typeof(string), byteLength, $"fixed_string[{byteLength}]", fixedLength: byteLength, equivalenceFamily: "string");
    }

    public static DType CreateFixedBytes(int byteLength)
    {
        ValidateFixedLength(byteLength);
        return new DType("fixed_bytes", typeof(byte[]), byteLength, $"fixed_bytes[{byteLength}]", fixedLength: byteLength, equivalenceFamily: "bytes");
    }

    public static DType CreateStructured(Type clrType, IEnumerable<KeyValuePair<string, DType>> fields)
    {
        if (fields is null)
        {
            throw new ArgumentNullException(nameof(fields));
        }

        return CreateStructuredInternal(
            clrType ?? throw new ArgumentNullException(nameof(clrType)),
            fields.Select(field => new RecordField(field.Key, field.Value)).ToArray());
    }

    public static DType FromType(Type type)
    {
        if (DTypeRegistry.TryFromType(type, out var dataType))
        {
            return dataType;
        }

        if (IsStructuredCandidate(type))
        {
            return CreateStructuredInternal(type);
        }

        throw new NotSupportedException($"Type '{type.FullName}' is not supported by this Zarr core implementation.");
    }

    public static DType FromName(string name)
    {
        if (DTypeRegistry.TryFromName(name, out var dataType))
        {
            return dataType;
        }

        if (TryParseParameterizedBuiltIn(name, out dataType))
        {
            return dataType;
        }

        throw new NotSupportedException($"DType '{name}' is not supported by this Zarr core implementation.");
    }

    public static DType FromMetadata(object? value, ZarrFormat format)
    {
        if (DTypeRegistry.TryFromMetadata(value, format, out var registeredDataType))
        {
            return registeredDataType;
        }

        if (value is string name)
        {
            return FromName(name);
        }

        if (value is Dictionary<string, object?> dictionary)
        {
            var dtypeName = JsonUtilities.ConvertValue<string>(dictionary.TryGetValue("name", out var rawName) ? rawName : null)
                ?? throw new MetadataException("DType metadata is missing its name.");

            if (string.Equals(dtypeName, "fixed_string", StringComparison.Ordinal))
            {
                return CreateFixedString(JsonUtilities.ConvertValue<int>(dictionary.TryGetValue("length", out var rawLength) ? rawLength : null));
            }

            if (string.Equals(dtypeName, "fixed_bytes", StringComparison.Ordinal))
            {
                return CreateFixedBytes(JsonUtilities.ConvertValue<int>(dictionary.TryGetValue("length", out var rawLength) ? rawLength : null));
            }

            if (string.Equals(dtypeName, "structured", StringComparison.Ordinal))
            {
                if (format != ZarrFormat.V3)
                {
                    throw new MetadataException("Structured record dtypes are only supported for Zarr format V3.");
                }

                if (!dictionary.TryGetValue("configuration", out var rawConfiguration))
                {
                    throw new MetadataException("Structured dtype metadata is missing its configuration.");
                }

                var configuration = JsonUtilities.ToDictionary(rawConfiguration, "data_type.configuration");
                var fields = ParseRecordFields(
                    configuration.TryGetValue("fields", out var configuredFields) ? configuredFields : null,
                    "data_type.configuration.fields");
                return CreateStructuredInternal(typeof(StructuredRecord), fields);
            }

            return FromName(dtypeName);
        }

        throw new MetadataException("DType metadata is missing or malformed.");
    }

    public object DefaultValue
    {
        get
        {
            if (TryGetCustomRuntime(out var customRuntime))
            {
                return GetCustomDefaultValue(customRuntime);
            }

            if (IsStructured)
            {
                return new StructuredRecord(_recordFields.ToDictionary(
                    field => field.Name,
                    field => (object?)field.Type.DefaultValue,
                    StringComparer.Ordinal));
            }

            return Name switch
            {
                "bool" => false,
                "uint8" => (byte)0,
                "uint16" => (ushort)0,
                "uint32" => 0u,
                "uint64" => 0ul,
                "int16" => (short)0,
                "int32" => 0,
                "int64" => 0L,
                "float32" => 0f,
                "float64" => 0d,
                "complex128" => Complex.Zero,
                "string" => string.Empty,
                "fixed_string" => string.Empty,
                "bytes" => Array.Empty<byte>(),
                "fixed_bytes" => new byte[_fixedLength ?? 0],
                "datetime" => default(DateTime),
                "timespan" => default(TimeSpan),
                "datetimeoffset" => default(DateTimeOffset),
                "guid" => default(Guid),
                _ => throw new InvalidOperationException($"No default fill value is defined for '{Name}'."),
            };
        }
    }

    public object Normalize(object? value)
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return NormalizeCustomValue(customRuntime, value);
        }

        if (value is null)
        {
            return DefaultValue;
        }

        if (Name == "bytes")
        {
            return value switch
            {
                byte[] bytes => (byte[])bytes.Clone(),
                Memory<byte> memory => memory.ToArray(),
                ReadOnlyMemory<byte> readOnlyMemory => readOnlyMemory.ToArray(),
                string text => Encoding.UTF8.GetBytes(text),
                _ => throw new InvalidCastException($"Unable to convert '{value}' to '{Name}'."),
            };
        }

        if (Name == "fixed_bytes")
        {
            var rawBytes = value switch
            {
                byte[] bytes => bytes,
                Memory<byte> memory => memory.ToArray(),
                ReadOnlyMemory<byte> readOnlyMemory => readOnlyMemory.ToArray(),
                string text => Encoding.UTF8.GetBytes(text),
                _ => throw new InvalidCastException($"Unable to convert '{value}' to '{Name}'."),
            };

            var fixedLength = _fixedLength ?? throw new InvalidOperationException("The fixed-bytes dtype is missing its length.");
            if (rawBytes.Length > fixedLength)
            {
                throw new InvalidCastException($"The provided byte value length {rawBytes.Length} exceeds the fixed length {fixedLength}.");
            }

            var padded = new byte[fixedLength];
            System.Buffer.BlockCopy(rawBytes, 0, padded, 0, rawBytes.Length);
            return padded;
        }

        if (Name == "fixed_string")
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            var fixedLength = _fixedLength ?? throw new InvalidOperationException("The fixed-string dtype is missing its length.");
            if (Encoding.UTF8.GetByteCount(text) > fixedLength)
            {
                throw new InvalidCastException($"The provided string value requires more than the fixed length {fixedLength} bytes.");
            }

            return text;
        }

        if (IsStructured)
        {
            return NormalizeStructured(value);
        }

        if (ClrType.IsInstanceOfType(value))
        {
            return value;
        }

        if (ClrType == typeof(bool))
        {
            return value switch
            {
                bool match => match,
                string text => bool.Parse(text),
                _ => Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0L,
            };
        }

        if (ClrType == typeof(string))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (ClrType == typeof(Complex))
        {
            return value switch
            {
                Complex complex => complex,
                string text => ParseComplex(text),
                List<object?> list when list.Count == 2 => new Complex(
                    Convert.ToDouble(list[0], CultureInfo.InvariantCulture),
                    Convert.ToDouble(list[1], CultureInfo.InvariantCulture)),
                Dictionary<string, object?> dictionary => new Complex(
                    Convert.ToDouble(dictionary["real"], CultureInfo.InvariantCulture),
                    Convert.ToDouble(dictionary["imag"], CultureInfo.InvariantCulture)),
                _ => new Complex(Convert.ToDouble(value, CultureInfo.InvariantCulture), 0d),
            };
        }

        if (ClrType == typeof(DateTime))
        {
            return value switch
            {
                DateTime dateTime => dateTime,
                string text => System.DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                long ticks => System.DateTime.FromBinary(ticks),
                _ => throw new InvalidCastException($"Unable to convert '{value}' to '{ClrType.Name}'."),
            };
        }

        if (ClrType == typeof(TimeSpan))
        {
            return value switch
            {
                TimeSpan timeSpan => timeSpan,
                string text => System.TimeSpan.Parse(text, CultureInfo.InvariantCulture),
                long ticks => new System.TimeSpan(ticks),
                _ => throw new InvalidCastException($"Unable to convert '{value}' to '{ClrType.Name}'."),
            };
        }

        if (ClrType == typeof(Guid))
        {
            return value switch
            {
                Guid guid => guid,
                string text => System.Guid.Parse(text),
                byte[] bytes when bytes.Length == 16 => new System.Guid(bytes),
                _ => throw new InvalidCastException($"Unable to convert '{value}' to '{ClrType.Name}'."),
            };
        }

        if (ClrType == typeof(DateTimeOffset))
        {
            return value switch
            {
                DateTimeOffset dateTimeOffset => dateTimeOffset,
                DateTime dateTime => new DateTimeOffset(dateTime),
                string text => System.DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                _ => throw new InvalidCastException($"Unable to convert '{value}' to '{ClrType.Name}'."),
            };
        }

        return Convert.ChangeType(value, ClrType, CultureInfo.InvariantCulture)
            ?? throw new InvalidCastException($"Unable to convert '{value}' to '{ClrType.Name}'.");
    }

    public ReadOnlyMemory<byte> Serialize(Array values)
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return customRuntime.Serialize(values);
        }

        if (IsStructured)
        {
            return SerializeStructured((StructuredRecord[])values);
        }

        return Name switch
        {
            "bool" => SerializeBools((bool[])values),
            "uint8" => (byte[])values.Clone(),
            "uint16" => SerializeBlittable((ushort[])values),
            "uint32" => SerializeBlittable((uint[])values),
            "uint64" => SerializeBlittable((ulong[])values),
            "int16" => SerializeBlittable((short[])values),
            "int32" => SerializeBlittable((int[])values),
            "int64" => SerializeBlittable((long[])values),
            "float32" => SerializeBlittable((float[])values),
            "float64" => SerializeBlittable((double[])values),
            "complex128" => SerializeComplexes((Complex[])values),
            "string" => SerializeStrings((string[])values),
            "fixed_string" => SerializeFixedStrings((string[])values, _fixedLength ?? throw new InvalidOperationException("The fixed-string dtype is missing its length.")),
            "bytes" => SerializeByteArrays((byte[][])values),
            "fixed_bytes" => SerializeFixedBytes((byte[][])values, _fixedLength ?? throw new InvalidOperationException("The fixed-bytes dtype is missing its length.")),
            "datetime" => SerializeDateTimes((DateTime[])values),
            "timespan" => SerializeTimeSpans((TimeSpan[])values),
            "datetimeoffset" => SerializeDateTimeOffsets((DateTimeOffset[])values),
            "guid" => SerializeGuids((Guid[])values),
            _ => throw new NotSupportedException($"Cannot serialize dtype '{Name}'."),
        };
    }

    public Array Deserialize(ReadOnlyMemory<byte> bytes, int length)
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return customRuntime.Deserialize(bytes, length);
        }

        if (IsStructured)
        {
            return DeserializeStructured(bytes, length);
        }

        return Name switch
        {
            "bool" => DeserializeBools(bytes, length),
            "uint8" => DeserializeBytes(bytes, length),
            "uint16" => DeserializeBlittable<ushort>(bytes, length),
            "uint32" => DeserializeBlittable<uint>(bytes, length),
            "uint64" => DeserializeBlittable<ulong>(bytes, length),
            "int16" => DeserializeBlittable<short>(bytes, length),
            "int32" => DeserializeBlittable<int>(bytes, length),
            "int64" => DeserializeBlittable<long>(bytes, length),
            "float32" => DeserializeBlittable<float>(bytes, length),
            "float64" => DeserializeBlittable<double>(bytes, length),
            "complex128" => DeserializeComplexes(bytes, length),
            "string" => DeserializeStrings(bytes, length),
            "fixed_string" => DeserializeFixedStrings(bytes, length, _fixedLength ?? throw new InvalidOperationException("The fixed-string dtype is missing its length.")),
            "bytes" => DeserializeByteArrays(bytes, length),
            "fixed_bytes" => DeserializeFixedBytes(bytes, length, _fixedLength ?? throw new InvalidOperationException("The fixed-bytes dtype is missing its length.")),
            "datetime" => DeserializeDateTimes(bytes, length),
            "timespan" => DeserializeTimeSpans(bytes, length),
            "datetimeoffset" => DeserializeDateTimeOffsets(bytes, length),
            "guid" => DeserializeGuids(bytes, length),
            _ => throw new NotSupportedException($"Cannot deserialize dtype '{Name}'."),
        };
    }

    public Array CreateFilledArray(int length, object? fillValue = null)
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return CreateFilledCustomArray(customRuntime, length, fillValue);
        }

        var normalized = Normalize(fillValue);
        if (IsStructured)
        {
            return CreateFilledStructured(length, (StructuredRecord)normalized);
        }

        return Name switch
        {
            "bool" => CreateFilled(length, (bool)normalized),
            "uint8" => CreateFilled(length, (byte)normalized),
            "uint16" => CreateFilled(length, (ushort)normalized),
            "uint32" => CreateFilled(length, (uint)normalized),
            "uint64" => CreateFilled(length, (ulong)normalized),
            "int16" => CreateFilled(length, (short)normalized),
            "int32" => CreateFilled(length, (int)normalized),
            "int64" => CreateFilled(length, (long)normalized),
            "float32" => CreateFilled(length, (float)normalized),
            "float64" => CreateFilled(length, (double)normalized),
            "complex128" => CreateFilled(length, (Complex)normalized),
            "string" => CreateFilled(length, (string)normalized),
            "fixed_string" => CreateFilled(length, (string)normalized),
            "bytes" => CreateFilledByteArrays(length, (byte[])normalized),
            "fixed_bytes" => CreateFilledByteArrays(length, (byte[])normalized),
            "datetime" => CreateFilled(length, (DateTime)normalized),
            "timespan" => CreateFilled(length, (TimeSpan)normalized),
            "datetimeoffset" => CreateFilled(length, (DateTimeOffset)normalized),
            "guid" => CreateFilled(length, (Guid)normalized),
            _ => throw new NotSupportedException($"Cannot create chunk storage for dtype '{Name}'."),
        };
    }

    public object? DeserializeFillValue(object? value)
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return DeserializeCustomFillValue(customRuntime, value);
        }

        if (IsStructured)
        {
            return Normalize(value);
        }

        return Name switch
        {
            "bytes" when value is string text => Convert.FromBase64String(text),
            "fixed_bytes" when value is string text => Convert.FromBase64String(text),
            "datetime" when value is string text => System.DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "timespan" when value is string text => System.TimeSpan.Parse(text, CultureInfo.InvariantCulture),
            "datetimeoffset" when value is string text => System.DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "guid" when value is string text => System.Guid.Parse(text),
            _ => Normalize(value),
        };
    }

    public object? SerializeFillValue(object? value)
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return SerializeCustomFillValue(customRuntime, value);
        }

        var normalized = Normalize(value);
        if (IsStructured)
        {
            return SerializeStructuredRecord((StructuredRecord)normalized);
        }

        return Name switch
        {
            "bytes" => Convert.ToBase64String((byte[])normalized),
            "fixed_bytes" => Convert.ToBase64String((byte[])normalized),
            "datetime" => ((DateTime)normalized).ToString("O", CultureInfo.InvariantCulture),
            "timespan" => ((TimeSpan)normalized).ToString("c", CultureInfo.InvariantCulture),
            "datetimeoffset" => ((DateTimeOffset)normalized).ToString("O", CultureInfo.InvariantCulture),
            "guid" => ((Guid)normalized).ToString("D"),
            "complex128" => new object?[] { ((Complex)normalized).Real, ((Complex)normalized).Imaginary },
            _ => normalized,
        };
    }

    public Type GetPreferredReadType()
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return customRuntime.PreferredReadType ?? ClrType;
        }

        if (IsStructured)
        {
            return typeof(StructuredRecord);
        }

        return Name switch
        {
            "bytes" or "fixed_bytes" => typeof(byte[]),
            _ => ClrType,
        };
    }

    public object? ConvertForRead(object? value, Type requestedType)
    {
        if (value is null)
        {
            return null;
        }

        if (TryGetCustomRuntime(out var customRuntime))
        {
            var requestedCustomType = FromType(requestedType);
            if (!IsEquivalent(requestedCustomType))
            {
                throw new InvalidOperationException($"The array stores '{Signature}' values, not '{requestedCustomType.Signature}'.");
            }

            return ConvertCustomForRead(customRuntime, value, requestedType);
        }

        if (IsStructured && requestedType == typeof(StructuredRecord))
        {
            return NormalizeStructured(value);
        }

        var requested = FromType(requestedType);
        if (!IsEquivalent(requested))
        {
            throw new InvalidOperationException($"The array stores '{Signature}' values, not '{requested.Signature}'.");
        }

        if (_equivalenceFamily == "bytes")
        {
            var bytes = value switch
            {
                byte[] existing => (byte[])existing.Clone(),
                Memory<byte> memory => memory.ToArray(),
                ReadOnlyMemory<byte> readOnlyMemory => readOnlyMemory.ToArray(),
                _ => throw new InvalidCastException($"Unable to convert '{value}' to '{requestedType.Name}'."),
            };

            if (requestedType == typeof(byte[]))
            {
                return bytes;
            }

            if (requestedType == typeof(ReadOnlyMemory<byte>))
            {
                return new ReadOnlyMemory<byte>(bytes);
            }

            if (requestedType == typeof(Memory<byte>))
            {
                return new Memory<byte>(bytes);
            }
        }

        if (IsStructured)
        {
            return MaterializeStructured(NormalizeStructured(value), requestedType, requested._recordFields);
        }

        if (requestedType.IsInstanceOfType(value))
        {
            return value;
        }

        return Convert.ChangeType(value, requestedType, CultureInfo.InvariantCulture);
    }

    public object ToMetadata(ZarrFormat format)
    {
        if (TryGetCustomRuntime(out var customRuntime))
        {
            return GetCustomMetadata(customRuntime, format);
        }

        if (!IsStructured)
        {
            if (Name == "fixed_string")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = "fixed_string",
                    ["length"] = _fixedLength ?? throw new InvalidOperationException("The fixed-string dtype is missing its length."),
                };
            }

            if (Name == "fixed_bytes")
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["name"] = "fixed_bytes",
                    ["length"] = _fixedLength ?? throw new InvalidOperationException("The fixed-bytes dtype is missing its length."),
                };
            }

            return Name;
        }

        if (format != ZarrFormat.V3)
        {
            throw new MetadataException("Structured record dtypes are only supported for Zarr format V3.");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = "structured",
            ["configuration"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["fields"] = _recordFields
                    .Select(field => (object?)new object?[] { field.Name, field.Type.ToMetadata(ZarrFormat.V3) })
                    .ToArray(),
            },
        };
    }

    public bool IsEquivalent(DType other)
    {
        return string.Equals(Signature, other.Signature, StringComparison.Ordinal) ||
               string.Equals(_equivalenceFamily, other._equivalenceFamily, StringComparison.Ordinal);
    }

    public bool IsExact(DType other)
    {
        return string.Equals(Signature, other.Signature, StringComparison.Ordinal);
    }

    private static DType CreateStructuredInternal(Type clrType)
    {
        return CreateStructuredInternal(clrType, GetStructuredMembers(clrType)
            .Select(member => new RecordField(member.Name, FromType(member.MemberType)))
            .ToArray());
    }

    private static DType CreateStructuredInternal(Type clrType, RecordField[] fields)
    {
        if (fields.Length == 0)
        {
            throw new NotSupportedException($"Type '{clrType.FullName}' does not expose any supported structured fields.");
        }

        var signature = "record(" + string.Join(",", fields.Select(field => $"{field.Name}:{field.Type.Signature}")) + ")";
        return new DType("record", clrType, -1, signature, fields);
    }

    private static bool IsStructuredCandidate(Type type)
    {
        if (type == typeof(StructuredRecord) ||
            type.IsPrimitive ||
            type.IsEnum ||
            type.IsPointer ||
            type.IsInterface ||
            type.IsAbstract ||
            type.IsArray ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(DateTimeOffset))
        {
            return false;
        }

        return GetStructuredMembers(type).Length > 0;
    }

    private static RecordField[] ParseRecordFields(object? value, string propertyName)
    {
        if (value is not IReadOnlyList<object?> fields)
        {
            throw new MetadataException($"Metadata property '{propertyName}' is missing or malformed.");
        }

        return fields.Select(ParseRecordField).ToArray();
    }

    private static RecordField ParseRecordField(object? metadata)
    {
        if (metadata is IReadOnlyList<object?> pair)
        {
            if (pair.Count != 2)
            {
                throw new MetadataException("Structured record field metadata must contain a field name and field dtype.");
            }

            var pairName = JsonUtilities.ConvertValue<string>(pair[0])
                ?? throw new MetadataException("Structured record field metadata is missing its name.");
            var pairType = FromMetadata(pair[1], ZarrFormat.V3);
            return new RecordField(pairName, pairType);
        }

        if (metadata is not Dictionary<string, object?> dictionary)
        {
            throw new MetadataException("Structured record field metadata is missing or malformed.");
        }

        var name = JsonUtilities.ConvertValue<string>(dictionary.TryGetValue("name", out var rawName) ? rawName : null)
            ?? throw new MetadataException("Structured record field metadata is missing its name.");
        var type = FromMetadata(dictionary.TryGetValue("type", out var rawType) ? rawType : null, ZarrFormat.V3);
        return new RecordField(name, type);
    }

    private StructuredRecord NormalizeStructured(object value)
    {
        if (value is StructuredRecord record)
        {
            return new StructuredRecord(_recordFields.ToDictionary(
                field => field.Name,
                field => (object?)field.Type.Normalize(record.TryGetValue(field.Name, out var fieldValue) ? fieldValue : null),
                StringComparer.Ordinal));
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return new StructuredRecord(_recordFields.ToDictionary(
                field => field.Name,
                field => (object?)field.Type.Normalize(readOnlyDictionary.TryGetValue(field.Name, out var fieldValue) ? fieldValue : null),
                StringComparer.Ordinal));
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return new StructuredRecord(_recordFields.ToDictionary(
                field => field.Name,
                field => (object?)field.Type.Normalize(dictionary.TryGetValue(field.Name, out var fieldValue) ? fieldValue : null),
                StringComparer.Ordinal));
        }

        var members = GetStructuredMembers(value.GetType()).ToDictionary(member => member.Name, StringComparer.Ordinal);
        return new StructuredRecord(_recordFields.ToDictionary(
            field => field.Name,
            field => (object?)field.Type.Normalize(members.TryGetValue(field.Name, out var member) ? member.GetValue(value) : null),
            StringComparer.Ordinal));
    }

    private object MaterializeStructured(StructuredRecord record, Type requestedType, IReadOnlyList<RecordField> fields)
    {
        object instance;
        try
        {
            instance = requestedType.IsValueType
                ? Activator.CreateInstance(requestedType)!
                : Activator.CreateInstance(requestedType) ?? throw new MissingMethodException(requestedType.FullName, ".ctor()");
        }
        catch (Exception exception)
        {
            throw new InvalidCastException($"Unable to materialize structured value as '{requestedType.FullName}'. Ensure the type has a public parameterless constructor or is a value type.", exception);
        }

        var members = GetStructuredMembers(requestedType).ToDictionary(member => member.Name, StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!members.TryGetValue(field.Name, out var member))
            {
                continue;
            }

            var value = record.TryGetValue(field.Name, out var fieldValue)
                ? field.Type.ConvertForRead(fieldValue, member.MemberType)
                : field.Type.DefaultValue;
            member.SetValue(instance, value);
        }

        return instance;
    }

    private Dictionary<string, object?> SerializeStructuredRecord(StructuredRecord record)
    {
        return _recordFields.ToDictionary(
            field => field.Name,
            field => field.Type.SerializeFillValue(record.TryGetValue(field.Name, out var fieldValue) ? fieldValue : null),
            StringComparer.Ordinal);
    }

    private byte[] SerializeStructured(StructuredRecord[] values)
    {
        var payload = values.Select(SerializeStructuredRecord).Cast<object?>().ToArray();
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    private StructuredRecord[] DeserializeStructured(ReadOnlyMemory<byte> bytes, int length)
    {
        using var document = JsonDocument.Parse(bytes);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new MetadataException("Structured chunk payload must be a JSON array.");
        }

        var values = document.RootElement.EnumerateArray()
            .Select(element => JsonUtilities.ToDictionary(JsonUtilities.ToPlainObject(element), "structured_record"))
            .Select(record => new StructuredRecord(_recordFields.ToDictionary(
                field => field.Name,
                field => field.Type.DeserializeFillValue(record.TryGetValue(field.Name, out var fieldValue) ? fieldValue : null),
                StringComparer.Ordinal)))
            .ToArray();

        if (values.Length != length)
        {
            throw new MetadataException($"Expected {length} structured values for chunk data but found {values.Length}.");
        }

        return values;
    }

    private static byte[] SerializeBools(bool[] values)
    {
        var bytes = new byte[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            bytes[index] = values[index] ? (byte)1 : (byte)0;
        }

        return bytes;
    }

    private static bool[] DeserializeBools(ReadOnlyMemory<byte> bytes, int length)
    {
        if (bytes.Length != length)
        {
            throw new MetadataException($"Expected {length} bytes for bool chunk data but found {bytes.Length}.");
        }

        var values = new bool[length];
        for (var index = 0; index < bytes.Length; index++)
        {
            values[index] = bytes.Span[index] != 0;
        }

        return values;
    }

    private static byte[] DeserializeBytes(ReadOnlyMemory<byte> bytes, int length)
    {
        if (bytes.Length != length)
        {
            throw new MetadataException($"Expected {length} bytes for byte chunk data but found {bytes.Length}.");
        }

        return bytes.ToArray();
    }

    private static byte[] SerializeBlittable(Array values)
    {
        var bytes = new byte[System.Buffer.ByteLength(values)];
        System.Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static T[] DeserializeBlittable<T>(ReadOnlyMemory<byte> bytes, int length)
        where T : struct
    {
        var values = new T[length];
        var byteLength = System.Buffer.ByteLength(values);
        if (bytes.Length != byteLength)
        {
            throw new MetadataException($"Expected {byteLength} bytes for chunk data but found {bytes.Length}.");
        }

        bytes.Span.CopyTo(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()));
        return values;
    }

    private static byte[] SerializeComplexes(Complex[] values)
    {
        var payload = new double[values.Length * 2];
        for (var index = 0; index < values.Length; index++)
        {
            payload[index * 2] = values[index].Real;
            payload[(index * 2) + 1] = values[index].Imaginary;
        }

        return SerializeBlittable(payload);
    }

    private static Complex[] DeserializeComplexes(ReadOnlyMemory<byte> bytes, int length)
    {
        var payload = DeserializeBlittable<double>(bytes, length * 2);
        var values = new Complex[length];
        for (var index = 0; index < length; index++)
        {
            values[index] = new Complex(payload[index * 2], payload[(index * 2) + 1]);
        }

        return values;
    }

    private static Complex ParseComplex(string text)
    {
        var normalized = text.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.EndsWith("i", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^1];
        }

        if (normalized.Contains('+', StringComparison.Ordinal) ||
            normalized.LastIndexOf('-', normalized.Length - 1) > 0)
        {
            var splitIndex = Math.Max(normalized.LastIndexOf('+'), normalized.LastIndexOf('-', normalized.Length - 1));
            if (splitIndex > 0)
            {
                var real = double.Parse(normalized[..splitIndex], CultureInfo.InvariantCulture);
                var imaginary = double.Parse(normalized[splitIndex..], CultureInfo.InvariantCulture);
                return new Complex(real, imaginary);
            }
        }

        return new Complex(double.Parse(normalized, CultureInfo.InvariantCulture), 0d);
    }

    private static void ValidateFixedLength(int byteLength)
    {
        if (byteLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength), "Fixed-width dtypes require a positive byte length.");
        }
    }

    private static bool TryParseParameterizedBuiltIn(string name, out DType dataType)
    {
        dataType = null!;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (TryParseFixedLengthSignature(name, "fixed_string", out var fixedStringLength))
        {
            dataType = CreateFixedString(fixedStringLength);
            return true;
        }

        if (TryParseFixedLengthSignature(name, "fixed_bytes", out var fixedBytesLength))
        {
            dataType = CreateFixedBytes(fixedBytesLength);
            return true;
        }

        return false;
    }

    private static bool TryParseFixedLengthSignature(string name, string prefix, out int byteLength)
    {
        byteLength = default;
        if (!name.StartsWith(prefix + "[", StringComparison.Ordinal) || !name.EndsWith("]", StringComparison.Ordinal))
        {
            return false;
        }

        return int.TryParse(name.AsSpan(prefix.Length + 1, name.Length - prefix.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out byteLength);
    }

    private static byte[] SerializeStrings(string[] values)
    {
        return JsonSerializer.SerializeToUtf8Bytes(values);
    }

    private static byte[] SerializeFixedStrings(string[] values, int byteLength)
    {
        var bytes = new byte[values.Length * byteLength];
        for (var index = 0; index < values.Length; index++)
        {
            var encoded = Encoding.UTF8.GetBytes(values[index] ?? string.Empty);
            if (encoded.Length > byteLength)
            {
                throw new MetadataException($"The string value at index {index} exceeds the fixed length {byteLength}.");
            }

            System.Buffer.BlockCopy(encoded, 0, bytes, index * byteLength, encoded.Length);
        }

        return bytes;
    }

    private static string[] DeserializeStrings(ReadOnlyMemory<byte> bytes, int length)
    {
        var values = JsonSerializer.Deserialize<string[]>(bytes.ToArray()) ?? Array.Empty<string>();
        if (values.Length != length)
        {
            throw new MetadataException($"Expected {length} string values for chunk data but found {values.Length}.");
        }

        return values;
    }

    private static string[] DeserializeFixedStrings(ReadOnlyMemory<byte> bytes, int length, int byteLength)
    {
        if (bytes.Length != length * byteLength)
        {
            throw new MetadataException($"Expected {length * byteLength} bytes for fixed_string chunk data but found {bytes.Length}.");
        }

        var values = new string[length];
        for (var index = 0; index < length; index++)
        {
            var slice = new byte[byteLength];
            bytes.Span.Slice(index * byteLength, byteLength).CopyTo(slice);
            var actualLength = Array.FindLastIndex(slice, value => value != 0) + 1;
            values[index] = Encoding.UTF8.GetString(slice, 0, Math.Max(actualLength, 0));
        }

        return values;
    }

    private static byte[] SerializeByteArrays(byte[][] values)
    {
        var payload = values.Select(bytes => Convert.ToBase64String(bytes ?? Array.Empty<byte>())).ToArray();
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    private static byte[][] DeserializeByteArrays(ReadOnlyMemory<byte> bytes, int length)
    {
        var payload = JsonSerializer.Deserialize<string[]>(bytes.ToArray()) ?? Array.Empty<string>();
        if (payload.Length != length)
        {
            throw new MetadataException($"Expected {length} byte[] values for chunk data but found {payload.Length}.");
        }

        return payload.Select(Convert.FromBase64String).ToArray();
    }

    private static byte[] SerializeFixedBytes(byte[][] values, int byteLength)
    {
        var bytes = new byte[values.Length * byteLength];
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index] ?? Array.Empty<byte>();
            if (value.Length > byteLength)
            {
                throw new MetadataException($"The byte[] value at index {index} exceeds the fixed length {byteLength}.");
            }

            System.Buffer.BlockCopy(value, 0, bytes, index * byteLength, value.Length);
        }

        return bytes;
    }

    private static byte[][] DeserializeFixedBytes(ReadOnlyMemory<byte> bytes, int length, int byteLength)
    {
        if (bytes.Length != length * byteLength)
        {
            throw new MetadataException($"Expected {length * byteLength} bytes for fixed_bytes chunk data but found {bytes.Length}.");
        }

        var values = new byte[length][];
        for (var index = 0; index < length; index++)
        {
            var value = new byte[byteLength];
            bytes.Span.Slice(index * byteLength, byteLength).CopyTo(value);
            values[index] = value;
        }

        return values;
    }

    private static byte[] SerializeDateTimes(DateTime[] values)
    {
        var payload = values.Select(value => value.ToBinary()).ToArray();
        return SerializeBlittable(payload);
    }

    private static DateTime[] DeserializeDateTimes(ReadOnlyMemory<byte> bytes, int length)
    {
        var payload = DeserializeBlittable<long>(bytes, length);
        return payload.Select(System.DateTime.FromBinary).ToArray();
    }

    private static byte[] SerializeTimeSpans(TimeSpan[] values)
    {
        var payload = values.Select(value => value.Ticks).ToArray();
        return SerializeBlittable(payload);
    }

    private static TimeSpan[] DeserializeTimeSpans(ReadOnlyMemory<byte> bytes, int length)
    {
        var payload = DeserializeBlittable<long>(bytes, length);
        return payload.Select(ticks => new System.TimeSpan(ticks)).ToArray();
    }

    private static byte[] SerializeDateTimeOffsets(DateTimeOffset[] values)
    {
        var payload = new long[values.Length * 2];
        for (var index = 0; index < values.Length; index++)
        {
            payload[index * 2] = values[index].Ticks;
            payload[(index * 2) + 1] = values[index].Offset.Ticks;
        }

        return SerializeBlittable(payload);
    }

    private static DateTimeOffset[] DeserializeDateTimeOffsets(ReadOnlyMemory<byte> bytes, int length)
    {
        var payload = DeserializeBlittable<long>(bytes, length * 2);
        var values = new DateTimeOffset[length];
        for (var index = 0; index < length; index++)
        {
            values[index] = new DateTimeOffset(payload[index * 2], new TimeSpan(payload[(index * 2) + 1]));
        }

        return values;
    }

    private static byte[] SerializeGuids(Guid[] values)
    {
        var bytes = new byte[values.Length * 16];
        for (var index = 0; index < values.Length; index++)
        {
            var guidBytes = values[index].ToByteArray();
            System.Buffer.BlockCopy(guidBytes, 0, bytes, index * 16, 16);
        }

        return bytes;
    }

    private static Guid[] DeserializeGuids(ReadOnlyMemory<byte> bytes, int length)
    {
        if (bytes.Length != length * 16)
        {
            throw new MetadataException($"Expected {length * 16} bytes for guid chunk data but found {bytes.Length}.");
        }

        var values = new Guid[length];
        for (var index = 0; index < length; index++)
        {
            var guidBytes = new byte[16];
            bytes.Span.Slice(index * 16, 16).CopyTo(guidBytes);
            values[index] = new Guid(guidBytes);
        }

        return values;
    }

    private static T[] CreateFilled<T>(int length, T value)
    {
        var values = new T[length];
        Array.Fill(values, value);
        return values;
    }

    private Array CreateFilledStructured(int length, StructuredRecord value)
    {
        var values = new StructuredRecord[length];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = NormalizeStructured(value);
        }

        return values;
    }

    private static byte[][] CreateFilledByteArrays(int length, byte[] value)
    {
        var values = new byte[length][];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = (byte[])value.Clone();
        }

        return values;
    }

    private static StructuredMember[] GetStructuredMembers(Type type)
    {
        const BindingFlags BindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

        var fields = type.GetFields(BindingFlags)
            .Where(field => !field.IsStatic && !field.IsInitOnly)
            .Select(field => new StructuredMember(
                field.Name,
                field.FieldType,
                instance => field.GetValue(instance),
                (instance, value) => field.SetValue(instance, value),
                field.MetadataToken));

        var properties = type.GetProperties(BindingFlags)
            .Where(property => property.CanRead &&
                               property.CanWrite &&
                               property.GetMethod is not null &&
                               !property.GetMethod.IsStatic &&
                               property.SetMethod is not null &&
                               !property.SetMethod.IsStatic &&
                               property.GetIndexParameters().Length == 0)
            .Select(property => new StructuredMember(
                property.Name,
                property.PropertyType,
                instance => property.GetValue(instance),
                (instance, value) => property.SetValue(instance, value),
                property.MetadataToken));

        return fields
            .Concat(properties)
            .OrderBy(member => member.SortKey)
            .ThenBy(member => member.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class RecordField
    {
        public RecordField(string name, DType type)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public string Name { get; }

        public DType Type { get; }
    }

    private sealed class StructuredMember
    {
        private readonly Func<object, object?> _getter;
        private readonly Action<object, object?> _setter;

        public StructuredMember(string name, Type memberType, Func<object, object?> getter, Action<object, object?> setter, int sortKey)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            MemberType = memberType ?? throw new ArgumentNullException(nameof(memberType));
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            SortKey = sortKey;
        }

        public string Name { get; }

        public Type MemberType { get; }

        public int SortKey { get; }

        public object? GetValue(object instance)
        {
            return _getter(instance);
        }

        public void SetValue(object instance, object? value)
        {
            _setter(instance, value);
        }
    }
}
