namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_store/test_zip.py")]
public sealed class ZipStoreTests
{
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void ZipStorePersistsDataAcrossReopen(ZarrFormat format)
    {
        using var context = TestStoreContext.Create("zip");
        var root = ZarrApi.CreateGroup(context.Store, format: format);
        var data = new[,] { { 1, 2 }, { 3, 4 } };

        var array = root.CreateArray<int>("foo", new[] { 2, 2 }, new[] { 1, 1 }, overwrite: true);
        array.Write(data);
        var group = root.CreateGroup("bar", overwrite: true, attributes: new Dictionary<string, object?> { ["hello"] = "world" });

        using var reopenedStore = new ZipStore(((ZipStore)context.Store).Path);
        var reopenedRoot = ZarrApi.OpenGroup(reopenedStore, format: format);
        var reopenedArray = reopenedRoot.OpenArray("foo");
        var reopenedData = (int[,])reopenedArray.Read();

        Assert.Equal(1, reopenedData[0, 0]);
        Assert.Equal(4, reopenedData[1, 1]);
        Assert.Equal("world", reopenedRoot.OpenGroup("bar").Attrs["hello"]);
        Assert.True(reopenedStore.ListKeys().Any());
        Assert.True(reopenedArray.InitializedChunkCount > 0);
    }

    [Fact]
    public void ZipStoreReadOnlyModeRejectsWrites()
    {
        using var context = TestStoreContext.Create("zip");
        context.Store.Set("foo", new byte[] { 1, 2, 3 });

        using var reopened = new ZipStore(((ZipStore)context.Store).Path, readOnly: true);
        Assert.Equal(new byte[] { 1, 2, 3 }, reopened.Get("foo")!.Value.ToArray());
        Assert.Throws<InvalidOperationException>(() => reopened.Set("bar", new byte[] { 4 }));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void ZipStoreSupportsArrayConvenienceApis(ZarrFormat format)
    {
        using var context = TestStoreContext.Create("zip");
        var array = ZarrApi.SaveArray(new[] { 1, 2, 3, 4 }, context.Store, "data", format, chunks: new[] { 2 });

        using var reopenedStore = new ZipStore(((ZipStore)context.Store).Path);
        var loaded = (int[])ZarrApi.Load(reopenedStore, "data", format);

        Assert.Equal(new[] { 1, 2, 3, 4 }, loaded);
        Assert.Equal(2, array.InitializedChunkCount);
    }
}
