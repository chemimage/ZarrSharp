namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_metadata/test_consolidated.py")]
public sealed class ConsolidatedMetadataTests
{
    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void ConsolidateMetadataWritesManifestAndOpenConsolidatedWorks(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var a = root.CreateGroup("a");
        _ = a.CreateArray<int>("array", new[] { 2 }, new[] { 2 });
        _ = a.CreateGroup("b").CreateArray<int>("array", new[] { 2 }, new[] { 2 });

        var consolidated = ZarrApi.ConsolidateMetadata(store, format: format);
        var manifestKey = format == ZarrFormat.V2 ? ".zmetadata" : "zarr.consolidated.json";
        var payload = store.Get(manifestKey).RequiredArray();
        using var document = System.Text.Json.JsonDocument.Parse(payload);
        var entries = document.RootElement.GetProperty("metadata");
        var reopened = ZarrApi.OpenConsolidated(store, format: format);

        Assert.Equal(root.Path, consolidated.Path);
        Assert.True(store.Exists(manifestKey));
        Assert.True(entries.TryGetProperty(string.Empty, out _));
        Assert.True(entries.TryGetProperty("a", out _));
        Assert.True(entries.TryGetProperty("a/array", out _));
        Assert.True(entries.TryGetProperty("a/b", out _));
        Assert.True(entries.TryGetProperty("a/b/array", out _));
        Assert.Equal(new[] { "/a", "/a/array", "/a/b", "/a/b/array" }, reopened.Members(maxDepth: null).Select(item => item.Value.Name).ToArray());

        if (format == ZarrFormat.V3)
        {
            var inlinePayload = store.Get("zarr.json");
            Assert.NotNull(inlinePayload);

            using var inlineDocument = System.Text.Json.JsonDocument.Parse(inlinePayload.RequiredArray());
            Assert.True(inlineDocument.RootElement.TryGetProperty("consolidated_metadata", out var consolidatedMetadata));
            Assert.Equal("inline", consolidatedMetadata.GetProperty("kind").GetString());
            Assert.False(consolidatedMetadata.GetProperty("must_understand").GetBoolean());
        }
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void ConsolidateMetadataReturnsGroupThatCanStillTraverseAfterChildMetadataDeletion(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var branch = root.CreateGroup("branch");
        var leaf = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 });
        leaf.WriteFlat(new[] { 5, 8 });

        var consolidated = ZarrApi.ConsolidateMetadata(store, format: format);
        DeleteArrayMetadata(store, "branch/leaf", format);

