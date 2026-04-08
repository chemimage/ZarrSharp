namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_attributes.py")]
public sealed class AttributeTests
{
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void PutAndDeletePersistForGroupsAndArrays(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);

        var group = ZarrApi.CreateGroup(context.Store, format: format);
        group.Attrs.Put(new Dictionary<string, object?> { ["a"] = 1, ["b"] = "two" });
        group.Attrs.Remove("a");

        var reopenedGroup = ZarrApi.OpenGroup(context.Store, format: format);
        Assert.Equal(new Dictionary<string, object?> { ["b"] = "two" }, reopenedGroup.Attrs.AsDictionary());

        var array = group.CreateArray<int>("values", new[] { 4 }, new[] { 2 });
        array.Attrs.Put(new Dictionary<string, object?> { ["scale"] = 3, ["label"] = "counts" });
        array.Attrs["scale"] = 5;

        var reopenedArray = ZarrApi.OpenArray(context.Store, "values", format);
        Assert.Equal(new Dictionary<string, object?> { ["scale"] = 5, ["label"] = "counts" }, reopenedArray.Attrs.AsDictionary());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void UpdatePreservesExistingAttributes(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(store, new[] { 10 }, new[] { 5 }, format: format);

        array.Attrs["a"] = new List<object?> { 1L, 2L };
        array.Attrs["b"] = 3;
        array.UpdateAttributes(new Dictionary<string, object?> { ["a"] = new List<object?> { 3L, 4L }, ["c"] = 4 });

        var reopened = ZarrApi.OpenArray(store, format: format);
        var observed = reopened.Attrs.AsDictionary();

        Assert.True(observed.ContainsKey("a"));
        Assert.True(observed.ContainsKey("b"));
        Assert.True(observed.ContainsKey("c"));
        Assert.Equal(3, observed["b"]);
        Assert.Equal(4, observed["c"]);
    }
}
