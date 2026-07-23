Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="1.0.0-preview09"/> [1.0.0-preview09](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.0.0-preview09) - Unreleased

#### Added
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds opt-in stream-mode JSON processing for encryption feed iterators (query, LINQ, change-feed) on `net8.0`. Consumers opt in per-call via `RequestOptions.Properties["encryption-json-processor"]` or per-container via the new extension method `EncryptionContainerExtensions.UseStreamingJsonProcessingByDefault(Container)`. The new path decrypts each feed item lazily into a pooled `ArrayPool<byte>` buffer and is targeted at hot-path workloads that need to reduce per-document allocations. Default remains Newtonsoft; existing callers see no behavioral change.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Adds `DecryptableItem.DisposeAsync()` and makes `DecryptableItem` implement `IAsyncDisposable`. Stream-mode `DecryptableItem` instances hold a rented `ArrayPool<byte>` buffer that callers MUST dispose to return to the pool and clear plaintext residue. Existing `DecryptableItemCore` (Newtonsoft path) inherits a no-op default implementation, so existing callers are unaffected.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) `FeedResponse<DecryptableItem>` returned by stream-mode feed iterators implements `IAsyncDisposable` at runtime and cascades disposal to every item in the page. The cascade is best-effort: a single throwing item no longer strands the rented buffers of its peers (failures are surfaced as the original exception when only one item throws, or aggregated into an `AggregateException` when multiple do). Callers that obtain a `FeedResponse<DecryptableItem>` page MUST cast it to `IAsyncDisposable` and dispose it (typically in a `finally` block) so that items the caller skipped or never enumerated still release their pooled buffers. See the example on `DecryptableItem` for the recommended pattern.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds distributed-cache (`IDistributedCache`) support to the DEK properties cache. When the in-process cache entry expires, the next request consults the distributed cache before hitting Cosmos metadata, allowing a peer-populated entry to rescue the request during transient metadata unavailability. Adds optional proactive background refresh, cross-process cache-key prefix scoping, and format-version-scoped cache keys for rolling-upgrade safety.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `DekCacheOptions` and a new constructor overload (`CosmosDataEncryptionKeyProvider(EncryptionKeyStoreProvider, DekCacheOptions)`) so future cache settings can be added as properties on the options bag without further constructor-overload churn. For hybrid callers that still need `EncryptionKeyWrapProvider` alongside `EncryptionKeyStoreProvider` (e.g. legacy-algorithm migration), adds the static factory `CosmosDataEncryptionKeyProvider.Create(EncryptionKeyWrapProvider, EncryptionKeyStoreProvider, DekCacheOptions)`; a factory is used instead of an additional constructor to avoid `null`-literal overload ambiguity with the obsolete dual-provider constructor.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `IDisposable` and `IAsyncDisposable` to `CosmosDataEncryptionKeyProvider`. Disposal cancels and best-effort drains in-flight fire-and-forget distributed-cache writes (5-second bounded wait). Repeated calls to the same disposal method (`Dispose` or `DisposeAsync`) are idempotent; interleaving `Dispose` with `DisposeAsync` on the same instance is not supported (matches the public XML remarks). The provider does NOT dispose externally-supplied dependencies (`IDistributedCache`, `EncryptionKeyWrapProvider`, `EncryptionKeyStoreProvider`, `Container`) — caller owns those lifetimes. User-initiated `RemoveAsync` invalidations are not interrupted by disposal so the distributed cache cannot end up with stale entries.
- [#5428](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5428) Adds `EncryptionCustomEventSource` (named `Azure-Cosmos-Encryption-Custom`) for Release-visible best-effort failure diagnostics on the optional distributed-cache integration. Surfaces L2 read / write / background-write / remove failures at `EventLevel.Warning`. Auto-discovered by `Azure.Core.Diagnostics.AzureEventSourceListener` and `dotnet-trace --providers Azure-Cosmos-Encryption-Custom`. Activity-tag diagnostics on the existing `Microsoft.Azure.Cosmos.Encryption.Custom` `ActivitySource` remain the primary correlation channel.

#### Fixes
- [#TBD](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/TBD) Fixes cross-provider cache interference in processes that construct more than one `CosmosDataEncryptionKeyProvider` (for example multi-tenant hosts). Each provider's `EncryptionKeyStoreProvider.DataEncryptionKeyCacheTimeToLive` now applies only to that provider, instead of the most recently constructed provider's value governing every provider in the process. As part of this fix, setting `DataEncryptionKeyCacheTimeToLive` to `TimeSpan.Zero` reliably keeps decrypted key material from being cached across operations. Requires `Microsoft.Data.Encryption.Cryptography` `2.0.0-pre024`.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Stream-mode feed splitting (`JsonArrayStreamSplitter`) no longer fails on valid feed responses delivered as short reads. The buffer-growth guard previously grew whenever the JSON reader consumed nothing in a round, which under a stream that returns partial reads doubled the buffer on every read until it hit the 64 MiB cap and threw `InvalidOperationException` on an otherwise-parseable document. The buffer now grows only when it is genuinely full, so large encrypted values that span multiple short reads parse correctly (and truncated input still surfaces a clean `JsonException`).
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) `StreamDecryptableItem.GetItemAsync<T>` now marks the item disposed before throwing on a failed decrypt. Previously the catch nulled the content stream but left both `isDisposed` and `isDecrypted` at `false`, so a retry would dereference the now-null stream inside the MDE adapter and surface a second confusing `EncryptionException` with empty diagnostic fields, masking the original failure. Retries now surface a clean `ObjectDisposedException`.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) `EncryptionFeedIterator<T>.ReadNextAsync` (lazy `DecryptableItem` branch) now drains any partially-constructed items if `DecryptableFeedResponse.CreateResponse` throws after the items have been built. Closes a narrow leak window where pooled `ArrayPool<byte>` buffers held by each `StreamDecryptableItem` would otherwise persist until GC.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) `Container.UseStreamingJsonProcessingByDefault()` now throws `ArgumentException` (with the `nameof(container)` parameter name) when invoked on a non-`EncryptionContainer`. Previously threw `NotSupportedException`, which conventionally signals "operation fundamentally unsupported" rather than "wrong argument".
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Stream-mode `DecryptableItem.GetItemAsync<T>` now populates `EncryptionException.DataEncryptionKeyId` with the DEK id parsed from the document's `_ei` metadata (or the DEK id from the in-flight `DecryptionContext` if serialization throws after a successful decrypt). Previously the property was always `""`, breaking key-store/DEK-revocation diagnostics for stream-mode users. Matches the diagnostic surface of the existing Newtonsoft `DecryptableItemCore` path.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) `EncryptionProcessor.ConvertResponseToDecryptableItemsAsync` (stream-mode) now disposes already-yielded `StreamDecryptableItem`s if the underlying JSON splitter throws after the first document, returning the rented `ArrayPool<byte>` buffers to the pool and clearing any plaintext residue. Previously the partial list was abandoned and never reached the disposal-cascading `DecryptableFeedResponse`, leaking pool capacity on every mid-feed transport failure.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Stream-mode per-document buffer growth in `StreamProcessor` (encrypt and decrypt) now enforces the same 64 MiB cap that `JsonArrayStreamSplitter` already enforces at the splitter level. Previously a single malformed or maliciously-large encrypted property could drive the per-document buffer to OOM rather than throwing a clean `InvalidOperationException`.
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Stream-mode in-place feed decryption no longer risks corrupting the response body if the operation is cancelled. The in-place overwrite previously truncated the original ciphertext and then copied the decrypted bytes back asynchronously; a cancellation between the two steps could leave the body empty or partially written. The copy-back is now synchronous from the already-materialized buffer, eliminating that window (cancellation is still observed during the decrypt itself).
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) The internal `ArrayPool<byte>`-backed stream used on the streaming-decryption path now returns and zeroes its rented buffer from a finalizer if a consumer abandons a `FeedResponse<DecryptableItem>` page without disposing it. Forgetting to dispose now degrades to a benign, GC-timed cleanup instead of leaking pool capacity and leaving decrypted plaintext resident in pooled memory. Disposing the page (the documented contract) remains the prompt path and suppresses the finalizer.
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
- [#5478](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5478) Removes the unused `System.Text.RegularExpressions 4.3.1` `PackageReference` from `Microsoft.Azure.Cosmos.Encryption.Custom`. The dependency is no longer consumed by any source file in the package and was carried purely as a stale reference. Consumers that transitively depend on `System.Text.RegularExpressions` **through this package** must add a direct reference; this package's surface is unaffected.
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
