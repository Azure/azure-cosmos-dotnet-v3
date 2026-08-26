## Context

Client-side encryption in the Cosmos DB .NET SDK uses a global `SemaphoreSlim(1,1)` that serializes all encryption key resolution operations — specifically, the construction or cache lookup of `ProtectedDataEncryptionKey` and `KeyEncryptionKey` objects via the Microsoft Data Encryption library's `GetOrCreate` methods. These are the only operations guarded by the semaphore; encrypt/decrypt of document payloads happens outside the semaphore. The semaphore guards the Microsoft Data Encryption library's `ProtectedDataEncryptionKey.GetOrCreate` and `KeyEncryptionKey.GetOrCreate` — both of which are cache lookups on the hot path but trigger synchronous Key Vault HTTP calls on cache miss. `ProtectedDataEncryptionKey` resolution is sync, on the hot path, and makes two blocking HTTP calls to Key Vault (Resolve + UnwrapKey) under the semaphore.

The call amplification is severe: `DecryptObjectAsync` calls `BuildEncryptionAlgorithmForSettingAsync` per encrypted leaf value per document per page. With concurrent feed iterators, this produces thousands of semaphore acquisitions per second, all serialized to a single permit. Note: both read (decrypt) and write (encrypt) paths acquire the semaphore for key resolution; however, the customer workload triggering this issue is read-heavy (Change Feed Processor with concurrent `FeedIterator<T>`). A dedicated investigation task (1.9) covers whether specific operation types trigger disproportionate contention.

**Structural constraint**: The Microsoft Data Encryption library's `ProtectedDataEncryptionKey` constructor chain is sync (C# cannot make base-ctor calls async). `EncryptionKeyStoreProvider.UnwrapKey` returns `byte[]` (sync). The Microsoft Data Encryption library's `LocalCache.GetOrCreate` takes `Func<T>` not `Func<Task<T>>`. The sync chain from `ProtectedDataEncryptionKey` → `KeyEncryptionKey.DecryptEncryptionKey` → `UnwrapKey` → `Resolve` + `UnwrapKey` on Key Vault cannot be made async at any point in the Microsoft Data Encryption library's type hierarchy. However, `IKeyEncryptionKeyResolver` exposes `ResolveAsync` and `IKeyEncryptionKey` exposes `UnwrapKeyAsync` — these async variants are reachable from our code, just not through the Microsoft Data Encryption library's call chain.

## Goals / Non-Goals

**Goals:**
- Eliminate `OperationCanceledException` cascade when `ProtectedDataEncryptionKey` cache expires under concurrent load
- Reduce semaphore hold time on `ProtectedDataEncryptionKey` cache miss from 200ms–2.4s to microseconds
- No public API changes. No breaking changes.
- Minimal complexity: prefer the simplest change that solves the customer’s problem

**Non-Goals:**
- Changing the Microsoft Data Encryption library’s type hierarchy or making the `ProtectedDataEncryptionKey` constructor async
- Removing the semaphore entirely (it guards the Microsoft Data Encryption library internal state mutation — still needed)
- Per-key semaphores (reduces cross-key contention but same-key still serializes; added complexity for marginal gain)
- Client Encryption Key rotation handling (Client Encryption Key replacement requires data migration — re-encrypting every document — and is an offline operation; not a runtime concern for caching)
- Supporting key rotation detection at the cache level (Client Encryption Key rewrap doesn’t change plaintext bytes; new Client Encryption Key policy creates new `EncryptionSettingForProperty` objects)

## Approach Comparison

Two approaches were evaluated. Approach A (enable the Microsoft Data Encryption library’s built-in Data Encryption Key byte cache) is the recommended starting point due to its simplicity and minimal risk surface.

### Approach A: Enable Data Encryption Key Byte Cache (Low-Risk Baseline)

#### Why this was not considered earlier

The original design dismissed the Data Encryption Key byte cache with circular reasoning:

> “Enabling the Microsoft Data Encryption library Data Encryption Key byte cache (`DataEncryptionKeyCacheTimeToLive`) as a standalone layer (redundant when async prefetch is working)”

This is backward — the question is whether the prefetch is needed at all if enabling the built-in cache solves the problem. The oversight occurred because investigation focused on the `ProtectedDataEncryptionKey` cache layer and the semaphore, without tracing the full call chain through `UnwrapKey` → `GetOrCreateDataEncryptionKey` → `LocalCache.GetOrCreate` to discover that the SDK **explicitly disables** a cache that would prevent the Key Vault calls entirely.

#### How the built-in cache works

The Microsoft Data Encryption library’s `EncryptionKeyStoreProvider` base class has a `LocalCache<string, byte[]>` keyed by the hex-encoded wrapped key bytes. It is checked inside `UnwrapKey` → `GetOrCreateDataEncryptionKey` before the `UnwrapKeyCore` delegate (which calls `Resolve()` + `UnwrapKey()` on Key Vault) is invoked.

The Cosmos SDK’s `EncryptionKeyStoreProviderImpl` constructor **explicitly disables** this cache:

```csharp
this.DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero;
```

When `TimeToLive <= TimeSpan.Zero`, `LocalCache.GetOrCreate` skips the cache entirely and always invokes the delegate — meaning every `ProtectedDataEncryptionKey` cache miss triggers two Key Vault HTTP calls.

#### What enabling it solves

The customer’s problem is the **steady-state `ProtectedDataEncryptionKey` cache time-to-live expiry** (every 1–2 hours) under sustained concurrent load:

