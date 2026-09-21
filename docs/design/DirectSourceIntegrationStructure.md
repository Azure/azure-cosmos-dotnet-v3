# Direct Source Integration — File & Folder Structure Plan

**Status:** Draft / proposal
**Branch analysed:** `msdata/direct` @ `1bc2c522` (`[Internal] Direct package: Adds msdata/direct update from main (#5951)`)
**Target:** `main`
**Goal:** Land the `msdata/direct` source tree on `main` and **remove the `Microsoft.Azure.Cosmos.Direct` managed dependency** — the SDK compiles the Direct sources rather than referencing the shipped assembly. A restore-only reference for the native assets may have to survive; see D-3.

---

## 1. Objective & Scope

### In scope

1. Reorganise the flat `Microsoft.Azure.Cosmos/src/direct/` dump (373 files) into a coherent, navigable folder tree.
2. Define the project/build changes required so the SDK compiles the Direct sources **from source**, with no *compile-time* `PackageReference` to `Microsoft.Azure.Cosmos.Direct`. (A restore-only reference for the native assets is a separate question — D-3.)
3. Decide which assembly the `Microsoft.Azure.Documents.*` types ship from, since that governs whether this is customer-breaking — D-0.
4. Define the migration sequencing so history, CI and the shipped package surface stay intact.
5. Define the CI gates that keep the in-repo copy compatible with msdata and with existing consumers — §9.

### Out of scope (explicitly)

- Renaming namespaces. `Microsoft.Azure.Documents.*` stays as-is. Folder layout and namespace layout are decoupled on purpose (see §4, principle P2).
- Refactoring Direct code behaviour. This is a **move-only** change.

> **Previously out of scope, now blocking.** Earlier drafts deferred the ownership/sync
> story with the `msdata` CosmosDB repo. It cannot be deferred: §9's entire divergence
> apparatus — tiering, drift manifest, vendor-baseline branch, and which of the §9.E
> cross-repo gates is authoritative — inverts depending on whether `main` or msdata is
> upstream. It is now the **decision gate** at the head of §8.

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

> **Two more msdata-sourced files live *outside* this tree.** `msdata_sync.ps1` copies
> `RMResources.Designer.cs` and `RMResources.resx` to `$currentLocation\..` — i.e.
> `Microsoft.Azure.Cosmos/src/`, one level **above** `src/direct/`. Both exist on
> `msdata/direct` and **neither exists on `main`**, so phase 1a must bring them across.
> They are deliberately excluded from the §6 mapping (which covers only the 372 files
> that move into `Direct/`), but D-7's sync rewrite must still handle them — they are
> part of the msdata surface and drift the same way everything else does.

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
| P-12 | **The branch is version-stale.** `msdata/direct` sits at v3 `3.61.0` / Direct `3.43.2`; `main` is at `3.63.0` / `3.44.0`. The import is against a two-release-old baseline — see R-1. |

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

