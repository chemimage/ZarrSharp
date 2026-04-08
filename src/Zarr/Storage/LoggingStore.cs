using System.Collections.Concurrent;
using System.Diagnostics;

namespace Zarr.Storage;

public sealed class StoreLogEntry
{
    public StoreLogEntry(string operation, string? target, TimeSpan duration, bool isAsync, bool succeeded, Exception? exception = null)
    {
        Operation = operation;
        Target = target;
        Duration = duration;
        IsAsync = isAsync;
        Succeeded = succeeded;
        Exception = exception;
    }

    public string Operation { get; }

    public string? Target { get; }

    public TimeSpan Duration { get; }

    public bool IsAsync { get; }

    public bool Succeeded { get; }

    public Exception? Exception { get; }
}

public sealed class LoggingStore : WrapperStore
{
    private readonly ConcurrentDictionary<string, int> _operationCounts = new(StringComparer.Ordinal);
    private readonly Action<StoreLogEntry>? _sink;

    public LoggingStore(IStore innerStore, Action<StoreLogEntry>? sink = null)
        : base(innerStore)
    {
        _sink = sink;
    }

    public IReadOnlyDictionary<string, int> OperationCounts => _operationCounts;

    public int GetOperationCount(string operation)
    {
        return _operationCounts.TryGetValue(operation, out var count) ? count : 0;
    }

    public override ReadOnlyMemory<byte>? Get(string key) => Track(nameof(Get), key, isAsync: false, () => base.Get(key));

    public override void Set(string key, ReadOnlyMemory<byte> value) => Track(nameof(Set), key, isAsync: false, () => base.Set(key, value));

    public override bool Exists(string key) => Track(nameof(Exists), key, isAsync: false, () => base.Exists(key));

    public override void Delete(string key) => Track(nameof(Delete), key, isAsync: false, () => base.Delete(key));

    public override void DeletePrefix(string prefix) => Track(nameof(DeletePrefix), prefix, isAsync: false, () => base.DeletePrefix(prefix));

    public override IEnumerable<string> ListKeys(string prefix = "") => Track(nameof(ListKeys), prefix, isAsync: false, () => base.ListKeys(prefix).ToArray());

    public override ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default) => TrackAsync(nameof(GetAsync), key, () => base.GetAsync(key, cancellationToken));

    public override ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) => TrackAsync(nameof(SetAsync), key, () => base.SetAsync(key, value, cancellationToken));

    public override ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => TrackAsync(nameof(ExistsAsync), key, () => base.ExistsAsync(key, cancellationToken));

    public override ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default) => TrackAsync(nameof(DeleteAsync), key, () => base.DeleteAsync(key, cancellationToken));

    public override ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default) => TrackAsync(nameof(DeletePrefixAsync), prefix, () => base.DeletePrefixAsync(prefix, cancellationToken));

    public override ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default) => TrackAsync(nameof(ListKeysAsync), prefix, () => base.ListKeysAsync(prefix, cancellationToken));

    private T Track<T>(string operation, string? target, bool isAsync, Func<T> action)
    {
        Increment(operation);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = action();
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync, succeeded: true));
            return result;
        }
        catch (Exception error)
        {
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync, succeeded: false, error));
            throw;
        }
    }

    private void Track(string operation, string? target, bool isAsync, Action action)
    {
        Increment(operation);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            action();
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync, succeeded: true));
        }
        catch (Exception error)
        {
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync, succeeded: false, error));
            throw;
        }
    }

    private async ValueTask<T> TrackAsync<T>(string operation, string? target, Func<ValueTask<T>> action)
    {
        Increment(operation);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await action().ConfigureAwait(false);
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync: true, succeeded: true));
            return result;
        }
        catch (Exception error)
        {
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync: true, succeeded: false, error));
            throw;
        }
    }

    private async ValueTask TrackAsync(string operation, string? target, Func<ValueTask> action)
    {
        Increment(operation);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await action().ConfigureAwait(false);
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync: true, succeeded: true));
        }
        catch (Exception error)
        {
            _sink?.Invoke(new StoreLogEntry(operation, target, stopwatch.Elapsed, isAsync: true, succeeded: false, error));
            throw;
        }
    }

    private void Increment(string operation)
    {
        _operationCounts.AddOrUpdate(operation, 1, static (_, current) => current + 1);
    }
}
