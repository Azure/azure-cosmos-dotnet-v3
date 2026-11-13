## Why

Customer-side encryption operations throw `OperationCanceledException` under concurrent load because a global `SemaphoreSlim(1,1)` in `BuildProtectedDataEncryptionKeyAsync` serializes construction of `ProtectedDataEncryptionKey` objects — the resolved, unwrapped encryption keys needed for every encrypt/decrypt operation. The semaphore guards `KeyEncryptionKey.GetOrCreate` and `ProtectedDataEncryptionKey.GetOrCreate` — the Microsoft Data Encryption library's static get-or-create cache operations that, on cache miss, invoke the `createItem` delegate which triggers synchronous Key Vault HTTP calls (Resolve + UnwrapKey). The semaphore ensures only one thread at a time performs the expensive key creation, preventing duplicate Key Vault calls for the same key. However, this means every encrypted leaf value in every document on every page contends on this single-permit lock — even when the cache is warm and the hold time would be microseconds. The root cause: `ProtectedDataEncryptionKey` resolution is synchronous, happens on the hot path under the semaphore, and makes two blocking HTTP calls to Key Vault on cache miss.

The pain is concentrated at the `ProtectedDataEncryptionKey` cache time-to-live boundary (default 2 hours), where a fleet with many concurrent processes can produce a "thundering herd" of simultaneous cache-miss cascades — each thread stuck for 200 ms – 2.4 s of AKV I/O under the single-permit semaphore.

## What Changes

The shipped fix takes a **background-refresh-only** approach that keeps the hot path unchanged and eliminates the cache-miss cascade entirely by making sure the cache is never cold when the hot path arrives.

- **`PdekCacheRefreshWorker`** — a background `Task` per `EncryptionCosmosClient` that scans a set of tracked PDEK entries, performs Key Vault I/O (`ResolveAsync` + `UnwrapKeyAsync`) **outside** the semaphore, then briefly acquires the semaphore only to write the refreshed PDEK directly into the static `ProtectedDataEncryptionKey` cache via a new `SetInCache` API. Because the write is a pure cache poke (microseconds), the semaphore is never held across AKV I/O and the hot path never observes a cold cache in steady state.
- **`TrackEntry` from the hot path** — `EncryptionSettingForProperty.BuildProtectedDataEncryptionKeyAsync` registers each successfully-built PDEK with the worker so the worker knows which entries to refresh.
- **Activation gate** — the worker is only instantiated when `keyCacheTimeToLive >= 1 hour` (configurable via `COSMOS_PDEK_BG_REFRESH_MIN_TTL_SECONDS`). Shorter TTLs skip the worker because the hot path already refreshes often enough to amortize the AKV cost.
- **KEK revocation handling (403)** — on a Key Vault 403 during refresh, the worker evicts the PDEK from the static cache (new `ProtectedDataEncryptionKey.RemoveFromCache`) and stops re-arming the entry, so the very next hot-path access misses and re-runs the standard build flow (which surfaces the 403 to the caller).
- **429 handling** — bounded exponential backoff (1→30 s, max 5 attempts) that honors AKV `Retry-After`.

No public API changes. No breaking changes.

### What was considered and rejected

An earlier proposal added (a) an async `ResolveAsync`/`UnwrapKeyAsync` prefetch on the hot path outside the semaphore and (b) a per-CMK-URL `IKeyEncryptionKey` resolved-client cache, gated behind `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED`. That design was reverted in favor of background-refresh-only because:

- It required modifying the synchronous `GetOrCreate` path and adding a new prefetch cache that duplicated MDE state, expanding the surface for cache-coherence bugs.
- The observed contention is a **cache-miss cascade at TTL expiry**, not per-op sync overhead. Keeping the cache continuously warm addresses the root cause without touching the hot path.
- Fleet load testing at ~14.5 k ops/s aggregate confirmed the hot path exhibits **no measurable lock contention** in steady state; the only opportunity for contention is the TTL boundary — which the background worker eliminates.

See `design.md` for the full retrospective. The three legacy specs (`async-dek-prefetch`, `resolved-client-cache`, `env-var-feature-gate`) are marked SUPERSEDED and retained for history; the sole active spec is `pdek-background-refresh`.

## Capabilities

### New Capabilities
- `pdek-background-refresh` — Background worker that proactively refreshes tracked `ProtectedDataEncryptionKey` cache entries before TTL expiry using async Key Vault I/O, then writes the refreshed PDEK into the static cache under the semaphore in microseconds. Governs activation threshold, refresh window, safety margin, 429/403 handling, and prune behavior.

### Superseded Capabilities
- `async-dek-prefetch` — Rejected. Retained for history.
- `resolved-client-cache` — Rejected. Retained for history.
- `env-var-feature-gate` — Rejected. Retained for history.

### Modified Capabilities
<!-- No existing spec-level requirement changes. All changes are additive. -->

## Impact

- **Files touched (shipped)**: `EncryptionCosmosClient.cs`, `EncryptionSettingForProperty.cs`, `PdekCacheRefreshWorker.cs` (new), `MdeSrc/Cryptography/LocalCache.cs` (`Set` + `Remove` added), `MdeSrc/Cryptography/ProtectedDataEncryptionKey.cs` (`SetInCache` + `RemoveFromCache` + rootKey-accepting ctor added).
- **Dependencies**: No new packages. Uses existing `IKeyEncryptionKeyResolver.ResolveAsync()` / `IKeyEncryptionKey.UnwrapKeyAsync()` from `Azure.Core.Cryptography`.
- **APIs**: No public API changes. Internal-only.
- **Security**: Plaintext DEK bytes are already cached in-process by the MDE library's `ProtectedDataEncryptionKey`. The background worker writes the same bytes to the same cache with the same TTL — no new attack surface. On KEK revocation (403), the worker actively evicts the PDEK; worst-case stale-key window is the time from refresh-completion to next hot-path access (typically milliseconds) plus one PDEK TTL if best-effort eviction fails.
- **Configuration**: Three optional env vars (`COSMOS_PDEK_BG_REFRESH_MIN_TTL_SECONDS`, `COSMOS_PDEK_BG_REFRESH_MAX_SCAN_INTERVAL_SECONDS`, `COSMOS_PDEK_BG_REFRESH_WINDOW_FRACTION`).
- **Risk**: New background lifecycle (Task + CTS + tracking dictionary) per client instance. Mitigated by: activation gate on TTL, bounded scan interval, best-effort semantics (all exceptions caught inside the loop), safety-margin cap that guarantees refresh runway before expiry, hot-path-touch-based prune to bound tracking-set growth.
- **Testing**: Unit tests for `LocalCache.Set`/`Remove`, `ProtectedDataEncryptionKey.SetInCache`/`RemoveFromCache` round-trip and eviction semantics, activation gate, `TrackEntry`, dispose-safety (worker task must not fault).
