namespace Zarr.Registry;

public static class DTypeRegistry
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, DType> Types = new();
    private static readonly Dictionary<string, DType> Names = new(StringComparer.Ordinal);
    private static readonly List<IDTypeProvider> Providers = new();

    static DTypeRegistry()
    {
        RegisterBuiltIn(DType.Bool, typeof(bool), "bool");
        RegisterBuiltIn(DType.UInt8, typeof(byte), "uint8", "u1");
        RegisterBuiltIn(DType.UInt16, typeof(ushort), "uint16", "u2");
        RegisterBuiltIn(DType.UInt32, typeof(uint), "uint32", "u4");
        RegisterBuiltIn(DType.UInt64, typeof(ulong), "uint64", "u8");
        RegisterBuiltIn(DType.Int16, typeof(short), "int16", "i2");
        RegisterBuiltIn(DType.Int32, typeof(int), "int32", "i4");
        RegisterBuiltIn(DType.Int64, typeof(long), "int64", "i8");
        RegisterBuiltIn(DType.Float32, typeof(float), "float32", "f4");
        RegisterBuiltIn(DType.Float64, typeof(double), "float64", "f8");
        RegisterBuiltIn(DType.Complex128, typeof(System.Numerics.Complex), "complex128", "c16");
        RegisterBuiltIn(DType.String, typeof(string), "string");
        RegisterBuiltIn(DType.Bytes, typeof(byte[]), "bytes");
        RegisterTypeAlias(typeof(ReadOnlyMemory<byte>), DType.ReadOnlyMemoryBytes);
        RegisterTypeAlias(typeof(Memory<byte>), DType.MemoryBytes);
        RegisterBuiltIn(DType.DateTime, typeof(DateTime), "datetime");
        RegisterBuiltIn(DType.TimeSpan, typeof(TimeSpan), "timespan");
        RegisterBuiltIn(DType.DateTimeOffset, typeof(DateTimeOffset), "datetimeoffset");
        RegisterBuiltIn(DType.Guid, typeof(Guid), "guid");
    }

    public static IReadOnlyList<DType> RegisteredDataTypes
    {
        get
        {
            lock (Gate)
            {
                return Names.Values
                    .Distinct()
                    .OrderBy(dataType => dataType.Signature, StringComparer.Ordinal)
                    .ToArray();
            }
        }
    }

    public static void Register(DType dataType, Type clrType, params string[] aliases)
    {
        if (dataType is null)
        {
            throw new ArgumentNullException(nameof(dataType));
        }

        if (clrType is null)
        {
            throw new ArgumentNullException(nameof(clrType));
        }

        lock (Gate)
        {
            Types[clrType] = dataType;
            RegisterNamesUnsafe(dataType, aliases);
        }
    }

    public static void RegisterProvider(IDTypeProvider provider)
    {
        if (provider is null)
        {
            throw new ArgumentNullException(nameof(provider));
        }

        lock (Gate)
        {
            Providers.Add(provider);
        }
    }

    public static bool TryFromType(Type type, out DType dataType)
    {
        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        lock (Gate)
        {
            if (Types.TryGetValue(type, out dataType!))
            {
                return true;
            }

            foreach (var provider in Providers)
            {
                if (provider.TryFromType(type, out dataType!))
                {
                    return true;
                }
            }
        }

        dataType = null!;
        return false;
    }

    public static bool TryFromName(string name, out DType dataType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            dataType = null!;
            return false;
        }

        lock (Gate)
        {
            if (Names.TryGetValue(name, out dataType!))
            {
                return true;
            }

            foreach (var provider in Providers)
            {
                if (provider.TryFromName(name, out dataType!))
                {
                    return true;
                }
            }
        }

        dataType = null!;
        return false;
    }

    public static bool TryFromMetadata(object? metadata, ZarrFormat format, out DType dataType)
    {
        lock (Gate)
        {
            foreach (var provider in Providers)
            {
                if (provider.TryFromMetadata(metadata, format, out dataType!))
                {
                    return true;
                }
            }
        }

        dataType = null!;
        return false;
    }

    private static void RegisterBuiltIn(DType dataType, Type clrType, params string[] aliases)
    {
        Types[clrType] = dataType;
        RegisterNamesUnsafe(dataType, aliases);
    }

    private static void RegisterTypeAlias(Type clrType, DType dataType)
    {
        Types[clrType] = dataType;
    }

    private static void RegisterNamesUnsafe(DType dataType, IEnumerable<string> aliases)
    {
        Names[dataType.Name] = dataType;
        foreach (var alias in aliases.Where(alias => !string.IsNullOrWhiteSpace(alias)))
        {
            Names[alias] = dataType;
        }
    }
}
