Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="1.0.0-preview09"/> [1.0.0-preview09](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview09) - Unreleased

#### Added
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds opt-in stream-mode JSON processing for encryption feed iterators (query, LINQ, change-feed) on `net8.0`. Consumers opt in per-call via `RequestOptions.Properties["encryption-json-processor"]` or per-container via the new extension method `EncryptionContainerExtensions.UseStreamingJsonProcessingByDefault(Container)`. The new path decrypts each feed item lazily into a pooled `ArrayPool<byte>` buffer and is targeted at hot-path workloads that need to reduce per-document allocations. Default remains Newtonsoft; existing callers see no behavioral change.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds `DecryptableItem.DisposeAsync()` and makes `DecryptableItem` implement `IAsyncDisposable`. Stream-mode `DecryptableItem` instances hold a rented `ArrayPool<byte>` buffer that callers MUST dispose to return to the pool and clear plaintext residue. Existing `DecryptableItemCore` (Newtonsoft path) inherits a no-op default implementation, so existing callers are unaffected.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) `FeedResponse<DecryptableItem>` returned by stream-mode feed iterators implements `IAsyncDisposable` at runtime and cascades disposal to every item in the page. Callers that obtain a `FeedResponse<DecryptableItem>` page MUST cast it to `IAsyncDisposable` and dispose it (typically in a `finally` block) so that items the caller skipped or never enumerated still release their pooled buffers. See the example on `DecryptableItem` for the recommended pattern.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds distributed-cache (`IDistributedCache`) support to the DEK properties cache. When the in-process cache entry expires, the next request consults the distributed cache before hitting Cosmos metadata, allowing a peer-populated entry to rescue the request during transient metadata unavailability. Adds optional proactive background refresh, cross-process cache-key prefix scoping, and format-version-scoped cache keys for rolling-upgrade safety.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `DekCacheOptions` and a new constructor overload (`CosmosDataEncryptionKeyProvider(EncryptionKeyStoreProvider, DekCacheOptions)`) so future cache settings can be added as properties on the options bag without further constructor-overload churn. For hybrid callers that still need `EncryptionKeyWrapProvider` alongside `EncryptionKeyStoreProvider` (e.g. legacy-algorithm migration), adds the static factory `CosmosDataEncryptionKeyProvider.Create(EncryptionKeyWrapProvider, EncryptionKeyStoreProvider, DekCacheOptions)`; a factory is used instead of an additional constructor to avoid `null`-literal overload ambiguity with the obsolete dual-provider constructor.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `IDisposable` and `IAsyncDisposable` to `CosmosDataEncryptionKeyProvider`. Disposal cancels and best-effort drains in-flight fire-and-forget distributed-cache writes (5-second bounded wait). Repeated calls to the same disposal method (`Dispose` or `DisposeAsync`) are idempotent; interleaving `Dispose` with `DisposeAsync` on the same instance is not supported (matches the public XML remarks). The provider does NOT dispose externally-supplied dependencies (`IDistributedCache`, `EncryptionKeyWrapProvider`, `EncryptionKeyStoreProvider`, `Container`) — caller owns those lifetimes. User-initiated `RemoveAsync` invalidations are not interrupted by disposal so the distributed cache cannot end up with stale entries.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `EncryptionCustomEventSource` (named `Azure-Cosmos-Encryption-Custom`) for Release-visible best-effort failure diagnostics on the optional distributed-cache integration. Surfaces L2 read / write / background-write / remove failures at `EventLevel.Warning`. Auto-discovered by `Azure.Core.Diagnostics.AzureEventSourceListener` and `dotnet-trace --providers Azure-Cosmos-Encryption-Custom`. Activity-tag diagnostics on the existing `Microsoft.Azure.Cosmos.Encryption.Custom` `ActivitySource` remain the primary correlation channel.