**Critical coupling:** the Direct nupkg is also the *only* source of the native assets that
[Microsoft.Azure.Cosmos.targets](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.targets) copies and that the SDK nupkg republishes under `runtimes/win-x64/native`:
`Microsoft.Azure.Cosmos.ServiceInterop.dll`, `Cosmos.CRTCompat.dll`, `msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`.
Deleting the `PackageReference` outright breaks the query-plan ServiceInterop path. See §7, decision D-3.

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
├── README.md                          # provenance, sync process, "do not hand-edit" notice
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
tools/msdata-direct/msdata_sync.ps1    # moved from src/direct/msdata_sync.ps1
```

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

\* from `direct/rntbd2/`.
\*\* **Rename required.** `direct/rntbd2/TransportClient.cs` collides with `direct/TransportClient.cs`. Rename the file only — the type stays `Microsoft.Azure.Documents.Rntbd.TransportClient`. Alternative if renaming is unacceptable: keep an extra `Direct/Transport/Rntbd2/` folder. **Recommendation: rename**, and update `msdata_sync.ps1`'s special-case block (it already special-cases this file).

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

### D-0 — Which assembly do the Direct types ship from? (**decision required — resolves O-4**)

The highest-leverage decision in this document. D-1, D-4, §9.C and phases 3–5 all
hang off it, so settle it before any code moves.

**The constraint is assembly type identity, not file location.** Today
`Microsoft.Azure.Documents.StoreResponse` has exactly one identity:
`[Microsoft.Azure.Cosmos.Direct, Version=3.44.0, PublicKeyToken=…]`. If those types are
recompiled into `Microsoft.Azure.Cosmos.Client.dll` while any consumer still references
the old `Direct.dll`, the runtime sees **two unrelated types with the same name**:
`CS0433` at compile time, `TypeLoadException` at load, or — worst — a silent
`InvalidCastException` deep in a retry path. Affected consumers are every project in
§2.4 plus every assembly in the `AssemblyKeys.cs` `InternalsVisibleTo` graph.

| Option | Mechanism | Binary compat | Consequences |
| --- | --- | --- | --- |
| **A** — merge into `Client.dll` *(the assumption in earlier drafts)* | Compile `src/Direct/**` into `Microsoft.Azure.Cosmos.Client.dll`; stop shipping `Direct.dll` | ❌ Breaks every external consumer | Forces D-4, the §9.C-1 facade and C-4 binding redirects. Also merges the `Microsoft.Azure.Documents.*` public surface into the SDK assembly, which **conflicts with C-2** (`contracts/API_*.txt` must not change) unless every such type is `internal`. |
| **B** — type-forwarding facade | Ship a `Direct.dll` containing only `[assembly: TypeForwardedTo]` entries | ⚠️ Public types only; fragile with `InternalsVisibleTo` + strong names | A *mitigation* for option A, not an architecture in its own right. This is §9.C-1. |
| **C (recommended)** — same assembly, new source home | Add `Microsoft.Azure.Cosmos.Direct.csproj` **in this repo** producing the same `AssemblyName`, strong name and surface; `Client.csproj` takes a `ProjectReference` instead of a `PackageReference` | ✅ Zero change for any consumer | D-4, §9.C-1 and §9.C-4 become unnecessary. D-5 degrades to a `PackageReference` → `ProjectReference` swap. C-2 is satisfied by construction. |

**Recommendation: C.** The stated goal is "Direct code editable in the same PR as SDK
code." Option C delivers exactly that while every consumer keeps binding to the
identical assembly identity — nobody outside this repo changes anything. Option A buys
no additional capability and costs a customer-visible break plus a facade to carry for
two minor versions.

**Gating check.** Option C requires that delay-signing with
[35MSSharedLib1024.snk](35MSSharedLib1024.snk) yields the same public key token as the
shipped `Direct.dll`. Verify this **first** (R-8, §11). If the tokens differ, C is not
viable and the fallback is A + B. Note §9.C-1's facade has the same requirement, so
this check is unavoidable either way.

### D-1 — Delete the `Microsoft.Azure.Cosmos.Direct` compile reference

`Microsoft.Azure.Cosmos.csproj` currently carries:

```xml
<PackageReference Include="Microsoft.Azure.Cosmos.Direct" Version="[$(DirectVersion)]" PrivateAssets="All">
  <ExcludeAssets>compile</ExcludeAssets>
</PackageReference>
```

With the sources compiled in-tree, the `compile` exclusion is already a no-op. Remove the reference from the *compile* perspective and keep only what D-3 requires.

Under **D-0 option C** this becomes a `PackageReference` → `ProjectReference` swap to the new `Microsoft.Azure.Cosmos.Direct.csproj` rather than a deletion; the D-3 restore-only reference for natives is unaffected either way.

**Three package references must be added** for the Direct sources to compile. `msdata/direct` already carries them and they are absent from `main`:

```xml
<PackageReference Include="System.Diagnostics.PerformanceCounter" Version="6.0.0" />
<PackageReference Include="System.Net.Http" Version="4.3.4" />
<PackageReference Include="System.Text.RegularExpressions" Version="4.3.1" />
```

Under D-0 option C they belong on `Microsoft.Azure.Cosmos.Direct.csproj`, not on the SDK project — which also keeps them out of the SDK's transitive dependency graph. Under option A they land on `Microsoft.Azure.Cosmos.csproj` and become new transitive dependencies of the shipped package, which is a customer-visible change needing a changelog entry.

### D-2 — Retire the `ProjectRef` dual-mode switch

`ProjectRef != 'True'` currently gates the Direct/HybridRow/SourceLink block, packing and signing. Once Direct is source-compiled there is only one mode; keep the gate for SourceLink/signing/packing, but drop the Direct-specific conditionals so the two build shapes converge.

### D-3 — Native `ServiceInterop` assets (**decision required**)

**Verified origin.** The natives ship inside the `Microsoft.Azure.Cosmos.Direct` nupkg.
Contents of the restored 3.44.0 package:

```
build\netstandard2.0\Microsoft.Azure.Cosmos.Direct.targets
lib\netstandard2.0\Microsoft.Azure.Cosmos.Direct.dll
runtimes\win-x64\native\Microsoft.Azure.Cosmos.ServiceInterop.dll
runtimes\win-x64\native\Cosmos.CRTCompat.dll
runtimes\win-x64\native\msvcp140.dll
runtimes\win-x64\native\vcruntime140.dll
runtimes\win-x64\native\vcruntime140_1.dll
```

They reach the SDK nupkg in four hops, none of which v3 owns:

1. msdata builds them and publishes them in the Direct nupkg.
2. That package's **own** `build/netstandard2.0/Microsoft.Azure.Cosmos.Direct.targets`
   declares `ContentWithTargetPath` items with `CopyToOutputDirectory=PreserveNewest`,
   landing the 5 DLLs in `$(OutputPath)`.
3. [Microsoft.Azure.Cosmos.csproj](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L155-L159)
   re-packs them *from* `$(OutputPath)` into `runtimes/win-x64/native`.
4. [Microsoft.Azure.Cosmos.targets](Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.targets#L29-L54)
   repeats the copy in the customer's build, adding a RID guard the Direct-side
   targets lacks.

**The asset filter is load-bearing.** Hop 2 happens only because the Direct package's
`build` assets are imported, so `compile` and `all` are *not* interchangeable:

| Setting | `lib/` compile ref | `build/` targets | Natives reach `$(OutputPath)`? |
| --- | --- | --- | --- |
| `ExcludeAssets="compile"` | dropped | **imported** | ✅ yes — what `msdata/direct` uses today |
| `ExcludeAssets="all"` | dropped | **not imported** | ❌ **no** — needs explicit copy logic |

`PrivateAssets="All"` only blocks *transitive* flow to downstream consumers; it does
not suppress the targets import for this project.

Three options, in order of preference:

| Option | Description | Trade-off |
| --- | --- | --- |
| **A (recommended)** | Keep a *build-only* `PackageReference` to a slimmed native package (`Microsoft.Azure.Cosmos.ServiceInterop`, native assets only) with `ExcludeAssets="all"` + explicit `GeneratePathProperty` to copy the DLLs. | Cleanest separation; requires the service team to publish a native-only package. Because `all` suppresses the `build` import, the explicit copy target is **mandatory**, not optional. |
| **B** | Keep `Microsoft.Azure.Cosmos.Direct` as a `PrivateAssets="All" ExcludeAssets="compile"` reference purely to source the 5 native DLLs. | Zero service-team work, but "Direct dependency removed" is only true for managed code. Must be `compile`, **not** `all` — see the table above. |
| **C** | Commit the native binaries into the repo under `Microsoft.Azure.Cosmos/runtimes/win-x64/native/`. | Removes the dependency completely; adds ~MBs of binaries to git and a manual update process. Also needs CredScan/binary-policy sign-off, and hop 2 must be replaced by a local `ContentWithTargetPath` block. |

**Failure mode if this is got wrong is silent:** the nupkg simply ships without
natives, `ServiceInteropWrapper.AssembliesExist` goes false, and query planning falls
back to the gateway path at runtime with no build error. Add a pack-time assertion
that the 5 DLLs are present in `runtimes/win-x64/native` rather than relying on tests.

Until D-3 is resolved, `<DirectVersion>` stays in [Directory.Build.props](Directory.Build.props#L6).

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

`tools/msdata-direct/msdata_sync.ps1` must learn the new destination layout. Add a `$fileToFolderMap` table generated from §6 so a copied file lands in the right folder instead of the flat root. Files new on the msdata side that are not in the map should land in a `Direct/_Unsorted/` staging folder and fail the build loudly, forcing an explicit classification.

The rewrite must also fix P-8/P-9/P-10, which requires inverting the script's current logic:

1. **Walk the 13 msdata source directories recursively** and discover every file, rather than iterating what v3 already has (fixes P-8).
2. Look each file up in `$fileToFolderMap` **by file name only**, never by msdata source path, so the v3 taxonomy stays decoupled from msdata's (P2).
3. Copy into the mapped folder including nested destinations, so `Compat/AzureCore/`, `Telemetry/` and `FaultInjection/` are covered like everything else (fixes P-9).
4. Emit a provenance report — msdata source dir → target folder, per file (fixes P-10). This is also the input to the §9.D drift manifest.
5. Map `SharedFiles/Rntbd2/TransportClient.cs` onto `RntbdTransportClient.cs`, replacing today's hard-coded special case.

A machine-readable projection of §6 is generated by
[scratch/Generate-DirectLayout.ps1](scratch/Generate-DirectLayout.ps1) into
[scratch/direct-layout.json](scratch/direct-layout.json) (31 folders, 372 files,
reconciled against `origin/msdata/direct`). Both move under `tools/msdata-direct/` when
this lands. **§6 stays the source of truth** — the JSON is regenerated from it, never
hand-edited, so the two cannot drift.

Also update [docs/sync_up_msdata_direct.md](docs/sync_up_msdata_direct.md), [.github/agents/msdata-direct-sync-agent.agent.md](.github/agents/msdata-direct-sync-agent.agent.md) and [tools/msdata-direct-sync-helper.ps1](tools/msdata-direct-sync-helper.ps1).

### D-8 — Solution & ownership

- Add `Direct` as a solution folder so Solution Explorer shows the new tree.
- Add a `CODEOWNERS` entry for `Microsoft.Azure.Cosmos/src/Direct/**`.
- Add `Microsoft.Azure.Cosmos/src/Direct/README.md` stating the code is mirrored from msdata and must not be hand-edited on `main`.

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

Five delivery phases behind one blocking decision gate. Each phase is one or more PRs,
and every PR must be green in CI on its own (**C-6**).

| Phase | Theme | Gate to exit |
| --- | --- | --- |
| **0** | Decision gate — no code | Three decisions signed off |
| **1** | Files in, folder structure right | Default build byte-identical; opt-in leg green |
| **2** | ServiceInterop / native assets | Natives flow without the managed reference |
| **3** | Compile from source, drop the managed dependency | `main` builds Direct from source |
| **4** | Backward-compatibility gates | All gates wired and green |
| **5** | Decommission and ship | GA on the source-built path |

---

### Phase 0 — Decision gate (**blocking, no code**)

Each answer re-shapes §7, §8 and §9, so none of the later phases can be scoped until
these land.

- [ ] **D-0** — which assembly do the Direct types ship from? Depends on the strong-name
      check (R-8), which should run first because it can eliminate option C outright.
- [ ] **D-3** — where do the native assets come from? If option A, **raise the
      natives-only package ask with the service team now** — it is the longest-lead
      external dependency in this plan and phase 2 cannot close without it.
- [ ] **Ownership (O-1)** — is `main` or msdata upstream? Determines the sync direction,
      `CODEOWNERS`, and which §9.E gate is authoritative in phase 4.

---

### Phase 1 — Land the files and the folder structure

Delivered as **two separate PRs**. Mixing a 373-file re-baseline with a 373-file move
makes the diff unreviewable and destroys `git log --follow`.

**1a — Import, as-is.** Bring `msdata/direct` content onto `main` in the flat
`src/direct/` shape, plus the csproj / `Directory.Build.props` / `.sln` deltas and the
two `RMResources.*` files that land in `src/` (R-13). Land it behind the
`UseDirectSource` switch (below) so the default build stays byte-identical.

**1b — Restructure.** Pure `git mv` into the §5/§6 tree. Zero content edits except the
single `RntbdTransportClient.cs` rename. Add `Direct/README.md`, `Direct/.editorconfig`
carrying the scoped analyzer suppressions (**D-6**), and the `CODEOWNERS` entry.

Also in this phase, because both must precede any edit to the Direct sources:

- [ ] **Capture the Direct contract baseline** — snapshot the *published* 3.44.0 API
      surface into `contracts/DirectSDKAPI.json`. Enforcement is phase 4, but the
      **capture has to happen before the source is touched**, otherwise the baseline
      records post-migration state and proves nothing.
- [ ] **Minimal drift gate** — T0 hashes for the highest-risk files only
      (`ServiceInteropWrapper.cs` plus the wire-format files in §9.B). The full §9.A
      tiering is phase 4; this subset closes the window described below.
- [ ] Opt-in CI leg: `-p:UseDirectSource=true`, full unit + emulator suite.

> ⚠️ **Do not defer the drift gate to phase 4.** Phase 3 makes `main` compile these
> sources; from that moment any PR can diverge a shared file with nothing to stop it.
> The minimal T0 gate above is cheap and closes the phase 3 → 4 window.

**Exit:** default build produces byte-identical artifacts; `-p:UseDirectSource=true`
is green; `git diff --stat` for 1b shows **only** renames (`R100`); `contracts/API_*.txt`
unchanged.

---

### Phase 2 — ServiceInterop and the native assets

Deliberately **before** phase 3: prove the natives still flow while the managed
reference is neutralised but not yet removed. Doing it the other way round means
debugging two changes at once, and the failure mode is silent (R-11).

- [ ] Implement the **D-3** decision (natives-only package / `ExcludeAssets="compile"` /
      committed binaries).
- [ ] **Pack-time assertion** that all 5 native DLLs are present under
      `runtimes/win-x64/native` — an assertion, not a test (R-11).
- [ ] **§9.F axis-D guards:** tier `ServiceInteropWrapper.cs` as T0, record its
      `$(DirectVersion)` coupling, and extend the interop smoke test to exercise each
      P/Invoke entry point with `AssembliesExist == true` so it cannot silently pass via
      the gateway fallback.

> The existing `AssembliesExist` check protects against the native DLL being **absent**,
> not against it being **present but incompatible**. Phase 3 makes the managed half
> editable here while the native half stays pinned, so incompatibility becomes
> reachable for the first time. That is what F-1/F-2/F-3 exist to catch.

**Exit:** produced nupkg carries all 5 natives with the managed compile reference
excluded; interop smoke test green.

---

### Phase 3 — Compile from source, drop the managed dependency

- [ ] Implement the **D-0** decision — under option C, add
      `Microsoft.Azure.Cosmos.Direct.csproj` and reference it from `Client.csproj`.
- [ ] **D-1** — remove the compile reference (or swap it for a `ProjectReference`).
- [ ] **D-2** — collapse the `ProjectRef` dual-mode switch.
- [ ] Add the three package references the Direct sources need (see D-1).
- [ ] Flip `UseDirectSource` to default `true`; keep `false` as an escape hatch.
- [ ] **D-5** — unhook downstream projects (FaultInjection, CTL, Benchmark, 4 test
      projects). Under option C this is a reference swap, not a migration.
- [ ] **D-4** — *only under D-0 option A*: stop packing the restored `Direct.dll` and
      ship the §9.C-1 facade.
- [ ] Validate the nupkg: exactly one `Direct.dll`, natives present, **no duplicate
      types** (P-11).

**Exit:** `main` builds Direct from source; full matrix green (unit, emulator,
multi-region, thin-client, AOT, benchmark, CTL, FaultInjection).

---

### Phase 4 — Backward-compatibility gates

The baseline captured in phase 1 becomes enforced here.

- [ ] **§9.E Gate 1** — enforce the source-built Direct surface against
      `contracts/DirectSDKAPI.json` on every PR. Build this one first.
- [ ] **§9.B wire golden-file tests** — RNTBD token IDs, HTTP header constants,
      `StatusCodes`/`SubStatusCodes` numerics, `OperationType`/`ResourceType` numerics,
      `JSonSerializable` property names.
- [ ] **§9.A full tiering** — extend the phase-1 minimal manifest to all 372 files, plus
      the non-`COSMOSCLIENT` compile canary (A-6) and the vendor-baseline branch (A-7).
- [ ] **§9.E Gates 2 and 3** — cross-repo compiles, **nightly and non-blocking** until
      proven stable.
- [ ] **§9.C-2 ApiCompat** — *only under D-0 option A*; option C satisfies this by
      construction.

**Exit:** §9.D's minimum viable set is green and wired into CI.

---

### Phase 5 — Decommission and ship

- [ ] **D-7** — rewrite the sync tooling: manifest-driven, recursive discovery,
      fail-closed on unmapped files, provenance report.
- [ ] **D-8** — solution folders, `CODEOWNERS`, `Direct/README.md` house rules.
- [ ] **D-9** — retire the four msdata pipelines; **fold the AOT coverage in rather than
      deleting it**.
- [ ] Remove the `UseDirectSource` switch and the `false` path.
- [ ] Retire the `msdata/direct` branch, `docs/sync_up_msdata_direct.md`, the sync agent
      and helper script.
- [ ] Remove `<DirectVersion>` — **only if** D-3 allows; otherwise document why it stays.
- [ ] `changelog.md` entry for the first customer-observable phase.
- [ ] Ship **one preview release** before GA (see Release staging below).

**Exit:** GA release built from source with no regressions.



### Landing safely — the `UseDirectSource` switch

R-1 makes phase 1a effectively a re-baseline off an Oct-2022 merge-base. To keep it
revertible by one property rather than by a revert of 373 files, land the tree
**excluded from the default build**:

```xml
<ItemGroup Condition=" '$(UseDirectSource)' != 'true' ">
  <Compile Remove="Direct/**/*.cs" />
