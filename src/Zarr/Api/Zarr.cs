namespace Zarr.Api;

public static class Zarr
{
    public static ZarrArray Array(Array data, IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return FromArray(data, store, path, format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrArray Create<T>(
        IStore store,
        int[] shape,
        int[]? chunks = null,
        string path = "",
        ZarrFormat? format = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        T? fillValue = default,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArray(store, typeof(T), shape, chunks, path, format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrGroup CreateGroup(
        IStore store,
        string path = "",
        ZarrFormat? format = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        var resolvedFormat = ResolveFormat(format);
        var normalizedPath = PathUtilities.Normalize(path);
        EnsureParentGroups(store, normalizedPath, resolvedFormat);
        EnsureCreatable(store, normalizedPath, overwrite);

        var metadata = MetadataStore.CreateGroupMetadata(resolvedFormat, JsonUtilities.CloneDictionary(attributes));
        MetadataStore.SaveGroup(store, normalizedPath, metadata);
        return new ZarrGroup(store, normalizedPath, metadata);
    }

    internal static async Task<ZarrGroup> CreateGroupAsyncCore(
        IStore store,
        string path = "",
        ZarrFormat? format = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedFormat = ResolveFormat(format);
        var normalizedPath = PathUtilities.Normalize(path);
        await EnsureParentGroupsAsync(store, normalizedPath, resolvedFormat, cancellationToken).ConfigureAwait(false);
        await EnsureCreatableAsync(store, normalizedPath, overwrite, cancellationToken).ConfigureAwait(false);

        var metadata = MetadataStore.CreateGroupMetadata(resolvedFormat, JsonUtilities.CloneDictionary(attributes));
        await MetadataStore.SaveGroupAsync(store, normalizedPath, metadata, cancellationToken).ConfigureAwait(false);
        return new ZarrGroup(store, normalizedPath, metadata);
    }

    public static ZarrArray CreateArray<T>(
        IStore store,
        int[] shape,
        int[]? chunks = null,
        string path = "",
        ZarrFormat? format = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        T? fillValue = default,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArray(store, typeof(T), shape, chunks, path, format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrArray CreateArray(
        IStore store,
        DType dataType,
        int[] shape,
        int[]? chunks = null,
        string path = "",
        ZarrFormat? format = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        object? fillValue = null,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArrayCore(
            store,
            dataType ?? throw new ArgumentNullException(nameof(dataType)),
            shape,
            chunks,
            path,
            format,
            overwrite,
            attributes,
            fillValue,
            order,
            chunkCodec,
            chunkKeyEncoding,
            v3Metadata);
    }

    public static ZarrArray CreateArray(
        IStore store,
        Type elementType,
        int[] shape,
        int[]? chunks = null,
        string path = "",
        ZarrFormat? format = null,
        bool overwrite = false,
        IReadOnlyDictionary<string, object?>? attributes = null,
        object? fillValue = null,
        ArrayOrder? order = null,
        IChunkCodec? chunkCodec = null,
        ChunkKeyEncoding? chunkKeyEncoding = null,
        V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArrayCore(
            store,
            DType.FromType(elementType),
            shape,
            chunks,
            path,
            format,
            overwrite,
            attributes,
            fillValue,
            order,
            chunkCodec,
            chunkKeyEncoding,
            v3Metadata);
    }

    public static ZarrArray Empty<T>(IStore store, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArray<T>(store, shape, chunks, path, format, overwrite, attributes, default, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrArray Zeros<T>(IStore store, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArray(store, typeof(T), shape, chunks, path, format, overwrite, attributes, 0, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrArray Ones<T>(IStore store, int[] shape, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArray(store, typeof(T), shape, chunks, path, format, overwrite, attributes, 1, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrArray Full<T>(IStore store, int[] shape, T fillValue, int[]? chunks = null, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return CreateArray(store, typeof(T), shape, chunks, path, format, overwrite, attributes, fillValue, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrArray EmptyLike(ZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return CreateLike(reference, store, path, shape, chunks, dtype, reference.FillValue, overwrite, attributes);
    }

    public static ZarrArray ZerosLike(ZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return CreateLike(reference, store, path, shape, chunks, dtype, 0, overwrite, attributes);
    }

    public static ZarrArray OnesLike(ZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return CreateLike(reference, store, path, shape, chunks, dtype, 1, overwrite, attributes);
    }

    public static ZarrArray FullLike(ZarrArray reference, IStore store, object fillValue, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return CreateLike(reference, store, path, shape, chunks, dtype, fillValue, overwrite, attributes);
    }

    public static ZarrArray OpenLike(ZarrArray reference, IStore store, string path = "", int[]? shape = null, int[]? chunks = null, Type? dtype = null, object? fillValue = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var existing = MetadataStore.DetectNode(store, normalizedPath);
        if (!overwrite && existing.Kind == NodeKind.Array)
        {
            return OpenArray(store, normalizedPath, existing.Format ?? reference.Format);
        }

        if (existing.Kind == NodeKind.Group)
        {
            throw new InvalidOperationException("Incompatible object already exists at the requested array path.");
        }

        return CreateLike(reference, store, path, shape, chunks, dtype, fillValue ?? reference.FillValue, overwrite, attributes);
    }

    public static ZarrArray FromArray(Array data, IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        var elementType = data.GetType().GetElementType() ?? throw new ArgumentException("Unable to determine the array element type.", nameof(data));
        var shape = Enumerable.Range(0, data.Rank).Select(data.GetLength).ToArray();
        var array = CreateArray(store, elementType, shape, chunks ?? v3Metadata?.ChunkGrid?.ChunkShape ?? shape, path, format, overwrite, attributes, null, order, chunkCodec, chunkKeyEncoding, v3Metadata);
        array.Write(data);
        return array;
    }

    internal static async Task<ZarrArray> FromArrayAsyncCore(Array data, IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        var elementType = data.GetType().GetElementType() ?? throw new ArgumentException("Unable to determine the array element type.", nameof(data));
        var shape = Enumerable.Range(0, data.Rank).Select(data.GetLength).ToArray();
        var array = await CreateArrayAsyncCore(
            store,
            DType.FromType(elementType),
            shape,
            chunks ?? v3Metadata?.ChunkGrid?.ChunkShape ?? shape,
            path,
            format,
            overwrite,
            attributes,
            null,
            order,
            chunkCodec,
            chunkKeyEncoding,
            v3Metadata,
            cancellationToken).ConfigureAwait(false);
        await array.WriteAsyncCore(data, cancellationToken).ConfigureAwait(false);
        return array;
    }

    public static object Load(IStore store, string path = "", ZarrFormat? format = null)
    {
        return LoadNodeData(Open(store, path, format));
    }

    internal static async Task<object> LoadAsyncCore(IStore store, string path = "", ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        return await LoadNodeDataAsync(await OpenAsyncCore(store, path, format, cancellationToken: cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    public static ZarrArray Save(IStore store, Array data, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return SaveArray(data, store, path, format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    public static ZarrGroup Save(IStore store, IReadOnlyDictionary<string, Array> arrays, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return SaveGroup(store, arrays, path, format, overwrite, attributes);
    }

    public static ZarrNode Save(IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null, params Array[] arrays)
    {
        return arrays.Length switch
        {
            0 => throw new ArgumentException("At least one array must be provided.", nameof(arrays)),
            1 => SaveArray(arrays[0], store, path, format, overwrite, attributes: attributes),
            _ => SaveGroup(store, ToPositionalArrayMap(arrays), path, format, overwrite, attributes),
        };
    }

    public static ZarrNode Open(IStore store, string path, ZarrOpenMode mode, ZarrFormat? format = null, IReadOnlyDictionary<string, object?>? attributes = null, bool? useConsolidated = null)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var existing = MetadataStore.DetectNode(store, normalizedPath);
        var resolvedFormat = existing.Format ?? ResolveFormat(format);

        if (existing.Kind == NodeKind.Array && mode == ZarrOpenMode.Create)
        {
            var existingArray = OpenArray(store, normalizedPath, resolvedFormat, useConsolidated);
            return CreateLike(
                existingArray,
                store,
                normalizedPath,
                null,
                null,
                null,
                existingArray.FillValue,
                overwrite: true,
                existingArray.Attrs.AsDictionary());
        }

        return existing.Kind switch
        {
            NodeKind.None => mode switch
            {
                ZarrOpenMode.Read or ZarrOpenMode.ReadWrite => throw new NodeNotFoundException(normalizedPath),
                ZarrOpenMode.Append => CreateGroup(store, normalizedPath, format, overwrite: false, attributes),
                ZarrOpenMode.Create => CreateGroup(store, normalizedPath, format, overwrite: true, attributes),
                ZarrOpenMode.CreateNew => CreateGroup(store, normalizedPath, format, overwrite: false, attributes),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
            },
            NodeKind.Group => mode switch
            {
                ZarrOpenMode.Read => OpenGroup(WrapReadOnlyStore(store), normalizedPath, resolvedFormat, useConsolidated),
                ZarrOpenMode.ReadWrite or ZarrOpenMode.Append => OpenGroup(store, normalizedPath, resolvedFormat, useConsolidated),
                ZarrOpenMode.Create => CreateGroup(store, normalizedPath, resolvedFormat, overwrite: true, attributes),
                ZarrOpenMode.CreateNew => throw new ContainsGroupException(normalizedPath),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
            },
            NodeKind.Array => mode switch
            {
                ZarrOpenMode.Read => OpenArray(WrapReadOnlyStore(store), normalizedPath, resolvedFormat, useConsolidated),
                ZarrOpenMode.ReadWrite or ZarrOpenMode.Append => OpenArray(store, normalizedPath, resolvedFormat, useConsolidated),
                ZarrOpenMode.Create => throw new InvalidOperationException(),
                ZarrOpenMode.CreateNew => throw new ContainsArrayException(normalizedPath),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
            },
            _ => throw new InvalidOperationException(),
        };
    }

    public static ZarrArray SaveArray(Array data, IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        return FromArray(data, store, path, format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata);
    }

    internal static Task<ZarrArray> SaveArrayAsyncCore(Array data, IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, int[]? chunks = null, IReadOnlyDictionary<string, object?>? attributes = null, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null, CancellationToken cancellationToken = default)
    {
        return FromArrayAsyncCore(data, store, path, format, overwrite, chunks, attributes, order, chunkCodec, chunkKeyEncoding, v3Metadata, cancellationToken);
    }

    public static ZarrGroup SaveGroup(IStore store, IReadOnlyDictionary<string, Array> arrays, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        if (arrays is null)
        {
            throw new ArgumentNullException(nameof(arrays));
        }

        if (arrays.Count == 0)
        {
            throw new ArgumentException("At least one array must be provided.", nameof(arrays));
        }

        var resolvedFormat = ResolveFormat(format);
        var group = CreateGroup(store, path, resolvedFormat, overwrite, attributes);
        foreach (var item in arrays.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (item.Value is null)
            {
                throw new ArgumentException($"Array '{item.Key}' cannot be null.", nameof(arrays));
            }

            FromArray(item.Value, store, PathUtilities.Combine(group.Path, item.Key), resolvedFormat, overwrite: true);
        }

        return group;
    }

    public static ZarrGroup SaveGroup(IStore store, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null, params Array[] arrays)
    {
        return SaveGroup(store, ToPositionalArrayMap(arrays), path, format, overwrite, attributes);
    }

    internal static async Task<ZarrGroup> SaveGroupAsyncCore(IStore store, IReadOnlyDictionary<string, Array> arrays, string path = "", ZarrFormat? format = null, bool overwrite = true, IReadOnlyDictionary<string, object?>? attributes = null, CancellationToken cancellationToken = default)
    {
        if (arrays is null)
        {
            throw new ArgumentNullException(nameof(arrays));
        }

        if (arrays.Count == 0)
        {
            throw new ArgumentException("At least one array must be provided.", nameof(arrays));
        }

        var resolvedFormat = ResolveFormat(format);
        var group = await CreateGroupAsyncCore(store, path, resolvedFormat, overwrite, attributes, cancellationToken).ConfigureAwait(false);
        foreach (var item in arrays.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (item.Value is null)
            {
                throw new ArgumentException($"Array '{item.Key}' cannot be null.", nameof(arrays));
            }

            await FromArrayAsyncCore(item.Value, store, PathUtilities.Combine(group.Path, item.Key), resolvedFormat, overwrite: true, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return group;
    }

    public static string Tree(ZarrGroup group, int? maxDepth = null, bool? useConsolidatedForChildren = null)
    {
        if (group is null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        return group.Tree(maxDepth, useConsolidatedForChildren);
    }

    public static CopySummary Copy(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", ZarrFormat? format = null, bool overwrite = false)
    {
        _ = Open(sourceStore, sourcePath, format);

        var entries = SnapshotEntries(sourceStore, sourcePath);
        if (entries.Count == 0)
        {
            throw new NodeNotFoundException(PathUtilities.Normalize(sourcePath));
        }

        return CopyEntries(entries, destinationStore, sourcePath, destinationPath, overwrite);
    }

    internal static async Task<CopySummary> CopyAsyncCore(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", ZarrFormat? format = null, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        _ = await OpenAsyncCore(sourceStore, sourcePath, format, cancellationToken: cancellationToken).ConfigureAwait(false);

        var entries = await SnapshotEntriesAsync(sourceStore, sourcePath, cancellationToken).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            throw new NodeNotFoundException(PathUtilities.Normalize(sourcePath));
        }

        return await CopyEntriesAsync(entries, destinationStore, sourcePath, destinationPath, overwrite, cancellationToken).ConfigureAwait(false);
    }

    public static CopySummary CopyAll(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", ZarrFormat? format = null, bool overwrite = false)
    {
        var sourceGroup = OpenGroup(sourceStore, sourcePath, format);
        _ = GetOrCreateGroup(destinationStore, destinationPath, sourceGroup.Format);

        var summary = new CopySummary(0, 0, 0);
        foreach (var child in sourceGroup.Members(0).OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            summary = summary.Add(Copy(
                sourceStore,
                destinationStore,
                PathUtilities.Combine(sourceGroup.Path, child.Key),
                PathUtilities.Combine(destinationPath, child.Key),
                sourceGroup.Format,
                overwrite));
        }

        return summary;
    }

    internal static async Task<CopySummary> CopyAllAsyncCore(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", ZarrFormat? format = null, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var sourceGroup = await OpenGroupAsyncCore(sourceStore, sourcePath, format, cancellationToken: cancellationToken).ConfigureAwait(false);
        _ = await GetOrCreateGroupAsync(destinationStore, destinationPath, sourceGroup.Format, cancellationToken).ConfigureAwait(false);

        var summary = new CopySummary(0, 0, 0);
        var children = (await sourceGroup.MembersAsyncCore(0, cancellationToken: cancellationToken).ConfigureAwait(false))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        foreach (var child in children)
        {
            summary = summary.Add(await CopyAsyncCore(
                sourceStore,
                destinationStore,
                PathUtilities.Combine(sourceGroup.Path, child.Key),
                PathUtilities.Combine(destinationPath, child.Key),
                sourceGroup.Format,
                overwrite,
                cancellationToken).ConfigureAwait(false));
        }

        return summary;
    }

    public static CopySummary CopyStore(IStore sourceStore, IStore destinationStore, string sourcePrefix = "", string destinationPrefix = "", bool overwrite = false)
    {
        var entries = SnapshotEntries(sourceStore, sourcePrefix);
        if (entries.Count == 0)
        {
            return new CopySummary(0, 0, 0);
        }

        return CopyEntries(entries, destinationStore, sourcePrefix, destinationPrefix, overwrite);
    }

    internal static async Task<CopySummary> CopyStoreAsyncCore(IStore sourceStore, IStore destinationStore, string sourcePrefix = "", string destinationPrefix = "", bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var entries = await SnapshotEntriesAsync(sourceStore, sourcePrefix, cancellationToken).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return new CopySummary(0, 0, 0);
        }

        return await CopyEntriesAsync(entries, destinationStore, sourcePrefix, destinationPrefix, overwrite, cancellationToken).ConfigureAwait(false);
    }

    public static IReadOnlyDictionary<string, ZarrNode> CreateHierarchy(IStore store, IReadOnlyDictionary<string, HierarchyNodeDefinition> nodes, ZarrFormat? format = null, bool overwrite = false)
    {
        if (nodes is null)
        {
            throw new ArgumentNullException(nameof(nodes));
        }

        if (nodes.Count == 0)
        {
            throw new ArgumentException("At least one hierarchy node definition must be provided.", nameof(nodes));
        }

        var normalizedNodes = NormalizeHierarchyDefinitions(nodes);
        ValidateHierarchyDefinitions(normalizedNodes);

        var created = new Dictionary<string, ZarrNode>(StringComparer.Ordinal);
        var implicitGroups = new HashSet<string>(EnumerateImplicitGroups(normalizedNodes.Keys), StringComparer.Ordinal);
        var resolvedFormat = ResolveFormat(format);
        var orderedPaths = implicitGroups
            .Concat(normalizedNodes.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(PathDepth)
            .ThenBy(path => path, StringComparer.Ordinal);

        foreach (var path in orderedPaths)
        {
            if (!normalizedNodes.TryGetValue(path, out var explicitNode))
            {
                created[path] = GetOrCreateGroup(store, path, resolvedFormat);
                continue;
            }

            created[path] = explicitNode switch
            {
                HierarchyGroupDefinition groupDefinition => CreateGroup(
                    store,
                    path,
                    resolvedFormat,
                    overwrite,
                    groupDefinition.Attributes),
                HierarchyArrayDefinition arrayDefinition => CreateArrayCore(
                    store,
                    arrayDefinition.DataType,
                    arrayDefinition.Shape,
                    arrayDefinition.Chunks,
                    path,
                    resolvedFormat,
                    overwrite,
                    arrayDefinition.Attributes,
                    arrayDefinition.FillValue,
                    arrayDefinition.Order,
                    arrayDefinition.ChunkCodec,
                    arrayDefinition.ChunkKeyEncoding,
                    arrayDefinition.CloneOptions()),
                _ => throw new InvalidOperationException($"Unsupported hierarchy node definition '{explicitNode.GetType().Name}'."),
            };
        }

        return created;
    }

    internal static async Task<IReadOnlyDictionary<string, ZarrNode>> CreateHierarchyAsyncCore(IStore store, IReadOnlyDictionary<string, HierarchyNodeDefinition> nodes, ZarrFormat? format = null, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (nodes is null)
        {
            throw new ArgumentNullException(nameof(nodes));
        }

        if (nodes.Count == 0)
        {
            throw new ArgumentException("At least one hierarchy node definition must be provided.", nameof(nodes));
        }

        var normalizedNodes = NormalizeHierarchyDefinitions(nodes);
        ValidateHierarchyDefinitions(normalizedNodes);

        var created = new Dictionary<string, ZarrNode>(StringComparer.Ordinal);
        var implicitGroups = new HashSet<string>(EnumerateImplicitGroups(normalizedNodes.Keys), StringComparer.Ordinal);
        var resolvedFormat = ResolveFormat(format);
        var orderedPaths = implicitGroups
            .Concat(normalizedNodes.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(PathDepth)
            .ThenBy(path => path, StringComparer.Ordinal);

        foreach (var path in orderedPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!normalizedNodes.TryGetValue(path, out var explicitNode))
            {
                created[path] = await GetOrCreateGroupAsync(store, path, resolvedFormat, cancellationToken).ConfigureAwait(false);
                continue;
            }

            switch (explicitNode)
            {
                case HierarchyGroupDefinition groupDefinition:
                    created[path] = await CreateGroupAsyncCore(
                        store,
                        path,
                        resolvedFormat,
                        overwrite,
                        groupDefinition.Attributes,
                        cancellationToken).ConfigureAwait(false);
                    break;

                case HierarchyArrayDefinition arrayDefinition:
                    created[path] = await CreateArrayAsyncCore(
                        store,
                        arrayDefinition.DataType,
                        arrayDefinition.Shape,
                        arrayDefinition.Chunks,
                        path,
                        resolvedFormat,
                        overwrite,
                        arrayDefinition.Attributes,
                        arrayDefinition.FillValue,
                        arrayDefinition.Order,
                        arrayDefinition.ChunkCodec,
                        arrayDefinition.ChunkKeyEncoding,
                        arrayDefinition.CloneOptions(),
                        cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported hierarchy node definition '{explicitNode.GetType().Name}'.");
            }
        }

        return created;
    }

    public static ZarrNode Migrate(IStore sourceStore, IStore destinationStore, ZarrFormat targetFormat, string sourcePath = "", string destinationPath = "", bool overwrite = false)
    {
        var sourceNode = Open(sourceStore, sourcePath);
        var targetPath = string.IsNullOrWhiteSpace(destinationPath)
            ? PathUtilities.Normalize(sourcePath)
            : PathUtilities.Normalize(destinationPath);

        return sourceNode switch
        {
            ZarrGroup sourceGroup => MigrateGroup(destinationStore, sourceGroup, targetPath, targetFormat, overwrite),
            ZarrArray sourceArray => MigrateArray(destinationStore, sourceArray, targetPath, targetFormat, overwrite),
            _ => throw new InvalidOperationException($"Unsupported node type '{sourceNode.GetType().Name}'."),
        };
    }

    internal static async Task<ZarrNode> MigrateAsyncCore(IStore sourceStore, IStore destinationStore, ZarrFormat targetFormat, string sourcePath = "", string destinationPath = "", bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var sourceNode = await OpenAsyncCore(sourceStore, sourcePath, cancellationToken: cancellationToken).ConfigureAwait(false);
        var targetPath = string.IsNullOrWhiteSpace(destinationPath)
            ? PathUtilities.Normalize(sourcePath)
            : PathUtilities.Normalize(destinationPath);

        return sourceNode switch
        {
            ZarrGroup sourceGroup => await MigrateGroupAsync(destinationStore, sourceGroup, targetPath, targetFormat, overwrite, cancellationToken).ConfigureAwait(false),
            ZarrArray sourceArray => await MigrateArrayAsync(destinationStore, sourceArray, targetPath, targetFormat, overwrite, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported node type '{sourceNode.GetType().Name}'."),
        };
    }

    public static ZarrNode MigrateV2ToV3(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", bool overwrite = false)
    {
        return Migrate(sourceStore, destinationStore, ZarrFormat.V3, sourcePath, destinationPath, overwrite);
    }

    public static ZarrNode MigrateV3ToV2(IStore sourceStore, IStore destinationStore, string sourcePath = "", string destinationPath = "", bool overwrite = false)
    {
        return Migrate(sourceStore, destinationStore, ZarrFormat.V2, sourcePath, destinationPath, overwrite);
    }

    public static ZarrGroup ConsolidateMetadata(IStore store, string path = "", ZarrFormat? format = null)
    {
        var group = OpenGroup(store, path, format, useConsolidated: false);
        MetadataStore.SaveConsolidatedMetadata(store, group.Path, group.Format);
        return OpenGroup(store, group.Path, group.Format, useConsolidated: true);
    }

    internal static async Task<ZarrGroup> ConsolidateMetadataAsyncCore(IStore store, string path = "", ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        var group = await OpenGroupAsyncCore(store, path, format, useConsolidated: false, cancellationToken).ConfigureAwait(false);
        await MetadataStore.SaveConsolidatedMetadataAsync(store, group.Path, group.Format, cancellationToken).ConfigureAwait(false);
        return await OpenGroupAsyncCore(store, group.Path, group.Format, useConsolidated: true, cancellationToken).ConfigureAwait(false);
    }

    public static ZarrGroup OpenConsolidated(IStore store, string path = "", ZarrFormat? format = null)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var resolvedFormat = format
            ?? (MetadataStore.HasConsolidatedMetadata(store, normalizedPath, ZarrFormat.V3) ? ZarrFormat.V3 : ZarrFormat.V2);

        if (!MetadataStore.HasConsolidatedMetadata(store, normalizedPath, resolvedFormat))
        {
            throw new NodeNotFoundException(MetadataStore.ConsolidatedMetadataKey(resolvedFormat, normalizedPath));
        }

        _ = MetadataStore.LoadConsolidatedMetadata(store, normalizedPath, resolvedFormat);
        return OpenGroup(store, normalizedPath, resolvedFormat, useConsolidated: true);
    }

    internal static async Task<ZarrGroup> OpenConsolidatedAsyncCore(IStore store, string path = "", ZarrFormat? format = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var resolvedFormat = format;
        if (resolvedFormat is null)
        {
            resolvedFormat = await MetadataStore.HasConsolidatedMetadataAsync(store, normalizedPath, ZarrFormat.V3, cancellationToken).ConfigureAwait(false)
                ? ZarrFormat.V3
                : ZarrFormat.V2;
        }

        if (!await MetadataStore.HasConsolidatedMetadataAsync(store, normalizedPath, resolvedFormat.Value, cancellationToken).ConfigureAwait(false))
        {
            throw new NodeNotFoundException(MetadataStore.ConsolidatedMetadataKey(resolvedFormat.Value, normalizedPath));
        }

        _ = await MetadataStore.LoadConsolidatedMetadataAsync(store, normalizedPath, resolvedFormat.Value, cancellationToken).ConfigureAwait(false);
        return await OpenGroupAsyncCore(store, normalizedPath, resolvedFormat.Value, useConsolidated: true, cancellationToken).ConfigureAwait(false);
    }

    public static ZarrGroup Group(IStore store, string path = "", ZarrFormat? format = null, bool overwrite = false, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        return CreateGroup(store, path, format, overwrite, attributes);
    }

    public static ZarrNode Open(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var preferConsolidated = useConsolidated ?? ZarrConfiguration.PreferConsolidatedMetadata;
        var resolvedFormatFromRequest = format;

        if (preferConsolidated)
        {
            foreach (var candidateFormat in EnumerateCandidateFormats(resolvedFormatFromRequest))
            {
                if (MetadataStore.TryLoadConsolidatedNodeMetadata(store, normalizedPath, candidateFormat, out var consolidatedMetadata))
                {
                    return BuildNodeFromMetadata(store, normalizedPath, consolidatedMetadata);
                }
            }
        }

        var detected = MetadataStore.DetectNode(store, normalizedPath);
        var resolvedFormat = format ?? detected.Format;

        return (detected.Kind, resolvedFormat) switch
        {
            (NodeKind.Group, not null) => OpenGroup(store, normalizedPath, resolvedFormat, useConsolidated),
            (NodeKind.Array, not null) => OpenArray(store, normalizedPath, resolvedFormat, useConsolidated),
            _ => throw new NodeNotFoundException(normalizedPath),
        };
    }

    internal static async Task<ZarrNode> OpenAsyncCore(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var preferConsolidated = useConsolidated ?? ZarrConfiguration.PreferConsolidatedMetadata;
        var resolvedFormatFromRequest = format;

        if (preferConsolidated)
        {
            foreach (var candidateFormat in EnumerateCandidateFormats(resolvedFormatFromRequest))
            {
                var consolidatedMetadata = await MetadataStore.TryLoadConsolidatedNodeMetadataAsync(store, normalizedPath, candidateFormat, cancellationToken).ConfigureAwait(false);
                if (consolidatedMetadata is not null)
                {
                    return BuildNodeFromMetadata(store, normalizedPath, consolidatedMetadata);
                }
            }
        }

        var detected = await MetadataStore.DetectNodeAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);
        var resolvedFormat = format ?? detected.Format;

        return (detected.Kind, resolvedFormat) switch
        {
            (NodeKind.Group, not null) => await OpenGroupAsyncCore(store, normalizedPath, resolvedFormat, useConsolidated, cancellationToken).ConfigureAwait(false),
            (NodeKind.Array, not null) => await OpenArrayAsyncCore(store, normalizedPath, resolvedFormat, useConsolidated, cancellationToken).ConfigureAwait(false),
            _ => throw new NodeNotFoundException(normalizedPath),
        };
    }

    internal static async Task<ZarrNode> OpenAsyncCore(IStore store, string path, ZarrOpenMode mode, ZarrFormat? format = null, IReadOnlyDictionary<string, object?>? attributes = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var existing = await MetadataStore.DetectNodeAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);
        var resolvedFormat = existing.Format ?? ResolveFormat(format);

        if (existing.Kind == NodeKind.Array && mode == ZarrOpenMode.Create)
        {
            var existingArray = await OpenArrayAsyncCore(store, normalizedPath, resolvedFormat, useConsolidated, cancellationToken).ConfigureAwait(false);
            return await CreateLikeAsyncCore(
                existingArray,
                store,
                normalizedPath,
                null,
                null,
                null,
                existingArray.FillValue,
                overwrite: true,
                existingArray.Attrs.AsDictionary(),
                cancellationToken).ConfigureAwait(false);
        }

        return existing.Kind switch
        {
            NodeKind.None => mode switch
            {
                ZarrOpenMode.Read or ZarrOpenMode.ReadWrite => throw new NodeNotFoundException(normalizedPath),
                ZarrOpenMode.Append => await CreateGroupAsyncCore(store, normalizedPath, format, overwrite: false, attributes, cancellationToken).ConfigureAwait(false),
                ZarrOpenMode.Create => await CreateGroupAsyncCore(store, normalizedPath, format, overwrite: true, attributes, cancellationToken).ConfigureAwait(false),
                ZarrOpenMode.CreateNew => await CreateGroupAsyncCore(store, normalizedPath, format, overwrite: false, attributes, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
            },
            NodeKind.Group => mode switch
            {
                ZarrOpenMode.Read => await OpenGroupAsyncCore(WrapReadOnlyStore(store), normalizedPath, resolvedFormat, useConsolidated, cancellationToken).ConfigureAwait(false),
                ZarrOpenMode.ReadWrite or ZarrOpenMode.Append => await OpenGroupAsyncCore(store, normalizedPath, resolvedFormat, useConsolidated, cancellationToken).ConfigureAwait(false),
                ZarrOpenMode.Create => await CreateGroupAsyncCore(store, normalizedPath, resolvedFormat, overwrite: true, attributes, cancellationToken).ConfigureAwait(false),
                ZarrOpenMode.CreateNew => throw new ContainsGroupException(normalizedPath),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
            },
            NodeKind.Array => mode switch
            {
                ZarrOpenMode.Read => await OpenArrayAsyncCore(WrapReadOnlyStore(store), normalizedPath, resolvedFormat, useConsolidated, cancellationToken).ConfigureAwait(false),
                ZarrOpenMode.ReadWrite or ZarrOpenMode.Append => await OpenArrayAsyncCore(store, normalizedPath, resolvedFormat, useConsolidated, cancellationToken).ConfigureAwait(false),
                ZarrOpenMode.Create => throw new InvalidOperationException(),
                ZarrOpenMode.CreateNew => throw new ContainsArrayException(normalizedPath),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
            },
            _ => throw new InvalidOperationException(),
        };
    }

    public static ZarrGroup OpenGroup(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var resolvedFormat = format ?? MetadataStore.DetectNode(store, normalizedPath).Format ?? ResolveFormat(null);
        if (MetadataStore.DetectNode(store, normalizedPath).Format is null && format is null)
        {
            throw new NodeNotFoundException(normalizedPath);
        }
        var preferConsolidated = useConsolidated ?? ZarrConfiguration.PreferConsolidatedMetadata;

        if (preferConsolidated &&
            MetadataStore.TryLoadConsolidatedNodeMetadata(store, normalizedPath, resolvedFormat, out var consolidatedMetadata) &&
            consolidatedMetadata is GroupMetadata groupMetadata)
        {
            return new ZarrGroup(store, normalizedPath, groupMetadata);
        }

        return new ZarrGroup(
            store,
            normalizedPath,
            preferConsolidated
                ? MetadataStore.LoadGroupWithConsolidatedMetadata(store, normalizedPath, resolvedFormat)
                : MetadataStore.LoadGroup(store, normalizedPath, resolvedFormat));
    }

    public static ZarrGroup OpenGroup(IStore store, string path, ZarrOpenMode mode, ZarrFormat? format = null, IReadOnlyDictionary<string, object?>? attributes = null, bool? useConsolidated = null)
    {
        var resolvedFormat = ResolveFormat(format);
        return mode switch
        {
            ZarrOpenMode.Read => OpenGroup(WrapReadOnlyStore(store), path, resolvedFormat, useConsolidated),
            ZarrOpenMode.ReadWrite => OpenGroup(store, path, resolvedFormat, useConsolidated),
            ZarrOpenMode.Append => MetadataStore.DetectNode(store, path).Kind == NodeKind.None
                ? CreateGroup(store, path, resolvedFormat, overwrite: false, attributes)
                : OpenGroup(store, path, resolvedFormat, useConsolidated),
            ZarrOpenMode.Create => CreateGroup(store, path, resolvedFormat, overwrite: true, attributes),
            ZarrOpenMode.CreateNew => CreateGroup(store, path, resolvedFormat, overwrite: false, attributes),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported open mode."),
        };
    }

    internal static async Task<ZarrGroup> OpenGroupAsyncCore(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var detected = await MetadataStore.DetectNodeAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);
        var resolvedFormat = format ?? detected.Format ?? ResolveFormat(null);
        if (detected.Format is null && format is null)
        {
            throw new NodeNotFoundException(normalizedPath);
        }
        var preferConsolidated = useConsolidated ?? ZarrConfiguration.PreferConsolidatedMetadata;

        if (preferConsolidated &&
            await MetadataStore.TryLoadConsolidatedNodeMetadataAsync(store, normalizedPath, resolvedFormat, cancellationToken).ConfigureAwait(false) is GroupMetadata groupMetadata)
        {
            return new ZarrGroup(store, normalizedPath, groupMetadata);
        }

        return new ZarrGroup(
            store,
            normalizedPath,
            preferConsolidated
                ? await MetadataStore.LoadGroupWithConsolidatedMetadataAsync(store, normalizedPath, resolvedFormat, cancellationToken).ConfigureAwait(false)
                : await MetadataStore.LoadGroupAsync(store, normalizedPath, resolvedFormat, cancellationToken).ConfigureAwait(false));
    }

    public static ZarrArray OpenArray(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var detected = MetadataStore.DetectNode(store, normalizedPath);
        var resolvedFormat = format ?? detected.Format ?? ResolveFormat(null);
        if (detected.Format is null && format is null)
        {
            throw new NodeNotFoundException(normalizedPath);
        }
        var preferConsolidated = useConsolidated ?? ZarrConfiguration.PreferConsolidatedMetadata;

        if (preferConsolidated &&
            MetadataStore.TryLoadConsolidatedNodeMetadata(store, normalizedPath, resolvedFormat, out var consolidatedMetadata) &&
            consolidatedMetadata is ArrayMetadata arrayMetadata)
        {
            return new ZarrArray(store, normalizedPath, arrayMetadata);
        }

        return new ZarrArray(store, normalizedPath, MetadataStore.LoadArray(store, normalizedPath, resolvedFormat));
    }

    public static ZarrArray OpenArray<T>(IStore store, string path = "", ZarrOpenMode mode = ZarrOpenMode.Append, int[]? shape = null, int[]? chunks = null, ZarrFormat? format = null, IReadOnlyDictionary<string, object?>? attributes = null, T? fillValue = default, ArrayOrder? order = null, IChunkCodec? chunkCodec = null, ChunkKeyEncoding? chunkKeyEncoding = null, V3ArrayMetadataOptions? v3Metadata = null)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var existing = MetadataStore.DetectNode(store, normalizedPath);
        var resolvedFormat = ResolveFormat(format);
        var resolvedOrder = ResolveOrder(order);

        return mode switch
        {
            ZarrOpenMode.Read => OpenArray(WrapReadOnlyStore(store), normalizedPath, existing.Format ?? resolvedFormat),
            ZarrOpenMode.ReadWrite => OpenArray(store, normalizedPath, existing.Format ?? resolvedFormat),
            ZarrOpenMode.Append when existing.Kind == NodeKind.None => CreateArray(store, typeof(T), shape ?? throw CreateShapeRequiredException(mode), chunks, normalizedPath, resolvedFormat, overwrite: false, attributes, fillValue, resolvedOrder, chunkCodec, chunkKeyEncoding, v3Metadata),
            ZarrOpenMode.Append => OpenArray(store, normalizedPath, existing.Format ?? resolvedFormat),
            ZarrOpenMode.Create => CreateArray(store, typeof(T), shape ?? throw CreateShapeRequiredException(mode), chunks, normalizedPath, resolvedFormat, overwrite: true, attributes, fillValue, resolvedOrder, chunkCodec, chunkKeyEncoding, v3Metadata),
            ZarrOpenMode.CreateNew => CreateArray(store, typeof(T), shape ?? throw CreateShapeRequiredException(mode), chunks, normalizedPath, resolvedFormat, overwrite: false, attributes, fillValue, resolvedOrder, chunkCodec, chunkKeyEncoding, v3Metadata),
            _ => OpenArray(store, normalizedPath, existing.Format ?? resolvedFormat),
        };
    }

    internal static async Task<ZarrArray> OpenArrayAsyncCore(IStore store, string path = "", ZarrFormat? format = null, bool? useConsolidated = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var detected = await MetadataStore.DetectNodeAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);
        var resolvedFormat = format ?? detected.Format ?? ResolveFormat(null);
        if (detected.Format is null && format is null)
        {
            throw new NodeNotFoundException(normalizedPath);
        }
        var preferConsolidated = useConsolidated ?? ZarrConfiguration.PreferConsolidatedMetadata;

        if (preferConsolidated &&
            await MetadataStore.TryLoadConsolidatedNodeMetadataAsync(store, normalizedPath, resolvedFormat, cancellationToken).ConfigureAwait(false) is ArrayMetadata arrayMetadata)
        {
            return new ZarrArray(store, normalizedPath, arrayMetadata);
        }

        return new ZarrArray(store, normalizedPath, await MetadataStore.LoadArrayAsync(store, normalizedPath, resolvedFormat, cancellationToken).ConfigureAwait(false));
    }

    private static ZarrArray CreateLike(ZarrArray reference, IStore store, string path, int[]? shape, int[]? chunks, Type? dtype, object? fillValue, bool overwrite, IReadOnlyDictionary<string, object?>? attributes)
    {
        var v3Metadata = reference.Format != ZarrFormat.V3
            ? null
            : new V3ArrayMetadataOptions
            {
                ChunkGrid = new RegularChunkGrid(chunks ?? reference.Chunks),
                DimensionNames = shape is null || shape.Length == reference.Shape.Length ? reference.DimensionNames : null,
                StorageTransformers = reference.StorageTransformers,
                ExtraFields = reference.ExtraFields,
            };

        return CreateArrayCore(
            store,
            dtype is null ? reference.DataType : DType.FromType(dtype),
            shape ?? reference.Shape,
            chunks ?? reference.Chunks,
            path,
            reference.Format,
            overwrite,
            attributes,
            fillValue,
            reference.Order,
            reference.ChunkCodec,
            reference.ChunkKeyEncoding,
            v3Metadata);
    }

    internal static Task<ZarrArray> CreateLikeAsyncCore(ZarrArray reference, IStore store, string path, int[]? shape, int[]? chunks, Type? dtype, object? fillValue, bool overwrite, IReadOnlyDictionary<string, object?>? attributes, CancellationToken cancellationToken)
    {
        var v3Metadata = reference.Format != ZarrFormat.V3
            ? null
            : new V3ArrayMetadataOptions
            {
                ChunkGrid = new RegularChunkGrid(chunks ?? reference.Chunks),
                DimensionNames = shape is null || shape.Length == reference.Shape.Length ? reference.DimensionNames : null,
                StorageTransformers = reference.StorageTransformers,
                ExtraFields = reference.ExtraFields,
            };

        return CreateArrayAsyncCore(
            store,
            dtype is null ? reference.DataType : DType.FromType(dtype),
            shape ?? reference.Shape,
            chunks ?? reference.Chunks,
            path,
            reference.Format,
            overwrite,
            attributes,
            fillValue,
            reference.Order,
            reference.ChunkCodec,
            reference.ChunkKeyEncoding,
            v3Metadata,
            cancellationToken);
    }

    private static ZarrArray CreateArrayCore(
        IStore store,
        DType elementType,
        int[] shape,
        int[]? chunks,
        string path,
        ZarrFormat? format,
        bool overwrite,
        IReadOnlyDictionary<string, object?>? attributes,
        object? fillValue,
        ArrayOrder? order,
        IChunkCodec? chunkCodec,
        ChunkKeyEncoding? chunkKeyEncoding,
        V3ArrayMetadataOptions? v3Metadata)
    {
        var resolvedFormat = ResolveFormat(format);
        var resolvedOrder = ResolveOrder(order);
        if (shape is null || shape.Length == 0)
        {
            throw new ArgumentException("shape must contain at least one dimension.", nameof(shape));
        }

        var normalizedShape = shape.ToArray();
        if (normalizedShape.Any(dimension => dimension <= 0))
        {
            throw new ArgumentException("shape values must be positive.", nameof(shape));
        }

        var chunkGridShape = TryGetChunkGridShape(v3Metadata?.ChunkGrid);
        var normalizedChunks = chunks?.ToArray() ?? chunkGridShape ?? shape.ToArray();
        if (normalizedChunks.Length != normalizedShape.Length)
        {
            throw new ArgumentException("chunks must have the same rank as shape.", nameof(chunks));
        }

        if (normalizedChunks.Any(chunk => chunk <= 0))
        {
            throw new ArgumentException("chunk sizes must be positive.", nameof(chunks));
        }

        var normalizedPath = PathUtilities.Normalize(path);
        EnsureParentGroups(store, normalizedPath, resolvedFormat);
        EnsureCreatable(store, normalizedPath, overwrite);

        var metadata = MetadataStore.CreateArrayMetadata(
            resolvedFormat,
            normalizedShape,
            normalizedChunks,
            elementType,
            fillValue,
            JsonUtilities.CloneDictionary(attributes),
            resolvedOrder,
            chunkCodec,
            chunkKeyEncoding,
            v3Metadata);

        MetadataStore.SaveArray(store, normalizedPath, metadata);
        return new ZarrArray(store, normalizedPath, metadata);
    }

    internal static async Task<ZarrArray> CreateArrayAsyncCore(
        IStore store,
        DType elementType,
        int[] shape,
        int[]? chunks,
        string path,
        ZarrFormat? format,
        bool overwrite,
        IReadOnlyDictionary<string, object?>? attributes,
        object? fillValue,
        ArrayOrder? order,
        IChunkCodec? chunkCodec,
        ChunkKeyEncoding? chunkKeyEncoding,
        V3ArrayMetadataOptions? v3Metadata,
        CancellationToken cancellationToken)
    {
        var resolvedFormat = ResolveFormat(format);
        var resolvedOrder = ResolveOrder(order);
        if (shape is null || shape.Length == 0)
        {
            throw new ArgumentException("shape must contain at least one dimension.", nameof(shape));
        }

        var normalizedShape = shape.ToArray();
        if (normalizedShape.Any(dimension => dimension <= 0))
        {
            throw new ArgumentException("shape values must be positive.", nameof(shape));
        }

        var chunkGridShape = TryGetChunkGridShape(v3Metadata?.ChunkGrid);
        var normalizedChunks = chunks?.ToArray() ?? chunkGridShape ?? shape.ToArray();
        if (normalizedChunks.Length != normalizedShape.Length)
        {
            throw new ArgumentException("chunks must have the same rank as shape.", nameof(chunks));
        }

        if (normalizedChunks.Any(chunk => chunk <= 0))
        {
            throw new ArgumentException("chunk sizes must be positive.", nameof(chunks));
        }

        var normalizedPath = PathUtilities.Normalize(path);
        await EnsureParentGroupsAsync(store, normalizedPath, resolvedFormat, cancellationToken).ConfigureAwait(false);
        await EnsureCreatableAsync(store, normalizedPath, overwrite, cancellationToken).ConfigureAwait(false);

        var metadata = MetadataStore.CreateArrayMetadata(
            resolvedFormat,
            normalizedShape,
            normalizedChunks,
            elementType,
            fillValue,
            JsonUtilities.CloneDictionary(attributes),
            resolvedOrder,
            chunkCodec,
            chunkKeyEncoding,
            v3Metadata);

        await MetadataStore.SaveArrayAsync(store, normalizedPath, metadata, cancellationToken).ConfigureAwait(false);
        return new ZarrArray(store, normalizedPath, metadata);
    }

    private static void EnsureCreatable(IStore store, string path, bool overwrite)
    {
        var existing = MetadataStore.DetectNode(store, path);
        if (existing.Kind == NodeKind.None)
        {
            return;
        }

        if (!overwrite)
        {
            throw existing.Kind switch
            {
                NodeKind.Array => new ContainsArrayException(path),
                NodeKind.Group => new ContainsGroupException(path),
                _ => new InvalidOperationException(),
            };
        }

        store.DeletePrefix(path);
    }

    private static async Task EnsureCreatableAsync(IStore store, string path, bool overwrite, CancellationToken cancellationToken)
    {
        var existing = await MetadataStore.DetectNodeAsync(store, path, cancellationToken).ConfigureAwait(false);
        if (existing.Kind == NodeKind.None)
        {
            return;
        }

        if (!overwrite)
        {
            throw existing.Kind switch
            {
                NodeKind.Array => new ContainsArrayException(path),
                NodeKind.Group => new ContainsGroupException(path),
                _ => new InvalidOperationException(),
            };
        }

        await store.DeletePrefixAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureParentGroups(IStore store, string path, ZarrFormat format)
    {
        foreach (var parentPath in PathUtilities.EnumerateParentPaths(path))
        {
            var existing = MetadataStore.DetectNode(store, parentPath);
            if (existing.Kind == NodeKind.None)
            {
                MetadataStore.SaveGroup(store, parentPath, MetadataStore.CreateGroupMetadata(format, new Dictionary<string, object?>(StringComparer.Ordinal)));
                continue;
            }

            if (existing.Kind == NodeKind.Array)
            {
                throw new ContainsArrayException(parentPath);
            }
        }
    }

    private static async Task EnsureParentGroupsAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken)
    {
        foreach (var parentPath in PathUtilities.EnumerateParentPaths(path))
        {
            var existing = await MetadataStore.DetectNodeAsync(store, parentPath, cancellationToken).ConfigureAwait(false);
            if (existing.Kind == NodeKind.None)
            {
                await MetadataStore.SaveGroupAsync(
                    store,
                    parentPath,
                    MetadataStore.CreateGroupMetadata(format, new Dictionary<string, object?>(StringComparer.Ordinal)),
                    cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (existing.Kind == NodeKind.Array)
            {
                throw new ContainsArrayException(parentPath);
            }
        }
    }

    private static ZarrGroup GetOrCreateGroup(IStore store, string path, ZarrFormat format)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var detected = MetadataStore.DetectNode(store, normalizedPath);
        return detected.Kind switch
        {
            NodeKind.None => CreateGroup(store, normalizedPath, format),
            NodeKind.Group => OpenGroup(store, normalizedPath, detected.Format ?? format),
            NodeKind.Array => throw new ContainsArrayException(normalizedPath),
            _ => throw new InvalidOperationException(),
        };
    }

    private static async Task<ZarrGroup> GetOrCreateGroupAsync(IStore store, string path, ZarrFormat format, CancellationToken cancellationToken)
    {
        var normalizedPath = PathUtilities.Normalize(path);
        var detected = await MetadataStore.DetectNodeAsync(store, normalizedPath, cancellationToken).ConfigureAwait(false);
        return detected.Kind switch
        {
            NodeKind.None => await CreateGroupAsyncCore(store, normalizedPath, format, cancellationToken: cancellationToken).ConfigureAwait(false),
            NodeKind.Group => await OpenGroupAsyncCore(store, normalizedPath, detected.Format ?? format, cancellationToken: cancellationToken).ConfigureAwait(false),
            NodeKind.Array => throw new ContainsArrayException(normalizedPath),
            _ => throw new InvalidOperationException(),
        };
    }

    private static InvalidOperationException CreateShapeRequiredException(ZarrOpenMode mode)
    {
        return new InvalidOperationException($"Opening an array with mode '{mode}' requires a shape when the array does not already exist.");
    }

    private static IStore WrapReadOnlyStore(IStore store)
    {
        if (!ZarrConfiguration.PreferReadOnlyWrapping)
        {
            return store;
        }

        return store is ReadOnlyStore ? store : new ReadOnlyStore(store);
    }

    private static ZarrFormat ResolveFormat(ZarrFormat? format)
    {
        return format ?? ZarrConfiguration.DefaultFormat;
    }

    private static ArrayOrder ResolveOrder(ArrayOrder? order)
    {
        return order ?? ZarrConfiguration.DefaultArrayOrder;
    }

    private static CopySummary CopyEntries(IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> entries, IStore destinationStore, string sourcePrefix, string destinationPrefix, bool overwrite)
    {
        var normalizedSource = PathUtilities.Normalize(sourcePrefix);
        var normalizedDestination = PathUtilities.Normalize(destinationPrefix);

        if (overwrite)
        {
            destinationStore.DeletePrefix(normalizedDestination);
        }

        var bytesCopied = 0L;
        foreach (var entry in entries)
        {
            var targetKey = RemapKey(entry.Key, normalizedSource, normalizedDestination);
            if (!overwrite && destinationStore.Exists(targetKey))
            {
                throw new InvalidOperationException($"A store entry already exists at '{targetKey}'.");
            }

            destinationStore.Set(targetKey, entry.Value);
            bytesCopied += entry.Value.Length;
        }

        return new CopySummary(CountMetadataEntries(entries), entries.Count, bytesCopied);
    }

    private static async Task<CopySummary> CopyEntriesAsync(IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> entries, IStore destinationStore, string sourcePrefix, string destinationPrefix, bool overwrite, CancellationToken cancellationToken)
    {
        var normalizedSource = PathUtilities.Normalize(sourcePrefix);
        var normalizedDestination = PathUtilities.Normalize(destinationPrefix);

        if (overwrite)
        {
            await destinationStore.DeletePrefixAsync(normalizedDestination, cancellationToken).ConfigureAwait(false);
        }

        var bytesCopied = 0L;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetKey = RemapKey(entry.Key, normalizedSource, normalizedDestination);
            if (!overwrite && await destinationStore.ExistsAsync(targetKey, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"A store entry already exists at '{targetKey}'.");
            }

            await destinationStore.SetAsync(targetKey, entry.Value, cancellationToken).ConfigureAwait(false);
            bytesCopied += entry.Value.Length;
        }

        return new CopySummary(CountMetadataEntries(entries), entries.Count, bytesCopied);
    }

    private static List<KeyValuePair<string, ReadOnlyMemory<byte>>> SnapshotEntries(IStore store, string prefix)
    {
        return store.ListKeys(prefix)
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new KeyValuePair<string, ReadOnlyMemory<byte>>(key, store.Get(key) ?? throw new MetadataException($"Missing store entry '{key}' during copy.")))
            .ToList();
    }

    private static async Task<List<KeyValuePair<string, ReadOnlyMemory<byte>>>> SnapshotEntriesAsync(IStore store, string prefix, CancellationToken cancellationToken)
    {
        var keys = (await store.ListKeysAsync(prefix, cancellationToken).ConfigureAwait(false))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var entries = new List<KeyValuePair<string, ReadOnlyMemory<byte>>>(keys.Length);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new KeyValuePair<string, ReadOnlyMemory<byte>>(
                key,
                await store.GetAsync(key, cancellationToken).ConfigureAwait(false) ?? throw new MetadataException($"Missing store entry '{key}' during copy.")));
        }

        return entries;
    }

    private static string RemapKey(string key, string sourcePrefix, string destinationPrefix)
    {
        if (sourcePrefix.Length == 0)
        {
            return PathUtilities.Combine(destinationPrefix, key);
        }

        if (key.Equals(sourcePrefix, StringComparison.Ordinal))
        {
            return destinationPrefix;
        }

        if (!key.StartsWith(sourcePrefix + "/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Store key '{key}' does not belong to source prefix '{sourcePrefix}'.");
        }

        return PathUtilities.Combine(destinationPrefix, key[(sourcePrefix.Length + 1)..]);
    }

    private static int CountMetadataEntries(IEnumerable<KeyValuePair<string, ReadOnlyMemory<byte>>> entries)
    {
        return entries.Count(entry =>
            entry.Key.Equals(".zgroup", StringComparison.Ordinal) ||
            entry.Key.EndsWith("/.zgroup", StringComparison.Ordinal) ||
            entry.Key.Equals(".zarray", StringComparison.Ordinal) ||
            entry.Key.EndsWith("/.zarray", StringComparison.Ordinal) ||
            entry.Key.Equals("zarr.json", StringComparison.Ordinal) ||
            entry.Key.EndsWith("/zarr.json", StringComparison.Ordinal));
    }

    private static object LoadNodeData(ZarrNode node)
    {
        return node switch
        {
            ZarrArray array => array.Read(),
            ZarrGroup group => LoadGroupData(group),
            _ => throw new InvalidOperationException($"Unsupported node type '{node.GetType().Name}'."),
        };
    }

    private static async Task<object> LoadNodeDataAsync(ZarrNode node, CancellationToken cancellationToken)
    {
        return node switch
        {
            ZarrArray array => await array.ReadAsyncCore(cancellationToken).ConfigureAwait(false),
            ZarrGroup group => await LoadGroupDataAsync(group, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported node type '{node.GetType().Name}'."),
        };
    }

    private static IReadOnlyDictionary<string, object> LoadGroupData(ZarrGroup group)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var child in group.Members(0).OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            result[child.Key] = LoadNodeData(child.Value);
        }

        return result;
    }

    private static async Task<IReadOnlyDictionary<string, object>> LoadGroupDataAsync(ZarrGroup group, CancellationToken cancellationToken)
    {
        var children = (await group.MembersAsyncCore(0, cancellationToken: cancellationToken).ConfigureAwait(false))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[child.Key] = await LoadNodeDataAsync(child.Value, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    internal static IReadOnlyDictionary<string, Array> ToPositionalArrayMap(IReadOnlyList<Array> arrays)
    {
        if (arrays is null)
        {
            throw new ArgumentNullException(nameof(arrays));
        }

        if (arrays.Count == 0)
        {
            throw new ArgumentException("At least one array must be provided.", nameof(arrays));
        }

        var result = new Dictionary<string, Array>(StringComparer.Ordinal);
        for (var index = 0; index < arrays.Count; index++)
        {
            var value = arrays[index] ?? throw new ArgumentException($"Array 'arr_{index}' cannot be null.", nameof(arrays));
            result[$"arr_{index}"] = value;
        }

        return result;
    }

    private static ZarrNode BuildNodeFromMetadata(IStore store, string path, object metadata)
    {
        return metadata switch
        {
            GroupMetadata groupMetadata => new ZarrGroup(store, path, groupMetadata),
            ArrayMetadata arrayMetadata => new ZarrArray(store, path, arrayMetadata),
            _ => throw new MetadataException($"Unsupported metadata node '{metadata.GetType().Name}'."),
        };
    }

    private static IEnumerable<ZarrFormat> EnumerateCandidateFormats(ZarrFormat? requestedFormat)
    {
        if (requestedFormat is not null)
        {
            yield return requestedFormat.Value;
            yield break;
        }

        yield return ZarrFormat.V3;
        yield return ZarrFormat.V2;
    }

    private static ZarrGroup MigrateGroup(IStore destinationStore, ZarrGroup sourceGroup, string destinationPath, ZarrFormat targetFormat, bool overwrite)
    {
        ValidateGroupMigration(sourceGroup, targetFormat);

        var destinationGroup = CreateGroup(
            destinationStore,
            destinationPath,
            targetFormat,
            overwrite,
            sourceGroup.Attrs.AsDictionary());

        foreach (var child in sourceGroup.Members(0).OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var destinationChildPath = PathUtilities.Combine(destinationGroup.Path, child.Key);
            switch (child.Value)
            {
                case ZarrGroup childGroup:
                    _ = MigrateGroup(destinationStore, childGroup, destinationChildPath, targetFormat, overwrite: false);
                    break;

                case ZarrArray childArray:
                    _ = MigrateArray(destinationStore, childArray, destinationChildPath, targetFormat, overwrite: false);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported node type '{child.Value.GetType().Name}'.");
            }
        }

        return destinationGroup;
    }

    private static async Task<ZarrGroup> MigrateGroupAsync(IStore destinationStore, ZarrGroup sourceGroup, string destinationPath, ZarrFormat targetFormat, bool overwrite, CancellationToken cancellationToken)
    {
        ValidateGroupMigration(sourceGroup, targetFormat);

        var destinationGroup = await CreateGroupAsyncCore(
            destinationStore,
            destinationPath,
            targetFormat,
            overwrite,
            sourceGroup.Attrs.AsDictionary(),
            cancellationToken).ConfigureAwait(false);

        var children = (await sourceGroup.MembersAsyncCore(0, cancellationToken: cancellationToken).ConfigureAwait(false))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        foreach (var child in children)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationChildPath = PathUtilities.Combine(destinationGroup.Path, child.Key);
            switch (child.Value)
            {
                case ZarrGroup childGroup:
                    _ = await MigrateGroupAsync(destinationStore, childGroup, destinationChildPath, targetFormat, overwrite: false, cancellationToken).ConfigureAwait(false);
                    break;

                case ZarrArray childArray:
                    _ = await MigrateArrayAsync(destinationStore, childArray, destinationChildPath, targetFormat, overwrite: false, cancellationToken).ConfigureAwait(false);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported node type '{child.Value.GetType().Name}'.");
            }
        }

        return destinationGroup;
    }

    private static ZarrArray MigrateArray(IStore destinationStore, ZarrArray sourceArray, string destinationPath, ZarrFormat targetFormat, bool overwrite)
    {
        ValidateArrayMigration(sourceArray, targetFormat);

        var targetChunkCodec = targetFormat == ZarrFormat.V2
            ? ChunkCodecFactory.FromV2Metadata(
                ChunkCodecFactory.ToV2FiltersMetadata(sourceArray.ChunkCodec, sourceArray.DataType),
                ChunkCodecFactory.ToV2CompressorMetadata(sourceArray.ChunkCodec, sourceArray.DataType))
            : sourceArray.ChunkCodec;

        var destinationArray = CreateArrayCore(
            destinationStore,
            sourceArray.DataType,
            sourceArray.Shape,
            sourceArray.Chunks,
            destinationPath,
            targetFormat,
            overwrite,
            sourceArray.Attrs.AsDictionary(),
            sourceArray.FillValue,
            sourceArray.Order,
            targetChunkCodec,
            targetFormat == ZarrFormat.V2
                ? new V2ChunkKeyEncoding(sourceArray.ChunkKeyEncoding.Separator)
                : sourceArray.ChunkKeyEncoding,
            targetFormat == ZarrFormat.V3
                ? BuildV3MigrationMetadata(sourceArray)
                : null);

        destinationArray.Write(sourceArray.Read());
        return destinationArray;
    }

    private static async Task<ZarrArray> MigrateArrayAsync(IStore destinationStore, ZarrArray sourceArray, string destinationPath, ZarrFormat targetFormat, bool overwrite, CancellationToken cancellationToken)
    {
        ValidateArrayMigration(sourceArray, targetFormat);

        var targetChunkCodec = targetFormat == ZarrFormat.V2
            ? ChunkCodecFactory.FromV2Metadata(
                ChunkCodecFactory.ToV2FiltersMetadata(sourceArray.ChunkCodec, sourceArray.DataType),
                ChunkCodecFactory.ToV2CompressorMetadata(sourceArray.ChunkCodec, sourceArray.DataType))
            : sourceArray.ChunkCodec;

        var destinationArray = await CreateArrayAsyncCore(
            destinationStore,
            sourceArray.DataType,
            sourceArray.Shape,
            sourceArray.Chunks,
            destinationPath,
            targetFormat,
            overwrite,
            sourceArray.Attrs.AsDictionary(),
            sourceArray.FillValue,
            sourceArray.Order,
            targetChunkCodec,
            targetFormat == ZarrFormat.V2
                ? new V2ChunkKeyEncoding(sourceArray.ChunkKeyEncoding.Separator)
                : sourceArray.ChunkKeyEncoding,
            targetFormat == ZarrFormat.V3
                ? BuildV3MigrationMetadata(sourceArray)
                : null,
            cancellationToken).ConfigureAwait(false);

        await destinationArray.WriteAsyncCore(await sourceArray.ReadAsyncCore(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return destinationArray;
    }

    private static V3ArrayMetadataOptions BuildV3MigrationMetadata(ZarrArray sourceArray)
    {
        return new V3ArrayMetadataOptions
        {
            ChunkGrid = sourceArray.ChunkGrid,
            DimensionNames = sourceArray.DimensionNames,
            StorageTransformers = sourceArray.StorageTransformers,
            ExtraFields = sourceArray.ExtraFields,
        };
    }

    private static void ValidateArrayMigration(ZarrArray sourceArray, ZarrFormat targetFormat)
    {
        if (targetFormat != ZarrFormat.V2)
        {
            return;
        }

        var unsupported = new List<string>();
        if (!string.Equals(sourceArray.ChunkGrid.Name, "regular", StringComparison.Ordinal))
        {
            unsupported.Add("chunk_grid");
        }

        if (sourceArray.DimensionNames is not null)
        {
            unsupported.Add("dimension_names");
        }

        if (sourceArray.StorageTransformers.Count > 0)
        {
            unsupported.Add("storage_transformers");
        }

        if (sourceArray.ExtraFields.Count > 0)
        {
            unsupported.Add("extra_fields");
        }

        if (unsupported.Count == 0)
        {
            return;
        }

        throw new MetadataException(
            $"Cannot migrate array '{sourceArray.Path}' to Zarr format V2 because it uses v3-only metadata: {string.Join(", ", unsupported.Distinct(StringComparer.Ordinal))}.");
    }

    private static void ValidateGroupMigration(ZarrGroup sourceGroup, ZarrFormat targetFormat)
    {
        if (targetFormat != ZarrFormat.V2 || sourceGroup.ExtraFields.Count == 0)
        {
            return;
        }

        throw new MetadataException(
            $"Cannot migrate group '{sourceGroup.Path}' to Zarr format V2 because it uses v3-only metadata: extra_fields.");
    }

    private static int[]? TryGetChunkGridShape(ChunkGrid? chunkGrid)
    {
        if (chunkGrid is null || !chunkGrid.SupportsDataAccess)
        {
            return null;
        }

        return chunkGrid.ChunkShape;
    }

    private static Dictionary<string, HierarchyNodeDefinition> NormalizeHierarchyDefinitions(IReadOnlyDictionary<string, HierarchyNodeDefinition> nodes)
    {
        var normalized = new Dictionary<string, HierarchyNodeDefinition>(StringComparer.Ordinal);
        foreach (var item in nodes)
        {
            var path = PathUtilities.Normalize(item.Key);
            if (!normalized.TryAdd(path, item.Value))
            {
                throw new ArgumentException($"Duplicate hierarchy node definition for path '{path}'.", nameof(nodes));
            }
        }

        return normalized;
    }

    private static void ValidateHierarchyDefinitions(IReadOnlyDictionary<string, HierarchyNodeDefinition> nodes)
    {
        var paths = nodes.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        foreach (var path in paths)
        {
            if (nodes[path] is not HierarchyArrayDefinition)
            {
                continue;
            }

            if (paths.Any(other => !string.Equals(other, path, StringComparison.Ordinal) &&
                                   other.StartsWith(path.Length == 0 ? string.Empty : path + "/", StringComparison.Ordinal)))
            {
                throw new ArgumentException($"Array node '{path}' cannot have child nodes in the hierarchy definition.", nameof(nodes));
            }
        }
    }

    private static IEnumerable<string> EnumerateImplicitGroups(IEnumerable<string> explicitPaths)
    {
        var explicitPathSet = new HashSet<string>(explicitPaths, StringComparer.Ordinal);
        var implicitGroups = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in explicitPathSet)
        {
            foreach (var parent in PathUtilities.EnumerateParentPaths(path))
            {
                if (!explicitPathSet.Contains(parent))
                {
                    implicitGroups.Add(parent);
                }
            }
        }

        return implicitGroups
            .OrderBy(PathDepth)
            .ThenBy(path => path, StringComparer.Ordinal);
    }

    private static int PathDepth(string path)
    {
        return string.IsNullOrEmpty(path)
            ? 0
            : path.Count(character => character == '/') + 1;
    }
}
