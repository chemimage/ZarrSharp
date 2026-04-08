namespace Zarr.Tests;

public sealed class ApiTests
{
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api.py")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api/test_synchronous.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void SaveLoadAndFromArrayRoundTrip(ZarrFormat format)
    {
        var store = new MemoryStore();
        var source = new[,] { { 1, 2 }, { 3, 4 } };

        var saved = ZarrApi.SaveArray(source, store, "data", format);
        var loaded = (int[,])ZarrApi.Load(store, "data", format);

        Assert.Equal("data", saved.Path);
        Assert.Equal(1, loaded[0, 0]);
        Assert.Equal(2, loaded[0, 1]);
        Assert.Equal(3, loaded[1, 0]);
        Assert.Equal(4, loaded[1, 1]);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api/test_synchronous.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void LikeHelpersCanOverrideShapeChunksAndType(ZarrFormat format)
    {
        var store = new MemoryStore();
        var reference = ZarrApi.Ones<byte>(store, new[] { 11, 12 }, new[] { 11, 12 }, "reference", format);

        var zeros = ZarrApi.ZerosLike(reference, store, "zeros", shape: new[] { 10, 10 }, chunks: new[] { 5, 5 }, dtype: typeof(int), overwrite: true);
        var ones = ZarrApi.OnesLike(reference, store, "ones", overwrite: true);
        var full = ZarrApi.FullLike(reference, store, 4, "full", overwrite: true);
        var opened = ZarrApi.OpenLike(reference, store, "opened", fillValue: 9, overwrite: true);

        Assert.Equal(new[] { 10, 10 }, zeros.Shape);
        Assert.Equal(new[] { 5, 5 }, zeros.Chunks);
        Assert.Equal("int32", zeros.DataType.Name);
        Assert.Equal(0, zeros.GetValue<int>(0, 0));

        Assert.Equal(1, ones.GetValue<byte>(0, 0));
        Assert.Equal(4, full.GetValue<byte>(0, 0));
        Assert.Equal(9, opened.GetValue<byte>(0, 0));
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_group.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void GroupConvenienceAndRequireHelpersWork(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.Group(store, format: format);

        var foo = root.RequireGroup("foo");
        foo.Attrs["foo"] = 100;
        var fooAgain = root.RequireGroup("foo");
        Assert.Equal(100, fooAgain.Attrs["foo"]);

        var fooOverwrite = root.RequireGroup("foo", overwrite: true);
        Assert.Empty(fooOverwrite.Attrs);

        var array1 = root.RequireArray<long>("values", new[] { 4 }, attributes: new Dictionary<string, object?> { ["flag"] = true });
        var array2 = root.RequireArray<long>("values", new[] { 4 });
        Assert.Equal(array1.Path, array2.Path);
        Assert.True((bool)array1.Attrs["flag"]!);

        Assert.Throws<InvalidOperationException>(() => root.RequireArray<int>("values", new[] { 5 }));
        Assert.Throws<InvalidOperationException>(() => root.RequireArray<int>("values", new[] { 4 }, exact: true));
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public void RequireArrayExactRejectsEquivalentButParameterizedDType()
    {
        var store = new MemoryStore();
        var root = ZarrApi.Group(store, format: ZarrFormat.V3);
        _ = root.CreateArray("labels", DType.CreateFixedString(8), new[] { 2 }, new[] { 2 });

        var reopened = root.RequireArray<string>("labels", new[] { 2 });
        Assert.Equal("fixed_string[8]", reopened.DataType.Signature);

        Assert.Throws<InvalidOperationException>(() => root.RequireArray<string>("labels", new[] { 2 }, exact: true));
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_group.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void GroupArrayCreationHelpersMirrorReferenceShapeAndFill(ZarrFormat format)
    {
        var store = new MemoryStore();
        var group = ZarrApi.Group(store, format: format);

        var empty = group.Empty<int>("empty", new[] { 2, 2 });
        var zeros = group.Zeros<int>("zeros", new[] { 2, 2 });
        var ones = group.Ones<int>("ones", new[] { 2, 2 });
        var full = group.Full("full", new[] { 2, 2 }, 42);

        var emptyLike = group.EmptyLike("empty_like", empty);
        var zerosLike = group.ZerosLike("zeros_like", zeros);
        var onesLike = group.OnesLike("ones_like", ones);
        var fullLike = group.FullLike("full_like", full, 43);

        Assert.Equal(0, empty.GetValue<int>(0, 0));
        Assert.Equal(0, zerosLike.GetValue<int>(0, 0));
        Assert.Equal(1, onesLike.GetValue<int>(0, 0));
        Assert.Equal(42, full.GetValue<int>(0, 0));
        Assert.Equal(43, fullLike.GetValue<int>(0, 0));
        Assert.Equal(new[] { 2, 2 }, emptyLike.Shape);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void SaveAndSaveGroupConvenienceHelpersWork(ZarrFormat format)
    {
        var store = new MemoryStore();

        var single = ZarrApi.Save(store, new[,] { { 1, 2 }, { 3, 4 } }, "single", format);
        var bundle = ZarrApi.SaveGroup(
            store,
            new Dictionary<string, Array>
            {
                ["alpha"] = new[] { 5, 6, 7 },
                ["beta"] = new[,] { { 8, 9 } },
            },
            "bundle",
            format,
            attributes: new Dictionary<string, object?> { ["kind"] = "group" });

        Assert.Equal("single", single.Path);
        Assert.Equal("group", bundle.Attrs["kind"]);

        var alpha = ZarrApi.OpenArray(store, "bundle/alpha", format);
        var beta = ZarrApi.OpenArray(store, "bundle/beta", format);

        Assert.Equal(new[] { 5, 6, 7 }, alpha.ReadFlat<int>());
        Assert.Equal(8, beta.GetValue<int>(0, 0));
        Assert.Equal(9, beta.GetValue<int>(0, 1));
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void LoadCanReturnNestedDictionaryForGroupsAndVariadicSaveUsesPositionalNames(ZarrFormat format)
    {
        var store = new MemoryStore();
        var saved = Assert.IsType<ZarrGroup>(ZarrApi.Save(
            store,
            path: "bundle",
            format: format,
            arrays: new Array[]
            {
                new[] { 1, 2, 3 },
                new[,] { { 4, 5 } },
            }));
        var branch = saved.CreateGroup("nested");
        var leaf = branch.CreateArray<int>("leaf", new[] { 2 });
        leaf.WriteFlat(new[] { 6, 7 });

        var loaded = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(ZarrApi.Load(store, "bundle", format));
        var nested = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(loaded["nested"]);

        Assert.Equal(new[] { 1, 2, 3 }, (int[])loaded["arr_0"]);
        Assert.Equal(4, ((int[,])loaded["arr_1"])[0, 0]);
        Assert.Equal(5, ((int[,])loaded["arr_1"])[0, 1]);
        Assert.Equal(new[] { 6, 7 }, (int[])nested["leaf"]);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_tree.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void TreeRendersHierarchyStructure(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.Group(store, format: format);
        var branch = root.CreateGroup("branch");
        _ = branch.CreateArray<int>("leaf", new[] { 2 });
        _ = root.CreateArray<int>("top", new[] { 1 });

        var tree = ZarrApi.Tree(root);

        Assert.Contains("/", tree);
        Assert.Contains("branch/", tree);
        Assert.Contains("leaf (2) int32", tree);
        Assert.Contains("top (1) int32", tree);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void CopyDuplicatesNodeSubtreesAcrossStores(ZarrFormat format)
    {
        var sourceStore = new MemoryStore();
        var sourceRoot = ZarrApi.Group(sourceStore, format: format);
        var sourceGroup = sourceRoot.CreateGroup("source", attributes: new Dictionary<string, object?> { ["label"] = "copied" });
        var array = sourceGroup.CreateArray<int>("values", new[] { 4 }, new[] { 2 });
        array.WriteFlat(new[] { 1, 2, 3, 4 });

        var destinationStore = new MemoryStore();
        var summary = ZarrApi.Copy(sourceStore, destinationStore, "source", "clone", format);

        var clonedGroup = ZarrApi.OpenGroup(destinationStore, "clone", format);
        var clonedArray = ZarrApi.OpenArray(destinationStore, "clone/values", format);

        Assert.Equal("copied", clonedGroup.Attrs["label"]);
        Assert.Equal(new[] { 1, 2, 3, 4 }, clonedArray.ReadFlat<int>());
        Assert.True(summary.NodesCopied >= 2);
        Assert.True(summary.KeysCopied >= 3);
        Assert.True(summary.BytesCopied > 0);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void CopyAllCopiesDirectChildrenIntoDestinationGroup(ZarrFormat format)
    {
        var sourceStore = new MemoryStore();
        var sourceRoot = ZarrApi.Group(sourceStore, format: format);
        _ = sourceRoot.CreateGroup("nested");
        var array = sourceRoot.CreateArray<int>("values", new[] { 3 });
        array.WriteFlat(new[] { 9, 8, 7 });

        var destinationStore = new MemoryStore();
        _ = ZarrApi.Group(destinationStore, "target", format);

        var summary = ZarrApi.CopyAll(sourceStore, destinationStore, destinationPath: "target", format: format);

        Assert.EndsWith("nested", ZarrApi.OpenGroup(destinationStore, "target/nested", format).Path);
        Assert.Equal(new[] { 9, 8, 7 }, ZarrApi.OpenArray(destinationStore, "target/values", format).ReadFlat<int>());
        Assert.True(summary.NodesCopied >= 2);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void CopyStoreCopiesRawPrefixEntries(ZarrFormat format)
    {
        var sourceStore = new MemoryStore();
        _ = ZarrApi.SaveArray(new[] { 1, 3, 5, 7 }, sourceStore, "data", format, chunks: new[] { 2 });

        var destinationStore = new MemoryStore();
        var summary = ZarrApi.CopyStore(sourceStore, destinationStore, "data", "snapshot");

        var copied = ZarrApi.OpenArray(destinationStore, "snapshot", format);

        Assert.Equal(new[] { 1, 3, 5, 7 }, copied.ReadFlat<int>());
        Assert.True(summary.KeysCopied > 0);
        Assert.True(summary.BytesCopied > 0);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void CreateHierarchyCreatesImplicitGroupsAndRequestedNodes(ZarrFormat format)
    {
        var store = new MemoryStore();
        var created = ZarrApi.CreateHierarchy(
            store,
            new Dictionary<string, HierarchyNodeDefinition>
            {
                ["experiments"] = HierarchyNodeDefinition.Group(new Dictionary<string, object?> { ["team"] = "qa" }),
                ["experiments/run-01/data"] = HierarchyNodeDefinition.Array<int>(
                    new[] { 2, 2 },
                    chunks: new[] { 1, 2 },
                    fillValue: 6,
                    attributes: new Dictionary<string, object?> { ["kind"] = "matrix" }),
            },
            format);

        Assert.Contains(string.Empty, created.Keys);
        Assert.Contains("experiments/run-01", created.Keys);
        Assert.Equal("qa", ((ZarrGroup)created["experiments"]).Attrs["team"]);

        var reopened = ZarrApi.OpenArray(store, "experiments/run-01/data", format);
        Assert.Equal(new[] { 1, 2 }, reopened.Chunks);
        Assert.Equal(6, reopened.GetValue<int>(1, 1));
        Assert.Equal("matrix", reopened.Attrs["kind"]);
    }
}
