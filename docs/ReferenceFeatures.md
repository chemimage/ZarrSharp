# Python Reference Features

This repository uses `reference/zarr-python-main/src/zarr` as the comparison baseline for feature coverage and behavioral alignment.

## Test Basis

Behavioral comparison is grounded in `reference/zarr-python-main/tests`. The local xUnit suite tags tests with `TestBasis` and `ReferenceSource` so `PythonReference` assertions are distinguishable from the broader `DotNetImplementation` coverage. The detailed matrix lives in `docs/TestProvenance.md`.

## Current Missing Features

- `zarr.api`: Python-style URL and `storage_options` overload breadth.
- `zarr.core`: advanced indexing beyond integer and simple slice selection.
- `zarr.core.dtype`: more exact NumPy-compatible time and delta metadata variants, including broader unit-tagged behavior.
- `zarr.storage`: built-in remote provider implementations for concrete S3, GCS, or Azure-style backends.
- `zarr.storage`: built-in object-store provider integrations beyond the generic backend adapter.
- `zarr.storage`: broader Python-style URL, mapper, and `storage_options` coercion parity.
- `zarr.buffer`: the full public `buffer` / `ndbuffer` class surface and ndarray-style wrapper semantics from the Python reference.
- `zarr.experimental`: a public `Zarr.Experimental` package/namespace.
- `experimental`: experimental cache-store behavior.
- `zarr._cli`: the Python CLI surface.

## Namespace Mapping

Implemented:

- `zarr.api` -> `Zarr.Api`
- `zarr.core` -> `Zarr.Core`
- `zarr.codecs` -> `Zarr.Codecs`
- `zarr.storage` -> `Zarr.Storage`
- `zarr.buffer` -> `Zarr.Buffer`
- `zarr.abc` -> `Zarr.Abstractions`
- `zarr.registry` -> `Zarr.Registry`
- `zarr.types` -> `Zarr.Types`
- `zarr.core.config` -> `Zarr.Configuration`
- `zarr.metadata` and `zarr.core.metadata` -> `Zarr.Metadata`
- `zarr.errors` -> `Zarr.Errors`

Adjusted:

- Internal implementation helpers are grouped under `Zarr.Internal` instead of mirroring Python private-module layout one-to-one.
- `zarr.metadata` and `zarr.core.metadata` are merged into a single public `Zarr.Metadata` namespace.

Not Implemented:

- `zarr.experimental` is not yet shipped as a public `Zarr.Experimental` package/namespace.

## `zarr.api`

Implemented:

- Sync and async array/group open, create, read, write, load, save, tree, copy, hierarchy, migration, and consolidated-open flows.
- High-level helpers for `save`, `save_group`, `copy`, `copy_all`, `copy_store`, and `create_hierarchy`.
- Top-level sync and async `open` over existing array-or-group nodes.
- Top-level sync and async `load` for both arrays and groups.
- Async convenience helpers matching the current ported slice of `array`, `from_array`, `empty`, `zeros`, `ones`, `full`, and `*_like`.

Adjusted:

- File-mode behavior is exposed through `ZarrOpenMode` instead of Python string modes.
- Store and path coercion is shaped around explicit .NET types and store abstractions rather than Python `storage_options` and mapper conventions.

Not Implemented:

- Python-style URL and `storage_options` overload breadth.

## `zarr.core`

Implemented:

- Core synchronous array and group creation, open, read, write, delete, and hierarchy flows.
- Async-capable array/group flows over `IAsyncStore`.
- Array/group info summaries, plain-text tree rendering, and lifecycle operations including `resize` and `append`.
- v2 and v3 metadata persistence, chunk-key encoding, regular chunk-grid handling, and bidirectional v2/v3 migration helpers with explicit rejection of unrepresentable v3-only metadata when targeting v2.
- Stage-aware runtime codec pipeline splitting into array-array, array-bytes, and bytes-bytes phases.
- Sharding support, including deterministic `index_codecs` validation, targeted scalar and inner-chunk partial access, and basic contiguous region read/write behavior through outer bytes-to-bytes codecs.
- Consolidated metadata persistence, open, traversal, refreshed return values from consolidation helpers, child traversal control similar to Python's `use_consolidated_for_children`, and reconsolidation that rebuilds from store metadata instead of trusting stale descendant manifests.
- In-memory consolidated-metadata mutation propagation for descendants reached through already-consolidated parent views.
- Basic integer and simple slice selection.
- Strict metadata-extension and storage-transformer validation with configured metadata-policy enforcement, typed extension views on groups/arrays, and typed storage-transformer companion views on arrays.
- Runtime consumption of configuration defaults for format, array order, missing-chunk reads, empty-chunk writes, consolidated lookup, child consolidated traversal, metadata policy selection, JSON formatting, read-only wrapping, async timeout/concurrency hooks, and buffer prototype selection.

Adjusted:

- Non-regular chunk grids without a usable regular `chunk_shape` are parsed and preserved in metadata, but runtime data access is explicitly rejected. This matches the current Python reference behavior more closely than pretending they are executable.
- Storage transformers are parsed, validated, and preserved in metadata, but runtime execution is explicitly rejected. This is treated as aligned behavior, not silent omission.
- Async behavior is centered on `IAsyncStore` rather than Python coroutine-based store protocols.
- Contiguous sharded region reads currently materialize through the existing scalar partial-access path for correctness, rather than a separate grouped bulk-decode optimization.

