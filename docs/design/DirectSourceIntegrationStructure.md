# Direct Source Integration — File & Folder Structure Plan

**Status:** Draft / proposal\
**Branch analysed:** `msdata/direct` @ `1bc2c522` (`[Internal] Direct package: Adds msdata/direct update from main (#5951)`)\
**Import source (R-1):** msdata `sdkReleases/direct/EN20260409-3.44.1` — Direct **3.44.1** (O-7)\
**Target:** `main`

**Goal:** Land the Direct source tree on `main` and **remove the `PackageReference` to the `Microsoft.Azure.Cosmos.Direct` package entirely** — the SDK compiles those sources instead of restoring the published assembly, and the native query-plan engine comes from the public **`Microsoft.Azure.Cosmos.QueryPlanInterop.Windows`** package on nuget.org (D-3).

> **`Microsoft.Azure.Cosmos.Direct.dll` does *not* go away.** D-0 resolved to **option C**: the assembly keeps shipping in the nupkg with its existing identity, built from a new in-repo `Microsoft.Azure.Cosmos.Direct.csproj` rather than restored from nuget.org. What is removed is the **package dependency**, not the assembly. Folding these types into `Client.dll` and dropping `Direct.dll` was **option A**, and it was rejected — it breaks the 48 friend assemblies (C-5) and pulls 147 public types into the SDK surface (C-2).

---

## 1. Objective & Scope

### In scope

1. Reorganise the flat `Microsoft.Azure.Cosmos/src/direct/` dump (373 files) into a coherent, navigable folder tree.
2. Define the project/build changes required so the SDK compiles the Direct sources **from source**, with **no `PackageReference` to `Microsoft.Azure.Cosmos.Direct` at all** — neither compile-time nor restore-only. The native assets that package carried come from the public `QueryPlanInterop` package instead (D-3).
3. ~~Decide which assembly the `Microsoft.Azure.Documents.*` types ship from~~ — **resolved, D-0 option C**: they keep shipping from `Microsoft.Azure.Cosmos.Direct.dll`, now built in this repo. This is what keeps the effort non-breaking.
4. Define the migration sequencing so history, CI and the shipped package surface stay intact.
5. Define the CI gates that keep the in-repo copy compatible with msdata and with existing consumers — §9.

### Out of scope (explicitly)

- Renaming namespaces. `Microsoft.Azure.Documents.*` stays as-is. Folder layout and namespace layout are decoupled on purpose (see §4, principle P2).
- Refactoring Direct code behaviour. This is a **move-only** change.

> **Resolved 2026-09-23 — `main` is upstream.** Earlier drafts deferred the ownership/sync story with the `msdata` CosmosDB repo and treated it as the blocking decision gate, because §9's entire divergence apparatus inverts depending on the answer. It is now settled: **v3 `main` is the source of truth; msdata is downstream.** Consequences recorded throughout — §9.E Gate 2 is the authoritative gate, the sync tooling becomes an *export* rather than an import (D-7), `src/Direct/` is hand-editable here by design, and T0 changes originate here with msdata sign-off rather than “upstream only”.

---

## 2. Current State

### 2.1 Inventory

`Microsoft.Azure.Cosmos/src/direct/` — **373 files**, of which 371 are `.cs`:

| Location | Count |
| --- | --- |
| `direct/` (flat root) | 361 |
| `direct/Azure.Core/` | 3 |
| `direct/FaultInjection/` | 1 |
| `direct/rntbd2/` | 3 (2 `.cs` + 1 `.tt`) |
| `direct/Telemetry/` | 5 |

Non-`.cs` files: `direct/msdata_sync.ps1`, `direct/rntbd2/HeadersTransportSerialization.tt`.

> **Two more msdata-sourced files live *outside* this tree.** `msdata_sync.ps1` copies `RMResources.Designer.cs` and `RMResources.resx` to `$currentLocation\..` — i.e. `Microsoft.Azure.Cosmos/src/`, one level **above** `src/direct/`. Both exist on `msdata/direct` and **neither exists on `main`**, so phase 1a must bring them across. They are deliberately excluded from the §6 mapping (which covers only the 372 files that move into `Direct/`), but D-7's sync rewrite must still handle them — they are part of the msdata surface and drift the same way everything else does.

### 2.2 Namespace distribution

| Files | Namespace |
| ---: | --- |
| 277 | `Microsoft.Azure.Documents` |
| 35 | `Microsoft.Azure.Documents.Rntbd` |
| 21 | `Microsoft.Azure.Documents.Routing` |
| 10 | `Microsoft.Azure.Documents.Client` |
| 6 | `Microsoft.Azure.Documents.Collections` |
| 5 | `Microsoft.Azure.Documents.Telemetry` |
| 4 | `Microsoft.Azure.Cosmos.Rntbd` |
| 3 | `Azure.Core` |
| 3 | `Microsoft.Azure.Cosmos.Core.Trace` |
| 1 each | `Microsoft.Azure.Cosmos.Core`, `Microsoft.Azure.Cosmos.Direct`, `Microsoft.Azure.Cosmos.ServiceFramework.Core`, `Microsoft.Azure.Documents.Common`, `Microsoft.Azure.Documents.FaultInjection`, `Microsoft.Azure.Documents.SharedFiles.Routing`, `System.Diagnostics.CodeAnalysis` |

The namespace tree is *not* a usable folder taxonomy on its own — 74 % of files sit in one namespace.

### 2.3 Problems with the current layout

| # | Problem |
| --- | --- |
| P-1 | 361 files in a single flat folder. Unnavigable in Solution Explorer and in GitHub's file tree. |
| P-2 | Lowercase `direct/` and `rntbd2/` folder names are inconsistent with every other folder in `src/` (PascalCase). |
| P-3 | 55 filename collisions between `src/**` and `src/direct/**` (e.g. `PartitionKey.cs`, `Index.cs`, `Database.cs`, `UserAgentContainer.cs`, `UInt128.cs`, `IResourceResponseBase.cs`). Compiles fine (different namespaces) but is hostile to "go to file" navigation and to code review. |
| P-4 | Two `TransportClient.cs` files inside `direct/` itself (`direct/TransportClient.cs` = `Microsoft.Azure.Documents`, `direct/rntbd2/TransportClient.cs` = `Microsoft.Azure.Documents.Rntbd`). |
| P-5 | Direct's StyleCop/compiler-warning debt is suppressed **project-wide** via `<NoWarn>` in `Microsoft.Azure.Cosmos.csproj` (26 extra rule IDs), which silently weakens analysis on hand-written v3 code too. |
| P-6 | `direct/msdata_sync.ps1` (a developer tool) ships inside the source tree. |
| P-7 | The build still carries `PackageReference Microsoft.Azure.Cosmos.Direct` with `<ExcludeAssets>compile</ExcludeAssets>` plus a `ProjectRef` MSBuild switch — a dual-mode build that is hard to reason about. |
| P-8 | **`msdata_sync.ps1` cannot discover new files.** Its main loop is `foreach ($file in Get-ChildItem . -Name)` — it iterates files *already present in v3* and looks each one up in msdata. A file newly added to Direct is never pulled. This is why [docs/sync_up_msdata_direct.md](docs/sync_up_msdata_direct.md) instructs the operator to copy missing files by hand, and why the sync agent needs a separate "verify sync completeness" step to compensate. |
| P-9 | **Three of the four subfolders are never synced.** That `Get-ChildItem` is non-recursive and only `rntbd2/TransportClient.cs` is special-cased, so `Azure.Core/`, `Telemetry/` and `FaultInjection/` have **no automated sync at all** and can silently rot. |
| P-10 | **Provenance is discarded.** Files are flattened from 13 msdata source directories into one folder with no record of origin, so drift cannot be scoped or attributed back to a source tree. |
| P-11 | **The `msdata/direct` branch is not a shippable configuration.** It packs `Microsoft.Azure.Cosmos.Direct.dll` into `lib/netstandard2.0` *while also* compiling the same types into `Microsoft.Azure.Cosmos.Client.dll`. Any consumer restoring that package would get two definitions of every `Microsoft.Azure.Documents` type — `CS0433` at compile, `TypeLoadException` at runtime. The branch is a visualisation aid; promoting it verbatim is not an option. Resolved by D-0 + D-4. |
| P-12 | **The branch is version-stale.** `msdata/direct` sits at v3 `3.61.0` / Direct `3.43.2`; `main` is at `3.63.0` / `3.44.0`; the O-7 reference is Direct **`3.44.1`**. The import is against a two-release-old baseline and **both** sides move during the re-baseline — see R-1. |

### 2.4 Where the Direct dependency lives today