        var reopenedLeaf = Assert.IsType<ZarrArray>(consolidated.GetItem("branch/leaf"));
        Assert.Equal(new[] { 5, 8 }, reopenedLeaf.ReadFlat<int>());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncConsolidatedOpenMatchesSync(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = await ZarrAsync.Group(store, format: format);
        _ = await root.CreateGroupAsync("foo");
        _ = await root.CreateArrayAsync<int>("bar", new[] { 4 }, new[] { 2 });

        _ = await ZarrAsync.ConsolidateMetadata(store, format: format);
        var reopened = await ZarrAsync.OpenConsolidated(store, format: format);
        var members = await reopened.MembersAsync(maxDepth: null);

        Assert.Equal(new[] { "bar", "foo" }, members.Select(item => item.Key).OrderBy(x => x).ToArray());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public async Task AsyncConsolidateMetadataReturnsGroupThatCanStillTraverseAfterChildMetadataDeletion(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = await ZarrAsync.Group(store, format: format);
        var branch = await root.CreateGroupAsync("branch");
        var leaf = await branch.CreateArrayAsync<int>("leaf", new[] { 2 }, new[] { 2 });
        await leaf.WriteFlatAsync(new[] { 13, 21 });

        var consolidated = await ZarrAsync.ConsolidateMetadata(store, format: format);
        DeleteArrayMetadata(store, "branch/leaf", format);

        var reopenedLeaf = Assert.IsType<AsyncZarrArray>(await consolidated.GetItemAsync("branch/leaf"));
        Assert.Equal(new[] { 13, 21 }, (int[])await reopenedLeaf.ReadAsync<int>());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void OpenConsolidatedCanTraverseWhenChildMetadataFilesAreMissing(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var branch = root.CreateGroup("branch");
        var leaf = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 });
        leaf.WriteFlat(new[] { 7, 8 });

        _ = ZarrApi.ConsolidateMetadata(store, format: format);

        if (format == ZarrFormat.V2)
        {
            store.Delete("branch/.zgroup");
            store.Delete("branch/.zattrs");
            store.Delete("branch/leaf/.zarray");
            store.Delete("branch/leaf/.zattrs");
        }
        else
        {
            store.Delete("branch/zarr.json");
            store.Delete("branch/leaf/zarr.json");
        }

        var reopened = ZarrApi.OpenConsolidated(store, format: format);
        var reopenedLeaf = Assert.IsType<ZarrArray>(reopened.GetItem("branch/leaf"));

        Assert.Equal(new[] { "branch", "branch/leaf" }, reopened.Keys(maxDepth: null));
        Assert.Equal(new[] { 7, 8 }, reopenedLeaf.ReadFlat<int>());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void OpenGroupCanPreferConsolidatedMetadataViaConfiguration(ZarrFormat format)
    {
        var original = ZarrConfiguration.PreferConsolidatedMetadata;
        ZarrConfiguration.PreferConsolidatedMetadata = true;

        try
        {
            var store = new MemoryStore();
            var root = ZarrApi.CreateGroup(store, format: format);
            var branch = root.CreateGroup("branch");
            var leaf = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 });
            leaf.WriteFlat(new[] { 3, 4 });

            _ = ZarrApi.ConsolidateMetadata(store, format: format);

            if (format == ZarrFormat.V2)
            {
                store.Delete("branch/.zgroup");
                store.Delete("branch/.zattrs");
                store.Delete("branch/leaf/.zarray");
                store.Delete("branch/leaf/.zattrs");
            }
            else
            {
                store.Delete("branch/zarr.json");
                store.Delete("branch/leaf/zarr.json");
            }

            var reopened = ZarrApi.OpenGroup(store, format: format);
            var reopenedLeaf = Assert.IsType<ZarrArray>(reopened.GetItem("branch/leaf"));

            Assert.Equal(new[] { 3, 4 }, reopenedLeaf.ReadFlat<int>());
        }
        finally
        {
            ZarrConfiguration.PreferConsolidatedMetadata = original;
        }
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void MembersAndTreeCanTraverseChildConsolidatedMetadata(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var branch = root.CreateGroup("branch");
        var leaf = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 });
        leaf.WriteFlat(new[] { 11, 13 });

        _ = ZarrApi.ConsolidateMetadata(store, "branch", format);
        DeleteArrayMetadata(store, "branch/leaf", format);

        var reopened = ZarrApi.OpenGroup(store, format: format, useConsolidated: false);
        var withoutChildConsolidation = reopened.Keys(maxDepth: null, useConsolidatedForChildren: false);
        var withChildConsolidation = reopened.Keys(maxDepth: null, useConsolidatedForChildren: true);
        var tree = reopened.Tree(useConsolidatedForChildren: true);

        Assert.Equal(new[] { "branch" }, withoutChildConsolidation);
        Assert.Equal(new[] { "branch", "branch/leaf" }, withChildConsolidation);
        Assert.Contains("branch/", tree, StringComparison.Ordinal);
        Assert.Contains("leaf (2) int32", tree, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void GroupTraversalUsesConfiguredChildConsolidatedPreference(ZarrFormat format)
    {
        var original = ZarrConfiguration.UseConsolidatedMetadataForChildren;
        ZarrConfiguration.UseConsolidatedMetadataForChildren = false;

        try
        {
            var store = new MemoryStore();
            var root = ZarrApi.CreateGroup(store, format: format);
            var branch = root.CreateGroup("branch");
            _ = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 });

            _ = ZarrApi.ConsolidateMetadata(store, "branch", format);
            DeleteArrayMetadata(store, "branch/leaf", format);

            var reopened = ZarrApi.OpenGroup(store, format: format, useConsolidated: false);

            Assert.Equal(new[] { "branch" }, reopened.Keys(maxDepth: null));

            ZarrConfiguration.UseConsolidatedMetadataForChildren = true;
            Assert.Equal(new[] { "branch", "branch/leaf" }, reopened.Keys(maxDepth: null));
        }
        finally
        {
            ZarrConfiguration.UseConsolidatedMetadataForChildren = original;
        }
    }

    [Fact]
    public void ConsolidatedMetadataPreservesV3GroupExtraFields()
    {
        var store = new MemoryStore();
        store.Set("zarr.json", System.Text.Encoding.UTF8.GetBytes(@"
{
  ""zarr_format"": 3,
  ""node_type"": ""group"",
  ""attributes"": {},
  ""demo_extension"": {
    ""must_understand"": false,
    ""kind"": ""lab""
  }
}"));

        var consolidated = ZarrApi.ConsolidateMetadata(store, format: ZarrFormat.V3);
        var payload = store.Get("zarr.consolidated.json");
        Assert.NotNull(payload);

        using var document = System.Text.Json.JsonDocument.Parse(payload.RequiredArray());
        var rootMetadata = document.RootElement.GetProperty("metadata").GetProperty(string.Empty);

        Assert.True(consolidated.ExtraFields.ContainsKey("demo_extension"));
        Assert.Equal("lab", consolidated.ExtraFields["demo_extension"]["kind"]);
        Assert.False(rootMetadata.GetProperty("demo_extension").GetProperty("must_understand").GetBoolean());
        Assert.Equal("lab", rootMetadata.GetProperty("demo_extension").GetProperty("kind").GetString());
    }

    [Fact]
    public void V3InlineConsolidatedMetadataUsesFlatKeysAndExplicitEmptyChildGroups()
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: ZarrFormat.V3);
        var branch = root.CreateGroup("branch");
        _ = branch.CreateGroup("nested");
        _ = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 });

        _ = ZarrApi.ConsolidateMetadata(store, format: ZarrFormat.V3);
        var payload = store.Get("zarr.json");
        Assert.NotNull(payload);

        using var document = System.Text.Json.JsonDocument.Parse(payload.RequiredArray());
        var metadata = document.RootElement.GetProperty("consolidated_metadata").GetProperty("metadata");

        Assert.True(metadata.TryGetProperty("branch", out var branchMetadata));
        Assert.True(metadata.TryGetProperty("branch/nested", out var nestedMetadata));
        Assert.True(metadata.TryGetProperty("branch/leaf", out _));
        Assert.Equal("inline", branchMetadata.GetProperty("consolidated_metadata").GetProperty("kind").GetString());
        Assert.Empty(branchMetadata.GetProperty("consolidated_metadata").GetProperty("metadata").EnumerateObject());
        Assert.Empty(nestedMetadata.GetProperty("consolidated_metadata").GetProperty("metadata").EnumerateObject());
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void MutatingDescendantsOpenedFromConsolidatedGroupUpdatesLoadedParentView(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var branch = root.CreateGroup("branch");
        _ = branch.CreateGroup("nested");

        _ = ZarrApi.ConsolidateMetadata(store, format: format);

        var reopened = ZarrApi.OpenConsolidated(store, format: format);
        var reopenedBranch = Assert.IsType<ZarrGroup>(reopened.GetItem("branch"));

        _ = reopenedBranch.CreateArray<int>("new_leaf", new[] { 2 }, new[] { 2 });

        var leaf = Assert.IsType<ZarrArray>(reopened.GetItem("branch/new_leaf"));
        Assert.Equal(new[] { "branch", "branch/nested", "branch/new_leaf" }, reopened.Keys(maxDepth: null));
        Assert.Equal(new[] { 2 }, leaf.Shape);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void MetadataMutationsOnConsolidatedChildArraysUpdateLoadedParentView(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var branch = root.CreateGroup("branch");
        var leaf = branch.CreateArray<int>("leaf", new[] { 2 }, new[] { 2 }, attributes: new Dictionary<string, object?> { ["kind"] = "old" });
        leaf.WriteFlat(new[] { 1, 2 });

        _ = ZarrApi.ConsolidateMetadata(store, format: format);

        var reopened = ZarrApi.OpenConsolidated(store, format: format);
        var reopenedLeaf = Assert.IsType<ZarrArray>(reopened.GetItem("branch/leaf"));

        reopenedLeaf.Resize(new[] { 4 });
        reopenedLeaf.UpdateAttributes(new Dictionary<string, object?> { ["kind"] = "new" });

        var leafFromParent = Assert.IsType<ZarrArray>(reopened.GetItem("branch/leaf"));
        Assert.Equal(new[] { 4 }, leafFromParent.Shape);
        Assert.Equal("new", leafFromParent.Attrs["kind"]);
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
