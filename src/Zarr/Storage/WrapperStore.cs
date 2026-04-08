namespace Zarr.Storage;

public abstract class WrapperStore : IStore, IAsyncStore, IReadOnlyAwareStore, IDisposable
{
    protected WrapperStore(IStore innerStore)
    {
        InnerStore = innerStore ?? throw new ArgumentNullException(nameof(innerStore));
    }

    public IStore InnerStore { get; }

    public virtual bool IsReadOnly => InnerStore is IReadOnlyAwareStore readOnlyAwareStore && readOnlyAwareStore.IsReadOnly;

    public virtual ReadOnlyMemory<byte>? Get(string key) => InnerStore.Get(key);

    public virtual void Set(string key, ReadOnlyMemory<byte> value) => InnerStore.Set(key, value);

    public virtual bool Exists(string key) => InnerStore.Exists(key);

    public virtual void Delete(string key) => InnerStore.Delete(key);

    public virtual void DeletePrefix(string prefix) => InnerStore.DeletePrefix(prefix);

    public virtual IEnumerable<string> ListKeys(string prefix = "") => InnerStore.ListKeys(prefix);

    public virtual ValueTask<ReadOnlyMemory<byte>?> GetAsync(string key, CancellationToken cancellationToken = default) => InnerStore.GetAsync(key, cancellationToken);

    public virtual ValueTask SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) => InnerStore.SetAsync(key, value, cancellationToken);

    public virtual ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => InnerStore.ExistsAsync(key, cancellationToken);

    public virtual ValueTask DeleteAsync(string key, CancellationToken cancellationToken = default) => InnerStore.DeleteAsync(key, cancellationToken);

    public virtual ValueTask DeletePrefixAsync(string prefix, CancellationToken cancellationToken = default) => InnerStore.DeletePrefixAsync(prefix, cancellationToken);

    public virtual ValueTask<IReadOnlyList<string>> ListKeysAsync(string prefix = "", CancellationToken cancellationToken = default) => InnerStore.ListKeysAsync(prefix, cancellationToken);

    public virtual void Dispose()
    {
        if (InnerStore is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public override string ToString()
    {
        return $"{GetType().Name}({InnerStore})";
    }
}