</ItemGroup>
```

Default builds then produce byte-identical artifacts to today, while a new opt-in CI
leg (`-p:UseDirectSource=true`, full unit + emulator suite) proves the source path.
The switch is removed in phase 3 alongside the compile reference.

This also converts **C-6** from a discipline into a mechanism: both build shapes stay
green at every commit because the default shape is not changing at all until phase 3.

### Release staging

Phase 5 is the first customer-observable change under D-0 option A (and, under option
C, the first release whose `Direct.dll` is built here rather than restored). Ship **one
preview release** on the new path before any GA, and hold the `UseDirectSource=false`
escape hatch for one full release cycle after that.

---

## 9. Backward Compatibility & Divergence Strategy

Once `main` compiles the Direct sources directly, a v3 developer can edit a file in a way that is
**incompatible with the msdata copy** — and nothing will stop them. That divergence has three
independent failure modes, each needing its own mechanism.

| Axis | Question | Blast radius |
| --- | --- | --- |
| **A — Source compat with msdata** | Can the file still be shared with the server/backend build? | Next sync becomes a manual merge; drift compounds silently. |
| **B — Wire / serialization compat** | Does the change alter bytes on the wire or JSON on disk? | **Production outage.** Silent, and not caught by any current test. |
| **C — Binary compat for `Direct.dll` consumers** | Can code that bound to `Microsoft.Azure.Cosmos.Direct.dll` still load? | `TypeLoadException` for FaultInjection, CTL, Benchmark and external consumers. |
| **D — Managed/native ABI compat** | Do the `[DllImport]` signatures in `ServiceInteropWrapper.cs` still match the native `ServiceInterop.dll` they bind to? | `EntryPointNotFoundException`, or silent memory corruption. **New failure mode created by this migration** — see §9.F. |

### 9.A — Staying source-compatible with msdata

The rule of thumb: **express divergence, don't fork it.** Ranked by preference.

#### A-1. `#if COSMOSCLIENT` guards — the primary tool (already the idiom)

