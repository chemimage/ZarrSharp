namespace Zarr.Tests;

internal sealed class TestStoreContext : IDisposable
{
    private readonly string? _directory;

    private TestStoreContext(IStore store, string? directory)
    {
        Store = store;
        _directory = directory;
    }

    public IStore Store { get; }

    public static TestStoreContext Create(string kind)
    {
        return kind switch
        {
            "memory" => new TestStoreContext(new MemoryStore(), null),
            "local" => CreateLocal(),
            "zip" => CreateZip(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown store kind."),
        };
    }

    public void Dispose()
    {
        if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static TestStoreContext CreateLocal()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zarrnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new TestStoreContext(new LocalStore(directory), directory);
    }

    private static TestStoreContext CreateZip()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zarrnet-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var zipPath = System.IO.Path.Combine(directory, "data.zip");
        return new TestStoreContext(new ZipStore(zipPath), directory);
    }
}
