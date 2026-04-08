namespace Zarr.Tests;

[Trait("TestBasis", "DotNetImplementation")]
public sealed class FoundationTests
{
    [Fact]
    public void DTypeRegistryResolvesBuiltInAliasesAndCustomProviders()
    {
        Assert.True(DTypeRegistry.TryFromName("u1", out var unsignedByte));
        Assert.Equal(DType.UInt8.Signature, unsignedByte.Signature);

        DTypeRegistry.RegisterProvider(new AliasDTypeProvider());

        Assert.True(DTypeRegistry.TryFromName("uuid_alias", out var guidAlias));
        Assert.Equal(DType.Guid.Signature, guidAlias.Signature);
    }

    [Fact]
    public void StorePathRoundTripsWithMemoryStore()
    {
        var store = new MemoryStore();
        var path = new StorePath(store, "payload");

        path.Set(new byte[] { 1, 2, 3, 4 });
        var payload = path.Get();

        Assert.NotNull(payload);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, payload!.Value.ToArray());
    }

    [Fact]
    public void StoreFactoryCanOpenTypedProviderOptions()
    {
        StoreProviderRegistry.RegisterOptions<FakeStoreOptions>((options, mode) =>
            new ObjectStore(new InMemoryObjectStoreBackend(), prefix: options.Prefix, readOnly: mode == ZarrOpenMode.Read));

        var store = StoreFactory.MakeStore(new FakeStoreOptions("typed"), ZarrOpenMode.Append);

        store.Set("a", new byte[] { 9 });

        Assert.True(store.Exists("a"));
        Assert.IsType<ObjectStore>(store);
    }

    [Fact]
    public void ChunkGridRegistryCanRegisterCustomFactory()
    {
        ChunkGridRegistry.Register("demo-grid", configuration => new NamedChunkGrid("demo-grid", configuration));

        Assert.True(ChunkGridRegistry.TryCreate(
            "demo-grid",
            new Dictionary<string, object?> { ["chunk_shape"] = new object?[] { 2, 2 } },
            out var grid));
        Assert.Equal("demo-grid", grid.Name);
    }

    private sealed class AliasDTypeProvider : IDTypeProvider
    {
        public bool TryFromType(Type type, out DType dataType)
        {
            dataType = null!;
            return false;
        }

        public bool TryFromName(string name, out DType dataType)
        {
            if (string.Equals(name, "uuid_alias", StringComparison.Ordinal))
            {
                dataType = DType.Guid;
                return true;
            }

            dataType = null!;
            return false;
        }

        public bool TryFromMetadata(object? metadata, ZarrFormat format, out DType dataType)
        {
            dataType = null!;
            return false;
        }
    }

    private sealed record FakeStoreOptions(string Prefix);
}