The v3 build defines `DOCDBCLIENT;COSMOSCLIENT;NETSTANDARD20`; the msdata server build does not
define `COSMOSCLIENT`. The tree already uses this in **137 files / 251 directives**:

```csharp
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class DocumentServiceRequest
```

An "incompatible" change becomes a *guarded* change. The file stays **byte-identical in both repos**,
so `msdata_sync.ps1` keeps working as a plain copy and round-trips cleanly.

Use this whenever the change is additive-for-v3 or behavioural. Prefer it over every other option.

**Limits, stated honestly:** a guard cannot help when both sides need the *same* member with
*different* shape and the server actually calls it — you end up maintaining an `#else` branch that
v3 CI never compiles (see A-6). Guards also rot. Cap their use with the tier policy in A-4.

#### A-2. Partial-class sidecars

Make the type `partial` upstream once, then keep all v3-only additions in a file the sync never touches:

```
Direct/Transport/Rntbd/Channel.cs                 <- mirrored, never hand-edited
Direct/Extensions/Channel.Client.partial.cs       <- v3-only, outside the sync map
```

Zero drift in the mirrored file, and the addition is reviewable in isolation. Best for **added
members**. `NativeMethods.*` already demonstrates the pattern in-tree.

#### A-3. Seam interfaces — the established in-repo precedent

