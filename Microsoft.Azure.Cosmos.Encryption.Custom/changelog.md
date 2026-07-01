Preview features are treated as a separate branch and will not be included in the official release until the feature is ready. Each preview release lists all the additional features that are enabled.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### <a name="1.1.0-preview01"/> [1.1.0-preview01](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Encryption.Custom/1.1.0-preview01) - Unreleased

#### Added
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
- Fixes several silent data-corruption and fidelity defects in the internal `System.Text.Json` streaming encryption/decryption path (used for the MDE algorithm on `net8.0`):
  - **Only top-level configured paths are encrypted and decrypted.** A property nested below the document root that happened to share a name with a configured top-level encrypted path (for example a `Sensitive` property inside an `Outer` object when only `/Sensitive` is configured) was incorrectly encrypted on write and mishandled on read, corrupting the nested value and breaking interchange with the Newtonsoft processor. Path matching is now restricted to top-level properties on both the encrypt and decrypt paths, matching the Newtonsoft processor's top-level-only behavior. A nested user property literally named `_ei` is likewise preserved instead of being dropped on decrypt (only the top-level `_ei` metadata block is stripped).
  - **Escaped strings and property names are preserved.** Non-encrypted (pass-through) string values and property names that contain JSON escape sequences (`"`, `\`, `\n`, `\uXXXX`, control characters), including those nested inside an encrypted object or array, are no longer re-escaped during encrypt/decrypt. A decrypted value now matches the original exactly instead of accumulating extra backslashes.
  - **A `null` inside an encrypted object or array no longer corrupts the payload.** Previously a `null` element cleared the tracked encryption path, dropping the value's entry from the encrypted-paths metadata (`_ep`) and leaving the encrypted object/array undecryptable. The path is now retained.
  - **Integral floating-point values keep their numeric type.** A decrypted `Double` such as `5.0` is written as `5.0` rather than `5`, so a subsequent re-encrypt classifies the value identically and output matches the Newtonsoft processor.
  - **Out-of-range integer literals fail closed.** An integer literal larger than `Int64` is now rejected with a clear error instead of being silently coerced to a lossy `double`.
  - **Encrypting an already-encrypted document is rejected.** A document that already carries a top-level `_ei` property now throws instead of silently emitting a second, duplicate `_ei`.
  - **Reads tolerate non-standard `_ei` metadata (Newtonsoft parity).** An `_ei` whose value is not a JSON object is treated as a non-encrypted document and passed through instead of throwing, and an encryption-format-version (`_ef`) serialized as a numeric string is read correctly.
  - The streaming decrypt path now disposes the input stream on a successful decrypt, matching the Newtonsoft adapter's stream-ownership behavior.
- The lazy `DecryptableItem` decrypt path now honors the JSON processor configured for the operation. Reading items as `DecryptableItem` (point reads, `FeedResponse<DecryptableItem>` from queries and the change-feed iterator, and the `EncryptableItem`/`EncryptableItemStream` write-response wrappers) and then calling `await item.GetItemAsync<T>()` previously always decrypted with the Newtonsoft engine even when the rest of the operation ran through the `System.Text.Json` streaming path. The deferred decryption now runs through the same processor (decrypted output is byte-identical to the Newtonsoft path), and legacy AEAD documents continue to decrypt via the Newtonsoft path.

#### Other Changes
- When the internal `System.Text.Json` streaming processor is used to decrypt feed, query, and change-feed results, each page is now decrypted directly from the response payload. Previously each page was first materialized as an intermediate Newtonsoft `JObject`, and every document was re-serialized through Newtonsoft before the streaming decryptor ran, which negated the streaming processor's allocation and throughput benefits on multi-item reads. The response envelope is now copied through unchanged and each document is decrypted from its raw bytes and spliced back in order, eliminating the per-page and per-document Newtonsoft round-trips. Decrypted results, element ordering, the legacy-AEAD fallback, and decrypt diagnostics are unchanged; this applies to `net8.0` with the MDE algorithm only, and the Newtonsoft decryption path is untouched.
- Upgrades `Microsoft.Data.Encryption.Cryptography` from `0.2.0-preview` to `2.0.0-pre015` (MDE 2.0). The upgrade is wire-compatible: encrypted payloads keep MDE format version 3 / AEAD format version 2, so no re-encryption or data migration is required and documents stay fully backward- and forward-compatible with prior releases.
- Replaces the package's `Microsoft.Extensions.Caching.Memory` reference (previously `3.1.7` on `netstandard2.0` / `1.1.2` on `net46`) with `Microsoft.Extensions.Caching.Abstractions` `3.1.7`, unified across target frameworks. The library consumes only `IDistributedCache`; the `MemoryCache` reference was unused. Consumers that transitively depended on `Microsoft.Extensions.Caching.Memory` (for example `MemoryCache`) **through this package** must add a direct reference; consumers using only `IDistributedCache` are unaffected. The `Abstractions` floor stays at the lowest version the API surface compiles against, so consumers remain free to unify upward to any LTS.
- Drops the direct `Azure.Core` package reference; it is now brought in transitively via `Azure.Identity`. Consumers that relied on `Azure.Core` transitively through this package and use it directly should add a direct reference.

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
