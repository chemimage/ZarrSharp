namespace Zarr.Core;

public readonly struct SliceRange
{
    public SliceRange(int? start, int? stop, int step = 1)
    {
        Start = start;
        Stop = stop;
        Step = step;
    }

    public int? Start { get; }

    public int? Stop { get; }

    public int Step { get; }

    public static SliceRange All => new(null, null, 1);

    public static SliceRange Create(int? start = null, int? stop = null, int step = 1)
    {
        return new SliceRange(start, stop, step);
    }
}

public readonly struct ArraySelection
{
    private ArraySelection(bool isIndex, int index, SliceRange slice)
    {
        IsIndex = isIndex;
        Index = index;
        SliceSpec = slice;
    }

    public bool IsIndex { get; }

    public int Index { get; }

    public SliceRange SliceSpec { get; }

    public static ArraySelection All => new(false, 0, SliceRange.All);

    public static ArraySelection At(int index)
    {
        return new ArraySelection(true, index, SliceRange.All);
    }

    public static ArraySelection Slice(int? start = null, int? stop = null, int step = 1)
    {
        return new ArraySelection(false, 0, new SliceRange(start, stop, step));
    }
}

internal readonly struct NormalizedSlice
{
    public NormalizedSlice(int start, int stop, int step)
    {
        Start = start;
        Stop = stop;
        Step = step;
    }

    public int Start { get; }

    public int Stop { get; }

    public int Step { get; }

    public int Count
    {
        get
        {
            if (Stop <= Start)
            {
                return 0;
            }

            return ((Stop - Start - 1) / Step) + 1;
        }
    }
}

public static class Indexing
{
    public static int NormalizeIntegerSelection(int index, int length)
    {
        var normalized = index < 0 ? length + index : index;
        if (normalized < 0 || normalized >= length)
        {
            throw new IndexOutOfRangeException($"Index {index} is out of range for a dimension of length {length}.");
        }

        return normalized;
    }

    internal static NormalizedSlice NormalizeSlice(SliceRange slice, int length)
    {
        if (slice.Step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(slice), "Slice step must be greater than zero.");
        }

        var start = slice.Start ?? 0;
        var stop = slice.Stop ?? length;

        if (start < 0)
        {
            start += length;
        }

        if (stop < 0)
        {
            stop += length;
        }

        start = Math.Clamp(start, 0, length);
        stop = Math.Clamp(stop, 0, length);

        return new NormalizedSlice(start, stop, slice.Step);
    }
}
