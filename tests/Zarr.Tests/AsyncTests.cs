namespace Zarr.Tests;

public sealed class AsyncTests
{
    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api/test_asynchronous.py")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_group.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncGroupOpenGetItemAndDeleteWork(ZarrFormat format)
    {
        var store = new MemoryStore();
        var group = await AsyncZarrGroup.FromStore(store, format, attributes: new Dictionary<string, object?> { ["foo"] = 100 });
        var opened = await AsyncZarrGroup.Open(store, format: format);

        Assert.Equal(100, opened.Attrs["foo"]);

        var array = await opened.CreateArrayAsync<byte>("sub_array", new[] { 4 }, new[] { 2 }, attributes: new Dictionary<string, object?> { ["bar"] = 200 });
        var subgroup = await opened.CreateGroupAsync("sub_group", attributes: new Dictionary<string, object?> { ["baz"] = 300 });

        var arrayItem = await opened.GetItemAsync("sub_array");
        var groupItem = await opened.GetItemAsync("sub_group");

        Assert.IsType<AsyncZarrArray>(arrayItem);
        Assert.IsType<AsyncZarrGroup>(groupItem);
        Assert.Equal(200, array.Attrs["bar"]);
        Assert.Equal(300, subgroup.Attrs["baz"]);

        await opened.DeleteItemAsync("sub_array");
        Assert.False(opened.Group.ContainsItem("sub_array"));
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api/test_asynchronous.py")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_group.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncRequireAndMembersMirrorSyncBehavior(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = await AsyncZarrGroup.FromStore(store, format);
        var foo = await root.RequireGroupAsync("foo");
        foo.Attrs["hello"] = "world";

        var fooAgain = await root.RequireGroupAsync("foo");
        Assert.Equal("world", fooAgain.Attrs["hello"]);

        var arr = await root.RequireArrayAsync<int>("numbers", new[] { 4 }, attributes: new Dictionary<string, object?> { ["kind"] = "data" });
        await arr.WriteFlatAsync(new[] { 1, 2, 3, 4 });

        var members = await root.MembersAsync(maxDepth: null);
        Assert.Equal(new[] { "foo", "numbers" }, members.Select(item => item.Key).OrderBy(x => x).ToArray());
        Assert.Equal(4, ((int[])await arr.ReadAsync<int>())[3]);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public async Task AsyncRequireArrayExactRejectsEquivalentButParameterizedDType()
    {
        var store = new MemoryStore();
        var root = await AsyncZarrGroup.FromStore(store, ZarrFormat.V3);
        _ = await root.CreateArrayAsync("labels", DType.CreateFixedString(8), new[] { 2 }, new[] { 2 });

        var reopened = await root.RequireArrayAsync<string>("labels", new[] { 2 });
        Assert.Equal("fixed_string[8]", reopened.Array.DataType.Signature);

        await Assert.ThrowsAsync<InvalidOperationException>(() => root.RequireArrayAsync<string>("labels", new[] { 2 }, exact: true));
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api/test_asynchronous.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncConvenienceHelpersMirrorSyncApiSurface(ZarrFormat format)
    {
        var sourceStore = new MemoryStore();
        _ = await ZarrAsync.Save(sourceStore, new[] { 1, 2, 3 }, "source", format);

        var destinationStore = new MemoryStore();
        var copied = await ZarrAsync.Copy(sourceStore, destinationStore, "source", "clone", format);
        var hierarchy = await ZarrAsync.CreateHierarchy(
            destinationStore,
            new Dictionary<string, HierarchyNodeDefinition>
            {
                ["group"] = HierarchyNodeDefinition.Group(),
                ["group/data"] = HierarchyNodeDefinition.Array<int>(new[] { 2 }, fillValue: 4),
            },
            format);

        var openedRoot = await ZarrAsync.OpenGroup(destinationStore, format: format);
        var tree = await ZarrAsync.Tree(openedRoot);

        Assert.Equal(new[] { 1, 2, 3 }, (await ZarrAsync.OpenArray(destinationStore, "clone", format)).Array.ReadFlat<int>());
        Assert.True(copied.KeysCopied > 0);
        Assert.Contains("clone (3) int32", tree);
        Assert.Contains("group/", tree);
        Assert.Contains("group", hierarchy.Keys);
        Assert.Contains("group/data", hierarchy.Keys);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api/test_asynchronous.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncArrayConvenienceAliasesSupportCreateAndLikeFlows(ZarrFormat format)
    {
        var store = new MemoryStore();
        var source = await ZarrAsync.Array(new[] { 1, 2, 3 }, store, "source", format);
        var empty = await ZarrAsync.Empty<int>(store, new[] { 3 }, path: "empty", format: format);
        var ones = await ZarrAsync.Ones<int>(store, new[] { 2 }, path: "ones", format: format);
        var clone = await ZarrAsync.EmptyLike(source, store, "clone");

        await clone.WriteFlatAsync(new[] { 4, 5, 6 });
        var reopened = await ZarrAsync.OpenLike(source, store, "clone");

        Assert.Equal(new[] { 1, 2, 3 }, (int[])await source.ReadAsync<int>());
        Assert.Equal(new[] { 0, 0, 0 }, (int[])await empty.ReadAsync<int>());
        Assert.Equal(new[] { 1, 1 }, (int[])await ones.ReadAsync<int>());
        Assert.Equal(new[] { 4, 5, 6 }, (int[])await reopened.ReadAsync<int>());
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_api/test_asynchronous.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncLoadCanReturnNestedDictionaryAndVariadicSaveUsesPositionalNames(ZarrFormat format)
    {
        var store = new MemoryStore();
        var saved = Assert.IsType<AsyncZarrGroup>(await ZarrAsync.Save(
            store,
            path: "bundle",
            format: format,
            arrays: new Array[]
            {
                new[] { 2, 4, 6 },
                new[,] { { 8, 10 } },
            }));
        var nested = await saved.CreateGroupAsync("nested");
        var leaf = await nested.CreateArrayAsync<int>("leaf", new[] { 2 }, new[] { 2 });
        await leaf.WriteFlatAsync(new[] { 12, 14 });

        var loaded = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(await ZarrAsync.Load(store, "bundle", format));
        var nestedLoaded = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(loaded["nested"]);

        Assert.Equal(new[] { 2, 4, 6 }, (int[])loaded["arr_0"]);
        Assert.Equal(8, ((int[,])loaded["arr_1"])[0, 0]);
        Assert.Equal(10, ((int[,])loaded["arr_1"])[0, 1]);
        Assert.Equal(new[] { 12, 14 }, (int[])nestedLoaded["leaf"]);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_sync.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncArrayLifecycleHelpersExposeResizeAppendAndInfo(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = await ZarrAsync.CreateArray<int>(store, new[] { 2 }, new[] { 1 }, "values", format);
        await array.WriteFlatAsync(new[] { 1, 2 });

        await array.AppendAsync(new[] { 3, 4 });
        await array.ResizeAsync(new[] { 3 });
        var info = await array.InfoAsync();

        Assert.Equal(new[] { 1, 2, 3 }, array.Array.ReadFlat<int>());
        Assert.Contains("Chunks Initialized", info.ToString());
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public async Task AsyncApisPreferIAsyncStoreOverSyncStoreCalls()
    {
        var store = new CountingAsyncStore();

        var group = await ZarrAsync.CreateGroup(store);
        var array = await group.CreateArrayAsync<int>("values", new[] { 2 }, new[] { 2 });
        await array.WriteFlatAsync(new[] { 5, 6 });
        var reopened = await ZarrAsync.OpenArray(store, "values", ZarrFormat.V3);
        var values = (int[])await reopened.ReadAsync<int>();

        Assert.Equal(new[] { 5, 6 }, values);
        Assert.True(store.AsyncGetCalls > 0);
        Assert.True(store.AsyncSetCalls > 0);
        Assert.True(store.AsyncExistsCalls > 0);
        AssertNoSyncCalls(store);
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public async Task RemainingAsyncApiHelpersPreferIAsyncStoreOverSyncFallbacks()
    {
        var sourceStore = new CountingAsyncStore();
        var copyStore = new CountingAsyncStore();
        var copyAllStore = new CountingAsyncStore();
        var rawCopyStore = new CountingAsyncStore();
        var migrationStore = new CountingAsyncStore();

        var root = await ZarrAsync.Group(sourceStore);
        var branch = await root.CreateGroupAsync("branch");
        var array = await branch.CreateArrayAsync<int>("values", new[] { 2 }, new[] { 1 });
        await array.WriteFlatAsync(new[] { 4, 5 });

        _ = await ZarrAsync.ConsolidateMetadata(sourceStore);
        var consolidated = await ZarrAsync.OpenConsolidated(sourceStore);
        var tree = await ZarrAsync.Tree(consolidated);
        var copied = await ZarrAsync.Copy(sourceStore, copyStore, "branch", "clone");
        var copiedAll = await ZarrAsync.CopyAll(sourceStore, copyAllStore, destinationPath: "mirror");
        var copiedStore = await ZarrAsync.CopyStore(sourceStore, rawCopyStore, "branch", "snapshot");
        var created = await ZarrAsync.CreateHierarchy(
            copyStore,
            new Dictionary<string, HierarchyNodeDefinition>
            {
                ["extra"] = HierarchyNodeDefinition.Group(),
                ["extra/data"] = HierarchyNodeDefinition.Array<int>(new[] { 1 }, fillValue: 9),
            });
        var migrated = await ZarrAsync.Migrate(sourceStore, migrationStore, ZarrFormat.V2, "branch", "branch_v2");

        Assert.Contains("branch/", tree);
        Assert.True(copied.KeysCopied > 0);
        Assert.True(copiedAll.KeysCopied > 0);
        Assert.True(copiedStore.KeysCopied > 0);
        Assert.Contains("extra/data", created.Keys);
        Assert.IsType<AsyncZarrGroup>(migrated);
        Assert.Equal(new[] { 4, 5 }, (int[])await (await ZarrAsync.OpenArray(copyStore, "clone/values", ZarrFormat.V3)).ReadAsync<int>());
        Assert.Equal(new[] { 4, 5 }, (int[])await (await ZarrAsync.OpenArray(copyAllStore, "mirror/branch/values", ZarrFormat.V3)).ReadAsync<int>());
        Assert.Equal(new[] { 4, 5 }, (int[])await (await ZarrAsync.OpenArray(rawCopyStore, "snapshot/values", ZarrFormat.V3)).ReadAsync<int>());
        Assert.Equal(new[] { 4, 5 }, (int[])await (await ZarrAsync.OpenArray(migrationStore, "branch_v2/values", ZarrFormat.V2)).ReadAsync<int>());

        AssertNoSyncCalls(sourceStore);
        AssertNoSyncCalls(copyStore);
        AssertNoSyncCalls(copyAllStore);
        AssertNoSyncCalls(rawCopyStore);
        AssertNoSyncCalls(migrationStore);
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_consolidated.py")]
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncMembersCanTraverseChildConsolidatedMetadata(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var branch = root.CreateGroup("branch");
        var leaf = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 });
        leaf.WriteFlat(new[] { 21, 34 });

        _ = ZarrApi.ConsolidateMetadata(store, "branch", format);
        DeleteArrayMetadata(store, "branch/leaf", format);

        var reopened = await ZarrAsync.OpenGroup(store, format: format, useConsolidated: false);
        var withoutChildConsolidation = await reopened.MembersAsync(maxDepth: null, useConsolidatedForChildren: false);
        var withChildConsolidation = await reopened.MembersAsync(maxDepth: null, useConsolidatedForChildren: true);
        var tree = await reopened.TreeAsync(useConsolidatedForChildren: true);

        Assert.Equal(new[] { "branch" }, withoutChildConsolidation.Select(item => item.Key).ToArray());
        Assert.Equal(new[] { "branch", "branch/leaf" }, withChildConsolidation.Select(item => item.Key).ToArray());
        Assert.Contains("leaf (2) int32", tree, StringComparison.Ordinal);
    }

    private sealed class CountingAsyncStore : IStore, IAsyncStore
    {
        private readonly MemoryStore _inner = new();

        public int SyncGetCalls { get; private set; }

        public int SyncSetCalls { get; private set; }

        public int SyncExistsCalls { get; private set; }

        public int SyncDeleteCalls { get; private set; }

        public int SyncDeletePrefixCalls { get; private set; }

        public int SyncListCalls { get; private set; }

        public int AsyncGetCalls { get; private set; }

        public int AsyncSetCalls { get; private set; }

        public int AsyncExistsCalls { get; private set; }

        public int AsyncDeleteCalls { get; private set; }

        public int AsyncDeletePrefixCalls { get; private set; }

        public int AsyncListCalls { get; private set; }

        public ReadOnlyMemory<byte>? Get(string key)
        {
            SyncGetCalls++;
            return _inner.Get(key);
        }

        public void Set(string key, ReadOnlyMemory<byte> value)
        {
            SyncSetCalls++;
            _inner.Set(key, value);
        }

        public bool Exists(string key)
        {
            SyncExistsCalls++;
            return _inner.Exists(key);
        }

        public void Delete(string key)
        {
            SyncDeleteCalls++;
            _inner.Delete(key);
        }

        public void DeletePrefix(string prefix)
        {
            SyncDeletePrefixCalls++;
            _inner.DeletePrefix(prefix);
        }

        public IEnumerable<string> ListKeys(string prefix = "")
        {
            SyncListCalls++;
            return _inner.ListKeys(prefix);
        }

        public ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            AsyncGetCalls++;
            return _inner.GetAsync(key, cancellationToken);
        }

        public ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            AsyncSetCalls++;
            return _inner.SetAsync(key, value, cancellationToken);
        }

        public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            AsyncExistsCalls++;
            return _inner.ExistsAsync(key, cancellationToken);
        }

        public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            AsyncDeleteCalls++;
            return _inner.DeleteAsync(key, cancellationToken);
        }

        public ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            AsyncDeletePrefixCalls++;
            return _inner.DeletePrefixAsync(prefix, cancellationToken);
        }

        public ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default)
        {
            AsyncListCalls++;
            return _inner.ListKeysAsync(prefix, cancellationToken);
        }
    }

    private static void AssertNoSyncCalls(CountingAsyncStore store)
    {
        Assert.Equal(0, store.SyncGetCalls);
        Assert.Equal(0, store.SyncSetCalls);
        Assert.Equal(0, store.SyncExistsCalls);
        Assert.Equal(0, store.SyncDeleteCalls);
        Assert.Equal(0, store.SyncDeletePrefixCalls);
        Assert.Equal(0, store.SyncListCalls);
    }

    private static void DeleteArrayMetadata(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = path.Trim('/');
        switch (format)
        {
            case ZarrFormat.V2:
                store.Delete($"{normalizedPath}/.zarray");
                store.Delete($"{normalizedPath}/.zattrs");
                break;

            case ZarrFormat.V3:
                store.Delete($"{normalizedPath}/zarr.json");
                break;
        }
    }
}
