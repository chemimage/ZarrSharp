namespace Zarr.Tests;

[Trait("TestBasis", "DotNetImplementation")]
public sealed class OpenModeTests
{
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void OpenGroupAppendCreateAndCreateNewFollowExpectedSemantics(ZarrFormat format)
    {
        var store = new MemoryStore();

        var created = ZarrApi.OpenGroup(store, "root", ZarrOpenMode.Append, format);
        var reopened = ZarrApi.OpenGroup(store, "root", ZarrOpenMode.ReadWrite, format);

        Assert.Equal("root", created.Path);
        Assert.Equal(created.Path, reopened.Path);
        Assert.Throws<ContainsGroupException>(() => ZarrApi.OpenGroup(store, "root", ZarrOpenMode.CreateNew, format));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void OpenArrayAppendCanCreateAndReadModeWrapsStoreAsReadOnly(ZarrFormat format)
    {
        var store = new MemoryStore();
        var created = ZarrApi.OpenArray<int>(store, "values", ZarrOpenMode.Append, shape: new[] { 2 }, chunks: new[] { 2 }, format: format);
        created.WriteFlat(new[] { 1, 2 });

        var readOnly = ZarrApi.OpenArray<int>(store, "values", ZarrOpenMode.Read, format: format);
        Assert.Equal(new[] { 1, 2 }, readOnly.ReadFlat<int>());
        Assert.Throws<InvalidOperationException>(() => readOnly.WriteFlat(new[] { 3, 4 }));

        var overwrite = ZarrApi.OpenArray<int>(store, "values", ZarrOpenMode.Create, shape: new[] { 2 }, chunks: new[] { 2 }, format: format, fillValue: 9);
        Assert.Equal(new[] { 9, 9 }, overwrite.ReadFlat<int>());
        Assert.Throws<ContainsArrayException>(() => ZarrApi.OpenArray<int>(store, "values", ZarrOpenMode.CreateNew, shape: new[] { 2 }, chunks: new[] { 2 }, format: format));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void TopLevelOpenUsesFileModeSemanticsForGroupsAndArrays(ZarrFormat format)
    {
        var store = new MemoryStore();

        var createdGroup = Assert.IsType<ZarrGroup>(ZarrApi.Open(store, "root", ZarrOpenMode.Append, format));
        var sourceArray = ZarrApi.CreateArray<int>(store, new[] { 2 }, new[] { 2 }, "values", format, fillValue: 9);
        sourceArray.WriteFlat(new[] { 1, 2 });

        var reopenedArray = Assert.IsType<ZarrArray>(ZarrApi.Open(store, "values", ZarrOpenMode.ReadWrite, format));
        Assert.Equal(new[] { 1, 2 }, reopenedArray.ReadFlat<int>());

        var recreatedArray = Assert.IsType<ZarrArray>(ZarrApi.Open(store, "values", ZarrOpenMode.Create, format));

        Assert.Equal("root", createdGroup.Path);
        Assert.Equal(new[] { 9, 9 }, recreatedArray.ReadFlat<int>());
        Assert.Throws<NodeNotFoundException>(() => ZarrApi.Open(store, "missing", ZarrOpenMode.Read, format));
        Assert.Throws<ContainsArrayException>(() => ZarrApi.Open(store, "values", ZarrOpenMode.CreateNew, format));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void OpenLikeReopensExistingArraysWhenOverwriteIsFalse(ZarrFormat format)
    {
        var store = new MemoryStore();
        var reference = ZarrApi.CreateArray<int>(store, new[] { 2 }, new[] { 2 }, "reference", format, fillValue: 7);
        var existing = ZarrApi.CreateArray<int>(store, new[] { 2 }, new[] { 2 }, "target", format, fillValue: 5);
        existing.WriteFlat(new[] { 3, 4 });

        var reopened = ZarrApi.OpenLike(reference, store, "target");

        Assert.Equal(new[] { 3, 4 }, reopened.ReadFlat<int>());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncOpenModesMirrorSyncBehavior(ZarrFormat format)
    {
        var store = new MemoryStore();
        var created = await ZarrAsync.OpenArray<int>(store, "values", ZarrOpenMode.Append, shape: new[] { 2 }, chunks: new[] { 2 }, format: format);
        await created.WriteFlatAsync(new[] { 7, 8 });

        var readOnly = await ZarrAsync.OpenArray<int>(store, "values", ZarrOpenMode.Read, format: format);
        var values = (int[])await readOnly.ReadAsync<int>();

        Assert.Equal(new[] { 7, 8 }, values);
        await Assert.ThrowsAsync<InvalidOperationException>(() => readOnly.WriteFlatAsync(new[] { 1, 2 }));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncTopLevelOpenAndOpenLikeMirrorSyncConvenience(ZarrFormat format)
    {
        var store = new MemoryStore();
        _ = Assert.IsType<AsyncZarrGroup>(await ZarrAsync.Open(store, "root", ZarrOpenMode.Append, format));

        var reference = await ZarrAsync.CreateArray<int>(store, new[] { 2 }, new[] { 2 }, "reference", format, fillValue: 8);
        var existing = await ZarrAsync.CreateArray<int>(store, new[] { 2 }, new[] { 2 }, "target", format, fillValue: 5);
        await existing.WriteFlatAsync(new[] { 6, 7 });

        var reopened = Assert.IsType<AsyncZarrArray>(await ZarrAsync.Open(store, "target", ZarrOpenMode.ReadWrite, format));
        var reopenedLike = await ZarrAsync.OpenLike(reference, store, "target");

        Assert.Equal(new[] { 6, 7 }, (int[])await reopened.ReadAsync<int>());
        Assert.Equal(new[] { 6, 7 }, (int[])await reopenedLike.ReadAsync<int>());
    }
}