#### Fixes
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Cold-miss and forced-refresh distributed-cache writes (`FetchFromSourceAndUpdateCachesAsync`) are now consistently fire-and-forget via the same shared helper used by `SetDekProperties`. Callers no longer pay the L2 round-trip on the cold path, and the request's `CancellationToken` no longer aborts an in-flight L2 hydration.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Replaces four `Debug.WriteLine` calls with `EncryptionCustomEventSource` warnings. `Debug.WriteLine` is `[Conditional("DEBUG")]` and produced no output in shipped Release builds, so distributed-cache failures were silent in production.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Collapses the duplicate `ActivitySource` introduced for `DekCache` (`Microsoft.Azure.Cosmos.Encryption.Custom.DekCache`) back into the package-level source `Microsoft.Azure.Cosmos.Encryption.Custom`. Subscribers using `AddSource("Microsoft.Azure.Cosmos.Encryption.Custom")` no longer silently miss DEK cache spans.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) `RawDekCache` is now invalidated on every refresh of `DekPropertiesCache` (cold-source path and `RemoveAsync`) under both candidate keys (the `dekId` used by `SetRawDek` and the `SelfLink` used by `GetOrAddRawDekAsync`). Prevents serving a raw DEK derived from previous wrapped key bytes after a rewrap. `RemoveAsync` now invalidates the raw entry unconditionally rather than only when the local DEK properties entry is present.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) `RemoveAsync` now forwards the caller's `CancellationToken` to `IDistributedCache.RemoveAsync`.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `ConfigureAwait(false)` on all asynchronous awaits inside `DekCache` to reduce sync-context-capture deadlock risk for callers that bridge sync-over-async.

#### Breaking changes
- `DekCacheOptions` restructured: the three flat distributed-cache properties (`DistributedCache`, `DistributedCacheKeyPrefix`, `DistributedCacheEntryLifetime`) are replaced by a single nested `DistributedCacheOptions` instance on `DekCacheOptions.DistributedCache`. The nested type carries `Cache`, `KeyPrefix`, and `EntryLifetime`. A non-null nested instance enables L2; `null` disables it. Migration:
  ```csharp
  // before
  new DekCacheOptions { DistributedCache = cache, DistributedCacheKeyPrefix = "x", DistributedCacheEntryLifetime = TimeSpan.FromHours(2) }
  // after
  new DekCacheOptions { DistributedCache = new DistributedCacheOptions { Cache = cache, KeyPrefix = "x", EntryLifetime = TimeSpan.FromHours(2) } }
  ```

#### Updates
- Replaces the package's `Microsoft.Extensions.Caching.Memory` reference (previously `3.1.7` on `netstandard2.0` / `1.1.2` on `net46`) with `Microsoft.Extensions.Caching.Abstractions` `3.1.7`, unified across TFMs. The library consumes only `IDistributedCache`; the `MemoryCache` reference was dead. Consumers transitively depending on `Microsoft.Extensions.Caching.Memory` types **through this package** must add a direct reference. Consumers using only `IDistributedCache` are unaffected. The `Abstractions` floor stays at the lowest version the new API surface compiles against, so consumers remain free to unify upward to any LTS.

#### Notes
- The optional distributed cache stores wrapped (encrypted) DEK **properties** only. Raw (unwrapped) DEK material remains process-local for security and is never written to `IDistributedCache`.
- When configuring a distributed cache, ensure the cache infrastructure uses encryption in transit (TLS) and encryption at rest.

### <a name="1.0.0-preview08"/> [1.0.0-preview08](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview08) - 2024-09-11

#### Updates
- [#4673]: Updates `Microsoft.Data.Encryption.Cryptography` dependency to v1.2.0.

### <a name="1.0.0-preview07"/> [1.0.0-preview07](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview07) - 2024-06-12

#### Fixes 
- [#4546](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4546) Updates package reference Microsoft.Azure.Cosmos to version 3.41.0-preview and 3.40.0 for preview and stable version support.

### <a name="1.0.0-preview06"/> [1.0.0-preview06](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview06) - 2023-06-28

#### Fixes 
- [#3956](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3956) Updates package reference Microsoft.Azure.Cosmos to version 3.35.1-preview.

### <a name="1.0.0-preview05"/> [1.0.0-preview05](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview05) - 2023-04-27

#### Fixes 
- [#3809](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3809) Adds api FetchDataEncryptionKeyWithoutRawKeyAsync and FetchDataEncryptionKey to get DEK without and with raw key respectively.

### <a name="1.0.0-preview04"/> [1.0.0-preview04](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview04) - 2022-08-16

#### Fixes 
- [#3386](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3386) Fixes custom serializer issue with DataEncryptionKeyContainer operations.

### <a name="1.0.0-preview03"/> [1.0.0-preview03](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview03) - 2022-04-15
- [#3145](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/3145) Adds dependency on latest Microsoft.Azure.Cosmos preview (3.26.0-preview).

### <a name="1.0.0-preview02"/> [1.0.0-preview02](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview02) - 2021-10-29

#### Fixes 
- [#2834](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/2834) Adds fix for deserialization issue for invalid date type.


### <a name="1.0.0-preview"/> [1.0.0-preview](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview) - 2021-10-20
- First preview of custom client-side encryption feature. See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