1. `ProtectedDataEncryptionKey` cache time-to-live expires → cache miss → constructor runs under semaphore
2. Constructor → `KeyEncryptionKey.DecryptEncryptionKey` → `UnwrapKey` → **Data Encryption Key byte cache check**
3. **Today (cache disabled):** `LocalCache.GetOrCreate` skips cache → `UnwrapKeyCore` → `Resolve()` + `UnwrapKey()` on Key Vault (200ms–2.4s)
4. **With cache enabled:** `LocalCache.GetOrCreate` finds a warm entry (populated 1 hour ago when the previous `ProtectedDataEncryptionKey` was constructed) → returns instantly → **zero Key Vault calls**
5. Semaphore hold time drops to **microseconds** — identical to the prefetch approach’s goal

This works because the Data Encryption Key byte cache time-to-live (set to 2 hours or longer) outlives the `ProtectedDataEncryptionKey` cache time-to-live (1–2 hours, defaults to 1 hour via `keyCacheTimeToLive`). When the `ProtectedDataEncryptionKey` expires and is reconstructed, the wrapped key bytes haven’t changed — the Data Encryption Key byte cache key is `encryptedKey.ToHexString()`, which is the same wrapped blob — so the cache hits.

#### Critical limitation: periodic time-to-live alignment causes co-expiry

However, the Data Encryption Key byte cache uses `AbsoluteExpirationRelativeToNow` — the time-to-live is set once at creation and **not renewed on cache hit**. This means the Data Encryption Key byte cache entry created at T=0 expires at T=2h regardless of how many `ProtectedDataEncryptionKey` reconstructions hit it in between.

**With Data Encryption Key byte cache time-to-live = 2h, `ProtectedDataEncryptionKey` time-to-live = 1h (defaults):**

| Time | `ProtectedDataEncryptionKey` cache | Data Encryption Key byte cache | Semaphore hold time |
|------|-----------|----------------|-----|
| T=0h | Populated (expires T=1h) | Populated (expires T=2h) | Cold start — Key Vault I/O |
| T=1h | **MISS** → reconstruct | HIT (not renewed, still expires T=2h) | **Microseconds** |
| T=2h | **MISS** → reconstruct | **MISS** (co-expired) → Key Vault I/O, repopulated (expires T=4h) | **200ms–2.4s — thundering herd possible** |
| T=3h | **MISS** → reconstruct | HIT (not renewed, still expires T=4h) | **Microseconds** |
| T=4h | **MISS** → reconstruct | **MISS** (co-expired) → Key Vault I/O | **200ms–2.4s** |

**Pattern: every `DataEncryptionKeyCacheTimeToLive` interval, both caches co-expire and the full Key Vault call path is hit under the semaphore.** The thundering herd still occurs at these alignment boundaries — just less frequently (every 2h instead of every 1h with defaults).

Setting a much longer Data Encryption Key byte cache time-to-live (e.g. 24h) would reduce the frequency further (1 slow miss per 24h), but:
- The thundering herd at the 24h alignment boundary is identical to today’s problem — just rare
- A 24h stale key material window may raise security review concerns
- If `keyCacheTimeToLive` is configured shorter (e.g. 10 minutes), the alignment frequency increases proportionally

**Verdict: Approach A reduces frequency of the problem but does not eliminate it.** It’s a low-risk improvement but not a complete fix for customers with sustained high-concurrency workloads and strict cancellation timeouts.

#### The change

```csharp
// EncryptionKeyStoreProviderImpl constructor — change ONE line:
this.DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero;
// becomes:
this.DataEncryptionKeyCacheTimeToLive = TimeSpan.FromHours(2);
// (or: keyCacheTimeToLive * 2, ensuring it outlives the ProtectedDataEncryptionKey cache)
```

#### Key rotation correctness

Client Encryption Key rewrap (the only runtime rotation) changes the wrapped key bytes → different `encryptedKey.ToHexString()` → different cache key → cache miss → fresh Key Vault call. The plaintext Data Encryption Key bytes are unchanged, so the result is correct. No cache coherence issue.

#### KEK revocation detection

Enabling the Data Encryption Key byte cache introduces a conflict with KEK (Key Encryption Key) revocation detection. The existing error-handling path in `BuildEncryptionAlgorithmForSettingAsync` catches `RequestFailedException(403)` from `UnwrapKey` and triggers a force-refresh flow that fetches the latest Client Encryption Key properties from the backend. With the byte cache enabled, `UnwrapKey` → `GetOrCreateDataEncryptionKey` returns cached unwrapped bytes on a cache hit **without ever calling the key resolver**. The 403 is never thrown. KEK revocation becomes undetectable for the lifetime of the cache entry.

This is not a theoretical concern — the `VerifyKekRevokeHandling` emulator test explicitly validates this flow: cache a DEK, revoke the KEK, verify that subsequent operations fail with the "needs to be rewrapped" error. With a naïve cache enable (`TimeSpan.Zero` → `TimeSpan.FromHours(2)`), this test fails because the cached bytes mask the revocation.

Simple eviction doesn't work either — evicting the byte cache entry before every `BuildProtectedDataEncryptionKeyAsync` call renders the cache useless, since that's the only code path that reads it. This is functionally equivalent to `DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero`.

**This is resolved by the custom cache in Approach B** — see Decision 2 for the `revalidationInterval` design that separates the performance and security time scales.

#### Summary of Approach A limitations

