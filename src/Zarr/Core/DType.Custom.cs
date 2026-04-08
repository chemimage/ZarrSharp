using System.Globalization;
using System.Runtime.CompilerServices;

namespace Zarr.Core;

public sealed partial class DType
{
    private static readonly ConditionalWeakTable<DType, CustomDTypeRuntime> CustomRuntimes = new();

    public static DType CreateCustom(string name, Type clrType, int byteSize, CustomDTypeRuntime runtime, string? signature = null, string? equivalenceFamily = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A dtype name is required.", nameof(name));
        }

        if (clrType is null)
        {
            throw new ArgumentNullException(nameof(clrType));
        }

        if (runtime is null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        if (runtime.DefaultValueFactory is null ||
            runtime.Normalize is null ||
            runtime.Serialize is null ||
            runtime.Deserialize is null)
        {
            throw new ArgumentException("Custom dtype runtimes must define default-value, normalize, serialize, and deserialize delegates.", nameof(runtime));
        }

        var dataType = new DType(name, clrType, byteSize, signature, fixedLength: null, equivalenceFamily: equivalenceFamily);
        CustomRuntimes.Add(dataType, runtime);
        return dataType;
    }

    private bool TryGetCustomRuntime(out CustomDTypeRuntime runtime)
    {
        return CustomRuntimes.TryGetValue(this, out runtime!);
    }

    private object GetCustomDefaultValue(CustomDTypeRuntime runtime)
    {
        return runtime.DefaultValueFactory();
    }

    private object NormalizeCustomValue(CustomDTypeRuntime runtime, object? value)
    {
        return value is null
            ? runtime.DefaultValueFactory()
            : runtime.Normalize(value);
    }

    private Array CreateFilledCustomArray(CustomDTypeRuntime runtime, int length, object? fillValue)
    {
        var normalized = NormalizeCustomValue(runtime, fillValue);
        if (runtime.CreateFilledArray is not null)
        {
            return runtime.CreateFilledArray(length, normalized);
        }

        var values = Array.CreateInstance(ClrType, length);
        for (var index = 0; index < length; index++)
        {
            values.SetValue(normalized, index);
        }

        return values;
    }

    private object? DeserializeCustomFillValue(CustomDTypeRuntime runtime, object? value)
    {
        return runtime.DeserializeFillValue?.Invoke(value) ?? NormalizeCustomValue(runtime, value);
    }

    private object? SerializeCustomFillValue(CustomDTypeRuntime runtime, object? value)
    {
        var normalized = NormalizeCustomValue(runtime, value);
        return runtime.SerializeFillValue?.Invoke(normalized) ?? normalized;
    }

    private object? ConvertCustomForRead(CustomDTypeRuntime runtime, object? value, Type requestedType)
    {
        if (value is null)
        {
            return null;
        }

        if (runtime.ConvertForRead is not null)
        {
            return runtime.ConvertForRead(value, requestedType);
        }

        if (requestedType.IsInstanceOfType(value))
        {
            return value;
        }

        return Convert.ChangeType(value, requestedType, CultureInfo.InvariantCulture);
    }

    private object GetCustomMetadata(CustomDTypeRuntime runtime, ZarrFormat format)
    {
        return runtime.MetadataFactory?.Invoke(format) ?? Name;
    }
}
