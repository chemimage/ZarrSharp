namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_group.py")]
public sealed class GroupTests
{
    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void CreateArrayCreatesParentGroups(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);

        var root = ZarrApi.CreateGroup(context.Store, "a", format, attributes: new Dictionary<string, object?> { ["key"] = "value" });
        _ = root.CreateArray<int>("b/c/d", new[] { 2, 2 }, new[] { 2, 2 });

        var reopenedRoot = ZarrApi.OpenGroup(context.Store, format: format);
        Assert.Equal("a", reopenedRoot.OpenGroup("a").Path);
        Assert.Equal("value", reopenedRoot.OpenGroup("a").Attrs["key"]);
        Assert.Equal("a/b", reopenedRoot.OpenGroup("a/b").Path);
        Assert.Equal("a/b/c", reopenedRoot.OpenGroup("a/b/c").Path);
        Assert.Equal("a/b/c/d", reopenedRoot.OpenArray("a/b/c/d").Path);

        var keys = context.Store.ListKeys().ToArray();
        if (format == ZarrFormat.V2)
        {
            Assert.Contains(".zgroup", keys);
            Assert.Contains(".zattrs", keys);
            Assert.Contains("a/.zgroup", keys);
            Assert.Contains("a/b/.zgroup", keys);
            Assert.Contains("a/b/c/.zgroup", keys);
            Assert.Contains("a/b/c/d/.zarray", keys);
        }
        else
        {
            Assert.Contains("zarr.json", keys);
            Assert.Contains("a/zarr.json", keys);
            Assert.Contains("a/b/zarr.json", keys);
            Assert.Contains("a/b/c/zarr.json", keys);
            Assert.Contains("a/b/c/d/zarr.json", keys);
        }
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void NamePropertiesAreConsistent(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        Assert.Equal(string.Empty, root.Path);
        Assert.Equal("/", root.Name);
        Assert.Equal(string.Empty, root.Basename);

        var branch = root.CreateGroup("foo/bar");
        Assert.Equal("foo/bar", branch.Path);
        Assert.Equal("/foo/bar", branch.Name);
        Assert.Equal("bar", branch.Basename);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void MembersReturnDirectAndRecursiveNodes(ZarrFormat format)
    {
        var store = new MemoryStore();
        var group = ZarrApi.CreateGroup(store, "group", format);

        group.CreateGroup("subgroup").CreateGroup("subsubgroup").CreateGroup("subsubsubgroup");
        group.CreateArray<byte>("subarray", new[] { 4 }, new[] { 2 });
        store.Set("group/extra_object", new byte[] { 1, 2, 3 });

        var directMembers = group.Members().Select(item => item.Key).ToArray();
        Assert.Equal(new[] { "subarray", "subgroup" }, directMembers);

        var depthOneMembers = group.Members(maxDepth: 1).Select(item => item.Key).ToArray();
        Assert.Equal(new[] { "subarray", "subgroup", "subgroup/subsubgroup" }, depthOneMembers);

        var allMembers = group.Members(maxDepth: null).Select(item => item.Key).ToArray();
        Assert.Equal(new[] { "subarray", "subgroup", "subgroup/subsubgroup", "subgroup/subsubgroup/subsubsubgroup" }, allMembers);
    }
}
