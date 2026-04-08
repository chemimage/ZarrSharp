using System.Buffers;

namespace Zarr.Buffer;

public readonly struct ByteRange
{
    public ByteRange(long offset, long? length = null)
    {
        Offset = offset;
        Length = length;
    }

    public long Offset { get; }

    public long? Length { get; }

    public static ByteRange FromOffset(long offset)
    {
        return new ByteRange(offset);
    }

    public static ByteRange Slice(long offset, long length)
    {
        return new ByteRange(offset, length);
    }
}

public interface IBufferAllocator
{
    IMemoryOwner<byte> Allocate(int length);
}

public sealed class ArrayPoolBufferAllocator : IBufferAllocator
{
    public static ArrayPoolBufferAllocator Shared { get; } = new();

    public IMemoryOwner<byte> Allocate(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return new PooledMemoryOwner(ArrayPool<byte>.Shared.Rent(length), length);
    }

    private sealed class PooledMemoryOwner : IMemoryOwner<byte>
    {
        private byte[]? _buffer;
        private readonly int _length;

        public PooledMemoryOwner(byte[] buffer, int length)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _length = length;
        }

        public Memory<byte> Memory => (_buffer ?? throw new ObjectDisposedException(nameof(PooledMemoryOwner))).AsMemory(0, _length);

        public void Dispose()
        {
            if (_buffer is null)
            {
                return;
            }

            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null;
        }
    }
}

public sealed class BufferPrototype
{
    public BufferPrototype(IBufferAllocator? allocator = null)
    {
        Allocator = allocator ?? ArrayPoolBufferAllocator.Shared;
    }

    public static BufferPrototype Shared { get; } = new();

    public IBufferAllocator Allocator { get; }

    public IMemoryOwner<byte> Allocate(int length)
    {
        return Allocator.Allocate(length);
    }

    public IMemoryOwner<byte> Copy(ReadOnlyMemory<byte> source)
    {
        var owner = Allocate(source.Length);
        source.CopyTo(owner.Memory);
        return owner;
    }

    public byte[] ToArray(ReadOnlyMemory<byte> source)
    {
        return source.ToArray();
    }
}
