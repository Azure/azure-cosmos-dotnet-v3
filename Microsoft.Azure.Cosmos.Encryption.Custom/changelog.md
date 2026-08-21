Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="1.1.0-preview01"/> [1.1.0-preview01](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.1.0-preview01) - Unreleased

#### Added
- [#4766](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4766) Adds a `net8.0` target alongside the existing `netstandard2.0`. The `net8.0` build is what enables the opt-in System.Text.Json stream processor and its `IAsyncDisposable` surface; `netstandard2.0` consumers are unaffected.
- [#5423](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5423) Adds `CosmosDataEncryptionKeyProvider.Initialize(Container)`, a synchronous counterpart to `InitializeAsync(Container)` for binding the key container.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds opt-in stream-mode JSON processing for encryption feed iterators (query, LINQ, change-feed) on `net8.0`. Consumers opt in per-call via `RequestOptions.Properties["encryption-json-processor"]` or per-container via the new extension method `EncryptionContainerExtensions.UseStreamingJsonProcessingByDefault(Container)`. The new path decrypts each feed item lazily into a pooled `ArrayPool<byte>` buffer and is targeted at hot-path workloads that need to reduce per-document allocations. Default remains Newtonsoft; existing callers see no behavioral change.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds `DecryptableItem.DisposeAsync()` and makes `DecryptableItem` implement `IAsyncDisposable`. Stream-mode `DecryptableItem` instances hold a rented `ArrayPool<byte>` buffer that callers MUST dispose to return to the pool and clear plaintext residue. Existing `DecryptableItemCore` (Newtonsoft path) inherits a no-op default implementation, so existing callers are unaffected.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) `FeedResponse<DecryptableItem>` returned by stream-mode feed iterators implements `IAsyncDisposable` at runtime and cascades disposal to every item in the page. The cascade is best-effort: a single throwing item no longer strands the rented buffers of its peers (failures are surfaced as the original exception when only one item throws, or aggregated into an `AggregateException` when multiple do). Callers that obtain a `FeedResponse<DecryptableItem>` page MUST cast it to `IAsyncDisposable` and dispose it (typically in a `finally` block) so that items the caller skipped or never enumerated still release their pooled buffers. See the example on `DecryptableItem` for the recommended pattern.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds distributed-cache (`IDistributedCache`) support to the DEK properties cache. When the in-process cache entry expires, the next request consults the distributed cache before hitting Cosmos metadata, allowing a peer-populated entry to rescue the request during transient metadata unavailability. Adds optional proactive background refresh, cross-process cache-key prefix scoping, and format-version-scoped cache keys for rolling-upgrade safety.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `DekCacheOptions` and a new constructor overload (`CosmosDataEncryptionKeyProvider(EncryptionKeyStoreProvider, DekCacheOptions)`) so future cache settings can be added as properties on the options bag without further constructor-overload churn. For hybrid callers that still need `EncryptionKeyWrapProvider` alongside `EncryptionKeyStoreProvider` (e.g. legacy-algorithm migration), adds the static factory `CosmosDataEncryptionKeyProvider.Create(EncryptionKeyWrapProvider, EncryptionKeyStoreProvider, DekCacheOptions)`; a factory is used instead of an additional constructor to avoid `null`-literal overload ambiguity with the obsolete dual-provider constructor.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `IDisposable` and `IAsyncDisposable` to `CosmosDataEncryptionKeyProvider`. Disposal cancels and best-effort drains in-flight fire-and-forget distributed-cache writes (5-second bounded wait). Repeated calls to the same disposal method (`Dispose` or `DisposeAsync`) are idempotent; interleaving `Dispose` with `DisposeAsync` on the same instance is not supported (matches the public XML remarks). The provider does NOT dispose externally-supplied dependencies (`IDistributedCache`, `EncryptionKeyWrapProvider`, `EncryptionKeyStoreProvider`, `Container`) — caller owns those lifetimes. User-initiated `RemoveAsync` invalidations are not interrupted by disposal so the distributed cache cannot end up with stale entries.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `EncryptionCustomEventSource` (named `Azure-Cosmos-Encryption-Custom`) for Release-visible best-effort failure diagnostics on the optional distributed-cache integration. Surfaces L2 read / write / background-write / remove failures at `EventLevel.Warning`. Auto-discovered by `Azure.Core.Diagnostics.AzureEventSourceListener` and `dotnet-trace --providers Azure-Cosmos-Encryption-Custom`. Activity-tag diagnostics on the existing `Microsoft.Azure.Cosmos.Encryption.Custom` `ActivitySource` remain the primary correlation channel.

#### Fixes
- `EncryptableItem` create, replace, and upsert operations now preserve successful responses when content-on-write is disabled instead of dereferencing the absent response body.
- [#6009](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6009) Legacy AEAD authentication-tag verification now compares the full tag in constant time instead of returning on the first differing byte.
- [#6009](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/6009) Malformed encrypted metadata with a missing DEK id now preserves the underlying decrypt failure in `EncryptionException` instead of masking it with an `ArgumentNullException`.

#### Updates
- Stable builds depend on `Microsoft.Azure.Cosmos` `3.60.0` or later; preview builds retain `3.41.0-preview.0`.
- [#4753](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4753), [#5418](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5418) Updates `Microsoft.Data.Encryption.Cryptography` to `2.0.0-pre015` — a major bump from the `0.2.0-pre` referenced by `1.0.0-preview07` — and moves the internal MDE crypto calls to their async equivalents; `System.Threading.Tasks.Extensions` moves to `4.6.3`. Consumers that reference MDE directly should align to the 2.0 line to avoid a version conflict.
- [#4819](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/4819) Removes the direct `Azure.Core` package reference; it is still supplied transitively via `Azure.Identity`.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Removes the unused `System.Text.RegularExpressions 4.3.1` `PackageReference` from `Microsoft.Azure.Cosmos.Encryption.Custom`. The dependency is no longer consumed by any source file in the package and was carried purely as a stale reference. Consumers that transitively depend on `System.Text.RegularExpressions` **through this package** must add a direct reference; this package's surface is unaffected.
- Replaces the package's `Microsoft.Extensions.Caching.Memory` reference (previously `3.1.7` on `netstandard2.0` / `1.1.2` on `net46`) with `Microsoft.Extensions.Caching.Abstractions` `3.1.7`, unified across TFMs. The library consumes only `IDistributedCache`; the `MemoryCache` reference was dead. Consumers transitively depending on `Microsoft.Extensions.Caching.Memory` types **through this package** must add a direct reference. Consumers using only `IDistributedCache` are unaffected. The `Abstractions` floor stays at the lowest version the new API surface compiles against, so consumers remain free to unify upward to any LTS.

#### Notes
- The optional distributed cache stores wrapped (encrypted) DEK **properties** only. Raw (unwrapped) DEK material remains process-local for security and is never written to `IDistributedCache`.
- When configuring a distributed cache, ensure the cache infrastructure uses encryption in transit (TLS) and encryption at rest.

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
