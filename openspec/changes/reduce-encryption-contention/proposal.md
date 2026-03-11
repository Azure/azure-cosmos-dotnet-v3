## Why

Customer-side encryption operations throw `OperationCanceledException` under concurrent load because a global `SemaphoreSlim(1,1)` in `BuildProtectedDataEncryptionKeyAsync` serializes construction of `ProtectedDataEncryptionKey` objects — the resolved, unwrapped encryption keys needed for every encrypt/decrypt operation. The semaphore guards `KeyEncryptionKey.GetOrCreate` and `ProtectedDataEncryptionKey.GetOrCreate` — the Microsoft Data Encryption library's static get-or-create cache operations that, on cache miss, invoke the `createItem` delegate which triggers synchronous Key Vault HTTP calls (Resolve + UnwrapKey). The semaphore ensures only one thread at a time performs the expensive key creation, preventing duplicate Key Vault calls for the same key. However, this means every encrypted leaf value in every document on every page contends on this single-permit lock — even when the cache is warm and the hold time would be microseconds. The root cause: `ProtectedDataEncryptionKey` resolution is synchronous, happens on the hot path under the semaphore, and makes two blocking HTTP calls to Key Vault on cache miss. An internal customer is actively impacted.

## What Changes

- **Async prefetch of `ProtectedDataEncryptionKey` resolution outside the semaphore** via `ResolveAsync()` + `UnwrapKeyAsync()` before semaphore acquisition. The sync `UnwrapKey` inside the semaphore cannot be removed — the Microsoft Data Encryption library's `ProtectedDataEncryptionKey.GetOrCreate` constructor chain calls it unconditionally. Instead, the prefetch populates a cache so that when the Microsoft Data Encryption library's sync `UnwrapKey` fires, it returns cached bytes instantly instead of making HTTP calls to Key Vault. The semaphore is still acquired, but held for microseconds (cache read) instead of 200ms–2.4s (HTTP I/O).
- **Proactive background refresh** of the prefetched unwrapped Data Encryption Key bytes (the plaintext AES-256 key material returned by Key Vault's `UnwrapKey`) approximately 5 minutes before time-to-live expiry, deduplicated to one Azure Key Vault call per key per interval. Prevents thundering herd at time-to-live boundary.
- **Cache the resolved `IKeyEncryptionKey` (CryptographyClient)** per Customer Master Key URL so each refresh makes one Azure Key Vault call (`UnwrapKey`) instead of two (`Resolve` + `UnwrapKey`).
- All changes gated behind an opt-in environment variable (`AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED`), off by default. No public API changes. No breaking changes.

## Capabilities

### New Capabilities
- `async-dek-prefetch`: Async prefetch of `ProtectedDataEncryptionKey` resolution outside the semaphore using `ResolveAsync()` + `UnwrapKeyAsync()`, with a `ConcurrentDictionary` prefetch cache that the sync `UnwrapKey` reads from. Includes proactive background refresh before time-to-live expiry and lifecycle management via `CancellationTokenSource`.
- `resolved-client-cache`: Cache the `IKeyEncryptionKey` (CryptographyClient) returned by `Resolve()` per Customer Master Key URL to eliminate redundant Key Vault HTTP GETs on each refresh.
- `env-var-feature-gate`: Environment variable gate (`AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED`) to opt in to all caching/prefetch layers. Off by default. Follows existing SDK `ConfigurationManager` pattern.

### Modified Capabilities
<!-- No existing spec-level requirement changes. All changes are additive and gated. -->

## Impact

- **Files**: `EncryptionKeyStoreProviderImpl.cs` (or new subclass), `EncryptionCosmosClient.cs`, `EncryptionSettingForProperty.cs` (prefetch wiring only)
- **Dependencies**: No new packages. Uses existing `IKeyEncryptionKeyResolver.ResolveAsync()` / `IKeyEncryptionKey.UnwrapKeyAsync()` from `Azure.Core.Cryptography`.
- **APIs**: No public API changes. Internal-only.
- **Security**: Plaintext Data Encryption Key bytes are already cached in-process by the Microsoft Data Encryption library's `ProtectedDataEncryptionKey`. New caches hold the same bytes with the same time-to-live in the same process — no new attack surface.
- **Risk**: New caching layers introduce lifecycle complexity (background refresh, disposal, cache coherence on key rotation). Gated by environment variable for safe rollout.
- **Testing**: Unit tests for each cache layer + concurrency. Emulator end-to-end tests with environment variable enabled.
