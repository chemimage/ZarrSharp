namespace Zarr.Core;

public sealed class ZarrArray : ZarrNode
{
    private readonly Action<object?>? _consolidatedMutationCallback;
    private int[] _shape;
    private readonly int[] _chunks;
    private readonly object _fillValue;
    private readonly IChunkCodec? _chunkCodec;
    private readonly ChunkGrid _chunkGrid;
    private readonly ChunkKeyEncoding _chunkKeyEncoding;
    private readonly string?[]? _dimensionNames;
    private readonly Dictionary<string, object?>[] _storageTransformers;
    private readonly IStorageTransformer[] _parsedStorageTransformers;
    private readonly Dictionary<string, Dictionary<string, object?>> _extraFields;
    private readonly Dictionary<string, IMetadataExtension> _parsedExtensions;

    internal ZarrArray(IStore store, string path, ArrayMetadata metadata, Action<object?>? consolidatedMutationCallback = null)
        : base(store, path, metadata.Format, metadata.Attributes)
    {
        _consolidatedMutationCallback = consolidatedMutationCallback;
        _shape = metadata.Shape.ToArray();
        _chunks = metadata.Chunks.ToArray();
        DataType = metadata.DataType;
        _fillValue = metadata.FillValue;
        Order = metadata.Order;
        _chunkCodec = metadata.ChunkCodec;
        _chunkGrid = metadata.ChunkGrid;
        _chunkKeyEncoding = metadata.ChunkKeyEncoding;
        _dimensionNames = metadata.DimensionNames?.ToArray();
        _storageTransformers = metadata.StorageTransformers.Select(JsonUtilities.CloneDictionary).ToArray();
        _parsedStorageTransformers = metadata.ParsedStorageTransformers.ToArray();
        _extraFields = metadata.ExtraFields.ToDictionary(
            item => item.Key,
            item => JsonUtilities.CloneDictionary(item.Value),
            StringComparer.Ordinal);
        _parsedExtensions = metadata.ParsedExtensions.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal);
    }

    public int[] Shape => _shape.ToArray();

    public int[] Chunks => _chunks.ToArray();

    public DType DataType { get; }

    public object FillValue => _fillValue;

    public ArrayOrder Order { get; }

    public IChunkCodec? ChunkCodec => _chunkCodec;

    public IReadOnlyList<IChunkCodec> ChunkCodecs => ChunkCodecFactory.Expand(_chunkCodec);

    public ChunkGrid ChunkGrid => _chunkGrid;

    public ChunkKeyEncoding ChunkKeyEncoding => _chunkKeyEncoding;

    public string?[]? DimensionNames => _dimensionNames?.ToArray();

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> StorageTransformers => _storageTransformers
        .Select(item => (IReadOnlyDictionary<string, object?>)JsonUtilities.CloneDictionary(item))
        .ToArray();

    public IReadOnlyList<IStorageTransformer> ParsedStorageTransformers => _parsedStorageTransformers.ToArray();

    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> ExtraFields => _extraFields.ToDictionary(
        item => item.Key,
        item => (IReadOnlyDictionary<string, object?>)JsonUtilities.CloneDictionary(item.Value),
        StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IMetadataExtension> Extensions => _parsedExtensions.ToDictionary(
        item => item.Key,
        item => item.Value,
        StringComparer.Ordinal);

    public int Length => _shape.Aggregate(1, (current, value) => current * value);

    public int InitializedChunkCount
    {
        get
        {
            var prefix = Path.Length == 0 ? string.Empty : Path + "/";

            return Store.ListKeys(prefix)
                .Count(key => key != MetadataStore.V2ArrayMetadataKey(Path) &&
                              key != MetadataStore.V2AttributesKey(Path) &&
                              key != MetadataStore.V3MetadataKey(Path));
        }
    }

    public ArrayInfo Info()
    {
        long? logicalBytes = DataType.ByteSize > 0 ? (long)Length * DataType.ByteSize : null;
        var storedBytes = Store.ListKeys(Path)
            .Select(key => (long)(Store.Get(key)?.Length ?? 0))
            .Sum();

        return new ArrayInfo(
            Format,
            DataType.Name,
            _fillValue,
            _shape,
            _chunkGrid.SupportsDataAccess ? _chunkGrid.ChunkShape : null,
            Order,
            Store.GetType().Name,
            ChunkCodecs.Select(codec => codec.Id).ToArray(),
            logicalBytes,
            storedBytes,
            InitializedChunkCount);
    }

    internal async Task<ArrayInfo> InfoAsyncCore(CancellationToken cancellationToken = default)
    {
        long? logicalBytes = DataType.ByteSize > 0 ? (long)Length * DataType.ByteSize : null;
        var keys = await Store.ListKeysAsync(Path, cancellationToken).ConfigureAwait(false);
        long storedBytes = 0L;
        foreach (var key in keys)
        {
            storedBytes += (await Store.GetAsync(key, cancellationToken).ConfigureAwait(false))?.Length ?? 0;
        }

        return new ArrayInfo(
            Format,
            DataType.Name,
            _fillValue,
            _shape,
            _chunkGrid.SupportsDataAccess ? _chunkGrid.ChunkShape : null,
            Order,
            Store.GetType().Name,
            ChunkCodecs.Select(codec => codec.Id).ToArray(),
            logicalBytes,
            storedBytes,
            keys.Count(key => key != MetadataStore.V2ArrayMetadataKey(Path) &&
                              key != MetadataStore.V2AttributesKey(Path) &&
                              key != MetadataStore.V3MetadataKey(Path)));
    }

    public void Resize(int[] newShape, bool deleteOutsideChunks = true)
    {
        EnsureRuntimeSupported();
        var normalizedShape = NormalizeNewShape(newShape);
        var oldShape = _shape.ToArray();
        var onlyGrowing = normalizedShape.Zip(oldShape, (next, current) => next >= current).All(value => value);

        if (deleteOutsideChunks && !onlyGrowing)
        {
            var oldKeys = _chunkGrid.AllChunkCoordinates(oldShape)
                .Select(GetChunkKey)
                .ToHashSet(StringComparer.Ordinal);
            var newKeys = _chunkGrid.AllChunkCoordinates(normalizedShape)
                .Select(GetChunkKey)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var key in oldKeys.Except(newKeys, StringComparer.Ordinal))
            {
                Store.Delete(key);
            }
        }

        _shape = normalizedShape;
        PersistMetadata();
    }

    internal async Task ResizeAsyncCore(int[] newShape, bool deleteOutsideChunks = true, CancellationToken cancellationToken = default)
    {
        EnsureRuntimeSupported();
        var normalizedShape = NormalizeNewShape(newShape);
        var oldShape = _shape.ToArray();
        var onlyGrowing = normalizedShape.Zip(oldShape, (next, current) => next >= current).All(value => value);

        if (deleteOutsideChunks && !onlyGrowing)
        {
            var oldKeys = _chunkGrid.AllChunkCoordinates(oldShape)
                .Select(GetChunkKey)
                .ToHashSet(StringComparer.Ordinal);
            var newKeys = _chunkGrid.AllChunkCoordinates(normalizedShape)
                .Select(GetChunkKey)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var key in oldKeys.Except(newKeys, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Store.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }

        _shape = normalizedShape;
        await PersistMetadataAsyncCore(cancellationToken).ConfigureAwait(false);
    }

    public int[] Append(Array values, int axis = 0)
    {
        EnsureRuntimeSupported();
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var normalizedAxis = NormalizeAxis(axis, _shape.Length);
        if (values.Rank != _shape.Length)
        {
            throw new ArgumentException("Input rank does not match the array shape.", nameof(values));
        }

        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            if (dimension == normalizedAxis)
            {
                continue;
            }

            if (values.GetLength(dimension) != _shape[dimension])
            {
                throw new ArgumentException("All dimensions other than the append axis must match the existing array shape.", nameof(values));
            }
        }

        var oldShape = _shape.ToArray();
        var newShape = oldShape.ToArray();
        newShape[normalizedAxis] += values.GetLength(normalizedAxis);

        Resize(newShape, deleteOutsideChunks: false);

        var selection = new ArraySelection[_shape.Length];
        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            selection[dimension] = dimension == normalizedAxis
                ? ArraySelection.Slice(oldShape[dimension], newShape[dimension])
                : ArraySelection.All;
        }

        WriteSelection(values, selection);
        return _shape.ToArray();
    }

    internal async Task<int[]> AppendAsyncCore(Array values, int axis = 0, CancellationToken cancellationToken = default)
    {
        EnsureRuntimeSupported();
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var normalizedAxis = NormalizeAxis(axis, _shape.Length);
        if (values.Rank != _shape.Length)
        {
            throw new ArgumentException("Input rank does not match the array shape.", nameof(values));
        }

        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            if (dimension == normalizedAxis)
            {
                continue;
            }

            if (values.GetLength(dimension) != _shape[dimension])
            {
                throw new ArgumentException("All dimensions other than the append axis must match the existing array shape.", nameof(values));
            }
        }

        var oldShape = _shape.ToArray();
        var newShape = oldShape.ToArray();
        newShape[normalizedAxis] += values.GetLength(normalizedAxis);

        await ResizeAsyncCore(newShape, deleteOutsideChunks: false, cancellationToken).ConfigureAwait(false);

        var selection = new ArraySelection[_shape.Length];
        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            selection[dimension] = dimension == normalizedAxis
                ? ArraySelection.Slice(oldShape[dimension], newShape[dimension])
                : ArraySelection.All;
        }

        await WriteSelectionAsyncCore(values, selection, cancellationToken).ConfigureAwait(false);
        return _shape.ToArray();
    }

    public T GetValue<T>(params int[] indices)
    {
        EnsureType(typeof(T));
        var normalizedIndices = ValidateIndices(indices);
        var chunkCoords = new int[indices.Length];
        var localCoords = new int[indices.Length];

        for (var dimension = 0; dimension < indices.Length; dimension++)
        {
            chunkCoords[dimension] = normalizedIndices[dimension] / _chunks[dimension];
            localCoords[dimension] = normalizedIndices[dimension] % _chunks[dimension];
        }

        if (TryGetPartialShardedValue(typeof(T), chunkCoords, localCoords, out var value))
        {
            return (T)value!;
        }

        var chunk = LoadChunk(chunkCoords);
        value = chunk.GetValue(ToFlatIndex(localCoords, _chunks, Order));
        return (T)DataType.ConvertForRead(value, typeof(T))!;
    }

    internal async Task<T> GetValueAsyncCore<T>(params int[] indices)
    {
        EnsureType(typeof(T));
        var value = await GetValueAsyncCore(typeof(T), indices).ConfigureAwait(false);
        return (T)value!;
    }

    public object? GetValue(params int[] indices)
    {
        var normalizedIndices = ValidateIndices(indices);
        var chunkCoords = new int[indices.Length];
        var localCoords = new int[indices.Length];

        for (var dimension = 0; dimension < indices.Length; dimension++)
        {
            chunkCoords[dimension] = normalizedIndices[dimension] / _chunks[dimension];
            localCoords[dimension] = normalizedIndices[dimension] % _chunks[dimension];
        }

        if (TryGetPartialShardedValue(requestedType: null, chunkCoords, localCoords, out var value))
        {
            return value;
        }

        var chunk = LoadChunk(chunkCoords);
        return chunk.GetValue(ToFlatIndex(localCoords, _chunks, Order));
    }

    internal async Task<object?> GetValueAsyncCore(params int[] indices)
    {
        return await GetValueAsyncCore(requestedType: null, indices).ConfigureAwait(false);
    }

    public Array Read()
    {
        var result = Array.CreateInstance(DataType.GetPreferredReadType(), _shape);
        var normalized = NormalizeSelection(Array.Empty<ArraySelection>(), requireAllIndices: false);
        if (!TryPopulateSelectionArrayFromPartialShards(result, normalized))
        {
            PopulateSelectionArray(result, normalized);
        }
        return result;
    }

    internal async Task<Array> ReadAsyncCore(CancellationToken cancellationToken = default)
    {
        var result = Array.CreateInstance(DataType.GetPreferredReadType(), _shape);
        var normalized = NormalizeSelection(Array.Empty<ArraySelection>(), requireAllIndices: false);
        if (!await TryPopulateSelectionArrayFromPartialShardsAsync(result, normalized, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var indices = new int[_shape.Length];
            for (var flatIndex = 0; flatIndex < Length; flatIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FromFlatIndex(flatIndex, _shape, indices);
                result.SetValue(await GetValueAsyncCore(indices).ConfigureAwait(false), indices);
            }
        }

        return result;
    }

    public Array Read<T>()
    {
        EnsureType(typeof(T));
        var result = Array.CreateInstance(typeof(T), _shape);
        var normalized = NormalizeSelection(Array.Empty<ArraySelection>(), requireAllIndices: false);
        if (!TryPopulateSelectionArrayFromPartialShards(result, normalized, typeof(T)))
        {
            PopulateSelectionArray(result, normalized, typeof(T));
        }
        return result;
    }

    internal async Task<Array> ReadAsyncCore<T>(CancellationToken cancellationToken = default)
    {
        EnsureType(typeof(T));
        var result = Array.CreateInstance(typeof(T), _shape);
        var normalized = NormalizeSelection(Array.Empty<ArraySelection>(), requireAllIndices: false);
        if (!await TryPopulateSelectionArrayFromPartialShardsAsync(result, normalized, typeof(T), cancellationToken).ConfigureAwait(false))
        {
            var indices = new int[_shape.Length];
            for (var flatIndex = 0; flatIndex < Length; flatIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FromFlatIndex(flatIndex, _shape, indices);
                result.SetValue(await GetValueAsyncCore<T>(indices).ConfigureAwait(false), indices);
            }
        }

        return result;
    }

    public T[] ReadFlat<T>()
    {
        EnsureType(typeof(T));
        var result = new T[Length];
        var indices = new int[_shape.Length];
        for (var flatIndex = 0; flatIndex < Length; flatIndex++)
        {
            FromFlatIndex(flatIndex, _shape, indices);
            result[flatIndex] = GetValue<T>(indices);
        }

        return result;
    }

    public void SetValue<T>(T value, params int[] indices)
    {
        EnsureType(typeof(T));
        SetValueInternal(value, ValidateIndices(indices));
    }

    internal Task SetValueAsyncCore<T>(T value, params int[] indices)
    {
        EnsureType(typeof(T));
        return SetValueInternalAsync(value, ValidateIndices(indices));
    }

    public T GetScalar<T>(params ArraySelection[] selection)
    {
        var normalized = NormalizeSelection(selection, requireAllIndices: true);
        return GetValue<T>(normalized.FixedIndices);
    }

    public Array GetSelection(params ArraySelection[] selection)
    {
        var normalized = NormalizeSelection(selection, requireAllIndices: false);
        if (normalized.ResultShape.Length == 0)
        {
            return CreateScalarArray(GetValue(normalized.FixedIndices));
        }

        var result = Array.CreateInstance(DataType.GetPreferredReadType(), normalized.ResultShape);
        if (!TryPopulateSelectionArrayFromPartialShards(result, normalized))
        {
            PopulateSelectionArray(result, normalized);
        }
        return result;
    }

    public Array GetSelection<T>(params ArraySelection[] selection)
    {
        EnsureType(typeof(T));
        var normalized = NormalizeSelection(selection, requireAllIndices: false);
        if (normalized.ResultShape.Length == 0)
        {
            return CreateScalarArray(GetValue<T>(normalized.FixedIndices), typeof(T));
        }

        var result = Array.CreateInstance(typeof(T), normalized.ResultShape);
        if (!TryPopulateSelectionArrayFromPartialShards(result, normalized, typeof(T)))
        {
            PopulateSelectionArray(result, normalized, typeof(T));
        }
        return result;
    }

    public void Write(Array values)
    {
        if (values.Rank != _shape.Length)
        {
            throw new ArgumentException("Input rank does not match the array shape.", nameof(values));
        }

        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            if (values.GetLength(dimension) != _shape[dimension])
            {
                throw new ArgumentException("Input dimensions do not match the array shape.", nameof(values));
            }
        }

        var elementType = values.GetType().GetElementType() ?? throw new ArgumentException("Unable to determine the array element type.", nameof(values));
        EnsureType(elementType);

        if (TryWriteWholeArrayByChunks(values))
        {
            return;
        }

        var normalized = NormalizeSelection(Array.Empty<ArraySelection>(), requireAllIndices: false);
        if (TryWriteSelectionFromPartialShards(values, normalized))
        {
            return;
        }

        var flatIndex = 0;
        var indices = new int[_shape.Length];
        foreach (var value in values)
        {
            FromFlatIndex(flatIndex, _shape, indices);
            SetValueInternal(value, indices);
            flatIndex++;
        }
    }

    internal async Task WriteAsyncCore(Array values, CancellationToken cancellationToken = default)
    {
        if (values.Rank != _shape.Length)
        {
            throw new ArgumentException("Input rank does not match the array shape.", nameof(values));
        }

        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            if (values.GetLength(dimension) != _shape[dimension])
            {
                throw new ArgumentException("Input dimensions do not match the array shape.", nameof(values));
            }
        }

        var elementType = values.GetType().GetElementType() ?? throw new ArgumentException("Unable to determine the array element type.", nameof(values));
        EnsureType(elementType);

        if (await TryWriteWholeArrayByChunksAsync(values, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var normalized = NormalizeSelection(Array.Empty<ArraySelection>(), requireAllIndices: false);
        if (await TryWriteSelectionFromPartialShardsAsync(values, normalized, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var flatIndex = 0;
        var indices = new int[_shape.Length];
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FromFlatIndex(flatIndex, _shape, indices);
            await SetValueInternalAsync(value, indices).ConfigureAwait(false);
            flatIndex++;
        }
    }

    private bool TryWriteWholeArrayByChunks(Array values)
    {
        if (!_chunkGrid.SupportsDataAccess)
        {
            return false;
        }

        var chunkCoordsList = _chunkGrid.AllChunkCoordinates(_shape).ToArray();
        if (chunkCoordsList.Length <= 1)
        {
            return false;
        }

        var globalIndices = new int[_shape.Length];
        var localIndices = new int[_shape.Length];
        var chunkLength = _chunks.Aggregate(1, (current, value) => current * value);

        foreach (var chunkCoords in chunkCoordsList)
        {
            var chunk = CreateFilledChunk(_chunks);
            for (var flatIndex = 0; flatIndex < chunkLength; flatIndex++)
            {
                FromFlatIndex(flatIndex, _chunks, localIndices);
                var isInsideShape = true;
                for (var dimension = 0; dimension < _shape.Length; dimension++)
                {
                    globalIndices[dimension] = (chunkCoords[dimension] * _chunks[dimension]) + localIndices[dimension];
                    if (globalIndices[dimension] >= _shape[dimension])
                    {
                        isInsideShape = false;
                        break;
                    }
                }

                if (!isInsideShape)
                {
                    continue;
                }

                chunk.SetValue(
                    DataType.Normalize(values.GetValue(globalIndices)),
                    ToFlatIndex(localIndices, _chunks, Order));
            }

            SaveChunk(chunkCoords, chunk);
        }

        return true;
    }

    private async Task<bool> TryWriteWholeArrayByChunksAsync(Array values, CancellationToken cancellationToken)
    {
        if (!_chunkGrid.SupportsDataAccess)
        {
            return false;
        }

        var chunkCoordsList = _chunkGrid.AllChunkCoordinates(_shape).ToArray();
        if (chunkCoordsList.Length <= 1)
        {
            return false;
        }

        var globalIndices = new int[_shape.Length];
        var localIndices = new int[_shape.Length];
        var chunkLength = _chunks.Aggregate(1, (current, value) => current * value);

        foreach (var chunkCoords in chunkCoordsList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = CreateFilledChunk(_chunks);
            for (var flatIndex = 0; flatIndex < chunkLength; flatIndex++)
            {
                FromFlatIndex(flatIndex, _chunks, localIndices);
                var isInsideShape = true;
                for (var dimension = 0; dimension < _shape.Length; dimension++)
                {
                    globalIndices[dimension] = (chunkCoords[dimension] * _chunks[dimension]) + localIndices[dimension];
                    if (globalIndices[dimension] >= _shape[dimension])
                    {
                        isInsideShape = false;
                        break;
                    }
                }

                if (!isInsideShape)
                {
                    continue;
                }

                chunk.SetValue(
                    DataType.Normalize(values.GetValue(globalIndices)),
                    ToFlatIndex(localIndices, _chunks, Order));
            }

            await SaveChunkAsync(chunkCoords, chunk, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public void WriteFlat<T>(IReadOnlyList<T> values)
    {
        EnsureType(typeof(T));
        if (values.Count != Length)
        {
            throw new ArgumentException($"Expected {Length} values but received {values.Count}.", nameof(values));
        }

        var indices = new int[_shape.Length];
        for (var flatIndex = 0; flatIndex < values.Count; flatIndex++)
        {
            FromFlatIndex(flatIndex, _shape, indices);
            SetValueInternal(values[flatIndex], indices);
        }
    }

    internal async Task WriteFlatAsyncCore<T>(IReadOnlyList<T> values, CancellationToken cancellationToken = default)
    {
        EnsureType(typeof(T));
        if (values.Count != Length)
        {
            throw new ArgumentException($"Expected {Length} values but received {values.Count}.", nameof(values));
        }

        var indices = new int[_shape.Length];
        for (var flatIndex = 0; flatIndex < values.Count; flatIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FromFlatIndex(flatIndex, _shape, indices);
            await SetValueInternalAsync(values[flatIndex], indices).ConfigureAwait(false);
        }
    }

    public void WriteSelection(Array values, params ArraySelection[] selection)
    {
        var normalized = NormalizeSelection(selection, requireAllIndices: false);
        if (normalized.ResultShape.Length == 0)
        {
            if (values.Length != 1)
            {
                throw new ArgumentException("Scalar selections require a single input value.", nameof(values));
            }

            SetValueInternal(values.GetValue(0), normalized.FixedIndices);
            return;
        }

        ValidateSelectionInput(values, normalized.ResultShape);
        if (TryWriteSelectionFromPartialShards(values, normalized))
        {
            return;
        }

        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];

        for (var flatIndex = 0; flatIndex < values.Length; flatIndex++)
        {
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            SetValueInternal(values.GetValue(resultIndices), sourceIndices);
        }
    }

    private async Task WriteSelectionAsyncCore(Array values, ArraySelection[] selection, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSelection(selection, requireAllIndices: false);
        if (normalized.ResultShape.Length == 0)
        {
            if (values.Length != 1)
            {
                throw new ArgumentException("Scalar selections require a single input value.", nameof(values));
            }

            await SetValueInternalAsync(values.GetValue(0), normalized.FixedIndices, cancellationToken).ConfigureAwait(false);
            return;
        }

        ValidateSelectionInput(values, normalized.ResultShape);
        if (await TryWriteSelectionFromPartialShardsAsync(values, normalized, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];

        for (var flatIndex = 0; flatIndex < values.Length; flatIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            await SetValueInternalAsync(values.GetValue(resultIndices), sourceIndices, cancellationToken).ConfigureAwait(false);
        }
    }

    public void WriteSelection<T>(T value, params ArraySelection[] selection)
    {
        EnsureType(typeof(T));
        var normalized = NormalizeSelection(selection, requireAllIndices: false);
        if (normalized.ResultShape.Length == 0)
        {
            SetValueInternal(value, normalized.FixedIndices);
            return;
        }

        if (TryBroadcastSelectionFromPartialShards(value, normalized))
        {
            return;
        }

        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];
        var count = normalized.ResultShape.Aggregate(1, (current, item) => current * item);

        for (var flatIndex = 0; flatIndex < count; flatIndex++)
        {
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            SetValueInternal(value, sourceIndices);
        }
    }

    protected override void PersistMetadata()
    {
        var metadata = CreateCurrentMetadata();
        MetadataStore.SaveArray(Store, Path, metadata);
        _consolidatedMutationCallback?.Invoke(metadata);
    }

    private ArrayMetadata CreateCurrentMetadata()
    {
        return MetadataStore.CreateArrayMetadata(
            Format,
            _shape,
            _chunks,
            DataType,
            _fillValue,
            JsonUtilities.CloneDictionary(AttributeValues),
            Order,
            _chunkCodec,
            _chunkKeyEncoding,
            CreateV3MetadataOptions());
    }

    internal Task PersistMetadataAsyncCore(CancellationToken cancellationToken = default)
    {
        var metadata = CreateCurrentMetadata();
        return PersistMetadataAsyncCore(metadata, cancellationToken);
    }

    private async Task PersistMetadataAsyncCore(ArrayMetadata metadata, CancellationToken cancellationToken)
    {
        await MetadataStore.SaveArrayAsync(Store, Path, metadata, cancellationToken).ConfigureAwait(false);
        _consolidatedMutationCallback?.Invoke(metadata);
    }

    internal V3ArrayMetadataOptions? CreateV3MetadataOptions()
    {
        if (Format != ZarrFormat.V3)
        {
            return null;
        }

        return new V3ArrayMetadataOptions
        {
            ChunkGrid = _chunkGrid,
            DimensionNames = _dimensionNames?.ToArray(),
            StorageTransformers = _storageTransformers.Select(JsonUtilities.CloneDictionary).ToArray(),
            ExtraFields = _extraFields.ToDictionary(
                item => item.Key,
                item => (IReadOnlyDictionary<string, object?>)JsonUtilities.CloneDictionary(item.Value),
                StringComparer.Ordinal),
        };
    }

    private Array CreateFilledChunk()
    {
        return DataType.CreateFilledArray(_chunks.Aggregate(1, (current, value) => current * value), _fillValue);
    }

    private Array CreateFilledChunk(IReadOnlyList<int> chunkShape)
    {
        return DataType.CreateFilledArray(chunkShape.Aggregate(1, (current, value) => current * value), _fillValue);
    }

    private static Array CreateScalarArray(object? value)
    {
        var type = value?.GetType() ?? typeof(object);
        return CreateScalarArray(value, type);
    }

    private static Array CreateScalarArray(object? value, Type type)
    {
        var result = Array.CreateInstance(type, 1);
        result.SetValue(value, 0);
        return result;
    }

    private void EnsureType(Type type)
    {
        if (DataType.IsStructured && type == typeof(StructuredRecord))
        {
            return;
        }

        var dtype = DType.FromType(type);
        if (!DataType.IsEquivalent(dtype))
        {
            throw new InvalidOperationException($"The array stores '{DataType.Signature}' values, not '{dtype.Signature}'.");
        }
    }

    private string GetChunkKey(int[] chunkCoords)
    {
        var relative = _chunkKeyEncoding.EncodeChunkKey(chunkCoords);
        return Path.Length == 0 ? relative : $"{Path}/{relative}";
    }

    private Array LoadChunk(int[] chunkCoords)
    {
        EnsureRuntimeSupported();
        var key = GetChunkKey(chunkCoords);
        var payload = Store.Get(key);
        if (payload is null)
        {
            if (!ZarrConfiguration.ReadMissingChunks)
            {
                throw new ChunkNotFoundException(key);
            }

            return CreateFilledChunk();
        }

        return DataType.Deserialize(DecodeChunkPayload(payload.Value), _chunks.Aggregate(1, (current, value) => current * value));
    }

    private async Task<Array> LoadChunkAsync(int[] chunkCoords, CancellationToken cancellationToken = default)
    {
        EnsureRuntimeSupported();
        var key = GetChunkKey(chunkCoords);
        var payload = await Store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            if (!ZarrConfiguration.ReadMissingChunks)
            {
                throw new ChunkNotFoundException(key);
            }

            return CreateFilledChunk();
        }

        return DataType.Deserialize(DecodeChunkPayload(payload.Value), _chunks.Aggregate(1, (current, value) => current * value));
    }

    private void SaveChunk(int[] chunkCoords, Array chunk)
    {
        EnsureRuntimeSupported();
        var key = GetChunkKey(chunkCoords);
        if (!ZarrConfiguration.WriteEmptyChunks && IsFilledChunk(chunk))
        {
            Store.Delete(key);
            return;
        }

        Store.Set(key, EncodeChunkPayload(DataType.Serialize(chunk)));
    }

    private Task SaveChunkAsync(int[] chunkCoords, Array chunk, CancellationToken cancellationToken = default)
    {
        EnsureRuntimeSupported();
        var key = GetChunkKey(chunkCoords);
        if (!ZarrConfiguration.WriteEmptyChunks && IsFilledChunk(chunk))
        {
            return Store.DeleteAsync(key, cancellationToken).AsTask();
        }

        return Store.SetAsync(key, EncodeChunkPayload(DataType.Serialize(chunk)), cancellationToken).AsTask();
    }

    private bool TryGetPartialShardedValue(Type? requestedType, int[] chunkCoords, int[] localCoords, out object? value)
    {
        value = null;
        if (!TryGetPartialShardingCodec(out var shardingCodec, out var runtimeStages))
        {
            return false;
        }

        var payload = Store.Get(GetChunkKey(chunkCoords));
        if (payload is null && !ZarrConfiguration.ReadMissingChunks)
        {
            throw new ChunkNotFoundException(GetChunkKey(chunkCoords));
        }

        var shardPayload = payload is null
            ? null
            : ChunkCodecFactory.DecodeBytesBytesPayload(runtimeStages, payload.Value, DataType, _chunks, Order);
        var innerChunkCoords = new int[localCoords.Length];
        var innerLocalCoords = new int[localCoords.Length];
        GetInnerChunkCoordinates(localCoords, shardingCodec.ChunkShape, innerChunkCoords, innerLocalCoords);

        var innerChunk = shardingCodec.DecodePartialChunk(shardPayload, DataType, _chunks, innerChunkCoords, Order)
            ?? CreateFilledChunk(shardingCodec.ChunkShape);
        value = innerChunk.GetValue(ToFlatIndex(innerLocalCoords, shardingCodec.ChunkShape, Order));
        if (requestedType is not null)
        {
            value = DataType.ConvertForRead(value, requestedType);
        }

        return true;
    }

    private async Task<(bool Success, object? Value)> TryGetPartialShardedValueAsync(Type? requestedType, int[] chunkCoords, int[] localCoords, CancellationToken cancellationToken = default)
    {
        if (!TryGetPartialShardingCodec(out var shardingCodec, out var runtimeStages))
        {
            return (false, null);
        }

        var payload = await Store.GetAsync(GetChunkKey(chunkCoords), cancellationToken).ConfigureAwait(false);
        if (payload is null && !ZarrConfiguration.ReadMissingChunks)
        {
            throw new ChunkNotFoundException(GetChunkKey(chunkCoords));
        }

        var shardPayload = payload is null
            ? null
            : ChunkCodecFactory.DecodeBytesBytesPayload(runtimeStages, payload.Value, DataType, _chunks, Order);
        var innerChunkCoords = new int[localCoords.Length];
        var innerLocalCoords = new int[localCoords.Length];
        GetInnerChunkCoordinates(localCoords, shardingCodec.ChunkShape, innerChunkCoords, innerLocalCoords);

        var innerChunk = shardingCodec.DecodePartialChunk(shardPayload, DataType, _chunks, innerChunkCoords, Order)
            ?? CreateFilledChunk(shardingCodec.ChunkShape);
        object? value = innerChunk.GetValue(ToFlatIndex(innerLocalCoords, shardingCodec.ChunkShape, Order));
        if (requestedType is not null)
        {
            value = DataType.ConvertForRead(value, requestedType);
        }

        return (true, value);
    }

    private bool TrySetPartialShardedValue(object? value, int[] chunkCoords, int[] localCoords)
    {
        if (!TryGetPartialShardingCodec(out var shardingCodec, out var runtimeStages))
        {
            return false;
        }

        var payload = Store.Get(GetChunkKey(chunkCoords));
        var shardPayload = payload is null
            ? null
            : ChunkCodecFactory.DecodeBytesBytesPayload(runtimeStages, payload.Value, DataType, _chunks, Order);
        var innerChunkCoords = new int[localCoords.Length];
        var innerLocalCoords = new int[localCoords.Length];
        GetInnerChunkCoordinates(localCoords, shardingCodec.ChunkShape, innerChunkCoords, innerLocalCoords);

        var innerChunk = shardingCodec.DecodePartialChunk(shardPayload, DataType, _chunks, innerChunkCoords, Order)
            ?? CreateFilledChunk(shardingCodec.ChunkShape);
        innerChunk.SetValue(DataType.Normalize(value), ToFlatIndex(innerLocalCoords, shardingCodec.ChunkShape, Order));

        var updatedInnerChunk = !ZarrConfiguration.WriteEmptyChunks && IsFilledChunk(innerChunk)
            ? null
            : innerChunk;
        var updatedShardPayload = shardingCodec.EncodePartialChunk(shardPayload, DataType, _chunks, innerChunkCoords, updatedInnerChunk, Order);
        if (updatedShardPayload is null)
        {
            Store.Delete(GetChunkKey(chunkCoords));
            return true;
        }

        Store.Set(GetChunkKey(chunkCoords), ChunkCodecFactory.EncodeBytesBytesPayload(runtimeStages, updatedShardPayload.Value, DataType, _chunks, Order));
        return true;
    }

    private async Task<bool> TrySetPartialShardedValueAsync(object? value, int[] chunkCoords, int[] localCoords, CancellationToken cancellationToken = default)
    {
        if (!TryGetPartialShardingCodec(out var shardingCodec, out var runtimeStages))
        {
            return false;
        }

        var payload = await Store.GetAsync(GetChunkKey(chunkCoords), cancellationToken).ConfigureAwait(false);
        var shardPayload = payload is null
            ? null
            : ChunkCodecFactory.DecodeBytesBytesPayload(runtimeStages, payload.Value, DataType, _chunks, Order);
        var innerChunkCoords = new int[localCoords.Length];
        var innerLocalCoords = new int[localCoords.Length];
        GetInnerChunkCoordinates(localCoords, shardingCodec.ChunkShape, innerChunkCoords, innerLocalCoords);

        var innerChunk = shardingCodec.DecodePartialChunk(shardPayload, DataType, _chunks, innerChunkCoords, Order)
            ?? CreateFilledChunk(shardingCodec.ChunkShape);
        innerChunk.SetValue(DataType.Normalize(value), ToFlatIndex(innerLocalCoords, shardingCodec.ChunkShape, Order));

        var updatedInnerChunk = !ZarrConfiguration.WriteEmptyChunks && IsFilledChunk(innerChunk)
            ? null
            : innerChunk;
        var updatedShardPayload = shardingCodec.EncodePartialChunk(shardPayload, DataType, _chunks, innerChunkCoords, updatedInnerChunk, Order);
        if (updatedShardPayload is null)
        {
            await Store.DeleteAsync(GetChunkKey(chunkCoords), cancellationToken).ConfigureAwait(false);
            return true;
        }

        await Store.SetAsync(GetChunkKey(chunkCoords), ChunkCodecFactory.EncodeBytesBytesPayload(runtimeStages, updatedShardPayload.Value, DataType, _chunks, Order), cancellationToken).ConfigureAwait(false);
        return true;
    }

    private bool TryGetPartialShardingCodec([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ShardingChunkCodec? shardingCodec, out ChunkCodecRuntimeStages runtimeStages)
    {
        runtimeStages = ChunkCodecFactory.SplitForRuntime(_chunkCodec, Format, DataType);
        shardingCodec = runtimeStages.ArrayBytesCodec as ShardingChunkCodec;
        return shardingCodec is not null && runtimeStages.ArrayArrayCodecs.Count == 0;
    }

    private static void GetInnerChunkCoordinates(IReadOnlyList<int> localCoords, IReadOnlyList<int> innerChunkShape, int[] innerChunkCoords, int[] innerLocalCoords)
    {
        for (var dimension = 0; dimension < localCoords.Count; dimension++)
        {
            innerChunkCoords[dimension] = localCoords[dimension] / innerChunkShape[dimension];
            innerLocalCoords[dimension] = localCoords[dimension] % innerChunkShape[dimension];
        }
    }

    private void SetValueInternal(object? value, int[] indices)
    {
        var chunkCoords = new int[indices.Length];
        var localCoords = new int[indices.Length];

        for (var dimension = 0; dimension < indices.Length; dimension++)
        {
            chunkCoords[dimension] = indices[dimension] / _chunks[dimension];
            localCoords[dimension] = indices[dimension] % _chunks[dimension];
        }

        if (TrySetPartialShardedValue(value, chunkCoords, localCoords))
        {
            return;
        }

        var chunk = LoadChunk(chunkCoords);
        chunk.SetValue(DataType.Normalize(value), ToFlatIndex(localCoords, _chunks, Order));
        SaveChunk(chunkCoords, chunk);
    }

    private async Task SetValueInternalAsync(object? value, int[] indices, CancellationToken cancellationToken = default)
    {
        var chunkCoords = new int[indices.Length];
        var localCoords = new int[indices.Length];

        for (var dimension = 0; dimension < indices.Length; dimension++)
        {
            chunkCoords[dimension] = indices[dimension] / _chunks[dimension];
            localCoords[dimension] = indices[dimension] % _chunks[dimension];
        }

        if (await TrySetPartialShardedValueAsync(value, chunkCoords, localCoords, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var chunk = await LoadChunkAsync(chunkCoords, cancellationToken).ConfigureAwait(false);
        chunk.SetValue(DataType.Normalize(value), ToFlatIndex(localCoords, _chunks, Order));
        await SaveChunkAsync(chunkCoords, chunk, cancellationToken).ConfigureAwait(false);
    }

    private ReadOnlyMemory<byte> DecodeChunkPayload(ReadOnlyMemory<byte> payload)
    {
        return ChunkCodecFactory.DecodePayload(_chunkCodec, payload, Format, DataType, _chunks, Order);
    }

    private ReadOnlyMemory<byte> EncodeChunkPayload(ReadOnlyMemory<byte> payload)
    {
        return ChunkCodecFactory.EncodePayload(_chunkCodec, payload, Format, DataType, _chunks, Order);
    }

    private async Task<object?> GetValueAsyncCore(Type? requestedType, IReadOnlyList<int> indices)
    {
        var normalizedIndices = ValidateIndices(indices);
        var chunkCoords = new int[indices.Count];
        var localCoords = new int[indices.Count];

        for (var dimension = 0; dimension < indices.Count; dimension++)
        {
            chunkCoords[dimension] = normalizedIndices[dimension] / _chunks[dimension];
            localCoords[dimension] = normalizedIndices[dimension] % _chunks[dimension];
        }

        var partial = await TryGetPartialShardedValueAsync(requestedType, chunkCoords, localCoords).ConfigureAwait(false);
        if (partial.Success)
        {
            return partial.Value;
        }

        var chunk = await LoadChunkAsync(chunkCoords).ConfigureAwait(false);
        var value = chunk.GetValue(ToFlatIndex(localCoords, _chunks, Order));
        return requestedType is null ? value : DataType.ConvertForRead(value, requestedType);
    }

    private int[] ValidateIndices(IReadOnlyList<int> indices)
    {
        if (indices.Count != _shape.Length)
        {
            throw new ArgumentException("The number of indices does not match the array rank.", nameof(indices));
        }

        var normalized = indices.ToArray();
        for (var dimension = 0; dimension < normalized.Length; dimension++)
        {
            normalized[dimension] = Indexing.NormalizeIntegerSelection(normalized[dimension], _shape[dimension]);
        }

        return normalized;
    }

    private void PopulateSelectionArray(Array result, NormalizedSelection normalized, Type? requestedType = null)
    {
        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];

        for (var flatIndex = 0; flatIndex < result.Length; flatIndex++)
        {
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            var value = GetValue(sourceIndices);
            if (requestedType is not null)
            {
                value = DataType.ConvertForRead(value, requestedType);
            }

            result.SetValue(value, resultIndices);
        }
    }

    private bool TryPopulateSelectionArrayFromPartialShards(Array result, NormalizedSelection normalized, Type? requestedType = null)
    {
        if (!TryGetPartialShardingCodec(out _, out _) ||
            !IsContiguousSelection(normalized))
        {
            return false;
        }

        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];

        for (var flatIndex = 0; flatIndex < result.Length; flatIndex++)
        {
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            var value = GetValue(sourceIndices);
            if (requestedType is not null)
            {
                value = DataType.ConvertForRead(value, requestedType);
            }

            result.SetValue(value, resultIndices);
        }

        return true;
    }

    private async Task<bool> TryPopulateSelectionArrayFromPartialShardsAsync(Array result, NormalizedSelection normalized, Type? requestedType = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetPartialShardingCodec(out _, out _) ||
            !IsContiguousSelection(normalized))
        {
            return false;
        }

        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];

        for (var flatIndex = 0; flatIndex < result.Length; flatIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            var value = await GetValueAsyncCore(null, sourceIndices).ConfigureAwait(false);
            if (requestedType is not null)
            {
                value = DataType.ConvertForRead(value, requestedType);
            }

            result.SetValue(value, resultIndices);
        }

        return true;
    }

    private bool TryWriteSelectionFromPartialShards(Array values, NormalizedSelection normalized)
    {
        if (!TryGetPartialShardingCodec(out var shardingCodec, out var runtimeStages) ||
            !IsContiguousSelection(normalized))
        {
            return false;
        }

        foreach (var outerGroup in GroupWriteSelectionByOuterShard(values, normalized, shardingCodec.ChunkShape).Values)
        {
            ApplyOuterShardWriteGroup(outerGroup, shardingCodec, runtimeStages);
        }

        return true;
    }

    private async Task<bool> TryWriteSelectionFromPartialShardsAsync(Array values, NormalizedSelection normalized, CancellationToken cancellationToken = default)
    {
        if (!TryGetPartialShardingCodec(out var shardingCodec, out var runtimeStages) ||
            !IsContiguousSelection(normalized))
        {
            return false;
        }

        var outerGroups = GroupWriteSelectionByOuterShard(values, normalized, shardingCodec.ChunkShape).Values.ToArray();
        var concurrency = Math.Max(1, Configuration.ZarrConfiguration.Current.Async.Concurrency);
        using var semaphore = new SemaphoreSlim(concurrency);
        var tasks = outerGroups.Select(async outerGroup =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ApplyOuterShardWriteGroupAsync(outerGroup, shardingCodec, runtimeStages, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return true;
    }

    private bool TryBroadcastSelectionFromPartialShards(object? value, NormalizedSelection normalized)
    {
        if (!TryGetPartialShardingCodec(out var shardingCodec, out var runtimeStages) ||
            !IsContiguousSelection(normalized))
        {
            return false;
        }

        foreach (var outerGroup in GroupBroadcastSelectionByOuterShard(value, normalized, shardingCodec.ChunkShape).Values)
        {
            ApplyOuterShardWriteGroup(outerGroup, shardingCodec, runtimeStages);
        }

        return true;
    }

    private void ApplyOuterShardWriteGroup(PartialShardOuterWriteGroup outerGroup, ShardingChunkCodec shardingCodec, ChunkCodecRuntimeStages runtimeStages)
    {
        var key = GetChunkKey(outerGroup.ChunkCoords);
        var payload = Store.Get(key);
        var shardPayload = payload is null
            ? (ReadOnlyMemory<byte>?)null
            : ChunkCodecFactory.DecodeBytesBytesPayload(runtimeStages, payload.Value, DataType, _chunks, Order);

        foreach (var innerGroup in outerGroup.InnerGroups.Values)
        {
            var innerChunk = shardingCodec.DecodePartialChunk(shardPayload, DataType, _chunks, innerGroup.InnerChunkCoords, Order)
                ?? CreateFilledChunk(shardingCodec.ChunkShape);
            foreach (var item in innerGroup.Items)
            {
                innerChunk.SetValue(DataType.Normalize(item.Value), ToFlatIndex(item.InnerLocalCoords, shardingCodec.ChunkShape, Order));
            }

            var updatedInnerChunk = !ZarrConfiguration.WriteEmptyChunks && IsFilledChunk(innerChunk)
                ? null
                : innerChunk;
            shardPayload = shardingCodec.EncodePartialChunk(shardPayload, DataType, _chunks, innerGroup.InnerChunkCoords, updatedInnerChunk, Order);
        }

        if (shardPayload is null)
        {
            Store.Delete(key);
            return;
        }

        Store.Set(key, ChunkCodecFactory.EncodeBytesBytesPayload(runtimeStages, shardPayload.Value, DataType, _chunks, Order));
    }

    private async Task ApplyOuterShardWriteGroupAsync(PartialShardOuterWriteGroup outerGroup, ShardingChunkCodec shardingCodec, ChunkCodecRuntimeStages runtimeStages, CancellationToken cancellationToken)
    {
        var key = GetChunkKey(outerGroup.ChunkCoords);
        var payload = await Store.GetAsync(key, cancellationToken).ConfigureAwait(false);
        var shardPayload = payload is null
            ? (ReadOnlyMemory<byte>?)null
            : ChunkCodecFactory.DecodeBytesBytesPayload(runtimeStages, payload.Value, DataType, _chunks, Order);

        foreach (var innerGroup in outerGroup.InnerGroups.Values)
        {
            var innerChunk = shardingCodec.DecodePartialChunk(shardPayload, DataType, _chunks, innerGroup.InnerChunkCoords, Order)
                ?? CreateFilledChunk(shardingCodec.ChunkShape);
            foreach (var item in innerGroup.Items)
            {
                innerChunk.SetValue(DataType.Normalize(item.Value), ToFlatIndex(item.InnerLocalCoords, shardingCodec.ChunkShape, Order));
            }

            var updatedInnerChunk = !ZarrConfiguration.WriteEmptyChunks && IsFilledChunk(innerChunk)
                ? null
                : innerChunk;
            shardPayload = shardingCodec.EncodePartialChunk(shardPayload, DataType, _chunks, innerGroup.InnerChunkCoords, updatedInnerChunk, Order);
        }

        if (shardPayload is null)
        {
            await Store.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
            return;
        }

        await Store.SetAsync(key, ChunkCodecFactory.EncodeBytesBytesPayload(runtimeStages, shardPayload.Value, DataType, _chunks, Order), cancellationToken).ConfigureAwait(false);
    }

    private Dictionary<string, PartialShardOuterReadGroup> GroupReadSelectionByOuterShard(NormalizedSelection normalized, IReadOnlyList<int> innerChunkShape)
    {
        var groups = new Dictionary<string, PartialShardOuterReadGroup>(StringComparer.Ordinal);
        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];
        var chunkCoords = new int[_shape.Length];
        var localCoords = new int[_shape.Length];
        var innerChunkCoords = new int[_shape.Length];
        var innerLocalCoords = new int[_shape.Length];

        for (var flatIndex = 0; flatIndex < resultIndices.Aggregate(1, (current, value) => current * value); flatIndex++)
        {
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            GetChunkAndLocalCoordinates(sourceIndices, chunkCoords, localCoords);
            GetInnerChunkCoordinates(localCoords, innerChunkShape, innerChunkCoords, innerLocalCoords);

            var outerGroup = GetOrCreateOuterReadGroup(groups, chunkCoords);
            var innerGroup = outerGroup.GetOrCreateInnerGroup(innerChunkCoords);
            innerGroup.Items.Add(new PartialShardReadItem(innerLocalCoords.ToArray(), resultIndices.ToArray()));
        }

        return groups;
    }

    private Dictionary<string, PartialShardOuterWriteGroup> GroupWriteSelectionByOuterShard(Array values, NormalizedSelection normalized, IReadOnlyList<int> innerChunkShape)
    {
        var groups = new Dictionary<string, PartialShardOuterWriteGroup>(StringComparer.Ordinal);
        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];
        var chunkCoords = new int[_shape.Length];
        var localCoords = new int[_shape.Length];
        var innerChunkCoords = new int[_shape.Length];
        var innerLocalCoords = new int[_shape.Length];

        for (var flatIndex = 0; flatIndex < values.Length; flatIndex++)
        {
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            GetChunkAndLocalCoordinates(sourceIndices, chunkCoords, localCoords);
            GetInnerChunkCoordinates(localCoords, innerChunkShape, innerChunkCoords, innerLocalCoords);

            var outerGroup = GetOrCreateOuterWriteGroup(groups, chunkCoords);
            var innerGroup = outerGroup.GetOrCreateInnerGroup(innerChunkCoords);
            innerGroup.Items.Add(new PartialShardWriteItem(innerLocalCoords.ToArray(), values.GetValue(resultIndices)));
        }

        return groups;
    }

    private Dictionary<string, PartialShardOuterWriteGroup> GroupBroadcastSelectionByOuterShard(object? value, NormalizedSelection normalized, IReadOnlyList<int> innerChunkShape)
    {
        var groups = new Dictionary<string, PartialShardOuterWriteGroup>(StringComparer.Ordinal);
        var resultIndices = new int[normalized.ResultShape.Length];
        var sourceIndices = new int[_shape.Length];
        var chunkCoords = new int[_shape.Length];
        var localCoords = new int[_shape.Length];
        var innerChunkCoords = new int[_shape.Length];
        var innerLocalCoords = new int[_shape.Length];
        var count = normalized.ResultShape.Aggregate(1, (current, item) => current * item);

        for (var flatIndex = 0; flatIndex < count; flatIndex++)
        {
            FromFlatIndex(flatIndex, normalized.ResultShape, resultIndices);
            MapSelectionIndices(normalized, resultIndices, sourceIndices);
            GetChunkAndLocalCoordinates(sourceIndices, chunkCoords, localCoords);
            GetInnerChunkCoordinates(localCoords, innerChunkShape, innerChunkCoords, innerLocalCoords);

            var outerGroup = GetOrCreateOuterWriteGroup(groups, chunkCoords);
            var innerGroup = outerGroup.GetOrCreateInnerGroup(innerChunkCoords);
            innerGroup.Items.Add(new PartialShardWriteItem(innerLocalCoords.ToArray(), value));
        }

        return groups;
    }

    private static PartialShardOuterReadGroup GetOrCreateOuterReadGroup(IDictionary<string, PartialShardOuterReadGroup> groups, int[] chunkCoords)
    {
        var key = string.Join(",", chunkCoords);
        if (!groups.TryGetValue(key, out var group))
        {
            group = new PartialShardOuterReadGroup(chunkCoords.ToArray());
            groups[key] = group;
        }

        return group;
    }

    private static PartialShardOuterWriteGroup GetOrCreateOuterWriteGroup(IDictionary<string, PartialShardOuterWriteGroup> groups, int[] chunkCoords)
    {
        var key = string.Join(",", chunkCoords);
        if (!groups.TryGetValue(key, out var group))
        {
            group = new PartialShardOuterWriteGroup(chunkCoords.ToArray());
            groups[key] = group;
        }

        return group;
    }

    private static void MapSelectionIndices(NormalizedSelection normalized, IReadOnlyList<int> resultIndices, int[] sourceIndices)
    {
        var resultDimension = 0;
        for (var dimension = 0; dimension < normalized.Selections.Length; dimension++)
        {
            if (normalized.Selections[dimension].IsIndex)
            {
                sourceIndices[dimension] = normalized.FixedIndices[dimension];
                continue;
            }

            var slice = normalized.Slices[dimension];
            sourceIndices[dimension] = slice.Start + (resultIndices[resultDimension] * slice.Step);
            resultDimension++;
        }
    }

    private static bool IsContiguousSelection(NormalizedSelection normalized)
    {
        return normalized.Slices.All(slice => slice.Step == 0 || slice.Step == 1);
    }

    private void GetChunkAndLocalCoordinates(IReadOnlyList<int> indices, int[] chunkCoords, int[] localCoords)
    {
        for (var dimension = 0; dimension < indices.Count; dimension++)
        {
            chunkCoords[dimension] = indices[dimension] / _chunks[dimension];
            localCoords[dimension] = indices[dimension] % _chunks[dimension];
        }
    }

    private NormalizedSelection NormalizeSelection(IReadOnlyList<ArraySelection> selection, bool requireAllIndices)
    {
        if (selection.Count > _shape.Length)
        {
            throw new ArgumentException("Selection rank exceeds array rank.", nameof(selection));
        }

        var expanded = new ArraySelection[_shape.Length];
        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            expanded[dimension] = dimension < selection.Count ? selection[dimension] : ArraySelection.All;
        }

        var slices = new NormalizedSlice[_shape.Length];
        var fixedIndices = new int[_shape.Length];
        var resultShape = new List<int>();

        for (var dimension = 0; dimension < _shape.Length; dimension++)
        {
            var current = expanded[dimension];
            if (current.IsIndex)
            {
                fixedIndices[dimension] = Indexing.NormalizeIntegerSelection(current.Index, _shape[dimension]);
                continue;
            }

            if (requireAllIndices)
            {
                throw new ArgumentException("Scalar access requires an integer index for every dimension.", nameof(selection));
            }

            slices[dimension] = Indexing.NormalizeSlice(current.SliceSpec, _shape[dimension]);
            resultShape.Add(slices[dimension].Count);
        }

        return new NormalizedSelection(expanded, fixedIndices, slices, resultShape.ToArray());
    }

    private void ValidateSelectionInput(Array values, IReadOnlyList<int> expectedShape)
    {
        var elementType = values.GetType().GetElementType() ?? throw new ArgumentException("Unable to determine the array element type.", nameof(values));
        EnsureType(elementType);

        if (values.Rank != expectedShape.Count)
        {
            throw new ArgumentException("Input rank does not match the selection rank.", nameof(values));
        }

        for (var dimension = 0; dimension < expectedShape.Count; dimension++)
        {
            if (values.GetLength(dimension) != expectedShape[dimension])
            {
                throw new ArgumentException("Input dimensions do not match the selection shape.", nameof(values));
            }
        }
    }

    private void EnsureRuntimeSupported()
    {
        if (!_chunkGrid.SupportsDataAccess)
        {
            throw new NotSupportedException($"Arrays with chunk grid '{_chunkGrid.Name}' are not supported for data access in this implementation.");
        }

        if (_storageTransformers.Length > 0)
        {
            throw new NotSupportedException("Arrays with storage transformers are not supported for data access in this implementation.");
        }
    }

    private int[] NormalizeNewShape(int[] newShape)
    {
        if (newShape is null)
        {
            throw new ArgumentNullException(nameof(newShape));
        }

        var normalizedShape = newShape.ToArray();
        if (normalizedShape.Length != _shape.Length)
        {
            throw new ArgumentException("newShape must have the same rank as the existing array.", nameof(newShape));
        }

        if (normalizedShape.Any(length => length <= 0))
        {
            throw new ArgumentException("newShape values must be positive.", nameof(newShape));
        }

        return normalizedShape;
    }

    private bool IsFilledChunk(Array chunk)
    {
        foreach (var value in chunk)
        {
            if (!AreValuesEqual(value, _fillValue))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreValuesEqual(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        if (left is byte[] leftBytes && right is byte[] rightBytes)
        {
            return leftBytes.AsSpan().SequenceEqual(rightBytes);
        }

        if (left is ReadOnlyMemory<byte> leftReadOnlyMemory && right is ReadOnlyMemory<byte> rightReadOnlyMemory)
        {
            return leftReadOnlyMemory.Span.SequenceEqual(rightReadOnlyMemory.Span);
        }

        if (left is Memory<byte> leftMemory && right is Memory<byte> rightMemory)
        {
            return leftMemory.Span.SequenceEqual(rightMemory.Span);
        }

        return Equals(left, right);
    }

    private static int NormalizeAxis(int axis, int rank)
    {
        var normalizedAxis = axis < 0 ? rank + axis : axis;
        if (normalizedAxis < 0 || normalizedAxis >= rank)
        {
            throw new ArgumentOutOfRangeException(nameof(axis), $"Axis {axis} is out of range for rank {rank}.");
        }

        return normalizedAxis;
    }

    private static void FromFlatIndex(int flatIndex, int[] shape, int[] destination)
    {
        var remainder = flatIndex;
        for (var dimension = shape.Length - 1; dimension >= 0; dimension--)
        {
            destination[dimension] = remainder % shape[dimension];
            remainder /= shape[dimension];
        }
    }

    private static int ToFlatIndex(IReadOnlyList<int> indices, IReadOnlyList<int> shape, ArrayOrder order)
    {
        var result = 0;
        if (order == ArrayOrder.C)
        {
            for (var dimension = 0; dimension < indices.Count; dimension++)
            {
                result *= shape[dimension];
                result += indices[dimension];
            }

            return result;
        }

        var stride = 1;
        for (var dimension = 0; dimension < indices.Count; dimension++)
        {
            result += indices[dimension] * stride;
            stride *= shape[dimension];
        }

        return result;
    }

    private sealed class NormalizedSelection
    {
        public NormalizedSelection(ArraySelection[] selections, int[] fixedIndices, NormalizedSlice[] slices, int[] resultShape)
        {
            Selections = selections;
            FixedIndices = fixedIndices;
            Slices = slices;
            ResultShape = resultShape;
        }

        public ArraySelection[] Selections { get; }

        public int[] FixedIndices { get; }

        public NormalizedSlice[] Slices { get; }

        public int[] ResultShape { get; }
    }

    private sealed class PartialShardOuterReadGroup
    {
        public PartialShardOuterReadGroup(int[] chunkCoords)
        {
            ChunkCoords = chunkCoords;
        }

        public int[] ChunkCoords { get; }

        public Dictionary<string, PartialShardInnerReadGroup> InnerGroups { get; } = new(StringComparer.Ordinal);

        public PartialShardInnerReadGroup GetOrCreateInnerGroup(int[] innerChunkCoords)
        {
            var key = string.Join(",", innerChunkCoords);
            if (!InnerGroups.TryGetValue(key, out var group))
            {
                group = new PartialShardInnerReadGroup(innerChunkCoords.ToArray());
                InnerGroups[key] = group;
            }

            return group;
        }
    }

    private sealed class PartialShardInnerReadGroup
    {
        public PartialShardInnerReadGroup(int[] innerChunkCoords)
        {
            InnerChunkCoords = innerChunkCoords;
        }

        public int[] InnerChunkCoords { get; }

        public List<PartialShardReadItem> Items { get; } = new();
    }

    private sealed class PartialShardReadItem
    {
        public PartialShardReadItem(int[] innerLocalCoords, int[] resultIndices)
        {
            InnerLocalCoords = innerLocalCoords;
            ResultIndices = resultIndices;
        }

        public int[] InnerLocalCoords { get; }

        public int[] ResultIndices { get; }
    }

    private sealed class PartialShardOuterWriteGroup
    {
        public PartialShardOuterWriteGroup(int[] chunkCoords)
        {
            ChunkCoords = chunkCoords;
        }

        public int[] ChunkCoords { get; }

        public Dictionary<string, PartialShardInnerWriteGroup> InnerGroups { get; } = new(StringComparer.Ordinal);

        public PartialShardInnerWriteGroup GetOrCreateInnerGroup(int[] innerChunkCoords)
        {
            var key = string.Join(",", innerChunkCoords);
            if (!InnerGroups.TryGetValue(key, out var group))
            {
                group = new PartialShardInnerWriteGroup(innerChunkCoords.ToArray());
                InnerGroups[key] = group;
            }

            return group;
        }
    }

    private sealed class PartialShardInnerWriteGroup
    {
        public PartialShardInnerWriteGroup(int[] innerChunkCoords)
        {
            InnerChunkCoords = innerChunkCoords;
        }

        public int[] InnerChunkCoords { get; }

        public List<PartialShardWriteItem> Items { get; } = new();
    }

    private sealed class PartialShardWriteItem
    {
        public PartialShardWriteItem(int[] innerLocalCoords, object? value)
        {
            InnerLocalCoords = innerLocalCoords;
            Value = value;
        }

        public int[] InnerLocalCoords { get; }

        public object? Value { get; }
    }
}