Not Implemented:

- Advanced indexing beyond integer and simple slice selection.

## `zarr.core.dtype`

Implemented:

- Primitive numeric dtypes, complex values, strings, fixed-width strings, byte arrays, fixed-width bytes, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `Memory<byte>`, and `ReadOnlyMemory<byte>`.
- Python-style v3 `structured` metadata round-trip for the current structured-record model.
- Explicit array creation from `DType` instances for parameterized dtypes.
- Delegate-backed custom dtypes through `DTypeRegistry`, including runtime fill-value, encode/decode, reopen, and typed read/write participation.
- Exact dtype-signature checks for `require_array(..., exact: true)`.

Adjusted:

- The dtype surface includes CLR-native carriers such as `Guid`, `DateTimeOffset`, `Memory<byte>`, and `ReadOnlyMemory<byte>` that do not have direct Python equivalents.
- Structured dtype handling follows the Python reference's current `Structured` wrapper: ordered field definitions of `[name, dtype]` rather than a separate exposed layout-control model.

Not Implemented:

- More exact NumPy-compatible time and delta metadata variants, including broader unit-tagged behavior.

## `zarr.codecs`

Implemented:

- Bytes/endian
- CRC32C
- Gzip
- Zstd
- Blosc
- Sharding
- Transpose
- Variable-length UTF-8
- Variable-length bytes
- Public codec registration

Adjusted:

- The runtime codec path is exposed through explicit .NET codec-stage abstractions and registries.

Not Implemented:

- None beyond the remaining sharding limitations tracked under `zarr.core`.

## `zarr.storage`

Implemented:

- In-memory, local filesystem, zip-backed, and object-backed stores.
- Public `IStore` and `IAsyncStore` contracts using `ReadOnlyMemory<byte>` payloads for sync and async store operations.
- Wrapper and logging stores.
- `StorePath`, `StoreFactory`, URI-scheme registration, and typed object-spec store coercion.
- Generic object-store backend support through `IObjectStoreBackend` and `ObjectStore`.
- Typed provider registration hooks through `StoreProviderRegistry`.

Adjusted:

- Storage creation is based on explicit .NET store types, URIs, and typed option objects instead of Python fsspec-style mapper coercion.
- The core package stays provider-free and uses registration hooks rather than bundling cloud SDK integrations.

Not Implemented:

- Built-in remote provider implementations for concrete S3, GCS, or Azure-style backends.
- Built-in object-store provider integrations beyond the generic backend adapter.
- Broader Python-style URL, mapper, and `storage_options` coercion parity.

## `zarr.buffer`

Implemented:

- `ByteRange`, `BufferPrototype`, and `IBufferAllocator`.
- Buffer-native runtime plumbing centered on `ReadOnlyMemory<byte>`, `Memory<byte>`, and `IMemoryOwner<byte>`.
- Buffer-aware store, codec, dtype, metadata, and array chunk paths through the runtime stack.
- Registry-selected buffer prototypes for allocation-sensitive sharding and index-buffer paths.

Adjusted:

- The buffer surface follows .NET memory primitives instead of mirroring Python `buffer` and `ndbuffer` classes one-to-one.
- Buffer prototype selection is currently consumed by allocation-sensitive sharding paths, not every byte flow in the runtime.

Not Implemented:

- The full public `buffer` / `ndbuffer` class surface and ndarray-style wrapper semantics from the Python reference.

## `zarr.abc`, `zarr.registry`, `zarr.types`, `zarr.core.config`

Implemented:

- Public abstraction interfaces for codecs, chunk grids, storage transformers, dtypes, and store providers.
- Public abstraction interfaces for metadata extensions and metadata policies.
- Public registries for codecs, dtypes, chunk grids, storage transformers, store providers, metadata extensions, metadata policies, and buffer prototypes.
- Registry-driven runtime dtype selection for delegate-backed custom dtypes.
- Shared public type definitions for node detection and typed configuration defaults.
- Strongly typed configuration objects with a shared global `ZarrOptions` baseline.
- Runtime consumption of configured metadata-policy and buffer-prototype names.
- Runtime consumption of typed defaults for format, order, missing/empty chunk behavior, consolidated lookup preferences, read-only wrapping, JSON formatting, and async timeout/concurrency hooks.

Adjusted:

- Configuration is .NET-native and strongly typed rather than environment-variable and entry-point driven.
- Registry use is explicit and in-process rather than Python plugin discovery based.
- Concrete array/group metadata model types remain internal even though typed extension and policy protocols are public.
- Metadata policy and extension selection are configured explicitly through registries instead of environment variables or entry-point discovery.
- JSON indentation on `netstandard2.1` is effectively compact-vs-indented output rather than exact indent-width control.
- Async concurrency is currently used for parallel shard-write fan-out rather than as a library-wide scheduler policy.

Not Implemented:

- None beyond the intentional configuration and public-surface adjustments above.

## `experimental`

Implemented:

- None.

Adjusted:

- If added later, experimental work is expected to live in a separate `Zarr.Experimental` package rather than inside the core assembly.

Not Implemented:

- Experimental cache-store behavior.

## `zarr._cli`

Implemented:

- None.

Adjusted:

- None.

Not Implemented:

- The Python CLI surface has no C# counterpart in this repository.
