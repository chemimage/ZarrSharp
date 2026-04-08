namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_array.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_info.py")]
public sealed class LifecycleTests
{
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void ArrayAndGroupInfoExposeCoreStats(ZarrFormat format)
    {
        var store = new MemoryStore();
        var group = ZarrApi.Group(store, format: format);
        var array = group.CreateArray<int>("data", new[] { 4 }, new[] { 2 });
        array.WriteFlat(new[] { 1, 2, 3, 4 });

        var groupInfo = group.Info().ToString();
        var arrayInfo = array.Info().ToString();

        Assert.Contains("Type        : Group", groupInfo);
        Assert.Contains("No. arrays  : 1", groupInfo);
        Assert.Contains("Type               : Array", arrayInfo);
        Assert.Contains("Data type          : int32", arrayInfo);
        Assert.Contains("Chunk shape        : (2)", arrayInfo);
        Assert.Contains("Chunks Initialized : 2", arrayInfo);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void TreeIncludesHierarchyConnectorsShapesAndTypes(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.Group(store, format: format);
        var branch = root.CreateGroup("branch");
        _ = branch.CreateArray<int>("leaf", new[] { 2 });
        _ = root.CreateArray<int>("top", new[] { 1 });

        var tree = root.Tree();

        Assert.Contains("/", tree);
        Assert.Contains("branch/", tree);
        Assert.Contains("leaf (2) int32", tree);
        Assert.Contains("top (1) int32", tree);
        Assert.Contains("├──", tree);
        Assert.Contains("└──", tree);
    }

    [Theory]
    [InlineData(ZarrFormat.V2, "vector/1")]
    [InlineData(ZarrFormat.V3, "vector/c/1")]
    public void ResizeShrinkingDeletesOutsideChunksAndPreservesRemainingData(ZarrFormat format, string removedChunkKey)
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 4 },
            new[] { 2 },
            "vector",
            format);

        array.WriteFlat(new[] { 1, 2, 3, 4 });
        array.Resize(new[] { 2 });

        Assert.Equal(new[] { 2 }, array.Shape);
        Assert.Equal(new[] { 1, 2 }, array.ReadFlat<int>());
        Assert.Null(store.Get(removedChunkKey));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void AppendExtendsArrayAlongRequestedAxis(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(
            store,
            new[] { 2, 2 },
            new[] { 1, 2 },
            "matrix",
            format);

        array.Write(new[,] { { 1, 2 }, { 3, 4 } });
        var newShape = array.Append(new[,] { { 5, 6 } }, axis: 0);

        var observed = (int[,])array.Read<int>();

        Assert.Equal(new[] { 3, 2 }, newShape);
        Assert.Equal(new[] { 3, 2 }, array.Shape);
        Assert.Equal(1, observed[0, 0]);
        Assert.Equal(2, observed[0, 1]);
        Assert.Equal(3, observed[1, 0]);
        Assert.Equal(4, observed[1, 1]);
        Assert.Equal(5, observed[2, 0]);
        Assert.Equal(6, observed[2, 1]);
    }
}