| Scenario | Approach A behavior |
|---|---|
| **Steady-state time-to-live expiry (PDEK miss, DEK hit)** | Fast — no Key Vault calls. Covers most PDEK reconstructions. |
| **Periodic co-expiry (PDEK miss AND DEK miss)** | Slow — full Key Vault I/O under semaphore. Thundering herd still possible at alignment boundaries. |
| **Cold start** (first-ever call, no prior cache entry) | Key Vault I/O under semaphore — but at cold start there’s no concurrent load, so no thundering herd |
| **Reducing Resolve calls** | On a true cache miss, still calls `Resolve()` + `UnwrapKey()` (2 Key Vault calls). Does not cache the resolved `IKeyEncryptionKey` |
| **Proactive refresh before expiry** | No proactive refresh — relies on the next access to populate |
| **KEK revocation detection** | Not supported by simple byte cache — cached bytes mask revocation. Resolved by Approach B’s `revalidationInterval` (see Decision 2). |

### Approach B: Async Prefetch with Background Refresh (Recommended)

This is the full approach originally proposed. It adds a sealed subclass `CachingEncryptionKeyStoreProviderImpl` with async prefetch outside the semaphore, inflight deduplication, a resolved-client cache (caching the `IKeyEncryptionKey` / `CryptographyClient` per Customer Master Key URL), proactive background refresh with jitter, a concurrency limiter, disposal lifecycle with `CancellationTokenSource`, and environment variable gating.

Approach B eliminates the co-expiry problem because the prefetch cache is refreshed **proactively before time-to-live expiry** — the sync `UnwrapKey` inside the semaphore always finds a warm prefetch entry. It also covers cold start (prefetch runs outside the semaphore), reduces Key Vault calls from 2 to 1 (via resolved-client cache), and handles the background refresh lifecycle cleanly.

**However**, Approach B is significantly more complex. Consider whether the complexity is justified for the customer’s actual workload.

#### Can the approaches be combined?

**Yes — and this is recommended.** Enabling the Data Encryption Key byte cache (Approach A) as a baseline improvement is low-risk and benefits all customers immediately. Approach B layers on top for customers who need zero-contention guarantees:

- Approach A: default-on, no environment variable needed, reduces slow-miss frequency from every `keyCacheTimeToLive` to every `DataEncryptionKeyCacheTimeToLive`
- Approach B: opt-in via environment variable, eliminates slow misses entirely via proactive prefetch

The full Approach B design (decisions, risks, migration plan) is retained below.

## Recommendation

> **Note:** The recommendation below was the original proposal before the team's offline review. See "Final Approach (Team Decision): Proactive PDEK Cache Background Refresh" above for the chosen direction.

1. **Ship Approach A (enable Data Encryption Key byte cache) as a default-on baseline.** One-line change, benefits all customers, reduces slow-miss frequency. Low risk.
2. **Ship Approach B (async prefetch) gated behind `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` environment variable** for customers who need zero-contention guarantees under sustained concurrent load.
3. **Benchmark both** against the customer workload to validate: (a) Approach A reduces frequency as expected, (b) Approach B eliminates contention entirely.
4. The `AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate` cleanup (see Future Considerations) can ship independently with either approach.


## Decisions

> **⚠️ Superseded**: Decisions 1–3 below were made for the rejected Approach B (async prefetch subclass). They are retained for historical context. See "Final Approach (Team Decision)" section for the chosen design decisions.

### Decision 1: Async prefetch in a sealed subclass, not in the base `EncryptionKeyStoreProviderImpl`

**Chosen**: New `CachingEncryptionKeyStoreProviderImpl` (sealed, internal) extends `EncryptionKeyStoreProviderImpl`. Base class stays clean sync-only. `EncryptionCosmosClient` instantiates the subclass when environment variable is on.

**Rationale**: The base class serves all existing customers with zero behavior change. The subclass adds: prefetch `ConcurrentDictionary`, resolved-client cache (a `ConcurrentDictionary<string, IKeyEncryptionKey>` that caches the `CryptographyClient` returned by `IKeyEncryptionKeyResolver.Resolve()` per Customer Master Key URL — called "resolved-client" because it caches the resolved `IKeyEncryptionKey` client object, not the unwrapped key bytes), `CancellationTokenSource`, `Cleanup()` method, and overridden `UnwrapKey` that checks prefetch cache first. Separation keeps the risk surface isolated.

**Rejected alternative**: Adding prefetch directly to base class with conditional branches — mixes concerns, harder to reason about lifecycle, risk of unintended behavior when environment variable is off.

### Decision 2: `ConcurrentDictionary` for prefetch cache (not `MemoryCache` or the Microsoft Data Encryption library's `LocalCache`)

**Chosen**: `ConcurrentDictionary<string, byte[]>` with manual time-to-live tracking (expiry stored alongside value).

**Rationale**: The Microsoft Data Encryption library's `LocalCache.GetOrCreate` is get-or-create only — no `Set`, `Remove`, or `Evict` API. `MemoryCache` adds a dependency and has its own concurrency model. `ConcurrentDictionary` is simple, lock-free reads, and we control time-to-live ourselves.

#### KEK revocation via revalidation interval

The custom cache also resolves the KEK revocation detection problem described in Approach A's "KEK revocation detection" section. The fundamental tension is between two goals that both go through the same `UnwrapKey` → resolver path:
1. **Performance**: skip the resolver call (avoid Key Vault HTTP I/O under the semaphore)
2. **Security**: call the resolver (detect KEK revocation)

These are contradictory on any single call. The custom cache resolves this by tracking a `lastValidated` timestamp per entry and introducing a `revalidationInterval`:

