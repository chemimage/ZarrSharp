namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_group.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_tree.py")]
public sealed class HierarchyTests
{
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void GroupCanAccessChildrenByNameAndDeleteThem(ZarrFormat format)
    {
        var store = new MemoryStore();
        var group = ZarrApi.CreateGroup(store, format: format);
        _ = group.CreateArray<int>("sub_array", new[] { 4 }, new[] { 2 }, attributes: new Dictionary<string, object?> { ["foo"] = 100 });
        _ = group.CreateGroup("sub_group", attributes: new Dictionary<string, object?> { ["bar"] = 200 });

        var arrayNode = group["sub_array"];
        var groupNode = group["sub_group"];

        Assert.IsType<ZarrArray>(arrayNode);
        Assert.IsType<ZarrGroup>(groupNode);
        Assert.Equal(100, ((ZarrArray)arrayNode).Attrs["foo"]);
        Assert.Equal(200, ((ZarrGroup)groupNode).Attrs["bar"]);

        group.DeleteItem("sub_array");
        Assert.False(group.ContainsItem("sub_array"));

        group.DeleteItem("sub_group");
        Assert.False(group.ContainsItem("sub_group"));
        Assert.Throws<NodeNotFoundException>(() => group.GetItem("sub_group"));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void KeysValuesAndNMembersFollowMembersDepth(ZarrFormat format)
    {
        var store = new MemoryStore();
        var group = ZarrApi.CreateGroup(store, "root", format);
        var a = group.CreateGroup("a");
        _ = a.CreateArray<int>("array", new[] { 2 }, new[] { 2 });
        var b = a.CreateGroup("b");
        _ = b.CreateArray<int>("array", new[] { 2 }, new[] { 2 });

        Assert.Equal(new[] { "a" }, group.Keys());
        Assert.Equal(1, group.NMembers());

        var recursiveKeys = group.Keys(maxDepth: null);
        Assert.Equal(new[] { "a", "a/array", "a/b", "a/b/array" }, recursiveKeys);
        Assert.Equal(4, group.NMembers(maxDepth: null));
        Assert.Equal(4, group.Values(maxDepth: null).Count);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void CreateDatasetAliasesArrayCreationAndDataWrite(ZarrFormat format)
    {
        var store = new MemoryStore();
        var group = ZarrApi.CreateGroup(store, format: format);
        var created = group.CreateDataset<int>("numbers", new[] { 4 }, new[] { 2 }, attributes: new Dictionary<string, object?> { ["kind"] = "empty" });
        var fromData = group.CreateDataset("matrix", new[,] { { 1, 2 }, { 3, 4 } });

        Assert.Equal("numbers", created.Path);
        Assert.Equal("empty", created.Attrs["kind"]);
        Assert.Equal(4, ((int[,])fromData.Read())[1, 1]);
    }
}
