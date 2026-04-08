namespace Zarr.Tests;

[Trait("TestBasis", "PythonReference")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_array.py")]
[Trait("ReferenceSource", "reference/zarr-python-main/tests/test_v2.py")]
public sealed class ArrayTests
{
    [Theory]
    [InlineData(ZarrFormat.V2, false, "array")]
    [InlineData(ZarrFormat.V2, false, "group")]
    [InlineData(ZarrFormat.V2, true, "array")]
    [InlineData(ZarrFormat.V2, true, "group")]
    [InlineData(ZarrFormat.V3, false, "array")]
    [InlineData(ZarrFormat.V3, false, "group")]
    [InlineData(ZarrFormat.V3, true, "array")]
    [InlineData(ZarrFormat.V3, true, "group")]
    public void ArrayCreationRespectsExistingNodes(ZarrFormat format, bool overwrite, string extantNode)
    {
        var store = new MemoryStore();
        var group = ZarrApi.CreateGroup(store, format: format);

        if (extantNode == "array")
        {
            _ = group.CreateArray<int>("extant", new[] { 4 }, new[] { 2 });
        }
        else
        {
            _ = group.CreateGroup("extant");
        }

        if (!overwrite)
        {
            if (extantNode == "array")
            {
                Assert.Throws<ContainsArrayException>(() => ZarrApi.CreateArray<int>(store, new[] { 2, 2 }, new[] { 1, 1 }, "extant", format));
            }
            else
            {
                Assert.Throws<ContainsGroupException>(() => ZarrApi.CreateArray<int>(store, new[] { 2, 2 }, new[] { 1, 1 }, "extant", format));
            }

            return;
        }

        var replacement = ZarrApi.CreateArray<int>(store, new[] { 2, 2 }, new[] { 1, 1 }, "extant", format, overwrite: true);
        Assert.Equal(new[] { 2, 2 }, replacement.Shape);
        Assert.Equal("int32", replacement.DataType.Name);
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void ArrayNamePropertiesMatchItsPath(ZarrFormat format)
    {
        var store = new MemoryStore();
        var root = ZarrApi.CreateGroup(store, format: format);
        var array = root.CreateArray<int>("foo", new[] { 4 }, new[] { 2 });
        Assert.Equal("foo", array.Path);
        Assert.Equal("/foo", array.Name);
        Assert.Equal("foo", array.Basename);
    }

    [Theory]
    [InlineData("memory", ZarrFormat.V2)]
    [InlineData("memory", ZarrFormat.V3)]
    [InlineData("local", ZarrFormat.V2)]
    [InlineData("local", ZarrFormat.V3)]
    public void ReadWriteFlatValuesRoundTrip(string storeKind, ZarrFormat format)
    {
        using var context = TestStoreContext.Create(storeKind);
        var array = ZarrApi.CreateArray<int>(context.Store, new[] { 2, 3 }, new[] { 2, 2 }, format: format);

        array.WriteFlat(new[] { 1, 2, 3, 4, 5, 6 });
        var reopened = ZarrApi.OpenArray(context.Store, format: format);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, reopened.ReadFlat<int>());
        Assert.Equal(5, reopened.GetValue<int>(1, 1));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void MissingChunksReturnFillValueAndSetValuePersists(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(store, new[] { 4, 4 }, new[] { 2, 2 }, format: format, fillValue: 7);

        Assert.Equal(7, array.GetValue<int>(3, 3));

        array.SetValue(9, 3, 3);
        var reopened = ZarrApi.OpenArray(store, format: format);

        Assert.Equal(9, reopened.GetValue<int>(3, 3));
        Assert.Equal(7, reopened.GetValue<int>(0, 0));
    }

    [Theory]
    [InlineData(ZarrFormat.V2)]
    [InlineData(ZarrFormat.V3)]
    public void MultiDimensionalWriteAcceptsNativeArrays(ZarrFormat format)
    {
        var store = new MemoryStore();
        var array = ZarrApi.CreateArray<int>(store, new[] { 2, 2 }, new[] { 1, 2 }, format: format);

        array.Write(new[,] { { 1, 2 }, { 3, 4 } });
        var observed = (int[,])array.Read<int>();

        Assert.Equal(1, observed[0, 0]);
        Assert.Equal(2, observed[0, 1]);
        Assert.Equal(3, observed[1, 0]);
        Assert.Equal(4, observed[1, 1]);
    }
}
