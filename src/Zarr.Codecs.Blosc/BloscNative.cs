using System.Runtime.InteropServices;
using System.Text;

namespace Zarr.Codecs.Blosc;

public static class BloscNative
{
    private const string LibraryName = "blosc";
    public const int MaxOverhead = 16;

    public static unsafe byte[] Compress(ReadOnlySpan<byte> input, string compressor, int level, int shuffle, int typeSize, int blockSize)
    {
        if (compressor is null)
        {
            throw new ArgumentNullException(nameof(compressor));
        }

        var destination = new byte[checked(input.Length + MaxOverhead)];
        var compressorBytes = Encoding.ASCII.GetBytes(compressor + "\0");

        fixed (byte* sourcePointer = input)
        fixed (byte* destinationPointer = destination)
        fixed (byte* compressorPointer = compressorBytes)
        {
            var compressedLength = blosc_compress_ctx(
                level,
                shuffle,
                (nuint)typeSize,
                (nuint)input.Length,
                sourcePointer,
                destinationPointer,
                (nuint)destination.Length,
                compressorPointer,
                (nuint)blockSize,
                1);

            if (compressedLength <= 0)
            {
                throw new InvalidOperationException($"Blosc compression failed with status '{compressedLength}'.");
            }

            Array.Resize(ref destination, compressedLength);
            return destination;
        }
    }

    public static unsafe byte[] Decompress(ReadOnlySpan<byte> input)
    {
        var (uncompressedSize, _, _) = GetBufferSizes(input);
        var destination = new byte[uncompressedSize];

        fixed (byte* sourcePointer = input)
        fixed (byte* destinationPointer = destination)
        {
            var decodedLength = blosc_decompress_ctx(
                sourcePointer,
                destinationPointer,
                (nuint)destination.Length,
                1);

            if (decodedLength <= 0)
            {
                throw new InvalidOperationException($"Blosc decompression failed with status '{decodedLength}'.");
            }

            if (decodedLength != uncompressedSize)
            {
                throw new InvalidOperationException(
                    $"Blosc decompression produced '{decodedLength}' bytes but the header expected '{uncompressedSize}'.");
            }
        }

        return destination;
    }

    public static unsafe (int UncompressedSize, int CompressedSize, int BlockSize) GetBufferSizes(ReadOnlySpan<byte> input)
    {
        fixed (byte* sourcePointer = input)
        {
            nuint nbytes = 0;
            nuint cbytes = 0;
            nuint blockSize = 0;
            blosc_cbuffer_sizes(sourcePointer, &nbytes, &cbytes, &blockSize);

            if (nbytes > int.MaxValue || cbytes > int.MaxValue || blockSize > int.MaxValue)
            {
                throw new InvalidOperationException("The Blosc payload was larger than the managed wrapper supports.");
            }

            return ((int)nbytes, (int)cbytes, (int)blockSize);
        }
    }

    public static unsafe (int TypeSize, int Flags) GetBufferMetaInfo(ReadOnlySpan<byte> input)
    {
        fixed (byte* sourcePointer = input)
        {
            nuint typeSize = 0;
            var flags = 0;
            blosc_cbuffer_metainfo(sourcePointer, &typeSize, &flags);

            if (typeSize > int.MaxValue)
            {
                throw new InvalidOperationException("The Blosc payload typesize exceeded the managed wrapper limit.");
            }

            return ((int)typeSize, flags);
        }
    }

    public static unsafe string GetCompressionLibrary(ReadOnlySpan<byte> input)
    {
        fixed (byte* sourcePointer = input)
        {
            var value = blosc_cbuffer_complib(sourcePointer);
            return Marshal.PtrToStringAnsi(value) ?? string.Empty;
        }
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern unsafe int blosc_compress_ctx(
        int clevel,
        int doshuffle,
        nuint typesize,
        nuint nbytes,
        byte* src,
        byte* dest,
        nuint destsize,
        byte* compressor,
        nuint blocksize,
        int numinternalthreads);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern unsafe int blosc_decompress_ctx(
        byte* src,
        byte* dest,
        nuint destsize,
        int numinternalthreads);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern unsafe void blosc_cbuffer_sizes(
        byte* cbuffer,
        nuint* nbytes,
        nuint* cbytes,
        nuint* blocksize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern unsafe void blosc_cbuffer_metainfo(
        byte* cbuffer,
        nuint* typesize,
        int* flags);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern unsafe IntPtr blosc_cbuffer_complib(byte* cbuffer);
}