`IStoreModelExtension`, `IAddressResolverExtension`, `IServiceConfigurationReaderExtension` and
`IChaosInterceptor` all exist precisely so v3 (and FaultInjection) can extend Direct behaviour
without editing Direct types. When a change is "v3 needs different behaviour here", the first
question should be *"can this be a seam?"* — add a narrow interface upstream, implement it in
`src/` (not `src/Direct/`). Cost is one small upstream PR; benefit is permanent zero drift.

Related no-drift options: extension methods in a `src/`-side static class, or an `internal` decorator
wrapping the Direct type.

#### A-4. Tier the files — not all 372 are equal

Divergence policy should depend on who consumes the file. Record the tier in the sync manifest (A-5):

| Tier | Files | Policy |
| --- | --- | --- |
| **T0 — Lockstep / wire contract** | `RntbdConstants.cs`, `HeadersTransportSerialization.tt`, `HttpConstants.cs`, `WFConstants.cs`, `StatusCodes.cs`, `ResourceType.cs`, `OperationType.cs`, `PartitionKeyInternal*.cs`, `MurmurHash.cs`, `ResourceId.cs`, `Range.cs` | **Byte-identical to msdata. No exceptions.** CI hash gate. Changes originate upstream only. |
| **T1 — Shared implementation** | Transport, Store, Routing, Retry, Session, Diagnostics | Divergence allowed **only** via A-1/A-2/A-3, and only with a manifest drift entry naming an owner + backport issue. |
| **T2 — Client-only** | `ServiceModel/Client/`, `Telemetry/`, `Compat/`, `FaultInjection/`, most of `Resources/Settings` | v3 owns them. Drop from the sync map entirely — they stop being "shared files". |

Tiering is what turns "the whole folder is untouchable" into "1 in 4 files is untouchable", which is
the difference between a policy people follow and one they route around.

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

A `verify-direct-drift` CI job rehashes every file and fails when a T0/T1 file changed without a
matching `drift` record. This is the mechanism that makes divergence *loud* — everything else is
convention.

#### A-6. Compile canary for the non-`COSMOSCLIENT` branch

**Today, every `#if !COSMOSCLIENT` block in the repo is dead code that is never compiled by v3 CI.**
It can rot for months and only explode at the next sync. Fix: add a *build-only* project

```
Microsoft.Azure.Cosmos/tests/Direct.ServerShape.Canary/Direct.ServerShape.Canary.csproj
```

that globs the same `src/Direct/**/*.cs` with `COSMOSCLIENT` **not** defined, and is only required
to compile (never run, never packed). Server-only types it can't resolve get thin stubs under
`Canary/Stubs/`. Even a partial canary catches the common case — a v3 developer editing a shared
method and breaking the `#else` arm.

Start it as `ContinueOnError` and ratchet.

#### A-7. Sync becomes a 3-way merge, not a copy

`msdata_sync.ps1` today is `Copy-Item -Force` — it silently destroys local changes. Two ways out:

- **Vendor baseline branch (recommended for sustained drift).** Keep a `direct-upstream` branch that
  only ever receives verbatim msdata copies. Sync = commit to `direct-upstream`, then `git merge` it
  into the feature branch. Git does a real 3-way merge and surfaces conflicts on exactly the diverged
  hunks. Same shape as `git subtree`.
