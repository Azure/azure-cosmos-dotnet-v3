## Context

Client-side encryption in the Cosmos DB .NET SDK uses a global `SemaphoreSlim(1,1)` that serializes all encryption key resolution operations — specifically, the construction or cache lookup of `ProtectedDataEncryptionKey` and `KeyEncryptionKey` objects via the Microsoft Data Encryption library's `GetOrCreate` methods. These are the only operations guarded by the semaphore; encrypt/decrypt of document payloads happens outside the semaphore. The semaphore guards the Microsoft Data Encryption library's `ProtectedDataEncryptionKey.GetOrCreate` and `KeyEncryptionKey.GetOrCreate` — both of which are cache lookups on the hot path but trigger synchronous Key Vault HTTP calls on cache miss. `ProtectedDataEncryptionKey` resolution is sync, on the hot path, and makes two blocking HTTP calls to Key Vault (Resolve + UnwrapKey) under the semaphore.

The call amplification is severe: `DecryptObjectAsync` calls `BuildEncryptionAlgorithmForSettingAsync` per encrypted leaf value per document per page. With concurrent feed iterators, this produces thousands of semaphore acquisitions per second, all serialized to a single permit. Note: both read (decrypt) and write (encrypt) paths acquire the semaphore for key resolution; however, the customer workload triggering this issue is read-heavy (Change Feed Processor with concurrent `FeedIterator<T>`). A dedicated investigation task (1.9) covers whether specific operation types trigger disproportionate contention.

**Structural constraint**: The Microsoft Data Encryption library's `ProtectedDataEncryptionKey` constructor chain is sync (C# cannot make base-ctor calls async). `EncryptionKeyStoreProvider.UnwrapKey` returns `byte[]` (sync). The Microsoft Data Encryption library's `LocalCache.GetOrCreate` takes `Func<T>` not `Func<Task<T>>`. The sync chain from `ProtectedDataEncryptionKey` → `KeyEncryptionKey.DecryptEncryptionKey` → `UnwrapKey` → `Resolve` + `UnwrapKey` on Key Vault cannot be made async at any point in the Microsoft Data Encryption library's type hierarchy. However, `IKeyEncryptionKeyResolver` exposes `ResolveAsync` and `IKeyEncryptionKey` exposes `UnwrapKeyAsync` — these async variants are reachable from our code, just not through the Microsoft Data Encryption library's call chain.

## Goals / Non-Goals

**Goals:**
- Move `ProtectedDataEncryptionKey` resolution (Key Vault I/O) off the hot path via async prefetch outside the semaphore
- Prevent Azure Key Vault overload: proactive background refresh, deduplicated to one call per key per interval
- Reduce Key Vault calls per refresh: cache resolved `IKeyEncryptionKey` so only `UnwrapKey` hits Azure Key Vault, not `Resolve` + `UnwrapKey`
- Gate all changes behind an opt-in environment variable for safe rollout — zero behavior change when disabled
- No public API changes. No breaking changes.

