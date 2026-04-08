# Examples

This folder contains a small reference-style dataset generator for `Zarr` plus a
Python readback script.

## Generate data

Generate the containers:

```powershell
dotnet run --project .\examples\Zarr.Examples.GenerateData\Zarr.Examples.GenerateData.csproj
```

This writes a small matrix of containers under `examples/data`:

- `zarr_dotnet_flat.zr`
- `zarr_dotnet_nested.zr`
- `zarr_dotnet_flat.zr3`
- `zarr_dotnet_nested.zr3`

Each container contains:

- `raw`
- `gzip`
- `zstd`
- `blosc/lz4`

The datasets are generated from `examples/reference_image.png`, copied from the
reference interop corpus.

## Read back with Python

This folder now has a `uv`-managed Python environment that reads against the
checked-in Python reference implementation under `reference/zarr-python-main`.

Run the script directly:

```powershell
uv run --project .\examples python .\examples\test_read_zarr.py
```

Or run it with `pytest`:

```powershell
uv run --project .\examples pytest .\examples\test_read_zarr.py -q
```

Current status against the checked-in Python reference:

- `raw`, `gzip`, `zstd`, and `blosc/lz4` read back successfully
