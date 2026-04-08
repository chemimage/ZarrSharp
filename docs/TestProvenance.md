# Test Provenance

## Gold Standard

Behavioral parity for this repository is grounded in the Python reference tests under:

- `reference/zarr-python-main/tests`

The cross-implementation repository under:

- `reference/zarr_implementations/test/test_read_all.py`

is treated as a secondary interoperability reference, not the primary spec source. In this checkout it mainly serves as an external corpus and smoke-test target; some of its checked-in v3 example metadata predates the current Python reference layout, so it is not the authority for current parity decisions.

## Suite Labels

The xUnit suite now uses a `TestBasis` trait so parity claims can be filtered cleanly. The trait can appear at either class scope or method scope.

- `PythonReference`: direct port or close adaptation of Python-reference behavior
- `DotNetImplementation`: everything else, including `.NET` surface area and implementation-only invariants

`ReferenceSource` traits point at the Python modules used as the behavioral source when there is one.

## Current Mapping

| C# test class | Basis | Primary Python reference modules | Notes |
| --- | --- | --- | --- |
| `ApiTests` | `Method-level split` | `tests/test_api.py`, `tests/test_api/test_synchronous.py`, `tests/test_group.py`, `tests/test_tree.py` | Most methods are `PythonReference`; the parameterized exact-dtype check is `DotNetImplementation`. |
| `ArrayTests` | `PythonReference` | `tests/test_array.py`, `tests/test_v2.py` | Core array create/open/read/write behavior. |
| `AsyncTests` | `Method-level split` | `tests/test_api/test_asynchronous.py`, `tests/test_sync.py`, `tests/test_group.py`, `tests/test_metadata/test_consolidated.py` | Parity-backed async API behavior is separated from `.NET` exact-dtype convenience checks and async-store call-count invariants. |
| `AttributeTests` | `PythonReference` | `tests/test_attributes.py` | Attribute persistence and mutation behavior. |
| `ConsolidatedMetadataTests` | `PythonReference` | `tests/test_metadata/test_consolidated.py` | Consolidated metadata open/traversal behavior. |
| `CustomDTypeTests` | `DotNetImplementation` | none | Delegate-backed custom dtype runtime is a `.NET` extension. |
| `FoundationTests` | `DotNetImplementation` | none | Registry/options/buffer/store-factory scaffolding is `.NET`-specific. |
| `GroupTests` | `PythonReference` | `tests/test_group.py` | Group hierarchy semantics. |
| `HierarchyTests` | `PythonReference` | `tests/test_group.py`, `tests/test_tree.py` | Hierarchy traversal and rendering. |
| `IndexingTests` | `PythonReference` | `tests/test_indexing.py` | Limited to the currently supported indexing slice. |
| `LifecycleTests` | `PythonReference` | `tests/test_array.py`, `tests/test_info.py` | Resize/append/info behavior. |
| `MetadataBehaviorTests` | `PythonReference` | `tests/test_metadata/test_v2.py`, `tests/test_metadata/test_v3.py` | Metadata fidelity and validation. |
| `NativeTypeTests` | `Method-level split` | `tests/test_dtype/test_npy/test_string.py`, `tests/test_dtype/test_npy/test_bytes.py`, `tests/test_dtype/test_npy/test_complex.py`, `tests/test_dtype/test_npy/test_structured.py`, `tests/test_dtype/test_npy/test_time.py` | Python-backed string/bytes/complex/structured coverage is separated from `.NET`-native carriers and types such as `Guid`, `DateTimeOffset`, `Memory<byte>`, and `ReadOnlyMemory<byte>`. |
| `OpenModeTests` | `DotNetImplementation` | none | `ZarrOpenMode` is a `.NET` adaptation, not a Python surface area. |
| `ShardingRuntimeTests` | `DotNetImplementation` | `tests/test_codecs/test_sharding.py` | Verifies targeted partial-decode optimization, not Python-contract behavior. |
| `StorageFidelityTests` | `PythonReference` | `tests/test_metadata/test_v2.py`, `tests/test_metadata/test_v3.py`, `tests/test_codec_pipeline.py`, `tests/test_codecs/*` | Metadata and codec layout fidelity. |
| `StorageLayerTests` | `Method-level split` | `tests/test_store/test_logging.py`, `tests/test_store/test_wrapper.py`, `tests/test_store/test_object.py`, `tests/test_store/test_local.py`, `tests/test_store/test_memory.py` | Object-store parity checks are separated from `.NET`-specific `StorePath` and provider-registration behavior. |
| `ZipStoreTests` | `PythonReference` | `tests/test_store/test_zip.py` | Zip-backed store behavior. |

## Audit Outcome

- Python-reference-backed behavior is now explicitly separated from the rest of the local implementation assertions, including inside the previously mixed files.
- The main classes that should not be used as parity evidence on their own are `CustomDTypeTests`, `FoundationTests`, `OpenModeTests`, and `ShardingRuntimeTests`.
- For the method-level split suites, parity-backed methods now carry `PythonReference`, so `dotnet test --filter "TestBasis=PythonReference"` is a cleaner approximation of the Python-backed subset.
