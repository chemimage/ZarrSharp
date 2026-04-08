namespace Zarr.Tests;

internal static class TestBuffers
{
    public static ReadOnlyMemory<byte> Required(this ReadOnlyMemory<byte>? value)
    {
        return value ?? throw new Xunit.Sdk.XunitException("Expected a store payload but found null.");
    }

    public static byte[] RequiredArray(this ReadOnlyMemory<byte>? value)
    {
        return value.Required().ToArray();
    }
}
