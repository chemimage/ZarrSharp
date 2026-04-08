namespace Zarr.Core;

public sealed class ZarrGroup : ZarrNode
{
    private readonly Action<object?>? _consolidatedMutationCallback;
    private ConsolidatedMetadata? _consolidatedMetadata;
    private readonly Dictionary<string, Dictionary<string, object?>> _extraFields;
    private readonly Dictionary<string, IMetadataExtension> _parsedExtensions;

    internal ZarrGroup(IStore store, string path, GroupMetadata metadata, Action<object?>? consolidatedMutationCallback = null)
        : base(store, path, metadata.Format, metadata.Attributes)
    {
        _consolidatedMutationCallback = consolidatedMutationCallback;
        _consolidatedMetadata = metadata.ConsolidatedMetadata?.Clone();
        _extraFields = metadata.ExtraFields.ToDictionary(
            item => item.Key,
            item => JsonUtilities.CloneDictionary(item.Value),
            StringComparer.Ordinal);
        _parsedExtensions = metadata.ParsedExtensions.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ExtraFields => _extraFields.ToDictionary(
        item => item.Key,
        item => (IReadOnlyDictionary<string, object?>)JsonUtilities.CloneDictionary(item.Value),
        StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IMetadataExtension> Extensions => _parsedExtensions.ToDictionary(
        item => item.Key,
        item => item.Value,
        StringComparer.Ordinal);

    public ZarrGroup CreateGroup(
        string path,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        var group = ZarrApi.CreateGroup(Store, PathUtilities.Combine(Path, path), Format, overwrite, attributes);
        RefreshConsolidatedMetadataIfNeeded();
        return group;
    }

    internal async Task<ZarrGroup> CreateGroupAsyncCore(
        string path,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var group = await ZarrApi.CreateGroupAsyncCore(Store, PathUtilities.Combine(Path, path), Format, overwrite, attributes, cancellationToken).ConfigureAwait(false);
        await RefreshConsolidatedMetadataIfNeededAsync(cancellationToken).ConfigureAwait(false);
        return group;
    }

    public ZarrArray CreateArray<T>(
        string path,
        int[] shape,
        int[]? chunks = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        T? fillValue = default,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        var array = ZarrApi.CreateArray(Store, shape, chunks, PathUtilities.Combine(Path, path), Format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata);
        RefreshConsolidatedMetadataIfNeeded();
        return array;
    }

    public ZarrArray CreateArray(
        string path,
        DType dataType,
        int[] shape,
        int[]? chunks = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        object? fillValue = null,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        var array = ZarrApi.CreateArray(Store, dataType, shape, chunks, PathUtilities.Combine(Path, path), Format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata);
        RefreshConsolidatedMetadataIfNeeded();
        return array;
    }

    internal async Task<ZarrArray> CreateArrayAsyncCore<T>(
        string path,
        int[] shape,
        int[]? chunks = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        T? fillValue = default,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null,
        CancellationToken cancellationToken = default)
    {
        var array = await ZarrApi.CreateArrayAsyncCore(Store, DType.FromType(typeof(T)), shape, chunks, PathUtilities.Combine(Path, path), Format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false);
        await RefreshConsolidatedMetadataIfNeededAsync(cancellationToken).ConfigureAwait(false);
        return array;
    }

    internal async Task<ZarrArray> CreateArrayAsyncCore(
        string path,
        DType dataType,
        int[] shape,
        int[]? chunks = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        object? fillValue = null,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null,
        CancellationToken cancellationToken = default)
    {
        var array = await ZarrApi.CreateArrayAsyncCore(Store, dataType ?? throw new ArgumentNullException(nameof(dataType)), shape, chunks, PathUtilities.Combine(Path, path), Format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false);
        await RefreshConsolidatedMetadataIfNeededAsync(cancellationToken).ConfigureAwait(false);
        return array;
    }

    public ZarrArray CreateDataset<T>(
        string path,
        int[] shape,
        int[]? chunks = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        T? fillValue = default,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArray(path, shape, chunks, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public ZarrArray CreateDataset(string path, Array data, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        var array = ZarrApi.FromArray(data, Store, PathUtilities.Combine(Path, path), Format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
        RefreshConsolidatedMetadataIfNeeded();
        return array;
    }

    internal async Task<ZarrArray> CreateDatasetAsyncCore(string path, Array data, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        var array = await ZarrApi.FromArrayAsyncCore(data, Store, PathUtilities.Combine(Path, path), Format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken).ConfigureAwait(false);
        await RefreshConsolidatedMetadataIfNeededAsync(cancellationToken).ConfigureAwait(false);
        return array;
    }

    public ZarrGroup RequireGroup(string path, bool overwrite = false)
    {
        var fullPath = PathUtilities.Combine(Path, path);
        var existing = MetadataStore.DetectNode(Store, fullPath);
        return existing.Kind switch
        {
            NodeKind.None => CreateGroup(path),
            NodeKind.Group when overwrite => ZarrApi.CreateGroup(Store, fullPath, Format, overwrite: true),
            NodeKind.Group => ZarrApi.OpenGroup(Store, fullPath, Format),
            _ => throw new InvalidOperationException("Incompatible object already exists at the requested group path."),
        };
    }

    public IReadOnlyList<ZarrGroup> RequireGroups(params string[] paths)
    {
        return paths.Select(path => RequireGroup(path)).ToArray();
    }

    public ZarrArray RequireArray<T>(string path, int[] shape, bool exact = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        var fullPath = PathUtilities.Combine(Path, path);
        var existing = MetadataStore.DetectNode(Store, fullPath);
        if (existing.Kind == NodeKind.None)
        {
            return ZarrApi.CreateArray<T>(Store, shape, shape, fullPath, Format, attributes: attributes);
        }

        if (existing.Kind == NodeKind.Group)
        {
            throw new InvalidOperationException("Incompatible object already exists at the requested array path.");
        }

        var array = ZarrApi.OpenArray(Store, fullPath, Format);
        if (!array.Shape.SequenceEqual(shape))
        {
            throw new InvalidOperationException("Incompatible shape.");
        }

        if (exact && !array.DataType.IsExact(DType.FromType(typeof(T))))
        {
            throw new InvalidOperationException("Incompatible dtype.");
        }

        return array;
    }

    public ZarrArray Empty<T>(string name, int[] shape, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return ZarrApi.Empty<T>(Store, shape, chunks, PathUtilities.Combine(Path, name), Format, overwrite, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public ZarrArray Zeros<T>(string name, int[] shape, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return ZarrApi.Zeros<T>(Store, shape, chunks, PathUtilities.Combine(Path, name), Format, overwrite, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public ZarrArray Ones<T>(string name, int[] shape, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return ZarrApi.Ones<T>(Store, shape, chunks, PathUtilities.Combine(Path, name), Format, overwrite, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public ZarrArray Full<T>(string name, int[] shape, T fillValue, int[]? chunks = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return ZarrApi.Full(Store, shape, fillValue, chunks, PathUtilities.Combine(Path, name), Format, overwrite, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public ZarrArray EmptyLike(string name, ZarrArray data, int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return ZarrApi.EmptyLike(data, Store, PathUtilities.Combine(Path, name), shape, chunks, dtype, overwrite, attributes);
    }

    public ZarrArray ZerosLike(string name, ZarrArray data, int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return ZarrApi.ZerosLike(data, Store, PathUtilities.Combine(Path, name), shape, chunks, dtype, overwrite, attributes);
    }

    public ZarrArray OnesLike(string name, ZarrArray data, int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return ZarrApi.OnesLike(data, Store, PathUtilities.Combine(Path, name), shape, chunks, dtype, overwrite, attributes);
    }

    public ZarrArray FullLike(string name, ZarrArray data, object fillValue, int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return ZarrApi.FullLike(data, Store, fillValue, PathUtilities.Combine(Path, name), shape, chunks, dtype, overwrite, attributes);
    }

    public ZarrNode this[string path] => GetItem(path);

    public bool ContainsItem(string path)
    {
        if (_consolidatedMetadata?.TryResolve(path, out _) == true)
        {
            return true;
        }

        return MetadataStore.DetectNode(Store, PathUtilities.Combine(Path, path)).Kind != NodeKind.None;
    }

    public ZarrNode GetItem(string path)
    {
        if (_consolidatedMetadata?.TryResolve(path, out var metadata) == true)
        {
            return BuildNodeFromMetadata(PathUtilities.Combine(Path, path), metadata);
        }

        return ZarrApi.Open(Store, PathUtilities.Combine(Path, path), Format);
    }

    internal async Task<ZarrNode> GetItemAsyncCore(string path, CancellationToken cancellationToken = default)
    {
        if (_consolidatedMetadata?.TryResolve(path, out var metadata) == true)
        {
            return BuildNodeFromMetadata(PathUtilities.Combine(Path, path), metadata);
        }

        return await ZarrApi.OpenAsyncCore(Store, PathUtilities.Combine(Path, path), Format, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public void DeleteItem(string path)
    {
        var fullPath = PathUtilities.Combine(Path, path);
        if (MetadataStore.DetectNode(Store, fullPath).Kind == NodeKind.None)
        {
            throw new NodeNotFoundException(fullPath);
        }

        Store.DeletePrefix(fullPath);
        RefreshConsolidatedMetadataIfNeeded();
    }

    internal async Task DeleteItemAsyncCore(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = PathUtilities.Combine(Path, path);
        if ((await MetadataStore.DetectNodeAsync(Store, fullPath, cancellationToken).ConfigureAwait(false)).Kind == NodeKind.None)
        {
            throw new NodeNotFoundException(fullPath);
        }

        await Store.DeletePrefixAsync(fullPath, cancellationToken).ConfigureAwait(false);
        await RefreshConsolidatedMetadataIfNeededAsync(cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<string> Keys(int? maxDepth = 0, bool? useConsolidatedForChildren = null)
    {
        return Members(maxDepth, useConsolidatedForChildren).Select(item => item.Key).ToArray();
    }

    public IReadOnlyList<ZarrNode> Values(int? maxDepth = 0, bool? useConsolidatedForChildren = null)
    {
        return Members(maxDepth, useConsolidatedForChildren).Select(item => item.Value).ToArray();
    }

    public int NMembers(int? maxDepth = 0, bool? useConsolidatedForChildren = null)
    {
        return Members(maxDepth, useConsolidatedForChildren).Count();
    }

    public IEnumerable<KeyValuePair<string, ZarrNode>> Members(int? maxDepth = 0, bool? useConsolidatedForChildren = null)
    {
        if (maxDepth.HasValue && maxDepth.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth cannot be negative.");
        }

        if (_consolidatedMetadata is not null)
        {
            foreach (var entry in _consolidatedMetadata.Flatten()
                .Select(item => new KeyValuePair<string, ZarrNode>(item.Key, BuildNodeFromMetadata(PathUtilities.Combine(Path, item.Key), item.Value)))
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var depth = entry.Key.Count(character => character == '/');
                if (maxDepth.HasValue && depth > maxDepth.Value)
                {
                    continue;
                }

                yield return entry;
            }

            yield break;
        }

        var preferConsolidatedForChildren = useConsolidatedForChildren ?? ZarrConfiguration.UseConsolidatedMetadataForChildren;
        foreach (var entry in EnumerateStoreMembers(maxDepth, preferConsolidatedForChildren))
        {
            yield return entry;
        }
    }

    internal async Task<IReadOnlyList<KeyValuePair<string, ZarrNode>>> MembersAsyncCore(int? maxDepth = 0, bool? useConsolidatedForChildren = null, CancellationToken cancellationToken = default)
    {
        if (maxDepth.HasValue && maxDepth.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth cannot be negative.");
        }

        if (_consolidatedMetadata is not null)
        {
            return _consolidatedMetadata.Flatten()
                .Select(item => new KeyValuePair<string, ZarrNode>(item.Key, BuildNodeFromMetadata(PathUtilities.Combine(Path, item.Key), item.Value)))
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Where(entry => !maxDepth.HasValue || entry.Key.Count(character => character == '/') <= maxDepth.Value)
                .ToArray();
        }

        var preferConsolidatedForChildren = useConsolidatedForChildren ?? ZarrConfiguration.UseConsolidatedMetadataForChildren;
        return await EnumerateStoreMembersAsync(maxDepth, preferConsolidatedForChildren, cancellationToken).ConfigureAwait(false);
    }

    public ZarrArray OpenArray(string path)
    {
        return ZarrApi.OpenArray(Store, PathUtilities.Combine(Path, path), Format);
    }

    public ZarrGroup OpenGroup(string path)
    {
        return ZarrApi.OpenGroup(Store, PathUtilities.Combine(Path, path), Format);
    }

    public GroupInfo Info(bool? useConsolidatedForChildren = null)
    {
        var members = Members(maxDepth: null, useConsolidatedForChildren: useConsolidatedForChildren).ToArray();
        return new GroupInfo(
            Name,
            Format,
            Store.GetType().Name,
            members.Length,
            members.Count(item => item.Value is ZarrArray),
            members.Count(item => item.Value is ZarrGroup));
    }

    internal async Task<GroupInfo> InfoAsyncCore(bool? useConsolidatedForChildren = null, CancellationToken cancellationToken = default)
    {
        var members = await MembersAsyncCore(maxDepth: null, useConsolidatedForChildren: useConsolidatedForChildren, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new GroupInfo(
            Name,
            Format,
            Store.GetType().Name,
            members.Count,
            members.Count(item => item.Value is ZarrArray),
            members.Count(item => item.Value is ZarrGroup));
    }

    public string Tree(int? maxDepth = null, bool? useConsolidatedForChildren = null)
    {
        if (maxDepth.HasValue && maxDepth.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth cannot be negative.");
        }

        var lines = new List<string> { Name };
        AppendTree(lines, this, prefix: string.Empty, depth: 0, maxDepth, useConsolidatedForChildren);
        return string.Join(Environment.NewLine, lines);
    }

    internal async Task<string> TreeAsyncCore(int? maxDepth = null, bool? useConsolidatedForChildren = null, CancellationToken cancellationToken = default)
    {
        if (maxDepth.HasValue && maxDepth.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "maxDepth cannot be negative.");
        }

        var lines = new List<string> { Name };
        await AppendTreeAsync(lines, this, prefix: string.Empty, depth: 0, maxDepth, useConsolidatedForChildren, cancellationToken).ConfigureAwait(false);
        return string.Join(Environment.NewLine, lines);
    }

    protected override void PersistMetadata()
    {
        var metadata = CreateCurrentMetadata();
        MetadataStore.SaveGroup(Store, Path, metadata);
        _consolidatedMutationCallback?.Invoke(metadata.Clone());
    }

    private GroupMetadata CreateCurrentMetadata()
    {
        return MetadataStore.CreateGroupMetadata(
            Format,
            JsonUtilities.CloneDictionary(AttributeValues),
            _consolidatedMetadata?.Clone(),
            _extraFields.ToDictionary(
                item => item.Key,
                item => JsonUtilities.CloneDictionary(item.Value),
                StringComparer.Ordinal));
    }

    private static void AppendTree(List<string> lines, ZarrGroup group, string prefix, int depth, int? maxDepth, bool? useConsolidatedForChildren)
    {
        if (maxDepth.HasValue && depth >= maxDepth.Value)
        {
            return;
        }

        var children = group.Members(0, useConsolidatedForChildren).OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < children.Length; index++)
        {
            var child = children[index];
            var isLast = index == children.Length - 1;
            var connector = isLast ? "└── " : "├── ";

            switch (child.Value)
            {
                case ZarrGroup childGroup:
                    lines.Add($"{prefix}{connector}{childGroup.Basename}/");
                    AppendTree(lines, childGroup, prefix + (isLast ? "    " : "│   "), depth + 1, maxDepth, useConsolidatedForChildren);
                    break;

                case ZarrArray childArray:
                    lines.Add($"{prefix}{connector}{childArray.Basename} {FormatShape(childArray.Shape)} {childArray.DataType.Name}");
                    break;
            }
        }
    }

    private static async Task AppendTreeAsync(List<string> lines, ZarrGroup group, string prefix, int depth, int? maxDepth, bool? useConsolidatedForChildren, CancellationToken cancellationToken)
    {
        if (maxDepth.HasValue && depth >= maxDepth.Value)
        {
            return;
        }

        var children = (await group.MembersAsyncCore(0, useConsolidatedForChildren: useConsolidatedForChildren, cancellationToken: cancellationToken).ConfigureAwait(false))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < children.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var child = children[index];
            var isLast = index == children.Length - 1;
            var connector = isLast ? "└── " : "├── ";

            switch (child.Value)
            {
                case ZarrGroup childGroup:
                    lines.Add($"{prefix}{connector}{childGroup.Basename}/");
                    await AppendTreeAsync(lines, childGroup, prefix + (isLast ? "    " : "│   "), depth + 1, maxDepth, useConsolidatedForChildren, cancellationToken).ConfigureAwait(false);
                    break;

                case ZarrArray childArray:
                    lines.Add($"{prefix}{connector}{childArray.Basename} {FormatShape(childArray.Shape)} {childArray.DataType.Name}");
                    break;
            }
        }
    }

    private static string FormatShape(IReadOnlyList<int> shape)
    {
        return "(" + string.Join(", ", shape) + ")";
    }

    private ZarrNode BuildNodeFromMetadata(string fullPath, object metadata)
    {
        var relativePath = GetRelativePath(fullPath);
        Action<object?>? mutationCallback = _consolidatedMetadata is null
            ? null
            : updatedMetadata =>
            {
                if (updatedMetadata is null)
                {
                    _consolidatedMetadata?.Remove(relativePath);
                }
                else
                {
                    _consolidatedMetadata?.Set(relativePath, updatedMetadata);
                }

                _consolidatedMutationCallback?.Invoke(CreateCurrentMetadata().Clone());
            };

        return metadata switch
        {
            GroupMetadata groupMetadata => new ZarrGroup(Store, fullPath, groupMetadata.Clone(), mutationCallback),
            ArrayMetadata arrayMetadata => new ZarrArray(Store, fullPath, arrayMetadata, mutationCallback),
            _ => throw new MetadataException($"Unsupported consolidated node metadata '{metadata.GetType().Name}'."),
        };
    }

    private void RefreshConsolidatedMetadataIfNeeded()
    {
        if (_consolidatedMetadata is null)
        {
            return;
        }

        MetadataStore.SaveConsolidatedMetadata(Store, Path, Format);
        _consolidatedMetadata = MetadataStore.LoadGroupWithConsolidatedMetadata(Store, Path, Format).ConsolidatedMetadata?.Clone();
        _consolidatedMutationCallback?.Invoke(CreateCurrentMetadata().Clone());
    }

    private async Task RefreshConsolidatedMetadataIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_consolidatedMetadata is null)
        {
            return;
        }

        await MetadataStore.SaveConsolidatedMetadataAsync(Store, Path, Format, cancellationToken).ConfigureAwait(false);
        _consolidatedMetadata = (await MetadataStore.LoadGroupWithConsolidatedMetadataAsync(Store, Path, Format, cancellationToken).ConfigureAwait(false)).ConsolidatedMetadata?.Clone();
        _consolidatedMutationCallback?.Invoke(CreateCurrentMetadata().Clone());
    }

    private IEnumerable<KeyValuePair<string, ZarrNode>> EnumerateStoreMembers(int? maxDepth, bool useConsolidatedForChildren)
    {
        var children = ListDirectStoreChildren(useConsolidatedForChildren);
        foreach (var child in children)
        {
            yield return child;

            if ((maxDepth is null || maxDepth.Value > 0) &&
                child.Value is ZarrGroup childGroup)
            {
                var nextDepth = maxDepth.HasValue ? (int?)(maxDepth.Value - 1) : null;
                foreach (var descendant in childGroup.Members(nextDepth, useConsolidatedForChildren))
                {
                    yield return new KeyValuePair<string, ZarrNode>($"{child.Key}/{descendant.Key}", descendant.Value);
                }
            }
        }
    }

    private async Task<IReadOnlyList<KeyValuePair<string, ZarrNode>>> EnumerateStoreMembersAsync(int? maxDepth, bool useConsolidatedForChildren, CancellationToken cancellationToken)
    {
        var children = await ListDirectStoreChildrenAsync(useConsolidatedForChildren, cancellationToken).ConfigureAwait(false);
        var members = new List<KeyValuePair<string, ZarrNode>>();
        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            members.Add(child);

            if ((maxDepth is null || maxDepth.Value > 0) &&
                child.Value is ZarrGroup childGroup)
            {
                var nextDepth = maxDepth.HasValue ? (int?)(maxDepth.Value - 1) : null;
                var descendants = await childGroup.MembersAsyncCore(nextDepth, useConsolidatedForChildren: useConsolidatedForChildren, cancellationToken: cancellationToken).ConfigureAwait(false);
                members.AddRange(descendants.Select(descendant => new KeyValuePair<string, ZarrNode>($"{child.Key}/{descendant.Key}", descendant.Value)));
            }
        }

        return members;
    }

    private IReadOnlyList<KeyValuePair<string, ZarrNode>> ListDirectStoreChildren(bool useConsolidatedForChildren)
    {
        var prefix = Path.Length == 0 ? string.Empty : Path + "/";
        var childNames = MetadataStore.ListNodePaths(Store, Format)
            .Distinct(StringComparer.Ordinal)
            .Where(nodePath => !string.Equals(nodePath, Path, StringComparison.Ordinal))
            .Where(nodePath => prefix.Length == 0 || nodePath.StartsWith(prefix, StringComparison.Ordinal))
            .Select(nodePath => prefix.Length == 0 ? nodePath : nodePath[prefix.Length..])
            .Where(relativePath => relativePath.Length > 0)
            .Select(relativePath => relativePath.Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(relativePath => relativePath, StringComparer.Ordinal)
            .ToArray();

        return childNames
            .Select(childName => new KeyValuePair<string, ZarrNode>(
                childName,
                NormalizeChildTraversalNode(
                    ZarrApi.Open(Store, PathUtilities.Combine(Path, childName), Format, useConsolidated: useConsolidatedForChildren),
                    useConsolidatedForChildren)))
            .ToArray();
    }

    private async Task<IReadOnlyList<KeyValuePair<string, ZarrNode>>> ListDirectStoreChildrenAsync(bool useConsolidatedForChildren, CancellationToken cancellationToken)
    {
        var prefix = Path.Length == 0 ? string.Empty : Path + "/";
        var childNames = (await MetadataStore.ListNodePathsAsync(Store, Format, cancellationToken).ConfigureAwait(false))
            .Distinct(StringComparer.Ordinal)
            .Where(nodePath => !string.Equals(nodePath, Path, StringComparison.Ordinal))
            .Where(nodePath => prefix.Length == 0 || nodePath.StartsWith(prefix, StringComparison.Ordinal))
            .Select(nodePath => prefix.Length == 0 ? nodePath : nodePath[prefix.Length..])
            .Where(relativePath => relativePath.Length > 0)
            .Select(relativePath => relativePath.Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(relativePath => relativePath, StringComparer.Ordinal)
            .ToArray();

        var members = new List<KeyValuePair<string, ZarrNode>>(childNames.Length);
        foreach (var childName in childNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            members.Add(new KeyValuePair<string, ZarrNode>(
                childName,
                NormalizeChildTraversalNode(
                    await ZarrApi.OpenAsyncCore(Store, PathUtilities.Combine(Path, childName), Format, useConsolidated: useConsolidatedForChildren, cancellationToken).ConfigureAwait(false),
                    useConsolidatedForChildren)));
        }

        return members;
    }

    private static ZarrNode NormalizeChildTraversalNode(ZarrNode node, bool useConsolidatedForChildren)
    {
        if (useConsolidatedForChildren ||
            node is not ZarrGroup group ||
            group._consolidatedMetadata is null)
        {
            return node;
        }

        return new ZarrGroup(group.Store, group.Path, MetadataStore.CreateGroupMetadata(
            group.Format,
            group.Attrs.AsDictionary(),
            extraFields: group._extraFields.ToDictionary(
                item => item.Key,
                item => JsonUtilities.CloneDictionary(item.Value),
                StringComparer.Ordinal)));
    }

    private string GetRelativePath(string fullPath)
    {
        var normalizedFullPath = PathUtilities.Normalize(fullPath);
        if (Path.Length == 0)
        {
            return normalizedFullPath;
        }

        if (normalizedFullPath.Equals(Path, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (!normalizedFullPath.StartsWith(Path + "/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path '{normalizedFullPath}' does not belong to group '{Path}'.");
        }

        return normalizedFullPath[(Path.Length + 1)..];
    }

    internal async Task<ZarrGroup> RequireGroupAsyncCore(string path, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var fullPath = PathUtilities.Combine(Path, path);
        var existing = await MetadataStore.DetectNodeAsync(Store, fullPath, cancellationToken).ConfigureAwait(false);
        return existing.Kind switch
        {
            NodeKind.None => await CreateGroupAsyncCore(path, cancellationToken: cancellationToken).ConfigureAwait(false),
            NodeKind.Group when overwrite => await ZarrApi.CreateGroupAsyncCore(Store, fullPath, Format, overwrite: true, cancellationToken: cancellationToken).ConfigureAwait(false),
            NodeKind.Group => await ZarrApi.OpenGroupAsyncCore(Store, fullPath, Format, cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Incompatible object already exists at the requested group path."),
        };
    }

    internal async Task<ZarrArray> RequireArrayAsyncCore<T>(string path, int[] shape, bool exact = false, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        var fullPath = PathUtilities.Combine(Path, path);
        var existing = await MetadataStore.DetectNodeAsync(Store, fullPath, cancellationToken).ConfigureAwait(false);
        if (existing.Kind == NodeKind.None)
        {
            return await ZarrApi.CreateArrayAsyncCore(Store, DType.FromType(typeof(T)), shape, shape, fullPath, Format, overwrite: false, attributes, default(T), ArrayOrder.C, null, null, null, cancellationToken).ConfigureAwait(false);
        }

        if (existing.Kind == NodeKind.Group)
        {
            throw new InvalidOperationException("Incompatible object already exists at the requested array path.");
        }

        var array = await ZarrApi.OpenArrayAsyncCore(Store, fullPath, Format, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!array.Shape.SequenceEqual(shape))
        {
            throw new InvalidOperationException("Incompatible shape.");
        }

        if (exact && !array.DataType.IsExact(DType.FromType(typeof(T))))
        {
            throw new InvalidOperationException("Incompatible dtype.");
        }

        return array;
    }
}