```
CacheEntry {
    byte[] UnwrappedBytes     // the cached unwrapped DEK bytes
    DateTime LastValidated    // when we last confirmed KEK access via the resolver
}
```

Lookup behavior:

| Cache state | Action |
|---|---|
| **Cache miss** | Call resolver (`UnwrapKeyCore`), populate entry with `LastValidated = UtcNow` |
| **Cache hit, within `revalidationInterval`** | Return cached bytes immediately — no resolver call |
| **Cache hit, past `revalidationInterval`** | Call resolver to re-validate KEK access. **Succeeds**: update `LastValidated`, return bytes. **Fails (403)**: evict entry, rethrow — existing force-refresh path handles recovery |

**Key parameters**:

- `cacheTtl` (overall entry lifetime): 2 hours — same as the current `DataEncryptionKeyCacheTimeToLive` intent. Controls how long unwrapped bytes are retained at all.
- `revalidationInterval` (KEK access re-check frequency): recommended default matching `ProtectedDataEncryptionKey` time-to-live (1 hour). This is the maximum window during which KEK revocation goes undetected.

This **separates the time scales**: ~99% of `UnwrapKey` calls (within `revalidationInterval`) are pure cache hits for performance, while periodic calls (at revalidation boundaries) contact the resolver for security validation. KEK revocation is detected within `revalidationInterval` — not immediately, but within a bounded and configurable window. The existing `BuildEncryptionAlgorithmForSettingAsync` 403 → force-refresh flow is unchanged.

**Test compatibility**: The `VerifyKekRevokeHandling` test sets `ProtectedDataEncryptionKey.TimeToLive = TimeSpan.Zero`, meaning every operation rebuilds the PDEK and calls `UnwrapKey`. With `revalidationInterval` derived from the PDEK time-to-live (zero → every call validates), the test passes — every `UnwrapKey` call contacts the resolver and detects revocation immediately.

**Implementation notes**:
- Override `GetOrCreateDataEncryptionKey` in `EncryptionKeyStoreProviderImpl` (or the `CachingEncryptionKeyStoreProviderImpl` subclass) — the base class method is `protected virtual`
- Set `DataEncryptionKeyCacheTimeToLive = TimeSpan.Zero` on the base class to disable its simple cache; the override handles all caching with revalidation
- Derive `revalidationInterval` from `ProtectedDataEncryptionKey.TimeToLive`: if PDEK time-to-live is zero (no caching), revalidation is zero (every call validates); if PDEK time-to-live is 1 hour, revalidation is 1 hour (one validation per PDEK rebuild cycle)

### Decision 3: Proactive refresh via `Task.Run`, not `Timer`

**Chosen**: On cache access, if within a configurable refresh window of expiry, fire `Task.Run` to refresh in background. The refresh window SHALL be **20% of the cache time-to-live, capped at a maximum of 5 minutes**. For example: time-to-live = 1 hour → refresh at 12 minutes before expiry (20% × 60 min = 12 min, but capped to 5 min → 5 minutes); time-to-live = 10 minutes → refresh at 2 minutes before expiry (20% × 10 min = 2 min). Deduplicate via `ConcurrentDictionary<string, Task>`.

**Rationale**: `Timer` requires managing timer lifecycle per key, disposal ordering, and thread-pool callbacks. `Task.Run` on access is simpler — only fires when the key is actually being used (no wasted refresh for unused keys). Tied to `CancellationTokenSource` for cleanup on dispose. The refresh window is percentage-based rather than a fixed duration because the `keyCacheTimeToLive` is customer-configurable (defaults to 1 hour, max 2 hours from `ProtectedDataEncryptionKey.TimeToLive`, but can be set shorter). A fixed 5-minute window would be disproportionate for short time-to-live values.

**Rejected alternative**: `System.Threading.Timer` per key — more deterministic timing but complex lifecycle management (dispose timers on cache eviction, handle timer callbacks after disposal).

## Risks / Trade-offs

> **⚠️ Superseded**: The risks below were identified for the rejected Approach B (async prefetch subclass). Retained for historical context. See "Risks / Trade-offs (Final Approach)" below.

- **[Non-risk] Cache coherence on key rotation**: Client Encryption Key rewrap (same plaintext Data Encryption Key, new Customer Master Key wrapper) is the only runtime rotation — all caches remain valid. Client Encryption Key replacement is out of scope (requires data migration, offline operation).

- **[Risk] Background refresh lifecycle]: `Task.Run` captures `this`, keeping the provider and all its dependencies (resolver, credential chain) GC-rooted until the task completes. → **Mitigation**: `CancellationTokenSource` cancelled on `Cleanup()`/`Dispose()`. `Interlocked.Exchange` for idempotent dispose. Background tasks observe cancellation token.

- **[Risk] Two copies of plaintext Data Encryption Key in memory**: `ProtectedDataEncryptionKey` internal field + prefetch `ConcurrentDictionary`. → **Mitigation**: Same process, same threat boundary, same time-to-live. No new attack surface. Industry standard (Azure Key Vault SDK recommends caching).

- **[Risk] Environment variable read at construction only**: If an operator enables the environment variable and restarts the app, old client instances still run without optimization. → **Mitigation**: This is by design — matches SDK `ConfigurationManager` pattern. Client must be reconstructed to pick up changes.

