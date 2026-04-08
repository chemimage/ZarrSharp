using System.Collections.Concurrent;
using System.IO.Compression;

namespace Zarr.Storage;

public sealed class ZipStore : IStore, IAsyncStore, IReadOnlyAwareStore, IDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);

    public ZipStore(string path, bool readOnly = false)
    {
        Path = System.IO.Path.GetFullPath(path);
        ReadOnly = readOnly;

        if (File.Exists(Path))
        {
            using var stream = File.Open(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            foreach (var entry in archive.Entries)
            {
                using var entryStream = entry.Open();
                using var memory = new MemoryStream();
                entryStream.CopyTo(memory);
                _entries[PathUtilities.Normalize(entry.FullName)] = memory.ToArray();
            }
        }
        else if (readOnly)
        {
            throw new FileNotFoundException("The requested zip store does not exist.", Path);
        }
    }

    public string Path { get; }

    public bool ReadOnly { get; }

    public bool IsReadOnly => ReadOnly;

    public ReadOnlyMemory<byte>? Get(string key)
    {
        var normalized = PathUtilities.Normalize(key);
        return _entries.TryGetValue(normalized, out var value)
            ? (ReadOnlyMemory<byte>?)new ReadOnlyMemory<byte>((byte[])value.Clone())
            : null;
    }

    public void Set(string key, ReadOnlyMemory<byte> value)
    {
        EnsureWritable();
        _entries[PathUtilities.Normalize(key)] = value.ToArray();
        Persist();
    }

    public bool Exists(string key)
    {
        return _entries.ContainsKey(PathUtilities.Normalize(key));
    }

    public void Delete(string key)
    {
        EnsureWritable();
        _entries.TryRemove(PathUtilities.Normalize(key), out _);
        Persist();
    }

    public void DeletePrefix(string prefix)
    {
        EnsureWritable();
        var normalized = PathUtilities.Normalize(prefix);
        foreach (var key in MatchingKeys(normalized))
        {
            _entries.TryRemove(key, out _);
        }

        Persist();
    }

    public IEnumerable<string> ListKeys(string prefix = "")
    {
        var normalized = PathUtilities.Normalize(prefix);
        return MatchingKeys(normalized).OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    public ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return new ValueTask<ReadOnlyMemory<byte>?>(Get(key));
    }

    public ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        return new ValueTask(Task.Run(() =>
        {
            _entries[PathUtilities.Normalize(key)] = value.ToArray();
            Persist();
        }, cancellationToken));
    }

    public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return new ValueTask<bool>(Exists(key));
    }

    public ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        return new ValueTask(Task.Run(() =>
        {
            _entries.TryRemove(PathUtilities.Normalize(key), out _);
            Persist();
        }, cancellationToken));
    }

    public ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        EnsureWritable();
        return new ValueTask(Task.Run(() =>
        {
            var normalized = PathUtilities.Normalize(prefix);
            foreach (var key in MatchingKeys(normalized))
            {
                _entries.TryRemove(key, out _);
            }

            Persist();
        }, cancellationToken));
    }

    public ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default)
    {
        return new ValueTask<IReadOnlyList<string>>(ListKeys(prefix).ToArray());
    }

    public void Dispose()
    {
    }

    public override string ToString()
    {
        return $"zip://{Path}";
    }

    private void EnsureWritable()
    {
        if (ReadOnly)
        {
            throw new InvalidOperationException("The zip store is read-only.");
        }
    }

    private IEnumerable<string> MatchingKeys(string prefix)
    {
        return _entries.Keys.Where(key => prefix.Length == 0 ||
                                          key.Equals(prefix, StringComparison.Ordinal) ||
                                          key.StartsWith(prefix + "/", StringComparison.Ordinal));
    }

    private void Persist()
    {
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Open(Path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        foreach (var entry in _entries.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var zipEntry = archive.CreateEntry(entry.Key, CompressionLevel.NoCompression);
            using var entryStream = zipEntry.Open();
            entryStream.Write(entry.Value, 0, entry.Value.Length);
        }
    }
}