**Non-Goals:**
- Changing the Microsoft Data Encryption library's type hierarchy or making the `ProtectedDataEncryptionKey` constructor async
- Removing the semaphore entirely (it guards the Microsoft Data Encryption library internal state mutation — still needed)
- Per-key semaphores (reduces cross-key contention but same-key still serializes; added complexity for marginal gain)
- Caching the `AeadAes256CbcHmac256EncryptionAlgorithm` at the `EncryptionSettingForProperty` level (would need to be hoisted above the semaphore to reduce contention, which reintroduces ETag validation and Forbidden retry path complexity; however, the Microsoft Data Encryption library has a built-in `GetOrCreate` cache that the SDK currently bypasses — see Future Considerations)- Enabling the Microsoft Data Encryption library's Data Encryption Key byte cache (`DataEncryptionKeyCacheTimeToLive`) as a standalone layer (redundant when async prefetch is working — the sync `UnwrapKey` path reads from the prefetch cache, never reaching the Microsoft Data Encryption library’s built-in cache)
- Client Encryption Key rotation handling (Client Encryption Key replacement requires data migration — re-encrypting every document — and is an offline operation; not a runtime concern for caching)- Supporting key rotation detection at the cache level (Client Encryption Key rewrap doesn't change plaintext bytes; new Client Encryption Key policy creates new `EncryptionSettingForProperty` objects)

## Decisions

### Decision 1: Async prefetch in a sealed subclass, not in the base `EncryptionKeyStoreProviderImpl`

**Chosen**: New `CachingEncryptionKeyStoreProviderImpl` (sealed, internal) extends `EncryptionKeyStoreProviderImpl`. Base class stays clean sync-only. `EncryptionCosmosClient` instantiates the subclass when environment variable is on.

**Rationale**: The base class serves all existing customers with zero behavior change. The subclass adds: prefetch `ConcurrentDictionary`, resolved-client cache (a `ConcurrentDictionary<string, IKeyEncryptionKey>` that caches the `CryptographyClient` returned by `IKeyEncryptionKeyResolver.Resolve()` per Customer Master Key URL — called "resolved-client" because it caches the resolved `IKeyEncryptionKey` client object, not the unwrapped key bytes), `CancellationTokenSource`, `Cleanup()` method, and overridden `UnwrapKey` that checks prefetch cache first. Separation keeps the risk surface isolated.

**Rejected alternative**: Adding prefetch directly to base class with conditional branches — mixes concerns, harder to reason about lifecycle, risk of unintended behavior when environment variable is off.

### Decision 2: `ConcurrentDictionary` for prefetch cache (not `MemoryCache` or the Microsoft Data Encryption library's `LocalCache`)

**Chosen**: `ConcurrentDictionary<string, byte[]>` with manual time-to-live tracking (expiry stored alongside value).

**Rationale**: The Microsoft Data Encryption library's `LocalCache.GetOrCreate` is get-or-create only — no `Set`, `Remove`, or `Evict` API. `MemoryCache` adds a dependency and has its own concurrency model. `ConcurrentDictionary` is simple, lock-free reads, and we control time-to-live ourselves.

### Decision 3: Proactive refresh via `Task.Run`, not `Timer`

**Chosen**: On cache access, if within a configurable refresh window of expiry, fire `Task.Run` to refresh in background. The refresh window SHALL be **20% of the cache time-to-live, capped at a maximum of 5 minutes**. For example: time-to-live = 1 hour → refresh at 12 minutes before expiry (20% × 60 min = 12 min, but capped to 5 min → 5 minutes); time-to-live = 10 minutes → refresh at 2 minutes before expiry (20% × 10 min = 2 min). Deduplicate via `ConcurrentDictionary<string, Task>`.

**Rationale**: `Timer` requires managing timer lifecycle per key, disposal ordering, and thread-pool callbacks. `Task.Run` on access is simpler — only fires when the key is actually being used (no wasted refresh for unused keys). Tied to `CancellationTokenSource` for cleanup on dispose. The refresh window is percentage-based rather than a fixed duration because the `keyCacheTimeToLive` is customer-configurable (defaults to 1 hour, max 2 hours from `ProtectedDataEncryptionKey.TimeToLive`, but can be set shorter). A fixed 5-minute window would be disproportionate for short time-to-live values.

**Rejected alternative**: `System.Threading.Timer` per key — more deterministic timing but complex lifecycle management (dispose timers on cache eviction, handle timer callbacks after disposal).

## Risks / Trade-offs

- **[Non-risk] Cache coherence on key rotation**: Client Encryption Key rewrap (same plaintext Data Encryption Key, new Customer Master Key wrapper) is the only runtime rotation — all caches remain valid. Client Encryption Key replacement is out of scope (requires data migration, offline operation).

- **[Risk] Background refresh lifecycle]: `Task.Run` captures `this`, keeping the provider and all its dependencies (resolver, credential chain) GC-rooted until the task completes. → **Mitigation**: `CancellationTokenSource` cancelled on `Cleanup()`/`Dispose()`. `Interlocked.Exchange` for idempotent dispose. Background tasks observe cancellation token.

- **[Risk] Two copies of plaintext Data Encryption Key in memory**: `ProtectedDataEncryptionKey` internal field + prefetch `ConcurrentDictionary`. → **Mitigation**: Same process, same threat boundary, same time-to-live. No new attack surface. Industry standard (Azure Key Vault SDK recommends caching).

- **[Risk] Environment variable read at construction only**: If an operator enables the environment variable and restarts the app, old client instances still run without optimization. → **Mitigation**: This is by design — matches SDK `ConfigurationManager` pattern. Client must be reconstructed to pick up changes.

- **[Risk] Azure Key Vault burst on multi-Client Encryption Key proactive refresh**: If multiple Client Encryption Keys have similar time-to-live values (common at startup — all populated in a short window), their proactive refreshes fire simultaneously. Each refresh is deduplicated per key (1 call per Client Encryption Key), but N Client Encryption Keys = N concurrent Azure Key Vault calls. Azure Key Vault throttles at 4000 ops/vault/10s — unlikely to hit for typical customers (1–10 Client Encryption Keys), but possible for large deployments. → **Mitigation (recommended)**: Add jitter to the proactive refresh window (random offset within the percentage-based refresh window, per key) so Client Encryption Keys don’t all refresh at the same instant. Consider a concurrency limiter on background Azure Key Vault calls.