- **[Risk] Azure Key Vault burst on multi-Client Encryption Key proactive refresh**: If multiple Client Encryption Keys have similar time-to-live values (common at startup — all populated in a short window), their proactive refreshes fire simultaneously. Each refresh is deduplicated per key (1 call per Client Encryption Key), but N Client Encryption Keys = N concurrent Azure Key Vault calls. Azure Key Vault throttles at 4000 ops/vault/10s — unlikely to hit for typical customers (1–10 Client Encryption Keys), but possible for large deployments. → **Mitigation (recommended)**: Add jitter to the proactive refresh window (random offset within the percentage-based refresh window, per key) so Client Encryption Keys don’t all refresh at the same instant. Consider a concurrency limiter on background Azure Key Vault calls.

- **[Trade-off] Prefetch adds complexity for correctness**: Async prefetch + background refresh + disposal lifecycle is more complex than a one-liner (`TimeSpan.Zero` → `TimeSpan.FromHours(1)`). But it solves the actual problem — `ProtectedDataEncryptionKey` resolution is async, off the hot path, and doesn’t overload Azure Key Vault. → **Accept**: Complexity is isolated in a sealed subclass, gated by environment variable.

### Risks / Trade-offs (Final Approach)

- **[Non-risk] Cache coherence on key rotation**: Same as above - Client Encryption Key rewrap produces a different wrapped blob but identical plaintext bytes. PDEK cache key is (cekId + kek + wrapped bytes), so rewrap causes a cache miss -> fresh resolution. Correct behavior.

- **[Risk] Background worker GC-rooting**: The background `Task.Run` loop captures `EncryptionCosmosClient` (which holds the resolver, credential chain). If the `CosmosClient` is abandoned without `Dispose()`, the background worker keeps everything alive indefinitely. -> **Mitigation**: Document that `CosmosClient` (wrapping `EncryptionCosmosClient`) should be disposed. Add a weak reference or finalizer guard if feasible.

- **[Risk] PDEK cache entry access from background thread**: The MDE `ProtectedDataEncryptionKey.GetOrCreate` uses a static `LocalCache` - the background worker must call this same API to refresh entries. If the `LocalCache` implementation isn't thread-safe for concurrent `GetOrCreate` calls (e.g., if it evicts mid-construction), this could corrupt state. -> **Mitigation**: The existing `EncryptionKeyCacheSemaphore` already serializes `GetOrCreate` calls. The background worker must acquire this semaphore before refreshing. This means refresh hold time = Key Vault I/O, but it happens proactively (no concurrent waiter backlog at that moment).

- **[Risk] Scan interval tuning**: If the monitor thread scans too frequently, it wastes CPU. If too infrequently, it may miss the refresh window. -> **Mitigation**: Scan interval = `min(ttl * 0.1, 5 minutes)`. For TTL = 1 hour, scan every 5 minutes (capped). This provides multiple scan opportunities within the refresh window (80-100% of TTL).

- **[Risk] Refresh failure leaves stale entry**: If background refresh fails (transient error, network issue), the entry remains in the PDEK cache with its original TTL. If it expires before the next successful refresh, the hot path hits a cold miss under the semaphore - same as today. -> **Mitigation**: On refresh failure, re-enqueue for retry on next scan cycle. The worst case is identical to the current baseline (no regression).

- **[Trade-off] Background worker still acquires the semaphore**: The global `EncryptionKeyCacheSemaphore` is needed for `ProtectedDataEncryptionKey.GetOrCreate`. The background refresh will hold it for Key Vault I/O duration (~200ms-2.4s). -> **Acceptable because**: refresh happens proactively (before TTL expiry), so no concurrent hot-path threads are waiting for the same entry at that moment. The semaphore hold only overlaps with hot-path threads if they happen to need a *different* CEK at the same instant - rare with 1-10 CEKs.

- **[Trade-off] No benefit for TTL < 1 hour**: Customers who set short TTL for security reasons get no background refresh - they accept the current contention behavior. -> **Acceptable**: Short TTL implies frequent re-validation is desired. If these customers also have high concurrency, they should increase TTL or accept the trade-off.

## Migration Plan

> **⚠️ Superseded**: The migration plan below was for the rejected Approach B (environment variable gating). See "Migration Plan (Final Approach)" below.

1. All changes are gated by `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` (off by default).
2. Ship in next SDK release with release notes documenting the environment variable.
3. Customers experiencing contention enable the environment variable. No code changes to their app.
4. After bake time (1–2 release cycles), consider enabling by default.
5. **Rollback**: Set environment variable to false (or unset it). Immediate revert to current behavior on next client construction.

### Migration Plan (Final Approach)

1. **Default-on for qualifying TTL**: No environment variable needed. Customers using `WithEncryption` with `keyCacheTimeToLive >= 1 hour` (or default/null) automatically get the background refresh worker. Zero code changes required.
2. **No behavior change for short TTL**: Customers with `keyCacheTimeToLive < 1 hour` see no change — identical to current behavior.
3. **Ship in next SDK release**: Release notes document the proactive refresh behavior and the 1-hour activation threshold.
4. **Rollback**: Customer sets `keyCacheTimeToLive` to a value < 1 hour (e.g. `TimeSpan.FromMinutes(30)`) to disable the background worker. Or downgrades the SDK package.
5. **No opt-in friction**: Unlike the rejected env-var approach, customers benefit immediately on upgrade without learning about or configuring a new switch.

## Performance Benchmarking Strategy

The fix must be validated with quantitative evidence, not just passing tests. The benchmark should reproduce the customer's actual workload — not a synthetic microbenchmark.

