using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Zarr.Codecs;
using Zarr.Core;
using Zarr.Metadata;
using Zarr.Storage;
using ZarrApi = Zarr.Api.Zarr;

var repoRoot = FindRepoRoot();
var examplesRoot = Path.Combine(repoRoot, "examples");
var dataRoot = Path.Combine(examplesRoot, "data");
var referenceImagePath = Path.Combine(examplesRoot, "reference_image.png");

if (!File.Exists(referenceImagePath))
{
    throw new FileNotFoundException("The copied reference image was not found.", referenceImagePath);
}

Directory.CreateDirectory(dataRoot);

var image = LoadReferenceImage(referenceImagePath);
var containers = CreateContainers();
foreach (var container in containers)
{
    WriteContainer(dataRoot, image, container);
}

Console.WriteLine($"Generated {containers.Count} example containers under '{dataRoot}'.");

static string FindRepoRoot()
{
    var candidates = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory,
    };

    foreach (var candidate in candidates)
    {
        var current = new DirectoryInfo(candidate);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Zarr.Net.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }
    }

    throw new InvalidOperationException("Unable to locate the repository root from the current process context.");
}

static byte[,,] LoadReferenceImage(string imagePath)
{
    using var image = Image.Load<Rgba32>(imagePath);
    var result = new byte[image.Height, image.Width, 3];

    for (var y = 0; y < image.Height; y++)
    {
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            result[y, x, 0] = pixel.R;
            result[y, x, 1] = pixel.G;
            result[y, x, 2] = pixel.B;
        }
    }

    return result;
}

static IReadOnlyList<ContainerSpec> CreateContainers()
{
    var raw = new DatasetSpec("raw", null);
    var gzip = new DatasetSpec("gzip", new GzipChunkCodec(level: 1));
    var zstd = new DatasetSpec("zstd", new ZstdChunkCodec(level: 3, checksum: false));
    var bloscLz4 = new DatasetSpec(
        "blosc/lz4",
        new BloscChunkCodec(
            compressor: BloscCompressor.Lz4,
            level: 5,
            shuffle: BloscShuffle.Shuffle));

    return new[]
    {
        new ContainerSpec("zarr_dotnet_flat.zr", ZarrFormat.V2, new V2ChunkKeyEncoding("."), new[] { raw, gzip, zstd, bloscLz4 }),
        new ContainerSpec("zarr_dotnet_nested.zr", ZarrFormat.V2, new V2ChunkKeyEncoding("/"), new[] { raw, gzip, zstd, bloscLz4 }),
        new ContainerSpec("zarr_dotnet_flat.zr3", ZarrFormat.V3, new DefaultChunkKeyEncoding("."), new[] { raw, gzip, zstd, bloscLz4 }),
        new ContainerSpec("zarr_dotnet_nested.zr3", ZarrFormat.V3, new DefaultChunkKeyEncoding("/"), new[] { raw, gzip, zstd, bloscLz4 }),
    };
}

static void WriteContainer(
    string dataRoot,
    byte[,,] image,
    ContainerSpec container)
{
    var containerPath = Path.Combine(dataRoot, container.FileName);
    if (Directory.Exists(containerPath))
    {
        Directory.Delete(containerPath, recursive: true);
    }

    var store = new LocalStore(containerPath);
    _ = ZarrApi.CreateGroup(
        store,
        format: container.Format,
        overwrite: true,
        attributes: new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["writer"] = "zarr.net",
            ["source"] = "examples/reference_image.png",
        });

    foreach (var dataset in container.Datasets)
    {
        var array = ZarrApi.FromArray(
            image,
            store,
            chunks: new[] { 100, 100, 1 },
            path: dataset.Path,
            format: container.Format,
            overwrite: true,
            chunkCodec: dataset.Codec,
            chunkKeyEncoding: container.ChunkKeyEncoding);
        NormalizePythonInteropMetadata(store, array);
    }
}

static void NormalizePythonInteropMetadata(IStore store, ZarrArray array)
{
    var metadataKey = array.Format switch
    {
        ZarrFormat.V2 => string.IsNullOrEmpty(array.Path) ? ".zarray" : $"{array.Path}/.zarray",
        ZarrFormat.V3 => string.IsNullOrEmpty(array.Path) ? "zarr.json" : $"{array.Path}/zarr.json",
        _ => throw new InvalidOperationException($"Unsupported Zarr format '{array.Format}'."),
    };

    var payload = store.Get(metadataKey)
        ?? throw new InvalidOperationException($"Metadata payload '{metadataKey}' was not found.");
    var metadata = JsonNode.Parse(payload.ToArray())?.AsObject()
        ?? throw new InvalidOperationException($"Metadata payload '{metadataKey}' could not be parsed.");

    switch (array.Format)
    {
        case ZarrFormat.V2:
            metadata["dtype"] = ToPythonV2DType(array.DataType);
            break;

        case ZarrFormat.V3:
            metadata.Remove("order");
            break;
    }

    var json = metadata.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
    });
    store.Set(metadataKey, Encoding.UTF8.GetBytes(json));
}

static string ToPythonV2DType(DType dataType)
{
    return dataType.Name switch
    {
        "bool" => "|b1",
        "uint8" => "|u1",
        "uint16" => "<u2",
        "uint32" => "<u4",
        "uint64" => "<u8",
        "int16" => "<i2",
        "int32" => "<i4",
        "int64" => "<i8",
        "float32" => "<f4",
        "float64" => "<f8",
        _ => throw new NotSupportedException($"No Python-reference v2 dtype mapping is defined for '{dataType.Name}'."),
    };
}

internal sealed record DatasetSpec(string Path, IChunkCodec? Codec);

internal sealed record ContainerSpec(
    string FileName,
    ZarrFormat Format,
    ChunkKeyEncoding ChunkKeyEncoding,
    IReadOnlyList<DatasetSpec> Datasets);