- **Patch queue (recommended if drift should stay small).** Store local deltas as `.patch` files under
  `src/Direct/.sync/patches/`. Sync = copy verbatim, then re-apply the queue. A patch that no longer
  applies is a loud, actionable signal, and the queue's size is a visible drift budget.

#### A-8. Explicit fork — the escape hatch

When a file genuinely must diverge permanently, **move it out of `src/Direct/`** into `src/` proper
and delete it from the sync map. An explicit fork with a one-line "forked from msdata `<path>` at
`<sha>` because `<reason>`" header beats indefinite silent drift. Requires the manifest entry to be
retired, so it shows up in review.

### 9.B — Protecting wire & serialization compatibility

Axis B is the one that causes outages, and no mechanism above catches it on its own — a T0 hash gate
only helps if the file was correctly tiered. Add **behavioural golden-file tests** that fail
independently of the sync process:

- RNTBD token IDs / types snapshotted from `RntbdConstants` + `HeadersTransportSerialization.tt`.
- HTTP header name constants (`HttpConstants`, `WFConstants`).
- `StatusCodes` / `SubStatusCodes` numeric values.
- `OperationType` / `ResourceType` numeric values (they are serialized).
- JSON property names for every `JSonSerializable`-derived type in `Resources/` and `Resources/Settings/`
  (reflect over `[JsonProperty]` / constant name fields, compare to a checked-in baseline).

Each is a cheap reflection test over a `.baseline.txt` file. Updating a baseline is then a
deliberate, reviewable act — which is exactly the property that's missing today.

### 9.C — Binary compatibility for `Direct.dll` consumers

Relevant the moment D-4 lands (`Microsoft.Azure.Cosmos.Direct.dll` stops being packed).

- **C-1 — Type-forwarding facade.** Ship a `Microsoft.Azure.Cosmos.Direct.dll` containing only
  `[assembly: TypeForwardedTo(typeof(...))]` for every previously-public `Microsoft.Azure.Documents.*`
  type, forwarding into `Microsoft.Azure.Cosmos.Client`. Must be built from *this* repo (otherwise the
  dependency is circular) and must keep the **same strong-name identity** — the public keys are already
  in `Direct/AssemblyKeys.cs`. Keep it for at least two minor versions, then remove with a
  `Breaking Changes` changelog entry.
- **C-2 — ApiCompat baseline.** Run `Microsoft.DotNet.ApiCompat` between the last shipped
  `Microsoft.Azure.Cosmos.Direct.dll` (3.43.2) and the new `Microsoft.Azure.Cosmos.Client.dll`,
  restricted to the `Microsoft.Azure.Documents.*` surface. Check in the suppression baseline; any
  *new* break then requires an explicit baseline update in the PR.
- **C-3 — `InternalsVisibleTo` audit.** Enumerate every assembly granted access via `AssemblyKeys.cs`
  before removing the Direct assembly; each one is a consumer that must be migrated or facaded.
- **C-4 — Binding redirects.** .NET Framework consumers with an existing `assemblyBinding` entry for
  `Microsoft.Azure.Cosmos.Direct` need guidance in the release notes.

### 9.D — Recommended minimum viable set

If only some of this gets built, build these five — they cover the failure modes that are both
*likely* and *silent*:

1. **A-4 tiering + A-5 manifest with a T0 hash gate** — makes drift impossible to land accidentally.
2. **B golden-file tests** — the only defence against a wire break.
3. **A-6 compile canary** — stops the `#else` arms rotting.
4. **A-7 vendor-baseline branch** — makes the *next* sync a merge instead of a data-loss event.
5. **F-1 — tier `ServiceInteropWrapper.cs` as T0** (§9.F). A P/Invoke signature drift compiles cleanly and fails at runtime; this is the cheapest possible guard against it.

A-1/A-2/A-3 need no tooling at all; they just need to be written down as the house rules in
`src/Direct/README.md`.

### 9.E — Cross-repo compile gates

§9.A–9.D make divergence *discouraged and detectable inside this repo*. They cannot tell
you whether a v3-side edit still builds on the msdata side. That needs three gates, only
the first of which is in-repo.

| Gate | What it does | Cost | Catches |
| --- | --- | --- | --- |
| **1 — Surface baseline** *(in-repo, every PR)* | Snapshot the Direct 3.44.0 API surface into `contracts/DirectSDKAPI.json` using the existing [ContractEnforcement](Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Contracts/ContractEnforcement.cs) machinery, then diff the source-built assembly against it | Low — no cross-repo access | Accidental surface changes to Direct types made from v3. Subsumes §9.C-2 |
| **2 — v3 source built inside msdata** *(nightly)* | Drop `src/Direct/**` into the msdata CosmosDB build, compile, run msdata's Direct tests | Needs msdata agents + a cross-org service connection | v3-side edits that break the backend build or behaviour — the Axis A failure §9.A only *discourages* |
| **3 — msdata source built inside v3** *(nightly)* | Pull msdata's current Direct source, build v3 against it, run v3's suite, report drift as a file-level diff | Same | msdata-side changes v3 has not absorbed; keeps the sync backlog continuously visible |