**Customer workload profile (from incident investigation):**
- Service running high-concurrency `CosmosFeedIterator<T>` reads with client-side encryption enabled
- Multiple encrypted properties per document (nested objects/arrays → per-leaf-value semaphore acquisition)
- Azure Key Vault-backed key resolver (`KeyResolver(DefaultTokenCredential)`) with `DefaultAzureCredential` (Managed Identity in production)
- Sustained concurrent load — multiple feed iterators running in parallel
- Failure point: `ProtectedDataEncryptionKey` cache time-to-live expires (every 1–2 hours) → first thread blocks on sync Key Vault I/O inside semaphore → queued threads' cancellation tokens fire → `OperationCanceledException`
- Cancellation timeout: ~5s (inferred from call stack — Change Feed Processor lease rebalance or request timeout)

**The benchmark must reproduce this pattern**: concurrent feed iterator reads decrypting multi-property documents, crossing a `ProtectedDataEncryptionKey` cache time-to-live boundary, with realistic Key Vault latency.

**What to measure:**
- **Semaphore hold time** (p50, p95, p99): How long each thread holds the semaphore during `BuildProtectedDataEncryptionKeyAsync`. Baseline (without fix) should show 200ms–2.4s on cache miss. With fix, should drop to microseconds.
- **End-to-end decrypt latency** (p50, p95, p99): Time from `ReadNextAsync` call to decrypted response, under concurrent load.
- **Throughput**: Decrypted documents per second at N concurrent feed iterators.
- **Azure Key Vault call count**: Total `Resolve` + `UnwrapKey` calls to Key Vault over a fixed workload. Should drop from O(`ProtectedDataEncryptionKey` cache misses × 2) to O(Client Encryption Keys × 1 per time-to-live interval).
- **`OperationCanceledException` rate**: With a 5s cancellation timeout (matching customer pattern), count how many operations are cancelled. Baseline should reproduce the customer's issue; with fix, should be zero.

**Benchmark configuration:**
- Concurrent feed iterators: 1, 10, 50, 100
- Encrypted properties per document: 1, 5, 10
- Documents per page: 10, 50
- Client Encryption Key count: 1, 5 (to test multi-Client Encryption Key refresh burst)
- Key Vault latency: simulated via `InMemoryKeyResolver` with configurable delay (0ms, 100ms, 500ms, 2000ms) — 500ms represents typical Azure Key Vault latency, 2000ms represents cold token + Azure Key Vault worst case
- `ProtectedDataEncryptionKey` cache state: warm (cache hit) and cold (force time-to-live expiry to simulate the 2-hour boundary)
- **Key scenario (must reproduce customer failure)**: Start with warm cache under sustained concurrent reads (50+ feed iterators), then force `ProtectedDataEncryptionKey` cache time-to-live expiry mid-workload with 500ms simulated Azure Key Vault latency and 5s cancellation timeout. Baseline should show `OperationCanceledException` cascade; with fix, should show zero cancellations.

**Comparison:**
- Environment variable off (baseline — current behavior) vs. environment variable on (with fix)
- Same workload, same concurrency, same simulated Key Vault latency
- Output: CSV with latency percentiles, throughput, Azure Key Vault call counts, cancellation counts

**Where to run:**
- Unit-level microbenchmarks (in-process, `InMemoryKeyResolver` with configurable delay) for semaphore hold time and throughput
- Emulator end-to-end benchmarks for end-to-end latency with real Cosmos operations (requires emulator)

## Open Questions

> **⚠️ Superseded**: Questions 1–5 below were for the rejected Approach B. Retained for context. See "Open Questions (Final Approach)" below.

