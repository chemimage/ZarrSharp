namespace Zarr.Tests;

public sealed class StorageLayerTests
{
    [Trait("TestBasis", "DotNetImplementation")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_store/test_logging.py")]
    [Fact]
    public async Task LoggingStoreTracksSyncAndAsyncOperations()
    {
        var entries = new List<StoreLogEntry>();
        var store = new LoggingStore(new MemoryStore(), entries.Add);

        store.Set("sync", new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, store.Get("sync")!.Value.ToArray());
        Assert.True(store.Exists("sync"));
        Assert.Single(store.ListKeys());
        store.Delete("sync");

        await store.SetAsync("async", new byte[] { 4, 5, 6 });
        Assert.Equal(new byte[] { 4, 5, 6 }, (await store.GetAsync("async"))!.Value.ToArray());
        Assert.True(await store.ExistsAsync("async"));
        Assert.Single(await store.ListKeysAsync());
        await store.DeleteAsync("async");

        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.Set)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.Get)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.Exists)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.ListKeys)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.Delete)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.SetAsync)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.GetAsync)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.ExistsAsync)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.ListKeysAsync)));
        Assert.Equal(1, store.GetOperationCount(nameof(LoggingStore.DeleteAsync)));
        Assert.Equal(10, entries.Count);
        Assert.All(entries, entry => Assert.True(entry.Duration >= TimeSpan.Zero));
        Assert.Contains(entries, entry => entry.IsAsync && entry.Operation == nameof(LoggingStore.GetAsync));
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public void StorePathOpenModesRespectPrefixState()
    {
        var store = new MemoryStore();
        store.Set("root/foo", new byte[] { 1 });

        Assert.Throws<IOException>(() => StorePath.Open(store, "root", ZarrOpenMode.CreateNew));

        var readOnlyPath = StorePath.Open(store, "root", ZarrOpenMode.Read);
        Assert.True(readOnlyPath.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => readOnlyPath.Set(new byte[] { 9 }));

        var writablePath = StorePath.Open(store, "root", ZarrOpenMode.Create);
        Assert.True(writablePath.IsEmpty());
        writablePath.Child("bar").Set(new byte[] { 2 });

        Assert.Equal(new byte[] { 2 }, store.Get("root/bar")!.Value.ToArray());
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public async Task StoreFactoryCreatesMemoryAndLocalStorePaths()
    {
        using var context = TestStoreContext.Create("local");
        var localRoot = ((LocalStore)context.Store).RootPath;

        var memoryStore = StoreFactory.MakeStore();
        Assert.IsType<MemoryStore>(memoryStore);

        var seededStore = StoreFactory.MakeStore(
            new Dictionary<string, byte[]> { ["seeded/foo"] = new byte[] { 1, 2 } },
            ZarrOpenMode.Read);
        Assert.True(seededStore is IReadOnlyAwareStore { IsReadOnly: true });
        Assert.Equal(new byte[] { 1, 2 }, seededStore.Get("seeded/foo")!.Value.ToArray());

        var localPath = await StoreFactory.MakeStorePathAsync(localRoot, "nested", ZarrOpenMode.Append);
        Assert.IsType<LocalStore>(localPath.Store);
        localPath.Child("value").Set(new byte[] { 7 });

        Assert.Equal(new byte[] { 7 }, localPath.Child("value").Get()!.Value.ToArray());
        Assert.False(StorageUtilities.IsRemoteUri(localRoot));
        Assert.True(StorageUtilities.IsRemoteUri("https://example.test/zarr"));
    }

    [Trait("TestBasis", "PythonReference")]
    [Trait("ReferenceSource", "reference/zarr-python-main/tests/test_store/test_object.py")]
    [Fact]
    public void ObjectStoreSupportsZarrRoundTrips()
    {
        var backend = new InMemoryObjectStoreBackend();
        var store = new ObjectStore(backend, "tenant/demo");

        var array = ZarrApi.SaveArray(new[] { 1, 2, 3, 4 }, store, "data", ZarrFormat.V3, chunks: new[] { 2 });
        var reopened = ZarrApi.OpenArray(store, "data");
        var loaded = (int[])reopened.Read();
        var backendKeys = backend.ListKeysAsync().Result;

        Assert.Equal(new[] { 1, 2, 3, 4 }, loaded);
        Assert.Equal(2, array.InitializedChunkCount);
        Assert.Contains("tenant/demo/data/zarr.json", backendKeys);
        Assert.Contains("data/zarr.json", store.ListKeys());
    }

    [Trait("TestBasis", "DotNetImplementation")]
    [Fact]
    public void StoreFactoryUsesRegisteredUriSchemesForObjectStores()
    {
        var scheme = "test" + Guid.NewGuid().ToString("N");
        var backend = new InMemoryObjectStoreBackend();

        StoreFactory.RegisterUriScheme(
            scheme,
            (uri, options, mode) =>
            {
                Assert.Equal("demo", options?["profile"]);
                var prefix = StorageUtilities.CombinePath(uri.Host, uri.AbsolutePath.Trim('/'));
                return new ObjectStore(backend, prefix, readOnly: mode == ZarrOpenMode.Read);
            });

        try
        {
            var uri = new Uri($"{scheme}://bucket/root");
            var store = StoreFactory.MakeStore(uri, ZarrOpenMode.Append, new Dictionary<string, string> { ["profile"] = "demo" });
            ZarrApi.SaveArray(new[] { 5, 6 }, store, "data");

            var readOnlyPath = StoreFactory.MakeStorePath(uri, "data", ZarrOpenMode.Read, new Dictionary<string, string> { ["profile"] = "demo" });
            Assert.True(readOnlyPath.IsReadOnly);
            Assert.Equal(new[] { 5, 6 }, (int[])ZarrApi.Load(store, "data"));
            Assert.Contains("bucket/root/data/zarr.json", backend.ListKeysAsync().Result);
        }
        finally
        {
            _ = StoreFactory.UnregisterUriScheme(scheme);
        }
    }

}