**Build Gate 1 first.** It is entirely in-repo, reuses tooling that already exists, and
is independently valuable even if the migration stalls — a committed Direct surface
baseline gives drift detection today. It mirrors the proven NuGet-surface parity
pattern in [templates/build-preview.yml](templates/build-preview.yml#L87-L124), where a
parity build turns a runtime `TypeLoadException` into a build-time `CS0534`.

**Keep Gates 2 and 3 nightly and non-blocking until proven stable.** A flaky cross-org
gate that blocks every v3 PR is worse than no gate.

**Which gate is authoritative depends on the ownership answer** (§8 decision gate):
under v3-primary, Gate 2 is the real gate and Gate 3 is informational; under
msdata-primary it is the reverse; under dual-write both must block — which is precisely
why dual-write is the expensive option.

### 9.F — Managed/native ABI compatibility (Axis D)

**This migration creates a failure mode that does not exist today.** `ServiceInteropWrapper.cs`
is the *managed half* of the query-plan interop layer — it carries **12 `[DllImport]`
declarations** (6 entry points × 2 DLL names, `Microsoft.Azure.Cosmos.ServiceInterop.dll`
plus the legacy `Microsoft.Azure.Documents.ServiceInterop.dll`) with
`CallingConvention.Cdecl` and explicit marshalling attributes.

After this migration the two halves have **different release cadences**:

| Half | Where it comes from | Who can change it |
| --- | --- | --- |
| Managed — `Direct/Interop/ServiceInteropWrapper.cs` | **Source in this repo** | any v3 PR |
| Native — `Microsoft.Azure.Cosmos.ServiceInterop.dll` | Pinned `$(DirectVersion)` package (D-3) | msdata only |

Today both ship in the same nupkg and move together, so they cannot disagree. Once the
managed half is editable here, three new ways to break it appear:

1. A v3 PR edits a P/Invoke signature, struct layout or marshalling attribute →
   `EntryPointNotFoundException` on the happy path, or **silent memory corruption** if
   the entry point still resolves but the layout shifted.
2. msdata changes a native export and v3 syncs the managed side **without** bumping
   `$(DirectVersion)` — the two are now versioned independently, so nothing forces them
   to move together.
3. `$(DirectVersion)` is bumped for an unrelated reason and the native exports changed
   underneath an unmodified managed wrapper.

None of these are caught by §9.B (wire format), §9.C (assembly identity) or §9.E
(compilation). A signature mismatch **compiles perfectly**.

**Mitigations, cheapest first:**

- **F-1 — Tier `ServiceInteropWrapper.cs` as T0** in the §9.A-4/A-5 manifest, so any
  content change trips the hash gate and forces an explicit `drift` record. This is the
  single highest-value item and costs nothing beyond correct tiering.
- **F-2 — Pin the coupling explicitly.** Record the `$(DirectVersion)` that the current
  `ServiceInteropWrapper.cs` was synced against, and fail the build if the wrapper
  changes without the version moving (or vice versa). Under D-3 option A the natives get
  their own package version — pin against that instead.
- **F-3 — Smoke test the interop path in CI.** A single query that requires a
  ServiceInterop-generated plan, asserting `ServiceInteropWrapper.AssembliesExist` is
  true first so the test cannot silently pass via the gateway fallback.
  [SmokeTests](Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/SmokeTests.cs#L50)
  already asserts `AssembliesExist`; extend it to exercise each entry point.
- **F-4 — Treat the wrapper as `#if COSMOSCLIENT`-free.** It is a mirrored file with a
  hard external contract; divergence via guards is not appropriate here. State this in
  `src/Direct/README.md`.

---

## 10. Risks & Open Questions

| ID | Item |
| --- | --- |
| R-1 | Phase 1a is not a normal merge. `merge-base(origin/main, msdata/direct)` is `f5effced7` (Oct 2022). Plan for a **re-baseline**: refresh `msdata/direct` from current `main` first, then bring the result over. |
| R-2 | `git mv` of 373 files in one commit — verify rename detection survives (`git log --follow`, `git blame`). Use `git config diff.renameLimit` high enough. |
| R-3 | 55 filename collisions with `src/**` remain after the move (different folders now, so tolerable, but "go to file" is still noisy). Consider whether any Direct file should keep a disambiguating name. |
| R-4 | `TreatWarningsAsErrors=true` + un-suppressing 26 rules (phase 1b, D-6) may cascade. Budget for it. |
| R-5 | `Microsoft.HybridRow` is a separate remaining binary dependency — not covered by this plan. |
| R-6 | **Silent drift.** The moment `main` compiles these sources, any PR can diverge a shared file with nothing to stop it. §9.D is the mitigation and should land alongside phase 1, not at the end. |
| R-7 | Every `#if !COSMOSCLIENT` block (137 files carry `COSMOSCLIENT` directives) is currently **never compiled by v3 CI**. It can rot undetected until the next sync. Mitigation: §9.A-6 canary. |
| R-8 | **Strong-name identity is assumed, not verified.** Nobody has confirmed that delay-signing with [35MSSharedLib1024.snk](35MSSharedLib1024.snk) yields the same public key token as the shipped `Direct.dll`. If it does not, **both** D-0 option C and the §9.C-1 facade are non-viable. Verify in the decision gate, before any code moves. |
| R-9 | Cross-org gates (§9.E gates 2–3) depend on msdata agents and a service connection. Made blocking too early, a flaky gate stalls every v3 PR. Keep nightly until proven. |
| R-10 | Under D-0 option A the `Microsoft.Azure.Documents.*` public surface merges into `Microsoft.Azure.Cosmos.Client.dll`, which would churn `contracts/API_*.txt` and **violate C-2** unless every such type is `internal`. Option C avoids this entirely. |
| R-11 | Getting the D-3 asset filter wrong fails **silently** — the nupkg ships without natives, `ServiceInteropWrapper.AssembliesExist` goes false, and query planning falls back to the gateway with no build error. Mitigation: pack-time assertion (§11), not a test. |
| R-12 | **Managed/native ABI split-brain.** After this migration `ServiceInteropWrapper.cs` (12 `[DllImport]`s) is editable in this repo while the native DLL stays pinned to `$(DirectVersion)`. The two halves ship together today and cannot disagree; afterwards they can. A mismatch compiles cleanly and fails at runtime. Mitigation: §9.F. |
| R-13 | `RMResources.Designer.cs` / `RMResources.resx` are msdata-sourced, land in `src/` rather than `src/direct/`, and are **absent from `main`**. Easy to miss in phase 0 because they sit outside the §6 mapping. |
| O-1 | **Open — decision gate item 1.** After this lands, is `main` the source of truth or is msdata still upstream? Governs `Direct/README.md`, CODEOWNERS, §9.A-7's vendor-baseline direction and which §9.E gate is authoritative. |
| O-2 | **Open — decision gate item 3.** D-3: which native-asset option (A/B/C)? Option A requires a service-team ask; raise it first. |
| O-3 | Superseded by **D-0**. Under option C no facade is needed; under option A it is mandatory and §9.C-1 sets the terms. |
| O-4 | Superseded by **D-0**, which recommends option C (a separate `Microsoft.Azure.Cosmos.Direct.csproj` preserving the existing assembly identity). Earlier drafts assumed the opposite — compiling into `Microsoft.Azure.Cosmos.Client.dll`. |

---

## 11. Validation Checklist

- [ ] `dotnet build Microsoft.Azure.Cosmos.sln -c Release` succeeds with `TreatWarningsAsErrors=true`.
- [ ] `dotnet test` — unit tests, then emulator tests.
- [ ] `git diff --stat` for phase 1b shows **only** renames (`R100`), no content deltas.
- [ ] `contracts/API_*.txt` baseline unchanged (`UpdateContracts.ps1` produces no diff).
- [ ] Produced nupkg contains `runtimes/win-x64/native/{Microsoft.Azure.Cosmos.ServiceInterop,Cosmos.CRTCompat,msvcp140,vcruntime140,vcruntime140_1}.dll`.
- [ ] Produced nupkg `lib/netstandard2.0` contents reviewed against the previous release.
- [ ] `grep -r "Microsoft.Azure.Cosmos.Direct"` across `*.csproj`, `*.props`, `*.yml` returns only intentionally-retained hits.
- [ ] `msdata_sync.ps1` dry-run places every msdata file into a mapped folder (no `_Unsorted/` residue).
- [ ] `verify-direct-drift` passes: no T0/T1 file diverges from its manifest hash without a `drift` record.
- [ ] Wire golden-file tests pass unchanged (RNTBD tokens, header names, status/sub-status values, `OperationType`/`ResourceType` numerics, `JSonSerializable` property names).
- [ ] Non-`COSMOSCLIENT` compile canary builds.
- [ ] ApiCompat against `Microsoft.Azure.Cosmos.Direct` 3.43.2 reports no unsuppressed breaks on the `Microsoft.Azure.Documents.*` surface.
- [ ] Changelog entry added for the phase that first becomes customer-observable (phase 5 at the latest).
- [ ] **Strong-name public key token** of the source-built Direct assembly matches `Microsoft.Azure.Cosmos.Direct` 3.44.0 (gates D-0 option C and §9.C-1 — R-8).
- [ ] Produced nupkg does **not** contain `Direct.dll` and the same types compiled into `Client.dll` (P-11).
- [ ] `-p:UseDirectSource=false` still produces byte-identical artifacts to the previous release, until phase 3 removes the switch.
- [ ] Gate 1 (§9.E) green: source-built Direct surface matches `contracts/DirectSDKAPI.json`.
- [ ] One preview release shipped on the source-built path before GA.
- [ ] `RMResources.Designer.cs` and `RMResources.resx` present in `Microsoft.Azure.Cosmos/src/` (R-13).
- [ ] ServiceInterop smoke test passes with `AssembliesExist == true`, exercising each P/Invoke entry point (§9.F F-3) — it must not silently pass via the gateway fallback.
- [ ] `ServiceInteropWrapper.cs` is tiered T0 in the drift manifest and its `$(DirectVersion)` coupling is recorded (§9.F F-1/F-2).

---

## 12. Immediate Next Steps

In order. Steps 1–3 are **phase 0**, the decision gate — mostly investigation, not code.

1. **Raise the natives-only package ask** with the Direct publishing owner (D-3 option A).
   Longest-lead external dependency in this plan — start the clock even if the answer
   later turns out to be option B. Phase 2 cannot close without it.
2. **Verify the strong-name public key token** (R-8). A few hours, and it decides
   whether D-0 option C is available at all.
3. **Book the ownership decision** (O-1) with the msdata/CosmosDB owners. §9.E gates 2
   and 3 cannot be designed until this lands.
4. **Capture `contracts/DirectSDKAPI.json`** from the *published* 3.44.0 package. This is
   phase 1 work but can start immediately, and it is independently valuable: even if the
   migration stalls, a committed Direct surface baseline gives drift detection against
   the shipped package starting today. It must be captured **before** any Direct source
   is edited, or the baseline records post-migration state and proves nothing.
5. Only then open phase 1.

---

## 13. Document History

| Date | Change |
| --- | --- |
| 2026-09-15 | Merged the separate migration plan into this document. Added D-0 (assembly identity, resolving O-4), §9.E (cross-repo gates), the §8 decision gate and `UseDirectSource` switch, P-11/P-12, R-8…R-11, and §12. D-3 resolved with the verified native-asset chain and the `ExcludeAssets` correction. This file is now the single source of truth. |
| 2026-09-15 | Added §9.F (managed/native ABI, axis D), D-9 (retire the msdata pipelines), the `RMResources.*` files that land outside the mapped tree, and R-12/R-13. |
| 2026-09-15 | Restructured §8 from nine phases into **five delivery phases behind one decision gate**, grouped by theme rather than by decision ID. ServiceInterop moved ahead of the source-compile switch so native delivery is proven independently. Contract-baseline capture and a minimal T0 drift gate pulled forward into phase 1, since both must precede any edit to the Direct sources. |
