namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_indexing.py")]
public sealed class IndexingTests
{
    [Fact]
    public void NormalizeIntegerSelectionMatchesReferenceBehavior()
    {
        Assert.Equal(1, Indexing.NormalizeIntegerSelection(1, 100));
        Assert.Equal(99, Indexing.NormalizeIntegerSelection(-1, 100));
        Assert.Throws<IndexOutOfRangeException>(() => Indexing.NormalizeIntegerSelection(100, 100));
        Assert.Throws<IndexOutOfRangeException>(() => Indexing.NormalizeIntegerSelection(-101, 100));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void SelectionReadsCanMixIndicesAndSlices(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.FromArray(new[,] { { 1, 2, 3 }, { 4, 5, 6 } }, store, "matrix", format);

        Assert.Equal(6, array.GetValue<int>(-1, -1));
        Assert.Equal(4, array.GetScalar<int>(ArraySelection.At(1), ArraySelection.At(0)));

        var row = (int[])array.GetSelection<int>(ArraySelection.At(1), ArraySelection.All);
        Assert.Equal(new[] { 4, 5, 6 }, row);

        var column = (int[])array.GetSelection<int>(ArraySelection.All, ArraySelection.At(1));
        Assert.Equal(new[] { 2, 5 }, column);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void SelectionWritesCanAssignArrayAndScalar(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.Zeros<int>(store, new[] { 4, 4 }, new[] { 2, 2 }, "matrix", format);

        array.WriteSelection((Array)new[,] { { 1, 2 }, { 3, 4 } }, ArraySelection.Slice(1, 3), ArraySelection.Slice(1, 3));
        array.WriteSelection(9, ArraySelection.At(0), ArraySelection.Slice(0, 4, 2));

        var observed = (int[,])array.Read();
        Assert.Equal(9, observed[0, 0]);
        Assert.Equal(9, observed[0, 2]);
        Assert.Equal(1, observed[1, 1]);
        Assert.Equal(2, observed[1, 2]);
        Assert.Equal(3, observed[2, 1]);
        Assert.Equal(4, observed[2, 2]);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void SelectionReadsSupportSteps(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.FromArray(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, store, "vector", format, chunks: new[] { 4 });

        var observed = (int[])array.GetSelection<int>(ArraySelection.Slice(1, 10, 3));
        Assert.Equal(new[] { 1, 4, 7 }, observed);
    }
}