- **[Trade-off] Prefetch adds complexity for correctness**: Async prefetch + background refresh + disposal lifecycle is more complex than a one-liner (`TimeSpan.Zero` → `TimeSpan.FromHours(1)`). But it solves the actual problem — `ProtectedDataEncryptionKey` resolution is async, off the hot path, and doesn’t overload Azure Key Vault. → **Accept**: Complexity is isolated in a sealed subclass, gated by environment variable.

## Migration Plan

1. All changes are gated by `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` (off by default).
2. Ship in next SDK release with release notes documenting the environment variable.
3. Customers experiencing contention enable the environment variable. No code changes to their app.
4. After bake time (1–2 release cycles), consider enabling by default.
5. **Rollback**: Set environment variable to false (or unset it). Immediate revert to current behavior on next client construction.

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

1. Should the resolved-client cache (`IKeyEncryptionKey`) be invalidated on a timer, or only on Customer Master Key URL mismatch? The `CryptographyClient` is stateless and long-lived, so indefinite caching until URL change seems safe.
2. What should the prefetch cache time-to-live be? Should it match `ProtectedDataEncryptionKey` cache time-to-live (1–2 hours) or be shorter?
3. **Azure Key Vault burst on multi-Client Encryption Key refresh**: If a customer has N distinct Client Encryption Keys all created around the same time, their prefetch caches expire roughly together, causing N concurrent `ResolveAsync` + `UnwrapKeyAsync` calls to Azure Key Vault in one burst. Should the proactive refresh window include jitter/stagger (e.g., random offset within the percentage-based refresh window) to spread calls over time? Should there be a rate limiter (max M concurrent background Azure Key Vault calls)?
4. **Single vs. dual `ConcurrentDictionary` for prefetch**: The current design uses two dictionaries — `ConcurrentDictionary<string, byte[]>` (result cache) and `ConcurrentDictionary<string, Task>` (inflight deduplication). The result cache serves the sync `UnwrapKey` path (needs `byte[]` immediately, can't `await`). The inflight dictionary is ephemeral (populated only during active Azure Key Vault calls, removed on completion). An alternative is a single `ConcurrentDictionary<string, Task<byte[]>>` where the sync path calls `.Result` (safe when the task is already completed from a prior prefetch). Trade-off: fewer data structures vs. less explicit intent. Should the implementer choose based on readability?
5. **Stale or missing prefetch cache entry at sync `UnwrapKey` time**: If the prefetch cache does not have a refreshed `ProtectedDataEncryptionKey` entry when the sync `UnwrapKey` is called inside the semaphore (e.g. prefetch failed, prefetch was slow, or the entry expired between prefetch and semaphore acquisition), the sync path falls through to the existing `Resolve()` + `UnwrapKey()` Key Vault calls — identical to current behavior. The semaphore hold time in this case is the same as today (200ms–2.4s on cache miss). This is the expected fallback; the optimization is best-effort and the worst case is unchanged from the status quo.

## Future Considerations

### Use the Microsoft Data Encryption library's built-in algorithm cache (`AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate`)

The Microsoft Data Encryption library already has a static `LocalCache<Tuple<DataEncryptionKey, EncryptionType>, AeadAes256CbcHmac256EncryptionAlgorithm>` with a `GetOrCreate` method. The SDK currently bypasses it by calling `new AeadAes256CbcHmac256EncryptionAlgorithm(pdek, encType)` directly at `EncryptionSettingForProperty.cs:114`.

Switching to `GetOrCreate` would eliminate a redundant allocation per encrypted leaf value on the hot path (when the `ProtectedDataEncryptionKey` cache hits, the same `ProtectedDataEncryptionKey` reference produces equal `Tuple` keys → algorithm cache hit). `ProtectedDataEncryptionKey` overrides `Equals`/`GetHashCode` with structural equality (`Name` + `KeyEncryptionKey` + `rootKeyHexString`), so the cache key works correctly.

**Why it's not in this change**: The `GetOrCreate` call occurs *after* the semaphore is released — it doesn't reduce semaphore contention. To skip the semaphore entirely, the cache check would need to be hoisted above `BuildProtectedDataEncryptionKeyAsync`, which reintroduces the ETag validation complexity and interaction with the Forbidden retry path. The async prefetch already makes the semaphore hold time microseconds, so the marginal benefit of skipping it entirely is small.

**Low-risk cleanup**: `new` → `GetOrCreate` is a one-line change that saves allocations without changing semantics. Does not conflict with the `injectedAlgorithm` test hook (that path returns before reaching line 114). Could be done independently of this change.