| File | Line | Usage |
| --- | --- | --- |
| [Directory.Build.props](Directory.Build.props#L6) | 6 | `<DirectVersion>3.43.2</DirectVersion>` |
| [Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L121) | 121 | `PackageReference` (compile excluded) |
| [Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L167) | 167 | Packs `Microsoft.Azure.Cosmos.Direct.dll` into `lib/netstandard2.0` |
| [Microsoft.Azure.Cosmos/FaultInjection/src/FaultInjection.csproj](Microsoft.Azure.Cosmos/FaultInjection/src/FaultInjection.csproj#L54) | 54 | `PackageReference` |
| [Microsoft.Azure.Cosmos/FaultInjection/tests/FaultInjectionTests.csproj](Microsoft.Azure.Cosmos/FaultInjection/tests/FaultInjectionTests.csproj#L20) | 20 | `PackageReference` |
| [Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Microsoft.Azure.Cosmos.Tests.csproj](Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Microsoft.Azure.Cosmos.Tests.csproj#L43) | 43, 76 | `PackageReference` + reads the `.nuspec` in a contract test |
| [Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Microsoft.Azure.Cosmos.EmulatorTests.csproj](Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Microsoft.Azure.Cosmos.EmulatorTests.csproj#L22) | 22 | `PackageReference` |
| [Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/Microsoft.Azure.Cosmos.Performance.Tests.csproj](Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/Microsoft.Azure.Cosmos.Performance.Tests.csproj#L36) | 36 | `PackageReference` |
| [Microsoft.Azure.Cosmos.Samples/Tools/Benchmark/CosmosBenchmark.csproj](Microsoft.Azure.Cosmos.Samples/Tools/Benchmark/CosmosBenchmark.csproj#L46) | 46 | `PackageReference` |
| [Microsoft.Azure.Cosmos.Samples/Tools/CTL/CosmosCTL.csproj](Microsoft.Azure.Cosmos.Samples/Tools/CTL/CosmosCTL.csproj#L26) | 26 | `PackageReference` |

**Critical coupling:** the Direct nupkg is also the *only* source of the native assets that [Microsoft.Azure.Cosmos.targets](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.targets) copies and that the SDK nupkg republishes under `runtimes/win-x64/native`: `Microsoft.Azure.Cosmos.ServiceInterop.dll`, `Cosmos.CRTCompat.dll`, `msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`. Deleting the `PackageReference` outright breaks the query-plan ServiceInterop path.

> **The package is being discontinued.** There will be no Direct releases after the current one, so "keep a restore-only reference" is not a durable answer — it would pin this repo to a deprecated package indefinitely and trip Component Governance on every build. D-3 resolves this by replacing all five native assets with the single self-contained DLL from the separately published **`Microsoft.Azure.Cosmos.QueryPlanInterop.Windows`** package on public nuget.org.

---

## 3. Non-negotiable Constraints

| ID | Constraint |
| --- | --- |
| C-1 | **No namespace changes.** `Microsoft.Azure.Documents.*` types are referenced by FaultInjection, CTL, Benchmark, tests, and (via `InternalsVisibleTo`) other Microsoft assemblies. |
| C-2 | **No public API surface change** to `Microsoft.Azure.Cosmos`. The `contracts/API_*.txt` baselines must be unchanged by this work. |
| C-3 | **`git mv` only** — every move must preserve blame/history. No delete-and-re-add. |
| C-4 | The native `ServiceInterop` assets must keep flowing into `runtimes/win-x64/native` in the produced nupkg. |
| C-5 | `AssemblyKeys.cs` (`Microsoft.Azure.Cosmos.Direct.AssemblyKeys`) and the `InternalsVisibleTo` graph must keep working. |
| C-6 | Both the Direct-source build and the current package-based build must be green **at every commit** during the migration (§8 phases are ordered to guarantee this). |
| C-7 | The native assets must be **present in every produced nupkg and provenance-verifiable**: sourced from a signed package at a pinned version, and asserted present at pack time (RID-scoped). Acquisition may degrade gracefully where the feed is unreachable; **packing must never succeed without them**. |

---

## 4. Design Principles

- **P1 — One top-level home.** All Direct-origin code lives under `Microsoft.Azure.Cosmos/src/Direct/`. A single folder boundary makes "what came from msdata" answerable by path, which the sync tooling and CODEOWNERS both need.
- **P2 — Folders group by *function*, not by namespace.** C# does not require folder/namespace agreement, and 277 files share one namespace. Functional grouping is the only layout that scales here. Namespace is recorded per file in §6 so the mapping stays auditable.
- **P3 — Mirror `src/` conventions.** PascalCase folders, max depth 3 below `Direct/`, `I*.cs` interfaces sit next to their implementations.
- **P4 — Keep msdata-origin grouping recoverable.** Folder names deliberately echo the msdata source directories used by `msdata_sync.ps1` (`SharedFiles/Routing`, `SharedFiles/Rntbd2`, `SharedFiles/Collections`, `Core/Core.Trace`, …) so the sync script's path table maps 1:1 onto the new tree.
- **P5 — Scope analyzer debt.** Move the Direct `NoWarn` set out of the csproj into `src/Direct/.editorconfig`, so v3 hand-written code regains full analysis.
- **P6 — Tooling is not source.** `msdata_sync.ps1` moves to `tools/`.

---

## 5. Proposed Target Structure

```
Microsoft.Azure.Cosmos/src/Direct/
├── .editorconfig                      # Direct-scoped analyzer suppressions (replaces csproj NoWarn)
├── README.md                          # provenance, house rules, msdata coordination policy
├── AssemblyKeys.cs
│
├── Authorization/                     #   3 files
├── Collections/                       #   7 files   (msdata: SharedFiles/Collections)
├── Compat/
│   ├── AzureCore/                     #   3 files   (vendored Azure.Core diagnostics)
│   └── Polyfills/                     #   1 file
├── Diagnostics/                       #   2 files
│   ├── PerformanceCounters/           #   3 files
│   ├── SystemMonitoring/              #  12 files
│   └── Tracing/                       #   5 files   (msdata: Cosmos/Core/Core.Trace)
├── Exceptions/                        #  25 files
├── FaultInjection/                    #   1 file
├── Interop/                           #   8 files   (msdata: Client/LegacyXPlatform)
├── Resources/                         #  31 files
│   └── Settings/                      #  68 files
├── Retry/                             #   9 files
├── Routing/                           #  13 files   (msdata: SharedFiles/Routing)
│   ├── Addressing/                    #  12 files
│   └── PartitionKeyComponents/        #  12 files
├── Serialization/                     #   9 files
├── ServiceModel/                      #  29 files
│   ├── Client/                        #   9 files   (Microsoft.Azure.Documents.Client)
│   └── Constants/                     #   5 files
├── Session/                           #   8 files
├── Store/                             #  17 files
├── Telemetry/                         #   5 files
├── Transport/                         #  15 files
│   ├── Http/                          #   3 files
│   └── Rntbd/                         #  34 files   (msdata: SharedFiles/Rntbd, Rntbd2)
└── Utilities/                         #  16 files
    ├── IO/                            #   3 files
    └── Timers/                        #   3 files
```

**Total: 372 files** = the 373 currently in `src/direct/`, minus `msdata_sync.ps1` which moves to `tools/`. Every file is individually accounted for in §6.

Plus, outside `src/Direct/`:

```
tools/msdata-direct/msdata_sync.ps1                        # moved from src/direct/msdata_sync.ps1
```

**No native binaries are committed.** The five `ServiceInterop` assets are restored at build time into `$(OutputPath)` and packed from there — see D-3. `runtimes/win-x64/native/` remains the layout *inside the produced nupkg*, unchanged, but it is not a tracked directory in this repo.

### Folder charter (one line each)

| Folder | Contains |
| --- | --- |
| `Authorization/` | Token provider abstraction + RBAC / token-type contracts. |
| `Collections/` | `INameValueCollection` family and its concrete header collections. |
| `Compat/AzureCore/` | Vendored `Azure.Core` diagnostic-scope types (kept out of the SDK's own namespaces). |
| `Compat/Polyfills/` | BCL attribute polyfills for `netstandard2.0`. |
| `Diagnostics/` | Client-side request statistics. |
| `Diagnostics/PerformanceCounters/` | ETW/perf-counter emission. |
| `Diagnostics/SystemMonitoring/` | CPU / memory / thread-pool sampling used by the RNTBD health checks. |
| `Diagnostics/Tracing/` | `DefaultTrace`, ETW listener, communication event source. |
| `Exceptions/` | Every `DocumentClientException` subtype + exception helpers. |
| `FaultInjection/` | `IChaosInterceptor` seam consumed by the FaultInjection package. |
| `Interop/` | `ServiceInteropWrapper`, `NativeMethods.*`, platform detection. |
| `Resources/` | Server resource models (`Document`, `DocumentCollection`, `Offer`, …) + `JSonSerializable` derivatives. |
| `Resources/Settings/` | Policy/spec/enum types that hang off those resources (indexing, vector, full-text, encryption, backup…). |
| `Retry/` | Retry-policy abstractions and the Gone/RetryWith policies. |
| `Routing/` | `PartitionKeyInternal`, `PartitionKeyRange`, `Range`, path helpers, region proximity, hashing. |
| `Routing/Addressing/` | Address cache/resolution/selection contracts and models. |
| `Routing/PartitionKeyComponents/` | The `IPartitionKeyComponent` implementations. |
| `Serialization/` | JSON converters, type resolvers, serialization-format contracts. |
| `ServiceModel/` | `DocumentServiceRequest`/`Response`, `ResourceOperation`, operation/resource enums, capability negotiation. |
| `ServiceModel/Constants/` | `Constants`, `HttpConstants`, `WFConstants`, `RuntimeConstants`, `StatusCodes`. |
| `Session/` | Session container + session-token implementations and helpers. |
| `Store/` | `StoreClient`, `StoreReader`, quorum/consistency readers & writer, replicated resource client. |
| `Telemetry/` | OpenTelemetry recorder + distributed-context propagators. |
| `Transport/` | Protocol-agnostic transport contracts, `TransportAddressUri`, transport exceptions/stats. |
| `Transport/Http/` | HTTP (gateway) transport client and helpers. |
| `Transport/Rntbd/` | RNTBD channels, connections, dispatcher, token/serialization layer. |
| `Utilities/` | Small stateless helpers with no better home. |
| `Utilities/IO/` | Stream pooling / cloning helpers. |
| `Utilities/Timers/` | `TimerPool`, `PooledTimer`, `TimeoutHelper`. |

---

## 6. Complete File Mapping

Every path below is relative to `Microsoft.Azure.Cosmos/src/`. Source is `direct/<name>` unless noted.

### `Direct/` (root)
`AssemblyKeys.cs`

### `Direct/Authorization/`
`AuthorizationTokenType.cs`, `AzureRbac.cs`, `IAuthorizationTokenProvider.cs`

### `Direct/Collections/`
`DictionaryNameValueCollection.cs`, `INameValueCollection.cs`, `INameValueCollectionFactory.cs`, `NameValueCollectionWrapper.cs`, `RequestNameValueCollection.cs`, `SerializableNameValueCollection.cs`, `StoreResponseNameValueCollection.cs`

### `Direct/Compat/AzureCore/`  *(from `direct/Azure.Core/`)*
`AppContextSwitchHelper.cs`, `DiagnosticScope.cs`, `DiagnosticScopeFactory.cs`

### `Direct/Compat/Polyfills/`
`StringSyntaxAttribute.cs`

### `Direct/Diagnostics/`
`ClientSideRequestStatistics.cs`, `IClientSideRequestStatistics.cs`

### `Direct/Diagnostics/PerformanceCounters/`
`PerfCounters.cs`, `PerformanceActivities.cs`, `PerformanceActivity.cs`

### `Direct/Diagnostics/SystemMonitoring/`
`CpuLoad.cs`, `CpuLoadHistory.cs`, `CpuMonitor.cs`, `LinuxSystemUtilizationReader.cs`, `SystemUsageHistory.cs`, `SystemUsageLoad.cs`, `SystemUsageMonitor.cs`, `SystemUsageRecorder.cs`, `SystemUtilizationReaderBase.cs`, `ThreadInformation.cs`, `UnsupportedSystemUtilizationReader.cs`, `WindowsSystemUtilizationReader.cs`

### `Direct/Diagnostics/Tracing/`
`DefaultTrace.cs`, `DefaultTraceEx.cs`, `EtwNativeInterop.cs`, `EtwTraceListener.cs`, `ICommunicationEventSource.cs`

### `Direct/Exceptions/`
`ArchivalPartitionNotPresentException.cs`, `BadRequestException.cs`, `ConflictException.cs`, `Error.cs`, `ExceptionExtensions.cs`, `ForbiddenException.cs`, `GoneException.cs`, `HttpException.cs`, `InternalServerErrorException.cs`, `InvalidPartitionException.cs`, `LeaseNotFoundException.cs`, `LockedException.cs`, `MethodNotAllowedException.cs`, `NotFoundException.cs`, `PartitionIsMigratingException.cs`, `PartitionKeyRangeGoneException.cs`, `PartitionKeyRangeIsSplittingException.cs`, `PreconditionFailedException.cs`, `RequestEntityTooLargeException.cs`, `RequestRateTooLargeException.cs`, `RequestTimeoutException.cs`, `RetryWithException.cs`, `ServiceUnavailableException.cs`, `UnauthorizedException.cs`, `WebExceptionUtility.cs`

### `Direct/FaultInjection/`  *(unchanged path)*
`IChaosInterceptor.cs`

### `Direct/Interop/`
`CustomNetCoreTypes.cs`, `CustomTypeExtensions.cs`, `NativeMethods.Darwin.cs`, `NativeMethods.Unix.cs`, `NativeMethods.Windows.cs`, `Platform.cs`, `PlatformApis.cs`, `ServiceInteropWrapper.cs`

### `Direct/Resources/`
`AccountConfigurationProperties.cs`, `Attachment.cs`, `ClientEncryptionKey.cs`, `Conflict.cs`, `Database.cs`, `Document.cs`, `DocumentCollection.cs`, `FeedResource.cs`, `MaterializedViewDefinition.cs`, `MaterializedViews.cs`, `MaterializedViewsProperties.cs`, `Offer.cs`, `OfferContentV2.cs`, `OfferTypeResolver.cs`, `OfferV2.cs`, `PartitionedSystemDocument.cs`, `PartitionKeyRangeStatistics.cs`, `PartitionKeyStatistics.cs`, `Permission.cs`, `Resource.cs`, `ResourceId.cs`, `ResourceIdBase64Decoder.cs`, `Schema.cs`, `Snapshot.cs`, `SnapshotContent.cs`, `StoredProcedure.cs`, `SystemDocument.cs`, `Trigger.cs`, `User.cs`, `UserDefinedFunction.cs`, `UserDefinedType.cs`

### `Direct/Resources/Settings/`
`BoundingBoxSpec.cs`, `ByokConfig.cs`, `ByokStatus.cs`, `ChangeFeedPolicy.cs`, `ClientEncryptionIncludedPath.cs`, `ClientEncryptionPolicy.cs`, `CMKMetadataInfo.cs`, `CollectionBackupPolicy.cs`, `CollectionBackupType.cs`, `CollectionTieringPolicy.cs`, `CompositePath.cs`, `ComputedProperty.cs`, `ConflictResolutionMode.cs`, `ConflictResolutionPolicy.cs`, `DataEncryptionKeyStatus.cs`, `DataMaskingIncludedPath.cs`, `DataMaskingPolicy.cs`, `DataType.cs`, `DistanceFunction.cs`, `Embedding.cs`, `EmbeddingSource.cs`, `EncryptionScopeMetadata.cs`, `ExcludedPath.cs`, `ExternalBackupFeatureFlags.cs`, `FullTextIndexPath.cs`, `FullTextPath.cs`, `FullTextPolicy.cs`, `GeoLinkTypes.cs`, `GeospatialConfig.cs`, `GeospatialType.cs`, `HashIndex.cs`, `InAccountRestoreParameters.cs`, `IncludedPath.cs`, `Index.cs`, `IndexingDirective.cs`, `IndexingMode.cs`, `IndexingPolicy.cs`, `IndexKind.cs`, `InternalSchemaProperties.cs`, `KeyWrapMetadata.cs`, `PartitionKeyDefinition.cs`, `PartitionKeyDefinitionVersion.cs`, `PartitionKind.cs`, `PermissionMode.cs`, `PreviousImageRetentionPolicy.cs`, `QuantizerType.cs`, `RangeIndex.cs`, `ReadPolicy.cs`, `RemoteStorageType.cs`, `ReplicationPolicy.cs`, `SchemaBuilderMode.cs`, `SchemaDiscoveryPolicy.cs`, `SnapshotKind.cs`, `SnapshotState.cs`, `SoftDeletionMetadata.cs`, `SpatialIndex.cs`, `SpatialSpec.cs`, `SpatialType.cs`, `SystemDocumentType.cs`, `TriggerOperation.cs`, `TriggerType.cs`, `UniqueIndexReIndexContext.cs`, `UniqueKey.cs`, `UniqueKeyPolicy.cs`, `VectorDataType.cs`, `VectorEmbeddingPolicy.cs`, `VectorIndexPath.cs`, `VectorIndexType.cs`

### `Direct/Retry/`
`BackoffRetryUtility.cs`, `GoneAndRetryWithRequestRetryPolicy.cs`, `GoneAndRetryWithRetryPolicy.cs`, `GoneOnlyRequestRetryPolicy.cs`, `IRequestRetryPolicy.cs`, `IRetriableResponse.cs`, `IRetryPolicy.cs`, `IServiceRetryParams.cs`, `RequestRetryUtility.cs`

### `Direct/Routing/`
`Int128.cs`, `LocationNames.cs`, `MurmurHash.cs`, `PartitionKey.cs`, `PartitionKeyInternal.cs`, `PartitionKeyInternalJsonConverter.cs`, `PartitionKeyRange.cs`, `PartitionKeyRangeIdentity.cs`, `PartitionKeyRangeStatus.cs`, `Paths.cs`, `PathsHelper.cs`, `Range.cs`, `RegionProximityUtil.cs`

### `Direct/Routing/Addressing/`
`Address.cs`, `AddressCacheToken.cs`, `AddressEnumerator.cs`, `AddressInformation.cs`, `AddressSelector.cs`, `IAddressEnumerator.cs`, `IAddressResolver.cs`, `IAddressResolverExtension.cs`, `IMasterServiceIdentityProvider.cs`, `PartitionAddressInformation.cs`, `PerProtocolPartitionAddressInformation.cs`, `ServiceIdentity.cs`

### `Direct/Routing/PartitionKeyComponents/`
`BoolPartitionKeyComponent.cs`, `InfinityPartitionKeyComponent.cs`, `IPartitionKeyComponent.cs`, `MaxNumberPartitionKeyComponent.cs`, `MaxStringPartitionKeyComponent.cs`, `MinNumberPartitionKeyComponent.cs`, `MinStringPartitionKeyComponent.cs`, `NullPartitionKeyComponent.cs`, `NumberPartitionKeyComponent.cs`, `PartitionKeyComponentType.cs`, `StringPartitionKeyComponent.cs`, `UndefinedPartitionKeyComponent.cs`

### `Direct/Serialization/`
`IndexJsonConverter.cs`, `ITypeResolver.cs`, `JSonSerializable.cs`, `JsonSerializableList.cs`, `SerializationFormattingPolicy.cs`, `SupportedSerializationFeatures.cs`, `SupportedSerializationFormats.cs`, `Undefined.cs`, `UnixDateTimeConverter.cs`

### `Direct/ServiceModel/`
`ApiType.cs`, `ConsistencyLevel.cs`, `ContentSerializationFormat.cs`, `DatabaseOrCollectionCreateMode.cs`, `DocumentServiceRequest.cs`, `DocumentServiceRequestContext.cs`, `DocumentServiceRequestExtensions.cs`, `DocumentServiceResponse.cs`, `EnumerationDirection.cs`, `FanoutOperationState.cs`, `IServiceConfigurationReader.cs`, `IServiceConfigurationReaderExtension.cs`, `IServiceConfigurationReaderVnext.cs`, `MigrateCollectionDirective.cs`, `OperationKind.cs`, `OperationType.cs`, `PriorityLevel.cs`, `QueryPlanGenerationMode.cs`, `QueryResult.cs`, `ReadConsistencyStrategy.cs`, `ReadFeedKeyType.cs`, `ReadMode.cs`, `RequestedCollectionType.cs`, `RequestHelper.cs`, `ResourceOperation.cs`, `ResourceType.cs`, `SDKSupportedCapabilities.cs`, `SDKSupportedCapabilitiesHelpers.cs`, `UserAgentContainer.cs`

### `Direct/ServiceModel/Client/`
`AccessCondition.cs`, `AccessConditionType.cs`, `DocumentResponse.cs`, `IDocumentResponse.cs`, `IResourceResponse.cs`, `IResourceResponseBase.cs`, `RequestOptions.cs`, `ResourceResponse.cs`, `ResourceResponseBase.cs`

*(`Protocol.cs` is also `Microsoft.Azure.Documents.Client` but belongs functionally in `Direct/Transport/` — see below. Keeping it in `Transport/` is the recommendation; it is the one deliberate namespace/folder divergence.)*

### `Direct/ServiceModel/Constants/`
`Constants.cs`, `HttpConstants.cs`, `RuntimeConstants.cs`, `StatusCodes.cs`, `WFConstants.cs`

### `Direct/Session/`
`ISessionContainer.cs`, `ISessionRetryOptions.cs`, `ISessionToken.cs`, `NullSessionContainer.cs`, `SessionTokenHelper.cs`, `SessionTokenMismatchRetryPolicy.cs`, `SimpleSessionToken.cs`, `VectorSessionToken.cs`

### `Direct/Store/`
`BarrierRequestHelper.cs`, `BarrierType.cs`, `ConsistencyReader.cs`, `ConsistencyWriter.cs`, `IStoreClient.cs`, `IStoreClientFactory.cs`, `IStoreModel.cs`, `IStoreModelExtension.cs`, `QuorumReader.cs`, `ReplicatedResourceClient.cs`, `RequestChargeTracker.cs`, `ServerStoreModel.cs`, `StoreClient.cs`, `StoreClientFactory.cs`, `StoreReader.cs`, `StoreResponse.cs`, `StoreResult.cs`

### `Direct/Telemetry/`  *(unchanged path)*
`CosmosDistributedContextPropagatorBase.cs`, `DefaultCosmosDistributedContextPropagator.cs`, `DistributedTracingOptions.cs`, `OpenTelemetryRecorder.cs`, `OpenTelemetryRecorderFactory.cs`

### `Direct/Transport/`
`ConnectionEvent.cs`, `ConnectionStateMuxListener.cs`, `IConnectionStateListener.cs`, `IOpenConnectionsHandler.cs`, `Protocol.cs`, `ReceivedResponseEventArgs.cs`, `SendingRequestEventArgs.cs`, `TransportAddressHealthState.cs`, `TransportAddressUri.cs`, `TransportClient.cs`, `TransportErrorCode.cs`, `TransportException.cs`, `TransportExceptionCounters.cs`, `TransportPerformanceCounters.cs`, `TransportRequestStats.cs`

### `Direct/Transport/Http/`
`HttpClientExtension.cs`, `HttpTransportClient.cs`, `HttpUtility.cs`

### `Direct/Transport/Rntbd/`
`BufferProvider.cs`, `BytesDeserializer.cs`, `BytesSerializer.cs`, `Channel.cs`, `ChannelCallArguments.cs`, `ChannelCommonArguments.cs`, `ChannelDictionary.cs`, `ChannelOpenArguments.cs`, `ChannelOpenTimeline.cs`, `ChannelProperties.cs`, `Connection.cs`, `ConnectionHealthChecker.cs`, `Dispatcher.cs`, `HeadersTransportSerialization.cs`\*, `HeadersTransportSerialization.tt`\*, `IChannel.cs`, `IChannelDictionary.cs`, `IConnection.cs`, `LbChannelState.cs`, `LoadBalancingChannel.cs`, `LoadBalancingPartition.cs`, `PortReuseMode.cs`, `ProxyRequest.cs`, `RntbdConstants.cs`, `RntbdOpenConnectionHandler.cs`, `RntbdStreamReader.cs`, `RntbdToken.cs`, `RntbdTokenStream.cs`, `RntbdTransportClient.cs`\*\*, `ServerKey.cs`, `ServerProperties.cs`, `TransportExceptions.cs`, `TransportSerialization.cs`, `UserPortPool.cs`

\* from `direct/rntbd2/`. \*\* **Rename required.** `direct/rntbd2/TransportClient.cs` collides with `direct/TransportClient.cs`. Rename the file only — the type stays `Microsoft.Azure.Documents.Rntbd.TransportClient`. Alternative if renaming is unacceptable: keep an extra `Direct/Transport/Rntbd2/` folder. **Recommendation: rename**, and update `msdata_sync.ps1`'s special-case block (it already special-cases this file).

### `Direct/Utilities/`
`BitUtils.cs`, `ConcurrentPrng.cs`, `HexStringUtility.cs`, `Helpers.cs`, `MathUtils.cs`, `MediaIdHelper.cs`, `NetUtil.cs`, `ReferenceCountedDisposable.cs`, `Rfc1123DateTimeCache.cs`, `StringSegment.cs`, `SystemSynchronizationScope.cs`, `TaskFactoryExtensions.cs`, `UInt128.cs`, `UriUtility.cs`, `ValueStopwatch.cs`, `VersionUtility.cs`

### `Direct/Utilities/IO/`
`CloneableStream.cs`, `MemoryStreamPool.cs`, `StreamExtension.cs`

### `Direct/Utilities/Timers/`
`PooledTimer.cs`, `TimeoutHelper.cs`, `TimerPool.cs`

### Moved out of `src/`
| From | To |
| --- | --- |
| `Microsoft.Azure.Cosmos/src/direct/msdata_sync.ps1` | `tools/msdata-direct/msdata_sync.ps1` |

### Stays in `src/` (not part of the `Direct/` tree)
| File | Note |
| --- | --- |
| `Microsoft.Azure.Cosmos/src/RMResources.Designer.cs` | msdata-sourced, copied by the sync script to `src/`, **absent from `main`** — bring across in phase 0 |
| `Microsoft.Azure.Cosmos/src/RMResources.resx` | as above |

---

## 7. Build & Project Changes

### D-0 — Which assembly do the Direct types ship from? (**resolved — option C**)

> **Resolved 2026-09-23: option C.** The `Microsoft.Azure.Documents.*` types keep shipping from `Microsoft.Azure.Cosmos.Direct.dll`, built from this repo instead of restored from nuget.org. The produced nupkg's contents are unchanged. Two prerequisites were discovered while resolving this and are recorded at the end of this section — **neither is optional, and neither is covered by §6's file mapping.**

> **Framing that was missing.** The v3 nupkg **already ships `Direct.dll`** — [csproj L167](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L167) packs it into `lib/netstandard2.0`. So `Microsoft.Azure.Cosmos` contains two assemblies today and every customer already receives `Microsoft.Azure.Documents.*`. Option C does not start shipping anything new; it keeps emitting the same DLL from a new source home. **Option A is the change** — it removes `Direct.dll` from the nupkg.

The highest-leverage decision in this document. D-1, D-4, §9.C and phases 3–5 all hang off it, so settle it before any code moves.

**The constraint is assembly type identity, not file location.** Today `Microsoft.Azure.Documents.StoreResponse` has exactly one identity: `[Microsoft.Azure.Cosmos.Direct, Version=3.44.1, PublicKeyToken=…]`. If those types are recompiled into `Microsoft.Azure.Cosmos.Client.dll` while any consumer still references the old `Direct.dll`, the runtime sees **two unrelated types with the same name**: `CS0433` at compile time, `TypeLoadException` at load, or — worst — a silent `InvalidCastException` deep in a retry path. Affected consumers are every project in §2.4 plus every assembly in the `AssemblyKeys.cs` `InternalsVisibleTo` graph.

| Option | Mechanism | Binary compat | Consequences |
| --- | --- | --- | --- |
| **A** — merge into `Client.dll` *(the assumption in earlier drafts)* | Compile `src/Direct/**` into `Microsoft.Azure.Cosmos.Client.dll`; stop shipping `Direct.dll` | ❌ Breaks every external consumer | Forces D-4, the §9.C-1 facade and C-4 binding redirects. Also merges the `Microsoft.Azure.Documents.*` public surface into the SDK assembly, which **conflicts with C-2** (`contracts/API_*.txt` must not change) unless every such type is `internal`. |
| **B** — type-forwarding facade | Ship a `Direct.dll` containing only `[assembly: TypeForwardedTo]` entries | ⚠️ Public types only; fragile with `InternalsVisibleTo` + strong names | A *mitigation* for option A, not an architecture in its own right. This is §9.C-1. |
| **C (recommended)** — same assembly, new source home | Add `Microsoft.Azure.Cosmos.Direct.csproj` **in this repo** producing the same `AssemblyName`, strong name and surface; `Client.csproj` takes a `ProjectReference` instead of a `PackageReference` | ✅ Zero change for any consumer | D-4, §9.C-1 and §9.C-4 become unnecessary. D-5 degrades to a `PackageReference` → `ProjectReference` swap. C-2 is satisfied by construction. |

**Decision: C.** The stated goal is "Direct code editable in the same PR as SDK code." Option C delivers exactly that while every consumer keeps binding to the identical assembly identity — nobody outside this repo changes anything. Option A buys no additional capability and costs a customer-visible break plus a facade to carry for two minor versions.

**Gating check — R-8, evidence now favourable.** Option C requires that delay-signing with [35MSSharedLib1024.snk](35MSSharedLib1024.snk) yields the same public key token as the shipped `Direct.dll`. The reference assembly is **3.44.1** (O-7):

```
Microsoft.Azure.Cosmos.Direct, Version=3.44.1.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35
```

`31bf3856ad364e35` is the standard Microsoft shared-library token, which is what `35MSSharedLib1024.snk` produces — so C is very likely available. **Still run R-8 formally** before phase 1; delay-signing has its own wrinkles, and §9.C-1's facade has the identical requirement, so the check is unavoidable under either option.

#### Prerequisite 1 — replicate the `InternalsVisibleTo` graph (R-17)

**The friend-grant list is not in the source being imported.** Verified:

- `AssemblyKeys` is declared `internal static class` in the Direct tree, and is **not defined anywhere on `main`** — it arrives from the package.
- [AssemblyInfo.cs](Microsoft.Azure.Cosmos/src/AssemblyInfo.cs) uses `AssemblyKeys.ProductPublicKey`, so `Client.dll` compiles against a type that is *internal to* `Direct.dll`. That only works because the shipped `Direct.dll` grants `InternalsVisibleTo` to the client assembly.
- Yet across all **371** `.cs` files on `origin/msdata/direct` there are **zero** `InternalsVisibleTo` occurrences.

The attributes therefore live in msdata's build, outside the imported file set — the same blind spot as `RMResources.*` (R-13). Extracting them from the reference **3.44.1** assembly yields **48 friend assemblies** (the same count as 3.44.0), and most are *not* v3 SDK projects:

| Group | Examples |
| --- | --- |
| v3 SDK + tests | `Microsoft.Azure.Cosmos.Client`, `.Tests`, `.EmulatorTests`, `.Performance.Tests`, `.NetFramework.Tests`, `.FaultInjection`(`.Tests`) |
| Sibling SDKs | `Azure.Cosmos`, `.Table`(`.Tests`), `.Encryption`, `.Encryption.KeyVault`, `.Friends`(`.Tests`) |
| Service-side | `Microsoft.Azure.CosmosDB.Runtime`, `.Compute.*`, `.Sql.Service`, `.Mongo.Service*`, `.Cassandra.*`, `.Gremlin.Service.Tests.*`, `.DataTransfer.*`, `.Analytics.Core`, `.CosmosFabric`, `.Portal.Services.Backend`, `.ServiceFramework.HostConfiguration` |
| Other | `Microsoft.Azure.Documents.Management.Azure`, `Microsoft.Azure.Documents.Services.Runners.DocDBRunner`, `DynamicProxyGenAssembly2` |

**Action:** obtain the authoritative list from msdata (canonical) and cross-check against the shipped DLL. **Replicate it verbatim** on the new `Microsoft.Azure.Cosmos.Direct.csproj` — the grants cost nothing and guarantee parity. Do *not* prune the service-side entries on the assumption they consume msdata's Direct rather than the published one; that assumption is unverified, and getting it wrong breaks another team silently.

#### Prerequisite 2 — pin `AssemblyVersion` (R-18)

The reference assembly is `Version=3.44.1.0` (O-7, verified). This repo stamps from `ClientOfficialVersion` ([Directory.Build.props L3](Directory.Build.props#L3)), currently `3.63.0`. **Assembly version is part of assembly identity**, so a source-built `Direct.dll` carrying `3.63.0.0` changes the identity anyway and .NET Framework consumers then need binding redirects — defeating much of why C was chosen.

Set `<AssemblyVersion>3.44.1.0</AssemblyVersion>` explicitly on the new project and let `FileVersion` track the SDK. Trivial to do, easy to miss, and expensive to discover after release.

#### Also verify

- **Dependency leakage.** D-1 asserts that putting the three extra `PackageReference`s on `Direct.csproj` keeps them out of the SDK's transitive graph. That is **not automatic** with a `ProjectReference` plus a manually packed `Direct.dll`. Make the `PrivateAssets` intent explicit and diff the produced `.nuspec` against the previous release.
- **Signing.** The official pipeline now signs two assemblies. Confirm the signing step enumerates both.

### D-1 — Delete the `Microsoft.Azure.Cosmos.Direct` package reference

`main` carries a full compile reference:

```xml
<PackageReference Include="Microsoft.Azure.Cosmos.Direct" Version="[$(DirectVersion)]" PrivateAssets="All" />
```

`msdata/direct` already narrows it to build assets only — this single line is that branch's *entire* native story, and it is what D-3 replaces:

```xml
<PackageReference Include="Microsoft.Azure.Cosmos.Direct" Version="[$(DirectVersion)]" PrivateAssets="All">
  <ExcludeAssets>compile</ExcludeAssets>
</PackageReference>
```

With the sources compiled in-tree the `compile` exclusion is already a no-op, and with the natives committed (D-3) the `build` assets are no longer needed either. **Delete the reference outright** — nothing remains that the package supplies.

Under **D-0 option C** this becomes a `PackageReference` → `ProjectReference` swap to the new `Microsoft.Azure.Cosmos.Direct.csproj` rather than a deletion. Either way the package reference is gone once D-3's feed-restored natives land — there is no restore-only remnant, because the natives now come from the `ServiceInterop` package instead.

**Three package references must be added** for the Direct sources to compile. `msdata/direct` already carries them and they are absent from `main`:

```xml
<PackageReference Include="System.Diagnostics.PerformanceCounter" Version="6.0.0" />
<PackageReference Include="System.Net.Http" Version="4.3.4" />
<PackageReference Include="System.Text.RegularExpressions" Version="4.3.1" />
```

Under D-0 option C they belong on `Microsoft.Azure.Cosmos.Direct.csproj`, not on the SDK project — which also keeps them out of the SDK's transitive dependency graph. Under option A they land on `Microsoft.Azure.Cosmos.csproj` and become new transitive dependencies of the shipped package, which is a customer-visible change needing a changelog entry.

### D-2 — Retire the `ProjectRef` dual-mode switch (**revisit — see O-6**)

`ProjectRef != 'True'` currently gates the Direct/HybridRow/SourceLink block, packing and signing. Once Direct is source-compiled there is only one mode; keep the gate for SourceLink/signing/packing, but drop the Direct-specific conditionals so the two build shapes converge.

> ⚠️ **D-0 option C undercuts this.** `ProjectRef=True` is how msdata suppresses v3's Direct dependency so it can supply its own from its own tree. Under option C, `Client.csproj` gains a `ProjectReference` to the in-repo `Microsoft.Azure.Cosmos.Direct.csproj` — and if that reference is unconditional, msdata's build compiles **two** copies of `Microsoft.Azure.Documents.*`, which is P-11 in a new place. So the switch becomes **more** load-bearing, not less, and “collapse it” is the wrong instruction. Tracked as **O-6**.

### D-3 — Native query-plan assets (**resolved — public `QueryPlanInterop` package**)

> **Revised again 2026-09-23. Supersedes both the “commit the binaries” (09-22) and “internal `CosmosDB-Internal` feed” (09-23) resolutions.** [`Microsoft.Azure.Cosmos.QueryPlanInterop.Windows`](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.QueryPlanInterop.Windows) is published on **public nuget.org**, owned by `azure-sdk`/`Microsoft`, with its `projectUrl` pointing at **this repository**. It is the query-plan native, repackaged and self-contained. **Take a normal `PackageReference`. No internal feed, no pipeline acquisition step, no committed binaries.**

**Measured against the shipped 3.44.0 native** (v1.0.2, the only version at time of writing):

| | Direct 3.44.0 natives | QueryPlanInterop 1.0.2 |
| --- | --- | --- |
| Feed | discontinued package / internal feed | **public nuget.org** |
| Files | 5 | **1** (`Cosmos.QueryPlanInterop.dll`) |
| Payload | 9.25 MB | 7.40 MB |
| Imports | `MSVCP140`, `VCRUNTIME140`, `VCRUNTIME140_1`, `Cosmos.CRTCompat`, `api-ms-win-crt-*`, `ole32` | **OS only** — `kernel32`, `ntdll`, `bcrypt`, `ADVAPI32`, `psapi`, `RPCRT4` |
| Exports | 6 externs **+** JNI bindings **+** `Crc32/64ComputeInterop` | exactly the **6** externs the wrapper declares |

The import table is the decisive evidence: QueryPlanInterop is **statically linked against the CRT**, so `msvcp140` / `vcruntime140` / `vcruntime140_1` / `Cosmos.CRTCompat` are no longer needed at all. That closes the standing open item about sourcing the VC++ redistributables — they are not required, not merely relocatable.

Both binaries carry the identical C++ implementation types (`ServiceInteropServiceProvider`, `DocDbObject<…>`, `TraceProviderManager`), and QueryPlanInterop exports all six externs declared in `ServiceInteropWrapper.cs`: `CreateServiceProvider`, `UpdateServiceProvider`, `GetPartitionKeyRangesFromQuery`, `…2`, `…3`, `…4`. Nothing in the v3 tree or the Direct tree references `Crc32ComputeInterop` / `Crc64ComputeInterop`, so the dropped exports cost nothing.

#### The one mismatch — filename

`ServiceInteropWrapper.cs` hard-codes the library name and declares **no** `EntryPoint` values, so the method names *are* the entry points:

```csharp
#if COSMOSCLIENT
    [DllImport("Microsoft.Azure.Cosmos.ServiceInterop.dll", …)]
#else
    [DllImport("Microsoft.Azure.Documents.ServiceInterop.dll", …)]
#endif
```

**Rename on copy — do not edit the wrapper.** `ContentWithTargetPath` supports a `TargetPath` rename, so `Cosmos.QueryPlanInterop.dll` lands as `Microsoft.Azure.Cosmos.ServiceInterop.dll`. This means **zero source changes**: the wrapper stays byte-identical to msdata (Axis A), F-4's `#if COSMOSCLIENT` guards stay load-bearing and untouched, and no T0 file is disturbed.

Editing the `DllImport` names instead would diverge a shared T0 file from msdata for a packaging concern. Rejected.

#### Acquisition mechanism

A plain `PackageReference` — the internal-feed machinery is gone:

```xml
<PackageReference Include="Microsoft.Azure.Cosmos.QueryPlanInterop.Windows"
                  Version="[$(QueryPlanInteropVersion)]"
                  PrivateAssets="All"
                  ExcludeAssets="all"
                  GeneratePathProperty="true" />

<ItemGroup Condition="'$(RuntimeIdentifier)' == '' OR $(RuntimeIdentifier.StartsWith('win'))">
  <ContentWithTargetPath Include="$(PkgMicrosoft_Azure_Cosmos_QueryPlanInterop_Windows)\runtimes\win-x64\native\Cosmos.QueryPlanInterop.dll">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <TargetPath>Microsoft.Azure.Cosmos.ServiceInterop.dll</TargetPath>
  </ContentWithTargetPath>
</ItemGroup>
```

`ExcludeAssets="all"` keeps the package out of the compile and dependency graphs; `GeneratePathProperty` gives the restored path. `PrivateAssets="All"` keeps it off the SDK's published dependency list — customers still receive the native inside the `Microsoft.Azure.Cosmos` nupkg via the existing [pack block](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L155-L159), which needs only to be reduced from five entries to one.

#### Required conditions

1. **Pin the version.** `$(QueryPlanInteropVersion)` in [Directory.Build.props](Directory.Build.props), replacing `$(DirectVersion)` as the native coupling coordinate (§9.F F-2).
2. **Rename on copy** to `Microsoft.Azure.Cosmos.ServiceInterop.dll`, as above. A mis-set `TargetPath` fails silently — see condition 3.
3. **Pack-time assertion**, RID-scoped to match [Microsoft.Azure.Cosmos.targets L28](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.targets#L28): `runtimes/win-x64/native/Microsoft.Azure.Cosmos.ServiceInterop.dll` is present in the produced nupkg. This is C-7 and R-11.
4. **Reduce the pack block and `Microsoft.Azure.Cosmos.targets` from five natives to one.** Both currently enumerate `Cosmos.CRTCompat`, `msvcp140`, `vcruntime140`, `vcruntime140_1`; all four become dead entries. **This is customer-observable** — the nupkg stops shipping the VC++ redistributables — so it needs a changelog entry and an explicit check that no consumer scenario depended on the SDK supplying them.
5. **Functional equivalence gate.** 7.40 MB vs 8.47 MB is a different build, not just a different wrapper. Phase 2 must run the full query suite (unit + emulator, including the ODE, hybrid-search and vector paths) against it with `AssembliesExist == true` before the exit criteria are met.
6. **Confirm provenance** — which ServiceInterop build does 1.0.2 correspond to? The reference native (Direct **3.44.1**) is 9.14 MB against QueryPlanInterop's 7.40 MB, and the two artifacts have independent version schemes and cadences (O-5: ~2 months), so this cannot be inferred. Needed for F-2 to mean anything.

#### What this avoids

Versus the internal-feed plan: no `NuGet.config`, no `NuGetAuthenticate`, no `nuget install` step, no `$(InteropNativePath)` plumbing, no graceful-degradation path, no test gating on `AssembliesExist`, and no `CONTRIBUTING.md` split-experience note. **R-16 closes** — external contributors restore the native like any other package.

Versus committing: no 9.25 MB in git, no Git LFS retrofit (R-14 stays closed), no `native-assets.json`, no SHA/Authenticode gates.

**Verified origin.** The natives ship inside the `Microsoft.Azure.Cosmos.Direct` nupkg. Contents of the restored 3.44.0 package:

```
build\netstandard2.0\Microsoft.Azure.Cosmos.Direct.targets
lib\netstandard2.0\Microsoft.Azure.Cosmos.Direct.dll
runtimes\win-x64\native\Microsoft.Azure.Cosmos.ServiceInterop.dll
runtimes\win-x64\native\Cosmos.CRTCompat.dll
runtimes\win-x64\native\msvcp140.dll
runtimes\win-x64\native\vcruntime140.dll
runtimes\win-x64\native\vcruntime140_1.dll
```

They reach the SDK nupkg in four hops, none of which v3 owns **today**:

1. msdata builds them and publishes them in the Direct nupkg.
2. That package's **own** `build/netstandard2.0/Microsoft.Azure.Cosmos.Direct.targets` declares `ContentWithTargetPath` items with `CopyToOutputDirectory=PreserveNewest`, landing the 5 DLLs in `$(OutputPath)`.
3. [Microsoft.Azure.Cosmos.csproj](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L155-L159) re-packs them *from* `$(OutputPath)` into `runtimes/win-x64/native`.
4. [Microsoft.Azure.Cosmos.targets](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.targets#L29-L54) repeats the copy in the customer's build, adding a RID guard the Direct-side targets lacks.

**Hops 1 and 2 are what this decision replaces.** Hop 2 happens only because the Direct package's `build` assets are imported — which is why `compile` and `all` were never interchangeable, and why `msdata/direct` uses `compile`:

| Setting | `lib/` compile ref | `build/` targets | Natives reach `$(OutputPath)`? |
| --- | --- | --- | --- |
| `ExcludeAssets="compile"` | dropped | **imported** | ✅ yes — what `msdata/direct` uses today |
| `ExcludeAssets="all"` | dropped | **not imported** | ❌ **no** — needs explicit copy logic |

`PrivateAssets="All"` only blocks *transitive* flow to downstream consumers; it does not suppress the targets import for this project.

Once the binaries are committed, **hops 1 and 2 disappear** and are replaced by a local `ContentWithTargetPath` block in `Microsoft.Azure.Cosmos.csproj`. **Hops 3 and 4 are unchanged** — the pack block and the customer-side targets already read from `runtimes/win-x64/native` and need no edit.

#### Why only one build consumes this

`ProjectRef=True` drops *both* the Direct `PackageReference` ([csproj L120](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L120)) **and** the entire native pack/copy `ItemGroup` ([csproj L154](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L154)). msdata reads the shared files from their original location and supplies its own natives, so it needs nothing from this decision — **the public v3 build is the sole consumer.** The shipped nupkg stays self-contained: [csproj L155-159](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L155-L159) re-packs from `$(OutputPath)` into `runtimes/win-x64/native`.

#### Evidence retained from the committed-binaries analysis

Still relevant to O-5 and R-15: the native churned on every Direct release, and carries no usable version of its own.

| Direct | Bytes | PE build stamp | SHA-256 (first 12) |
| --- | ---: | --- | --- |
| 3.39.1 | 10,007,608 | 2025-05-30 | `6AC914A72A29` |
| 3.41.2 | 10,969,120 | 2025-11-03 | `B6201DA81DBA` |
| 3.41.3 | 10,968,648 | 2025-11-12 | `C468C2BE9B10` |
| 3.42.0 | 8,767,560 | 2026-02-06 | `338DD8D7C244` |
| 3.42.2 | 8,767,560 | 2026-02-28 | `909F6328C991` |
| 3.42.4 | 8,767,520 | 2026-04-08 | `EA373CA16E59` |
| 3.43.0 | 8,925,496 | 2026-05-02 | `93C45111B349` |
| 3.43.1 | 8,925,504 | 2026-05-09 | `6A8F38A57CF7` |
| 3.43.2 | 9,011,000 | 2026-05-21 | `44F7A8F9D154` |
| 3.44.0 | 8,882,024 | 2026-06-22 | `7B7260C4B53A` |

Ten releases, ten distinct binaries, sizes spanning 2.2 MB — and `FileVersion` frozen at `2.14.0.0` across every one of them. The native therefore carries **no usable version coordinate of its own**, which is why `$(DirectVersion)` was the only pin available, and why `$(QueryPlanInteropVersion)` must now inherit that role (§9.F F-2).

All five binaries are individually Authenticode-signed by Microsoft, and NuGet package signing on the feed carries that provenance end to end — which is what makes C-7 satisfiable without a checked-in hash manifest.

**Open item:** can `msvcp140.dll` / `vcruntime140.dll` / `vcruntime140_1.dll` be sourced from the VC++ redistributable or `Microsoft.VCRTForwarders.NETCore` rather than from the ServiceInterop package at all? They are stock Microsoft redistributables. They ship in the package precisely because customers may not have the redist installed, so this may not be movable — confirm before assuming it is.

**Failure mode if this is got wrong is silent:** the nupkg simply ships without natives, `ServiceInteropWrapper.AssembliesExist` goes false, and query planning falls back to the gateway path at runtime with no build error. That is why condition 3 is an assertion rather than a test.

`<DirectVersion>` is removed from [Directory.Build.props](Directory.Build.props#L6) in phase 5, once D-0 has also landed; `<QueryPlanInteropVersion>` takes its place as the native pin.

#### What O-5 still decides

Discontinuing **Direct-the-managed-assembly** is not the same as discontinuing **ServiceInterop-the-native-query-engine** — and the existence of a separately published ServiceInterop package is itself evidence the native is still a live artifact. The query-plan ABI is demonstrably still growing: `GetPartitionKeyRangesFromQuery4` takes a `vectorEmbeddingPolicy`, and `PartitionKeyRangesApiOptions` carries `bHybridSearchSkipOrderByRewrite`, `eGeospatialType` and `bUseSystemPrefix`, all added for vector, hybrid and geospatial search. The struct's reserved padding exists so it can keep growing.

O-5 is now **narrower**. It is no longer "is there a delivery vehicle at all" — there is — but:

- **What is the publishing cadence and ownership** of the ServiceInterop package now that Direct no longer gates it?
- **Should it also be published to nuget.org**, so external contributors and public source builds reach parity? That is the only remaining gap in D-3.

Neither question blocks phases 1–3.

#### Superseded options

| Option | Status |
| --- | --- |
| **A** — build-only reference to a natives-only package | **Adopted**, as a plain `PackageReference` to the **public** `Microsoft.Azure.Cosmos.QueryPlanInterop.Windows`. Rejected on 2026-09-22 only because the package was believed not to exist; an intermediate 09-23 draft routed it through the internal `CosmosDB-Internal` feed before the public package was found. |
| **B** — keep `Microsoft.Azure.Cosmos.Direct` as an `ExcludeAssets="compile"` reference purely to source the natives | What `msdata/direct` implements today. Restores fine even against an unlisted package, but pins this repo to a discontinued dependency indefinitely and will trip Component Governance once the package is formally deprecated. Still rejected. |
| **C** — commit the binaries | **Superseded 2026-09-23.** Remains the fallback if the ServiceInterop feed turns out to be unusable from the official pipeline. Costs 9.25 MB plus a Git LFS retrofit that cannot be deferred (R-14). |

### D-4 — Stop packing `Microsoft.Azure.Cosmos.Direct.dll`

> **Applies only under D-0 option A.** Under option C the assembly keeps shipping — built from this repo instead of restored from the feed — and this decision, along with §9.C-1 and §9.C-4, does not arise.

Remove line 167 of the csproj. **This is a binary-breaking change for any consumer that references `Microsoft.Azure.Documents.*` types out of `Microsoft.Azure.Cosmos.Direct.dll`.** Mitigation: ship a type-forwarding facade `Microsoft.Azure.Cosmos.Direct.dll` (assembly full of `[assembly: TypeForwardedTo(...)]`) for at least two minor versions. Decide before the first release that contains this change.

Either way, the produced nupkg must never contain `Direct.dll` *and* the same types compiled into `Client.dll` — that is P-11, the defect that makes `msdata/direct` unshippable today. Assert it at pack time (§11).

### D-5 — Downstream projects

`FaultInjection`, `CosmosCTL`, `CosmosBenchmark` and the four test projects reference the Direct package for `Microsoft.Azure.Documents.*` types. Once those types live in `Microsoft.Azure.Cosmos.Client.dll`, each of those `PackageReference` lines must be deleted and replaced by the existing `ProjectReference`/`PackageReference` to the SDK. `Microsoft.Azure.Cosmos.Tests.csproj` line 76 additionally reads the Direct `.nuspec` — that test needs rewriting or deleting.

### D-6 — Analyzer scoping

Move these from `<NoWarn>` in `Microsoft.Azure.Cosmos.csproj` into a new `Microsoft.Azure.Cosmos/src/Direct/.editorconfig`:

```
CS1572 CS1573 CS1574 CS1587 SA1000 SA1001 SA1002 SA1003 SA1008 SA1025 SA1027
SA1108 SA1122 SA1127 SA1137 SA1203 SA1310 SA1500 SA1505 SA1507 SA1518 SA1616
SA1627 SA1649 VSTHRD200
```

Keep `NU5125` in the csproj (it is a packaging warning, not source).

### D-7 — Sync tooling

> **Direction inverts (O-1).** With v3 `main` as upstream, the steady state is an **export** from `src/Direct/` to msdata, not an import. The rewrite below still has to be built — phases 1–4 need inbound sync to absorb msdata's current state — but it is transitional, and phase 5 replaces it with the export path. Design the manifest and provenance report to work in both directions so the flip is a configuration change rather than a second tool.

`tools/msdata-direct/msdata_sync.ps1` must learn the new destination layout. Add a `$fileToFolderMap` table generated from §6 so a copied file lands in the right folder instead of the flat root. Files new on the msdata side that are not in the map should land in a `Direct/_Unsorted/` staging folder and fail the build loudly, forcing an explicit classification.

The rewrite must also fix P-8/P-9/P-10, which requires inverting the script's current logic:

1. **Walk the 13 msdata source directories recursively** and discover every file, rather than iterating what v3 already has (fixes P-8).
2. Look each file up in `$fileToFolderMap` **by file name only**, never by msdata source path, so the v3 taxonomy stays decoupled from msdata's (P2).
3. Copy into the mapped folder including nested destinations, so `Compat/AzureCore/`, `Telemetry/` and `FaultInjection/` are covered like everything else (fixes P-9).
4. Emit a provenance report — msdata source dir → target folder, per file (fixes P-10). This is also the input to the §9.D drift manifest.
5. Map `SharedFiles/Rntbd2/TransportClient.cs` onto `RntbdTransportClient.cs`, replacing today's hard-coded special case.

A machine-readable projection of §6 is generated by [scratch/Generate-DirectLayout.ps1](scratch/Generate-DirectLayout.ps1) into [scratch/direct-layout.json](scratch/direct-layout.json) (31 folders, 372 files, reconciled against `origin/msdata/direct`). Both move under `tools/msdata-direct/` when this lands. **§6 stays the source of truth** — the JSON is regenerated from it, never hand-edited, so the two cannot drift.

Also update [docs/sync_up_msdata_direct.md](docs/sync_up_msdata_direct.md), [.github/agents/msdata-direct-sync-agent.agent.md](.github/agents/msdata-direct-sync-agent.agent.md) and [tools/msdata-direct-sync-helper.ps1](tools/msdata-direct-sync-helper.ps1).

### D-8 — Solution & ownership

- Add `Direct` as a solution folder so Solution Explorer shows the new tree.
- Add a `CODEOWNERS` entry for `Microsoft.Azure.Cosmos/src/Direct/**`.
- Add `Microsoft.Azure.Cosmos/src/Direct/README.md` carrying the §9.A house rules: v3 `main` is upstream (O-1), so these files **are** editable here — but T0 files need msdata sign-off, divergence goes through A-1/A-2/A-3 rather than ad-hoc edits, and the `#if !COSMOSCLIENT` arms are real code that v3 CI does not compile (A-6, R-7).

### D-9 — Retire the msdata-specific pipelines

The `msdata/direct` branch has its own CI surface. Once the source lives on `main`, four files become redundant or need folding into the main pipelines:

| File | Disposition |
| --- | --- |
| [azure-pipelines-msdata-direct.yml](azure-pipelines-msdata-direct.yml) | Delete — it triggers on `msdata/direct*` branches only. |
| [templates/build-test-msdata.yml](templates/build-test-msdata.yml) | Fold into `build-test.yml`, or delete if identical once the branch is gone. |
| [templates/build-preview-msdata.yml](templates/build-preview-msdata.yml) | As above versus `build-preview.yml`. Diff them first — any msdata-only step is a coverage gap on `main` today. |
| [azure-pipelines-msdata-aot.yml](azure-pipelines-msdata-aot.yml) | **Fold into the main pipelines, do not delete.** AOT/trimming behaviour changes when Direct is compiled from source instead of consumed as a pre-built assembly, so this coverage becomes *more* relevant after the migration, not less. |

Also add the phase-2 opt-in leg described in §8 (`-p:UseDirectSource=true`), and wire §9.E Gate 1 into the PR build.

---

## 8. Migration Phases

Five delivery phases. The decision gate at the head is **closed** — D-0, D-3 and O-1 are all resolved — but phase 0 still holds inputs and one procedure that gate later phases. Each phase is one or more PRs, and every PR must be green in CI on its own (**C-6**).

| Phase | Theme | Gate to exit |
| --- | --- | --- |
| **0** | Decision gate — no code | Three decisions signed off |
| **1** | Files in, folder structure right | Default build byte-identical; opt-in leg green |
| **2** | ServiceInterop / native assets | Native restored from the public `QueryPlanInterop` package; no Direct package involved |
| **3** | Compile from source, drop the managed dependency | `main` builds Direct from source |
| **4** | Backward-compatibility gates | All gates wired and green |
| **5** | Decommission and ship | GA on the source-built path |

---

### Phase 0 — Decision gate (**no code**)

The three original gate decisions are resolved. Resolving them raised two more, which still re-shape §7/§8/§9 and gate specific phases.

**Resolved:**

- [x] ~~**D-0** — which assembly do the Direct types ship from?~~ **Option C** — keep shipping `Direct.dll`, built from this repo. Two prerequisites moved into phase 1a (R-17 friend graph, R-18 version pin). **R-8 must still be run formally**, though the evidence is favourable.
- [x] ~~**D-3** — where do the native assets come from?~~ **The public `Microsoft.Azure.Cosmos.QueryPlanInterop.Windows` package on nuget.org** — one self-contained DLL replacing all five natives.
- [x] ~~**O-1** — is `main` or msdata upstream?~~ **v3 `main` is upstream, msdata is downstream.** Sets the sync direction (D-7), `CODEOWNERS`, the §9.A-4 T0 policy, and makes §9.E Gate 2 authoritative.

- [x] ~~**O-7** — which Direct version is the reference point?~~ **Direct `3.44.1`.** Verified: `Version=3.44.1.0`, `PublicKeyToken=31bf3856ad364e35`, 48 `InternalsVisibleTo` grants. R-1's re-baseline must land the source at 3.44.1 equivalence.

**Still open:**

- [ ] **O-6 — `ProjectRef=True` under option C.** msdata must not compile two copies of `Microsoft.Azure.Documents.*`. **Blocks phase 3.**
- [ ] **O-5 — `QueryPlanInterop` provenance.** Cadence is answered (~2 months). Which ServiceInterop build does 1.0.2 correspond to? F-2's pin needs that mapping. **Blocks phase 5**; also D-3 condition 6.

> **Also outstanding, but inputs rather than decisions:** R-8's formal run and the R-17 friend-assembly list (phase 1a), and the R-1 re-baseline procedure (phase 1a).

> **Gate status.** The three *original* gate decisions — D-0 (option C), D-3 (feed restore) and O-1 (v3-primary) — are all resolved. **Two decisions were raised by those resolutions and remain open:** O-7 (which Direct version is the reference point — blocks phase 1) and O-6 (`ProjectRef` behaviour under option C — blocks phase 3). O-5 blocks phase 5 only. The remaining phase-0 work is inputs and the R-1 procedure.

---

### Phase 1 — Land the files and the folder structure

Delivered as **two separate PRs**. Mixing a 373-file re-baseline with a 373-file move makes the diff unreviewable and destroys `git log --follow`.

**1a — Import, as-is.** Bring the Direct source onto `main` in the flat `src/direct/` shape, plus the csproj / `Directory.Build.props` / `.sln` deltas and the two `RMResources.*` files that land in `src/` (R-13). Land it behind the `UseDirectSource` switch (below) so the default build stays byte-identical. **The procedure is R-1, below — read it before starting.**

#### R-1 — the re-baseline procedure (phase 1a)

**Measured topology**, not estimates:

| Fact | Value |
| --- | --- |
| `merge-base(origin/main, origin/msdata/direct)` | `f5effced7` — **2022-10-26** |
| `origin/msdata/direct` ahead of that base | 61 commits |
| `origin/msdata/direct` behind `origin/main` | **1072 commits** |
| `<DirectVersion>` on `main` | 3.44.0 |
| `<DirectVersion>` on `msdata/direct` | 3.43.2 |
| **Target (O-7)** | **3.44.1** |
| Remotes configured in this clone | **`origin` only** (GitHub) |

**Decision: re-import, do not merge.** A three-year-old merge-base against 1072 commits of unrelated `main` movement produces conflicts in files that have nothing to do with Direct. More fundamentally, `src/direct/**` is a *mirror* — there is no v3-side authorship to preserve, so merging optimises for retaining edits that should not exist. The 61 commits on `msdata/direct` are the historical import record; keep the branch for diffing and retire it in phase 5, but do not merge it.

**Step 0 — reach the source.** The authoritative branch is msdata `sdkReleases/direct/EN20260409-3.44.1`. It lives in the **msdata ADO repo**, which this clone cannot reach — `origin` is the only remote. Either add an msdata remote with read access, or have an msdata-side owner produce a verbatim export of the mapped file set. **Record which was used, and the exact source SHA.**

**Step 1 — snapshot the target surface first.** Capture `contracts/DirectSDKAPI.json` from *published* `Microsoft.Azure.Cosmos.Direct` **3.44.1** before importing anything. This is the acceptance criterion for step 5 and must predate any source edit, or it records post-migration state and proves nothing.

**Step 2 — materialise the file map.** The import set is §6's 372 files plus `RMResources.Designer.cs` / `RMResources.resx` (R-13). Build it by walking the 13 msdata source directories **recursively** (the D-7 fix for P-8/P-9), never by iterating what v3 already has. A file present on the msdata branch and absent from the map is a **hard stop**, not a warning.

**Step 3 — import onto a branch cut from current `main`.** `users/<alias>/direct-import-3.44.1`, branched from `origin/main` at a recorded SHA. Copy the mapped files verbatim into `src/direct/` in today's flat shape. One commit, no edits, msdata branch + SHA in the commit message.

**Step 4 — reconcile build inputs.** Bring across the csproj / `Directory.Build.props` / `.sln` deltas and set `<DirectVersion>3.44.1</DirectVersion>`. Keep everything behind `UseDirectSource` so the default build stays byte-identical.

**Step 5 — prove equivalence. This is the acceptance gate.** Build the imported source and assert against published 3.44.1:

- Public surface matches `contracts/DirectSDKAPI.json` from step 1 (§9.E Gate 1).
- `Version=3.44.1.0`, `PublicKeyToken=31bf3856ad364e35` (R-8, R-18).
- All **48** `InternalsVisibleTo` grants present (R-17).
- `-p:UseDirectSource=true` — full unit + emulator suite green.

> A failure here means **the import is wrong**. Do not hand-edit an imported file to make the gate pass — fix the file map (step 2) or the source branch (step 0). Patching the source to satisfy the gate silently creates the exact drift §9 exists to prevent, on day one.

**Step 6 — record provenance.** Seed `src/Direct/.sync/manifest.json` at import time with the msdata branch, SHA, and per-file source path + hash. This is the A-5 manifest's initial state and the input to the §9.D drift gate.

**Do not:**

- `git merge origin/msdata/direct` — see the topology table.
- Import from `msdata/direct`, `msdata/direct_local` (350 files) or `msdata/direct_410_1022` (363 files). All are stale; the release branch is the only source. This clone carries four msdata-ish refs and picking the wrong one is silent.
- Read C-3 (`git mv` only) as applying here. It governs the **1b restructure**, where `git log --follow` matters. The 1a files have no v3 history to preserve — theirs lives in msdata, and on `msdata/direct` until phase 5 retires it.

**1b — Restructure.** Pure `git mv` into the §5/§6 tree. Zero content edits except the single `RntbdTransportClient.cs` rename. Add `Direct/README.md`, `Direct/.editorconfig` carrying the scoped analyzer suppressions (**D-6**), and the `CODEOWNERS` entry.

Also in this phase, because both must precede any edit to the Direct sources:

- [ ] **Replicate the `InternalsVisibleTo` graph** onto the new `Microsoft.Azure.Cosmos.Direct.csproj` — 48 grants, authoritative copy from msdata, cross-checked against the reference **3.44.1** assembly (D-0 prerequisite 1, R-17). Not in §6's mapping; easy to miss.
- [ ] **Pin `<AssemblyVersion>3.44.1.0</AssemblyVersion>`** on that project, letting `FileVersion` track the SDK (D-0 prerequisite 2, R-18).
- [ ] **Capture the Direct contract baseline** — snapshot the **3.44.1** API surface into `contracts/DirectSDKAPI.json`. Enforcement is phase 4, but the **capture has to happen before the source is touched**, otherwise the baseline records post-migration state and proves nothing.
- [ ] **Minimal drift gate** — T0 hashes for the highest-risk files only (`ServiceInteropWrapper.cs` plus the wire-format files in §9.B). The full §9.A tiering is phase 4; this subset closes the window described below.
- [ ] Opt-in CI leg: `-p:UseDirectSource=true`, full unit + emulator suite.

> ⚠️ **Do not defer the drift gate to phase 4.** Phase 3 makes `main` compile these sources; from that moment any PR can diverge a shared file with nothing to stop it. The minimal T0 gate above is cheap and closes the phase 3 → 4 window.

**Exit:** default build produces byte-identical artifacts; `-p:UseDirectSource=true` is green; `git diff --stat` for 1b shows **only** renames (`R100`); `contracts/API_*.txt` unchanged.

---

### Phase 2 — ServiceInterop and the native assets

Deliberately **before** phase 3: prove the natives still flow from their new home while the managed reference is neutralised but not yet removed. Doing it the other way round means debugging two changes at once, and the failure mode is silent (R-11).

- [ ] **Confirm provenance and cadence** of `Microsoft.Azure.Cosmos.QueryPlanInterop.Windows` with the query-engine owner: which ServiceInterop build does 1.0.2 correspond to? Cadence is settled at ~2 months (O-5); the mapping is not. (D-3 condition 6.)
- [ ] **Add `<QueryPlanInteropVersion>`** to [Directory.Build.props](Directory.Build.props) as the pinned native coordinate, replacing `$(DirectVersion)`.
- [ ] **Add the `PackageReference`** with `PrivateAssets="All" ExcludeAssets="all" GeneratePathProperty="true"` — a normal restore, no `NuGet.config`, no feed auth.
- [ ] **Replace hop 2** — `ContentWithTargetPath` from `$(Pkg…)` into `$(OutputPath)`, **renaming** `Cosmos.QueryPlanInterop.dll` → `Microsoft.Azure.Cosmos.ServiceInterop.dll` via `TargetPath`. No source edits.
- [ ] **Reduce five natives to one** in the pack block ([csproj L155-159](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L155-L159)) and in [Microsoft.Azure.Cosmos.targets](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.targets#L29). **Customer-observable** — the nupkg stops shipping the VC++ redistributables. Changelog entry required, plus a check that no consumer relied on the SDK supplying them.
- [ ] **Pack-time assertion** that `runtimes/win-x64/native/Microsoft.Azure.Cosmos.ServiceInterop.dll` is present — an assertion, not a test (R-11) — **scoped to Windows RIDs**. A mis-set `TargetPath` fails silently; this is the only thing that catches it.
- [ ] **Functional equivalence gate** — full query suite (unit + emulator, including ODE, hybrid-search and vector paths) green against the new native with `AssembliesExist == true`. 7.40 MB vs 8.47 MB is a different build (D-3 condition 5).
- [ ] **§9.F axis-D guards:** tier `ServiceInteropWrapper.cs` as T0, pin it to `$(QueryPlanInteropVersion)` (F-2), assert `COSMOSCLIENT` is defined (F-4), and extend the interop smoke test to exercise each of the six P/Invoke entry points.

> No internal feed, no committed binaries, no Git LFS, and no test gating — the package is public, so every build and every contributor gets the native identically.

> The existing `AssembliesExist` check protects against the native DLL being **absent**, not against it being **present but incompatible**. Phase 3 makes the managed half editable here while the native half moves on its own release cadence, so incompatibility becomes reachable for the first time. That is what F-1/F-2/F-3 exist to catch.

**Exit:** produced nupkg carries `Microsoft.Azure.Cosmos.ServiceInterop.dll` under `runtimes/win-x64/native`, sourced from the public QueryPlanInterop package, with no `Microsoft.Azure.Cosmos.Direct` restore in the graph for natives; pack assertion green; full query suite green against the new native; `dotnet restore` works with no `NuGet.config` and no authentication.

---

### Phase 3 — Compile from source, drop the managed dependency

- [ ] Implement the **D-0** decision — under option C, add `Microsoft.Azure.Cosmos.Direct.csproj` and reference it from `Client.csproj`.
- [ ] **D-1** — remove the compile reference (or swap it for a `ProjectReference`).
- [ ] **D-2** — collapse the `ProjectRef` dual-mode switch.
- [ ] Add the three package references the Direct sources need (see D-1).
- [ ] **Re-express `UseDirectSource` for the split-project shape.** The phase-1 form (`<Compile Remove="Direct/**/*.cs" />`) stops meaning anything once the sources move into `Direct.csproj`. Replace it with a `PackageReference` ↔ `ProjectReference` toggle, or the escape hatch silently stops working.
- [ ] Flip `UseDirectSource` to default `true`; keep `false` as an escape hatch.
- [ ] **D-5** — unhook downstream projects (FaultInjection, CTL, Benchmark, 4 test projects). Under option C this is a reference swap, not a migration.
- [ ] **D-4** — *only under D-0 option A*: stop packing the restored `Direct.dll` and ship the §9.C-1 facade.
- [ ] Validate the nupkg: exactly one `Direct.dll`, natives present, **no duplicate types** (P-11).

**Exit:** `main` builds Direct from source; full matrix green (unit, emulator, multi-region, thin-client, AOT, benchmark, CTL, FaultInjection).

---

### Phase 4 — Backward-compatibility gates

The baseline captured in phase 1 becomes enforced here.

- [ ] **§9.E Gate 1** — enforce the source-built Direct surface against `contracts/DirectSDKAPI.json` on every PR. Build this one first.
- [ ] **§9.B wire golden-file tests** — RNTBD token IDs, HTTP header constants, `StatusCodes`/`SubStatusCodes` numerics, `OperationType`/`ResourceType` numerics, `JSonSerializable` property names.
- [ ] **§9.A full tiering** — extend the phase-1 minimal manifest to all 372 files, plus the non-`COSMOSCLIENT` compile canary (A-6). The canary matters **more** under O-1: v3 developers now own the `#else` arms they never compile.
- [ ] **§9.E Gate 2** — v3 source built inside msdata. **The authoritative gate** under v3-primary; start nightly, graduate to blocking once stable.
- [ ] **§9.E Gate 3** — msdata source built inside v3. Nightly and **informational**; it measures msdata's lag rather than gating v3.
- [ ] ~~**§9.C-2 ApiCompat**~~ — not needed: D-0 option C satisfies binary compatibility by construction.

**Exit:** §9.D's minimum viable set is green and wired into CI.

---

### Phase 5 — Decommission and ship

- [ ] **D-7** — rewrite the sync tooling: manifest-driven, recursive discovery, fail-closed on unmapped files, provenance report.
- [ ] **D-8** — solution folders, `CODEOWNERS`, `Direct/README.md` house rules.
- [ ] **D-9** — retire the four msdata pipelines; **fold the AOT coverage in rather than deleting it**.
- [ ] Remove the `UseDirectSource` switch and the `false` path.
- [ ] Retire the `msdata/direct` branch, `docs/sync_up_msdata_direct.md`, the sync agent and helper script.
- [ ] Remove `<DirectVersion>` from [Directory.Build.props](Directory.Build.props#L6) — it has no remaining consumer once D-0 lands and the natives are committed.
- [ ] **Answer O-8** — state explicitly whether `Microsoft.HybridRow` stays a binary dependency permanently or is queued for the same treatment. Phase 5 should not declare “compile from source” achieved while the last binary dependency is unaddressed.
- [ ] `changelog.md` entry for the first customer-observable phase.
- [ ] Ship **one preview release** before GA (see Release staging below).

**Exit:** GA release built from source with no regressions.



### Landing safely — the `UseDirectSource` switch

R-1 makes phase 1a effectively a re-baseline off an Oct-2022 merge-base. To keep it revertible by one property rather than by a revert of 373 files, land the tree **excluded from the default build**:

```xml
<ItemGroup Condition=" '$(UseDirectSource)' != 'true' ">
  <Compile Remove="Direct/**/*.cs" />
</ItemGroup>
```

Default builds then produce byte-identical artifacts to today, while a new opt-in CI leg (`-p:UseDirectSource=true`, full unit + emulator suite) proves the source path.

> ⚠️ **This shape only works for phases 1–2.** It assumes the Direct sources are compiled *by `Microsoft.Azure.Cosmos.csproj`*. Under D-0 option C phase 3 moves them into `Microsoft.Azure.Cosmos.Direct.csproj`, at which point `<Compile Remove>` in the client project expresses nothing. From phase 3 the switch must instead toggle `PackageReference` ↔ `ProjectReference`. Specify that second form before phase 3, or the escape hatch silently stops being an escape hatch.
The switch is removed in phase 3 alongside the compile reference.

This also converts **C-6** from a discipline into a mechanism: both build shapes stay green at every commit because the default shape is not changing at all until phase 3.

### Release staging

Phase 5 is the first customer-observable change under D-0 option A (and, under option C, the first release whose `Direct.dll` is built here rather than restored). Ship **one preview release** on the new path before any GA, and hold the `UseDirectSource=false` escape hatch for one full release cycle after that.

---

## 9. Backward Compatibility & Divergence Strategy

Once `main` compiles the Direct sources directly, a v3 developer can edit a file in a way that is **incompatible with the msdata copy** — and nothing will stop them. That divergence has three independent failure modes, each needing its own mechanism.

| Axis | Question | Blast radius |
| --- | --- | --- |
| **A — Source compat with msdata** | Can the file still be shared with the server/backend build? | Next sync becomes a manual merge; drift compounds silently. |
| **B — Wire / serialization compat** | Does the change alter bytes on the wire or JSON on disk? | **Production outage.** Silent, and not caught by any current test. |
| **C — Binary compat for `Direct.dll` consumers** | Can code that bound to `Microsoft.Azure.Cosmos.Direct.dll` still load? | `TypeLoadException` for FaultInjection, CTL, Benchmark and external consumers. |
| **D — Managed/native ABI compat** | Do the `[DllImport]` signatures in `ServiceInteropWrapper.cs` still match the native `ServiceInterop.dll` they bind to? | `EntryPointNotFoundException`, or silent memory corruption. **New failure mode created by this migration** — see §9.F. |

### 9.A — Staying source-compatible with msdata

The rule of thumb: **express divergence, don't fork it.** Ranked by preference.

#### A-1. `#if COSMOSCLIENT` guards — the primary tool (already the idiom)

The v3 build defines `DOCDBCLIENT;COSMOSCLIENT;NETSTANDARD20`; the msdata server build does not define `COSMOSCLIENT`. The tree already uses this in **137 files / 251 directives**:

```csharp
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class DocumentServiceRequest
```

An "incompatible" change becomes a *guarded* change. The file stays **byte-identical in both repos**, so `msdata_sync.ps1` keeps working as a plain copy and round-trips cleanly.

Use this whenever the change is additive-for-v3 or behavioural. Prefer it over every other option.

**Limits, stated honestly:** a guard cannot help when both sides need the *same* member with *different* shape and the server actually calls it — you end up maintaining an `#else` branch that v3 CI never compiles (see A-6). Guards also rot. Cap their use with the tier policy in A-4.

#### A-2. Partial-class sidecars

Make the type `partial` upstream once, then keep all v3-only additions in a file the sync never touches:

```
Direct/Transport/Rntbd/Channel.cs                 <- mirrored, never hand-edited
Direct/Extensions/Channel.Client.partial.cs       <- v3-only, outside the sync map
```

Zero drift in the mirrored file, and the addition is reviewable in isolation. Best for **added members**. `NativeMethods.*` already demonstrates the pattern in-tree.

#### A-3. Seam interfaces — the established in-repo precedent

`IStoreModelExtension`, `IAddressResolverExtension`, `IServiceConfigurationReaderExtension` and `IChaosInterceptor` all exist precisely so v3 (and FaultInjection) can extend Direct behaviour without editing Direct types. When a change is "v3 needs different behaviour here", the first question should be *"can this be a seam?"* — add a narrow interface upstream, implement it in `src/` (not `src/Direct/`). Cost is one small upstream PR; benefit is permanent zero drift.

Related no-drift options: extension methods in a `src/`-side static class, or an `internal` decorator wrapping the Direct type.

#### A-4. Tier the files — not all 372 are equal

Divergence policy should depend on who consumes the file. Record the tier in the sync manifest (A-5):

| Tier | Files | Policy |
| --- | --- | --- |
| **T0 — Lockstep / wire contract** | `RntbdConstants.cs`, `HeadersTransportSerialization.tt`, `HttpConstants.cs`, `WFConstants.cs`, `StatusCodes.cs`, `ResourceType.cs`, `OperationType.cs`, `PartitionKeyInternal*.cs`, `MurmurHash.cs`, `ResourceId.cs`, `Range.cs` | **Byte-identical to msdata. No exceptions.** CI hash gate. Changes originate **here** (O-1: v3 is upstream) but require msdata sign-off before merge, because msdata must absorb them in lockstep. |
| **T1 — Shared implementation** | Transport, Store, Routing, Retry, Session, Diagnostics | Divergence allowed **only** via A-1/A-2/A-3, and only with a manifest drift entry naming an owner + backport issue. |
| **T2 — Client-only** | `ServiceModel/Client/`, `Telemetry/`, `Compat/`, `FaultInjection/`, most of `Resources/Settings` | v3 owns them. Drop from the sync map entirely — they stop being "shared files". |

Tiering is what turns "the whole folder is untouchable" into "1 in 4 files is untouchable", which is the difference between a policy people follow and one they route around.

#### A-5. Drift manifest + CI gate

`src/Direct/.sync/manifest.json`, one record per file:

```jsonc
{
  "path": "Transport/Rntbd/RntbdConstants.cs",
  "upstream": "Product/Microsoft.Azure.Documents/SharedFiles/Rntbd/RntbdConstants.cs",
  "tier": "T0",
  "upstreamSha256": "…",          // hash at last sync
  "drift": null                    // or { reason, owner, backportIssue, since }
}
```

A `verify-direct-drift` CI job rehashes every file and fails when a T0/T1 file changed without a matching `drift` record. This is the mechanism that makes divergence *loud* — everything else is convention.

#### A-6. Compile canary for the non-`COSMOSCLIENT` branch

**Today, every `#if !COSMOSCLIENT` block in the repo is dead code that is never compiled by v3 CI.** It can rot for months and only explode at the next sync. Fix: add a *build-only* project

```
Microsoft.Azure.Cosmos/tests/Direct.ServerShape.Canary/Direct.ServerShape.Canary.csproj
```

that globs the same `src/Direct/**/*.cs` with `COSMOSCLIENT` **not** defined, and is only required to compile (never run, never packed). Server-only types it can't resolve get thin stubs under `Canary/Stubs/`. Even a partial canary catches the common case — a v3 developer editing a shared method and breaking the `#else` arm.

Start it as `ContinueOnError` and ratchet.

#### A-7. Sync becomes a 3-way merge, not a copy

> **Applies to the catch-up window only.** O-1 makes v3 upstream, so the steady state is an **export** from `main` to msdata (D-7), not an import. But phases 1–4 still have to absorb msdata's current state — the branch is two releases stale (P-12) and the merge-base is from Oct 2022 (R-1) — so inbound sync remains real work until the export direction is live. Retire these mechanisms in phase 5.

`msdata_sync.ps1` today is `Copy-Item -Force` — it silently destroys local changes. Two ways out:

- **Vendor baseline branch (recommended for sustained drift).** Keep a `direct-upstream` branch that only ever receives verbatim msdata copies. Sync = commit to `direct-upstream`, then `git merge` it into the feature branch. Git does a real 3-way merge and surfaces conflicts on exactly the diverged hunks. Same shape as `git subtree`.
- **Patch queue (recommended if drift should stay small).** Store local deltas as `.patch` files under `src/Direct/.sync/patches/`. Sync = copy verbatim, then re-apply the queue. A patch that no longer applies is a loud, actionable signal, and the queue's size is a visible drift budget.

#### A-8. Explicit fork — the escape hatch

When a file genuinely must diverge permanently, **move it out of `src/Direct/`** into `src/` proper and delete it from the sync map. An explicit fork with a one-line "forked from msdata `<path>` at `<sha>` because `<reason>`" header beats indefinite silent drift. Requires the manifest entry to be retired, so it shows up in review.

### 9.B — Protecting wire & serialization compatibility

Axis B is the one that causes outages, and no mechanism above catches it on its own — a T0 hash gate only helps if the file was correctly tiered. Add **behavioural golden-file tests** that fail independently of the sync process:

- RNTBD token IDs / types snapshotted from `RntbdConstants` + `HeadersTransportSerialization.tt`.
- HTTP header name constants (`HttpConstants`, `WFConstants`).
- `StatusCodes` / `SubStatusCodes` numeric values.
- `OperationType` / `ResourceType` numeric values (they are serialized).
- JSON property names for every `JSonSerializable`-derived type in `Resources/` and `Resources/Settings/` (reflect over `[JsonProperty]` / constant name fields, compare to a checked-in baseline).

Each is a cheap reflection test over a `.baseline.txt` file. Updating a baseline is then a deliberate, reviewable act — which is exactly the property that's missing today.

### 9.C — Binary compatibility for `Direct.dll` consumers

Relevant the moment D-4 lands (`Microsoft.Azure.Cosmos.Direct.dll` stops being packed).

- **C-1 — Type-forwarding facade.** Ship a `Microsoft.Azure.Cosmos.Direct.dll` containing only `[assembly: TypeForwardedTo(typeof(...))]` for every previously-public `Microsoft.Azure.Documents.*` type, forwarding into `Microsoft.Azure.Cosmos.Client`. Must be built from *this* repo (otherwise the dependency is circular) and must keep the **same strong-name identity** — the public keys are already in `Direct/AssemblyKeys.cs`. Keep it for at least two minor versions, then remove with a `Breaking Changes` changelog entry.
- **C-2 — ApiCompat baseline.** Run `Microsoft.DotNet.ApiCompat` between the last shipped `Microsoft.Azure.Cosmos.Direct.dll` (3.43.2) and the new `Microsoft.Azure.Cosmos.Client.dll`, restricted to the `Microsoft.Azure.Documents.*` surface. Check in the suppression baseline; any *new* break then requires an explicit baseline update in the PR.
- **C-3 — `InternalsVisibleTo` audit.** Enumerate every assembly granted access via `AssemblyKeys.cs` before removing the Direct assembly; each one is a consumer that must be migrated or facaded.
- **C-4 — Binding redirects.** .NET Framework consumers with an existing `assemblyBinding` entry for `Microsoft.Azure.Cosmos.Direct` need guidance in the release notes.

### 9.D — Recommended minimum viable set

If only some of this gets built, build these five — they cover the failure modes that are both *likely* and *silent*:

1. **A-4 tiering + A-5 manifest with a T0 hash gate** — makes drift impossible to land accidentally.
2. **B golden-file tests** — the only defence against a wire break.
3. **A-6 compile canary** — stops the `#else` arms rotting.
4. **A-7 merge-based sync** — makes the *catch-up* syncs during phases 1–4 a merge instead of a data-loss event. Retired in phase 5 once the export direction (D-7) is live.
5. **F-1 — tier `ServiceInteropWrapper.cs` as T0** (§9.F). A P/Invoke signature drift compiles cleanly and fails at runtime; this is the cheapest possible guard against it.

A-1/A-2/A-3 need no tooling at all; they just need to be written down as the house rules in `src/Direct/README.md`.

### 9.E — Cross-repo compile gates

§9.A–9.D make divergence *discouraged and detectable inside this repo*. They cannot tell you whether a v3-side edit still builds on the msdata side. That needs three gates, only the first of which is in-repo.

| Gate | What it does | Cost | Catches |
| --- | --- | --- | --- |
| **1 — Surface baseline** *(in-repo, every PR)* | Snapshot the Direct **3.44.1** API surface into `contracts/DirectSDKAPI.json` using the existing [ContractEnforcement](Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/ContractEnforcement.cs) machinery, then diff the source-built assembly against it | Low — no cross-repo access | Accidental surface changes to Direct types made from v3. Subsumes §9.C-2 |
| **2 — v3 source built inside msdata** *(nightly)* | Drop `src/Direct/**` into the msdata CosmosDB build, compile, run msdata's Direct tests | Needs msdata agents + a cross-org service connection | v3-side edits that break the backend build or behaviour — the Axis A failure §9.A only *discourages* |
| **3 — msdata source built inside v3** *(nightly)* | Pull msdata's current Direct source, build v3 against it, run v3's suite, report drift as a file-level diff | Same | msdata-side changes v3 has not absorbed; keeps the sync backlog continuously visible |

**Build Gate 1 first.** It is entirely in-repo, reuses tooling that already exists, and is independently valuable even if the migration stalls — a committed Direct surface baseline gives drift detection today. It mirrors the proven NuGet-surface parity pattern in [templates/build-preview.yml](templates/build-preview.yml#L87-L124), where a parity build turns a runtime `TypeLoadException` into a build-time `CS0534`.

**Keep Gates 2 and 3 nightly and non-blocking until proven stable.** A flaky cross-org gate that blocks every v3 PR is worse than no gate.

**Gate 2 is the authoritative gate** — O-1 resolved 2026-09-23 as v3-primary, so the question that matters is “does a v3-side edit still build on the msdata side?” Gate 3 becomes **informational**: it measures how far behind msdata has fallen rather than gating v3. Gate 2 should therefore graduate from nightly to blocking once it is proven stable; Gate 3 stays nightly permanently.

### 9.F — Managed/native ABI compatibility (Axis D)

**This migration creates a failure mode that does not exist today.** `ServiceInteropWrapper.cs` is the *managed half* of the query-plan interop layer — 346 lines carrying **12 `[DllImport]` declarations** over **6 extern entry points**, with `CallingConvention.Cdecl` and explicit marshalling attributes. Each entry point is declared twice behind `#if COSMOSCLIENT`, selecting `Microsoft.Azure.Cosmos.ServiceInterop.dll` (v3) or the V2-SDK name `Microsoft.Azure.Documents.ServiceInterop.dll`. Only the v3 name is ever shipped — see F-4, which corrects an earlier draft of this item.

After this migration the two halves have **different lifecycles**:

| Half | Where it comes from | Who can change it |
| --- | --- | --- |
| Managed — `Direct/Interop/ServiceInteropWrapper.cs` | **Source in this repo** | any v3 PR |
| Native — `Microsoft.Azure.Cosmos.ServiceInterop.dll` (renamed from `Cosmos.QueryPlanInterop.dll`) | **Restored from the public `QueryPlanInterop` package** (D-3), pinned by `$(QueryPlanInteropVersion)` | a deliberate version bump only |

Today both ship in the same nupkg and move together, so they cannot disagree. Once the managed half is editable here, three new ways to break it appear:

1. A v3 PR edits a P/Invoke signature, struct layout or marshalling attribute → `EntryPointNotFoundException` on the happy path, or **silent memory corruption** if the entry point still resolves but the layout shifted. `PartitionKeyRangesApiOptions` is the sharp edge: a hand-maintained mirror of a native struct with 28 bytes of explicit reserved padding, so a new field fits without any size change.
2. A sync from msdata brings a wrapper change across **without** `$(QueryPlanInteropVersion)` being moved to a native that matches.
3. `$(QueryPlanInteropVersion)` is bumped and the wrapper is left untouched.

None of these are caught by §9.B (wire format), §9.C (assembly identity) or §9.E (compilation). A signature mismatch **compiles perfectly**.

> **D-3 keeps this manageable.** `FileVersion` on the native is frozen at `2.14.0.0` across all ten Direct releases measured, so the binary never carried a usable version coordinate; `$(DirectVersion)` was a proxy, and it is going away. `$(QueryPlanInteropVersion)` replaces it as the single pin on both halves.

**Mitigations, cheapest first:**

- **F-1 — Tier `ServiceInteropWrapper.cs` as T0** in the §9.A-4/A-5 manifest, so any content change trips the hash gate and forces an explicit `drift` record. This is the single highest-value item and costs nothing beyond correct tiering.
- **F-2 — Pin the coupling to a single native coordinate.** `$(QueryPlanInteropVersion)` (D-3 condition 1) is the pinned version of the restored native package. Fail the build if `ServiceInteropWrapper.cs` changes without that version moving, or vice versa. This replaces the earlier proposals to pin against `$(DirectVersion)` (being removed) and against a committed `native-assets.json` hash. A version pin is adequate here **only because** the package is immutable once published — the native's own `FileVersion` is frozen at `2.14.0.0` and never moved across ten releases, so it cannot be used. **O-5 sets the cadence at ~2 months**, so this pin moves roughly six times a year — build a routine absorption path (bump, run the §9.F F-3 smoke test, re-run the query suite), not a one-off.
- **F-3 — Smoke test the interop path in CI.** A single query that requires a ServiceInterop-generated plan, asserting `ServiceInteropWrapper.AssembliesExist` is true first so the test cannot silently pass via the gateway fallback. [SmokeTests](Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/SmokeTests.cs#L50) already asserts `AssembliesExist`; extend it to exercise each entry point.
- **F-4 — Do *not* strip the `#if COSMOSCLIENT` guards from the wrapper.** An earlier draft of this item said the opposite; that was wrong. The guards are load-bearing: they select the shipped DLL name over the V2-SDK name, and they also gate `IsGatewayAllowedToParseQueries`'s legacy `DisableSkipInterop` fallback. `COSMOSCLIENT` is defined unconditionally in [Microsoft.Azure.Cosmos.csproj](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L195); if that ever stops being true, every P/Invoke binds to `Microsoft.Azure.Documents.ServiceInterop.dll` — a file this package does not ship — and all query planning silently falls back to the gateway with no build error. Add a build-time assertion that the symbol is defined. Record the reasoning in `src/Direct/README.md` so the guards are not "cleaned up" by a future reader.

---

## 10. Risks & Open Questions

Live risks and open questions first; resolved items are compressed to one line each at the end of the section.

| ID | Item |
| --- | --- |
| R-2 | `git mv` of 373 files in one commit — verify rename detection survives (`git log --follow`, `git blame`). Use `git config diff.renameLimit` high enough. |
| R-3 | 55 filename collisions with `src/**` remain after the move (different folders now, so tolerable, but "go to file" is still noisy). Consider whether any Direct file should keep a disambiguating name. |
| R-4 | `TreatWarningsAsErrors=true` + un-suppressing 26 rules (phase 1b, D-6) may cascade. Budget for it. |
| R-5 | **`Microsoft.HybridRow` is the last remaining binary dependency** — not covered by this plan. Once Direct is source-compiled (D-0) and the native comes from `QueryPlanInterop` (D-3), HybridRow is the only package whose DLLs (`Microsoft.Azure.Cosmos.Core.dll`, `Microsoft.Azure.Cosmos.Serialization.HybridRow.dll`) are still restored and re-packed into the SDK nupkg. Tracked as **O-8**. |
| R-6 | **Silent drift.** The moment `main` compiles these sources, any PR can diverge a shared file with nothing to stop it. §9.D is the mitigation and should land alongside phase 1, not at the end. |
| R-7 | Every `#if !COSMOSCLIENT` block (137 files carry `COSMOSCLIENT` directives) is currently **never compiled by v3 CI**. It can rot undetected until the next sync. Mitigation: §9.A-6 canary. |
| R-8 | **Strong-name identity — evidence favourable, still formally unverified.** The O-7 reference assembly is `Microsoft.Azure.Cosmos.Direct, Version=3.44.1.0, PublicKeyToken=31bf3856ad364e35` — the standard Microsoft shared-library token, which is what [35MSSharedLib1024.snk](35MSSharedLib1024.snk) produces. So D-0 option C is very likely viable. Confirm by actually delay-signing and comparing the token before phase 1; §9.C-1's facade has the same requirement, so the check is unavoidable either way. |
| R-9 | Cross-org gates (§9.E gates 2–3) depend on msdata agents and a service connection. Made blocking too early, a flaky gate stalls every v3 PR. Keep nightly until proven. |
| R-11 | Getting the native delivery wrong fails **silently** — the nupkg ships without the native, `ServiceInteropWrapper.AssembliesExist` goes false, and query planning falls back to the gateway with no build error. Under D-3 the trigger is a mis-set `TargetPath` on the rename, or the `ContentWithTargetPath` block not running. Mitigation: pack-time assertion (§11), **not** a test — it is the only thing that catches it. |
| R-12 | **Managed/native ABI split-brain.** After this migration `ServiceInteropWrapper.cs` (12 `[DllImport]`s, 6 entry points) is editable in this repo while the native arrives as an independently versioned package. The two halves ship together today and cannot disagree; afterwards they can. A mismatch compiles cleanly and fails at runtime — or corrupts memory, since `PartitionKeyRangesApiOptions` has 28 bytes of reserved padding that absorbs a new field without changing size. Mitigation: §9.F, F-2 in particular. |
| R-13 | `RMResources.Designer.cs` / `RMResources.resx` are msdata-sourced, land in `src/` rather than `src/direct/`, and are **absent from `main`**. Easy to miss in phase 0 because they sit outside the §6 mapping. |
| R-17 | **The `InternalsVisibleTo` graph is not in the imported source.** Zero `InternalsVisibleTo` occurrences across the 371 `.cs` files on `origin/msdata/direct`, yet the shipped `Direct.dll` carries **48** friend grants — including service-side assemblies (`CosmosDB.Runtime`, `Compute.*`, `Sql.Service`, `Mongo.Service*`, `Cassandra.*`, `DataTransfer.*`, `Portal.Services.Backend`, `Analytics.Core`, `CosmosFabric`) and sibling SDKs (`Azure.Cosmos`, `Table`, `Encryption`). Under D-0 option C we build that assembly, so the list must be replicated verbatim. Omissions break either our own build (loud) or another team's (silent). Same class of blind spot as R-13. |
| R-18 | **`AssemblyVersion` stamping.** The O-7 reference assembly is `3.44.1.0` (verified); this repo stamps from `ClientOfficialVersion` (3.63.0). Assembly version is part of assembly identity, so inheriting the SDK version changes the identity that option C exists to preserve, and .NET Framework consumers would need binding redirects. Pin `<AssemblyVersion>3.44.1.0</AssemblyVersion>` and let `FileVersion` move. Trivial to fix, easy to miss, expensive post-release. |
| O-5 | **Cadence answered 2026-09-23; provenance still open.** `QueryPlanInterop` ships on a **~2-month cadence**, so the native is *not* frozen — R-15 is fully downgraded and F-2 will see roughly six version bumps a year, which needs a routine absorption process rather than a one-off. **Still open:** which ServiceInterop build does `QueryPlanInterop` 1.0.2 correspond to? The size gap widened with the O-7 reference — 3.44.1's `ServiceInterop.dll` is 9.14 MB against QueryPlanInterop's 7.40 MB — and the two artifacts now have independent cadences and version schemes. F-2's pin is only meaningful once that mapping is known. Needed before phase 5; also D-3 condition 6. |
| O-6 | **Open — blocks phase 3, raised 2026-09-23.** Under D-0 option C, what does `ProjectRef=True` (msdata's build) do with the new in-repo `Microsoft.Azure.Cosmos.Direct.csproj`? msdata supplies its own Direct from its own tree, so the `ProjectReference` must be conditional or msdata compiles two copies of `Microsoft.Azure.Documents.*` — P-11 relocated. This makes the `ProjectRef` switch **more** load-bearing, contradicting D-2's instruction to collapse it. O-1 narrows this: with v3 upstream, msdata should eventually consume v3's Direct sources rather than maintain its own, so the long-term answer is probably “retire `ProjectRef` after the export path lands” — but the phase-3 behaviour still needs deciding with the msdata owners. |
| O-8 | **Open — does not block any phase; scope question, raised 2026-09-23.** After this effort, `Microsoft.HybridRow` (R-5) is the **only** binary dependency left — its two DLLs are restored and re-packed into the SDK nupkg exactly as Direct's were. Does it eventually get the same source-integration treatment, or is it the permanent end state? Answering “permanent” is legitimate; leaving it unstated means the “compile from source” goal reads as achieved when it is half-achieved. Decide before phase 5 declares the migration complete. |

### Resolved

Kept as one-liners because these IDs are referenced throughout the document.

| ID | Resolution |
| --- | --- |
| R-1 | Phase 1a is a **re-import, not a merge** — procedure in §8 phase 1a. Only execution risk left is step 0, msdata source access. |
| R-10 | Option-A-only surface churn. Option C keeps the surface in `Direct.dll`, so C-2 holds by construction. Reopens only if R-8 fails. |
| R-14 | No binaries committed ⇒ no Git LFS rule needed. Reopens only if D-3 falls back to committing (LFS must precede the first binary commit). |
| R-15 | Query-plan ABI still grows, but absorbing a new engine is now a `$(QueryPlanInteropVersion)` bump rather than a freeze. Residual concern moved to O-5. |
| R-16 | Native comes from public nuget.org, so external contributors get it like any other package. No split local-dev experience. |
| O-1 | **v3 `main` is upstream; msdata is downstream.** Threaded through §9.A-4, §9.A-7, §9.E, D-7 and §5. |
| O-2 | Natives come from the public `QueryPlanInterop` package (D-3). Committing remains the documented fallback. |
| O-3 | No type-forwarding facade needed under D-0 option C. |
| O-4 | Direct types keep their assembly identity via an in-repo `Microsoft.Azure.Cosmos.Direct.csproj` (D-0 option C). |
| O-7 | **Reference Direct version is `3.44.1`** — verified `Version=3.44.1.0`, `PublicKeyToken=31bf3856ad364e35`, 48 IVT grants. R-1 must land the import at 3.44.1 equivalence. |

---

## 11. Validation Checklist

- [ ] `dotnet build Microsoft.Azure.Cosmos.sln -c Release` succeeds with `TreatWarningsAsErrors=true`.
- [ ] `dotnet test` — unit tests, then emulator tests.
- [ ] `git diff --stat` for phase 1b shows **only** renames (`R100`), no content deltas.
- [ ] `contracts/API_*.txt` baseline unchanged (`UpdateContracts.ps1` produces no diff).
- [ ] Produced nupkg contains `runtimes/win-x64/native/Microsoft.Azure.Cosmos.ServiceInterop.dll` (renamed from `Cosmos.QueryPlanInterop.dll`) and **no longer** contains `Cosmos.CRTCompat.dll`, `msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`.
- [ ] `dotnet restore` succeeds with **no `NuGet.config` and no authentication** — the QueryPlanInterop reference must not require a feed.
- [ ] Changelog entry for the removal of the four VC++/CRT DLLs from the nupkg (customer-observable, D-3 condition 4).
- [ ] Produced nupkg `lib/netstandard2.0` contents reviewed against the previous release.
- [ ] `grep -r "Microsoft.Azure.Cosmos.Direct"` across `*.csproj`, `*.props`, `*.yml` returns only intentionally-retained hits.
- [ ] `msdata_sync.ps1` dry-run places every msdata file into a mapped folder (no `_Unsorted/` residue).
- [ ] `verify-direct-drift` passes: no T0/T1 file diverges from its manifest hash without a `drift` record.
- [ ] Wire golden-file tests pass unchanged (RNTBD tokens, header names, status/sub-status values, `OperationType`/`ResourceType` numerics, `JSonSerializable` property names).
- [ ] Non-`COSMOSCLIENT` compile canary builds.
- [ ] Direct surface of the source-built assembly matches `contracts/DirectSDKAPI.json` (§9.E Gate 1). Replaces the option-A-only ApiCompat run, which D-0 option C makes unnecessary.
- [ ] Changelog entry added for the phase that first becomes customer-observable (phase 5 at the latest).
- [ ] **Strong-name public key token** of the source-built Direct assembly matches the O-7 reference `Microsoft.Azure.Cosmos.Direct` 3.44.1 (`31bf3856ad364e35`) — gates D-0 option C and §9.C-1 (R-8).
- [ ] Source-built Direct assembly reports `Version=3.44.1.0` — **not** the SDK version (R-18).
- [ ] All **48** `InternalsVisibleTo` grants present on the source-built Direct assembly; diff its attribute list against reference 3.44.1 and assert empty (R-17).
- [ ] Produced `.nuspec` dependency list is unchanged versus the previous release — Direct's three `PackageReference`s did not leak into the SDK's transitive graph (D-0 “also verify”).
- [ ] Produced nupkg does **not** contain `Direct.dll` and the same types compiled into `Client.dll` (P-11).
- [ ] `-p:UseDirectSource=false` still produces byte-identical artifacts to the previous release, until phase 3 removes the switch.
- [ ] Gate 1 (§9.E) green: source-built Direct surface matches `contracts/DirectSDKAPI.json`.
- [ ] One preview release shipped on the source-built path before GA.
- [ ] `RMResources.Designer.cs` and `RMResources.resx` present in `Microsoft.Azure.Cosmos/src/` (R-13).
- [ ] ServiceInterop smoke test passes with `AssembliesExist == true`, exercising each P/Invoke entry point (§9.F F-3) — it must not silently pass via the gateway fallback.
- [ ] `ServiceInteropWrapper.cs` is tiered T0 in the drift manifest and pinned to `$(QueryPlanInteropVersion)` (§9.F F-1/F-2).
- [ ] `COSMOSCLIENT` is asserted as defined at build time (§9.F F-4) — without it every `[DllImport]` binds to a filename the package does not ship.
- [ ] Natives are acquired by a plain `PackageReference` to the **public** QueryPlanInterop package — no internal feed, no `NuGet.config`, no pipeline acquisition step.
- [ ] The pack-time natives assertion **fails the build** when the native is missing — verified by a deliberate negative run, since a mis-set `TargetPath` fails silently (R-11).
- [ ] The pack-time natives assertion is **scoped to Windows RIDs**, matching `Microsoft.Azure.Cosmos.targets` — an unscoped assertion false-fires on Linux/macOS builds where the gateway fallback is correct.
- [ ] No `PackageReference` to `Microsoft.Azure.Cosmos.Direct` remains in any `*.csproj` after phase 3 — including restore-only forms.

---

## 12. Immediate Next Steps

In order. Four of the six gate decisions are resolved (D-0, D-3, O-1, O-7). **No decision blocks phase 1 any more** — what remains for phase 0 is two inputs and one procedure.

1. **Run R-8 formally** — delay-sign with [35MSSharedLib1024.snk](35MSSharedLib1024.snk) and confirm the token is `31bf3856ad364e35` against reference **3.44.1**. Evidence already points this way, so treat it as confirmation. Hours, not days.
2. **Get the authoritative `InternalsVisibleTo` list** for `Direct.dll` **3.44.1** from msdata (R-17). 48 grants observed on the published assembly; msdata's build is canonical. Blocks phase 1a.
3. **Arrange msdata source access** (R-1 step 0). The procedure is written; the one thing it cannot do for itself is reach `sdkReleases/direct/EN20260409-3.44.1` — this clone has `origin` (GitHub) as its only remote. Either add an msdata remote with read access, or get an msdata-side owner to produce a verbatim export of the mapped file set. **This is now the only thing gating phase 1.**
4. **Capture `contracts/DirectSDKAPI.json`** from published **3.44.1**. Can start immediately and is independently valuable: even if the migration stalls, a committed Direct surface baseline gives drift detection today. Must be captured **before** any Direct source is edited.
5. **Confirm `QueryPlanInterop` provenance** (O-5): which ServiceInterop build does 1.0.2 correspond to? Cadence is settled at ~2 months. Blocks phase 5, and F-2's pin needs the mapping; can run in parallel.
6. **Agree the msdata coordination model** with the msdata owners now that v3 is upstream: **O-6**'s phase-3 `ProjectRef` behaviour, the T0 sign-off path (§9.A-4), and the §9.E Gate 2 service connection. Blocks phases 3–4.
7. Only then open phase 1.