1. Should the resolved-client cache (`IKeyEncryptionKey`) be invalidated on a timer, or only on Customer Master Key URL mismatch? The `CryptographyClient` is stateless and long-lived, so indefinite caching until URL change seems safe.
2. What should the prefetch cache time-to-live be? Should it match `ProtectedDataEncryptionKey` cache time-to-live (1–2 hours) or be shorter?
3. **Azure Key Vault burst on multi-Client Encryption Key refresh**: If a customer has N distinct Client Encryption Keys all created around the same time, their prefetch caches expire roughly together, causing N concurrent `ResolveAsync` + `UnwrapKeyAsync` calls to Azure Key Vault in one burst. Should the proactive refresh window include jitter/stagger (e.g., random offset within the percentage-based refresh window) to spread calls over time? Should there be a rate limiter (max M concurrent background Azure Key Vault calls)?
4. **Single vs. dual `ConcurrentDictionary` for prefetch**: The current design uses two dictionaries — `ConcurrentDictionary<string, byte[]>` (result cache) and `ConcurrentDictionary<string, Task>` (inflight deduplication). The result cache serves the sync `UnwrapKey` path (needs `byte[]` immediately, can't `await`). The inflight dictionary is ephemeral (populated only during active Azure Key Vault calls, removed on completion). An alternative is a single `ConcurrentDictionary<string, Task<byte[]>>` where the sync path calls `.Result` (safe when the task is already completed from a prior prefetch). Trade-off: fewer data structures vs. less explicit intent. Should the implementer choose based on readability?
5. **Stale or missing prefetch cache entry at sync `UnwrapKey` time**: If the prefetch cache does not have a refreshed `ProtectedDataEncryptionKey` entry when the sync `UnwrapKey` is called inside the semaphore (e.g. prefetch failed, prefetch was slow, or the entry expired between prefetch and semaphore acquisition), the sync path falls through to the existing `Resolve()` + `UnwrapKey()` Key Vault calls — identical to current behavior. The semaphore hold time in this case is the same as today (200ms–2.4s on cache miss). This is the expected fallback; the optimization is best-effort and the worst case is unchanged from the status quo.

### Open Questions (Final Approach)

1. **How to access PDEK cache entries for monitoring**: MDE's `ProtectedDataEncryptionKey` uses a static `LocalCache` with no enumeration API. Options: (a) maintain a parallel tracking dictionary in `EncryptionCosmosClient` that records `(cekId, kekId, wrappedBytes, creationTime)` on every `GetOrCreate` call; (b) reflect into the `LocalCache` internals (fragile); (c) propose an API addition to MDE. Which approach is preferred?
2. **Semaphore acquisition during background refresh**: The background worker must acquire `EncryptionKeyCacheSemaphore` to call `ProtectedDataEncryptionKey.GetOrCreate`. Should it use a timeout (e.g., 30s) to avoid deadlocking with a slow hot-path thread? What happens if refresh is delayed beyond the TTL window — should it be skipped?
3. **Refresh window percentage**: Currently specified as 80% of TTL elapsed (i.e., refresh in the last 20%). Is 20% too aggressive (too early) or too conservative (too late)? For TTL = 1 hour, refresh starts at T=48 min — is 12 minutes of runway sufficient given Key Vault latency + potential 429 backoff?
4. **Multiple `EncryptionCosmosClient` instances**: If a customer creates multiple `CosmosClient.WithEncryption(...)` instances sharing the same `IKeyEncryptionKeyResolver`, should the background workers coordinate (to avoid duplicate Key Vault calls for the same CEK)? Or is this acceptable overhead given the serial queue and low CEK count?
5. **Cold start behavior**: On first-ever PDEK resolution (no prior cache entry), the background worker has nothing to refresh. The first request still hits Key Vault under the semaphore. Is this acceptable, or should the worker also handle pre-warming (e.g., eagerly resolve all known CEKs at startup)?
6. **Scan interval configuration**: Should the scan interval (how often the monitor thread checks for entries approaching expiry) be configurable, or is a fixed formula (`min(ttl * 0.1, 5 minutes)`) sufficient for all scenarios?

## Final Approach (Team Decision): Proactive PDEK Cache Background Refresh

After offline review, the team rejected both Approach A (DEK byte cache) and the full Approach B (async prefetch subclass). The reasons:

1. **DEK byte cache masks KEK revocation** (Approach A limitation): Cached unwrapped bytes bypass the resolver entirely — a revoked KEK is never detected until the cache entry expires. The `VerifyKekRevokeHandling` test fails. Simple eviction before each read defeats the cache.
2. **PDEK TTL < DEK TTL mismatch is a security risk**: If the customer configures a short PDEK TTL for security reasons, a longer DEK byte cache overrides that intent.
3. **Async prefetch subclass is over-engineered**: The `CachingEncryptionKeyStoreProviderImpl` with environment variable gating, dual `ConcurrentDictionary`, resolved-client cache, and disposal lifecycle adds significant complexity for what is fundamentally a cache warming problem.

### Chosen approach: Background refresh worker on the PDEK cache

The PDEK cache (`ProtectedDataEncryptionKey.GetOrCreate`) already holds the unwrapped key material — when a PDEK is cached, the plaintext DEK bytes are available to the consumer without any Key Vault call. The problem is only the **cache miss at TTL expiry** under concurrent load.

**Solution**: Add a background worker thread that monitors the TTL of every PDEK cache entry and proactively triggers a refresh for entries that are about to expire. This ensures the PDEK cache is always warm when concurrent requests arrive.

#### How it works

```
┌─────────────────────────────────────────────────────────────┐
│  WithEncryption(cosmosClient, resolver, name, ttl)          │
│      │                                                       │
│      ├─ keyCacheTimeToLive >= 1 hour?                        │
│      │     YES → Start PdekRefreshWorker background thread   │
│      │     NO  → No background thread (zero overhead)        │
│      │                                                       │
│  PdekRefreshWorker (single background thread):              │
│      │                                                       │
│      ├─ Periodically scans PDEK cache entries               │
│      ├─ Identifies entries approaching TTL expiry            │
│      ├─ Queues them for refresh                             │
│      │                                                       │
│  RefreshQueue (serial processing thread):                   │
│      │                                                       │
│      ├─ Dequeues one entry at a time                        │
│      ├─ Calls ResolveAsync + UnwrapKeyAsync                 │
│      ├─ Rebuilds ProtectedDataEncryptionKey                 │
│      ├─ Updates PDEK cache                                  │
│      ├─ On 429: backs off per Retry-After header            │
│      └─ Continues to next queued item                       │
└─────────────────────────────────────────────────────────────┘
```

#### Entry point

The `WithEncryption` extension method in `EncryptionCosmosClientExtensions.cs` passes `keyCacheTimeToLive` to the `EncryptionCosmosClient` constructor. This is where the background worker is conditionally started:

```csharp
public static CosmosClient WithEncryption(
    this CosmosClient cosmosClient,
    IKeyEncryptionKeyResolver keyEncryptionKeyResolver,
    string keyEncryptionKeyResolverName,
    TimeSpan? keyCacheTimeToLive = null)
```

If `keyCacheTimeToLive >= TimeSpan.FromHours(1)`, the `EncryptionCosmosClient` starts the background refresh infrastructure. Otherwise, no worker is created — zero CPU overhead for short-TTL configurations.

#### Activation rules

| `keyCacheTimeToLive` value | Behavior |
|---|---|
| `null` (default = 1 hour) | Background refresh worker **activated** |
| `>= 1 hour` | Background refresh worker **activated** |
| `< 1 hour` (e.g. 10 minutes) | **No background thread** — standard cache miss behavior. No wasted CPU cycles on NO-OPs |
| `TimeSpan.Zero` (no caching) | **No background thread** — every call rebuilds PDEK (existing test behavior preserved) |

#### Serial refresh queue with 429 backoff

To comply with Azure Key Vault RPS limits, refreshes are **not** performed in parallel:

1. **Monitor thread** scans PDEK cache entries, identifies those within a refresh window (e.g. 80% of TTL elapsed), and enqueues them
2. **Refresh thread** picks items from the queue one at a time:
   - Calls `ResolveAsync(keyId)` → `UnwrapKeyAsync(wrappedKey)` via the `IKeyEncryptionKeyResolver`
   - Rebuilds the `ProtectedDataEncryptionKey` and updates the cache
   - On success: proceeds to next queued item
   - On HTTP 429 (throttled): reads `Retry-After` header, sleeps for that duration (or exponential backoff if no header), then retries
   - On other transient failure: logs, skips entry (it will be retried on next scan cycle), proceeds to next item
3. **Single-threaded serial processing** ensures at most one Key Vault call is in-flight at any time from the background worker

#### KEK revocation compatibility

This approach is fully compatible with KEK revocation detection because:

- `DataEncryptionKeyCacheTimeToLive` remains `TimeSpan.Zero` — no DEK byte cache
- Every PDEK cache miss (including background refresh) calls the resolver → `Resolve()` + `UnwrapKey()`
- If KEK is revoked, the resolver throws `RequestFailedException(403)`
- The existing force-refresh flow in `BuildEncryptionAlgorithmForSettingAsync` handles this:
  1. Catches 403 → force-refreshes CEK properties from backend
  2. Retries PDEK build → still 403 → forces gateway cache refresh → throws "needs to be rewrapped"
- Background refresh encountering 403 removes/invalidates the PDEK cache entry, ensuring the next hot-path access triggers the force-refresh flow

The `VerifyKekRevokeHandling` test (which sets `ProtectedDataEncryptionKey.TimeToLive = TimeSpan.Zero`) continues to work because TTL < 1 hour means no background worker is started — every operation rebuilds the PDEK synchronously and detects revocation immediately.

#### Design decisions

| # | Decision | Rationale |
|---|---|---|
| 1 | Background worker in `EncryptionCosmosClient`, not a subclass | No need for `CachingEncryptionKeyStoreProviderImpl` — the refresh logic operates at the PDEK cache level, not the `UnwrapKey` level. Simpler lifecycle (tied to `EncryptionCosmosClient` disposal). |
| 2 | Serial queue (not parallel refresh) | Azure Key Vault throttles at 4000 ops/vault/10s. Serial processing with backoff ensures compliance. Customers typically have 1–10 CEKs — serial refresh completes in seconds. |
| 3 | 429 backoff with `Retry-After` | Standard Azure SDK retry pattern. Exponential backoff as fallback (1s, 2s, 4s, 8s, max 30s). |
| 4 | No worker for TTL < 1 hour | Short TTL implies the customer wants frequent re-validation (security-sensitive). Background refresh would fire too often, wasting CPU with NO-OP cycles. The refresh window would be very small relative to the interval. |
| 5 | No environment variable gating | Unlike the rejected Approach B, this is safe to enable by default for qualifying TTL values. No opt-in needed. Activation is purely TTL-driven. |

#### Lifecycle and disposal

- Background worker is a `Task.Run` long-running loop with `CancellationTokenSource`
- `EncryptionCosmosClient.Dispose()` cancels the token → worker exits gracefully
- Worker observes cancellation between queue items (not mid-Key Vault call — `CancellationToken` is passed to `ResolveAsync`/`UnwrapKeyAsync`)
- Idempotent disposal via `Interlocked.Exchange`

## Future Considerations

### Use the Microsoft Data Encryption library's built-in algorithm cache (`AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate`)

The Microsoft Data Encryption library already has a static `LocalCache<Tuple<DataEncryptionKey, EncryptionType>, AeadAes256CbcHmac256EncryptionAlgorithm>` with a `GetOrCreate` method. The SDK currently bypasses it by calling `new AeadAes256CbcHmac256EncryptionAlgorithm(pdek, encType)` directly at `EncryptionSettingForProperty.cs:114`.

Switching to `GetOrCreate` would eliminate a redundant allocation per encrypted leaf value on the hot path (when the `ProtectedDataEncryptionKey` cache hits, the same `ProtectedDataEncryptionKey` reference produces equal `Tuple` keys → algorithm cache hit). `ProtectedDataEncryptionKey` overrides `Equals`/`GetHashCode` with structural equality (`Name` + `KeyEncryptionKey` + `rootKeyHexString`), so the cache key works correctly.

**Why it's not in this change**: The `GetOrCreate` call occurs *after* the semaphore is released — it doesn't reduce semaphore contention. To skip the semaphore entirely, the cache check would need to be hoisted above `BuildProtectedDataEncryptionKeyAsync`, which reintroduces the ETag validation complexity and interaction with the Forbidden retry path. The async prefetch already makes the semaphore hold time microseconds, so the marginal benefit of skipping it entirely is small.

**Low-risk cleanup**: `new` → `GetOrCreate` is a one-line change that saves allocations without changing semantics. Does not conflict with the `injectedAlgorithm` test hook (that path returns before reaching line 114). Could be done independently of this change.
