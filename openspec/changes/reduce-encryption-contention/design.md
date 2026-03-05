## Context

Client-side encryption in the Cosmos DB .NET SDK uses a global `SemaphoreSlim(1,1)` that serializes all encryption key operations. The semaphore guards MDE’s `ProtectedDataEncryptionKey.GetOrCreate` and `KeyEncryptionKey.GetOrCreate` — both of which are cache lookups on the hot path but trigger synchronous Key Vault HTTP calls on cache miss. PDEK resolution is sync, on the hot path, and makes two blocking HTTP calls to Key Vault (Resolve + UnwrapKey) under the semaphore.

The call amplification is severe: `DecryptObjectAsync` calls `BuildEncryptionAlgorithmForSettingAsync` per encrypted leaf value per document per page. With concurrent feed iterators, this produces thousands of semaphore acquisitions per second, all serialized to a single permit.

**Structural constraint**: MDE's `ProtectedDataEncryptionKey` constructor chain is sync (C# cannot make base-ctor calls async). `EncryptionKeyStoreProvider.UnwrapKey` returns `byte[]` (sync). MDE's `LocalCache.GetOrCreate` takes `Func<T>` not `Func<Task<T>>`. The sync chain from `ProtectedDataEncryptionKey` → `KeyEncryptionKey.DecryptEncryptionKey` → `UnwrapKey` → `Resolve` + `UnwrapKey` on Key Vault cannot be made async at any point in MDE's type hierarchy. However, `IKeyEncryptionKeyResolver` exposes `ResolveAsync` and `IKeyEncryptionKey` exposes `UnwrapKeyAsync` — these async variants are reachable from our code, just not through MDE's call chain.

## Goals / Non-Goals

**Goals:**
- Move PDEK resolution (Key Vault I/O) off the hot path via async prefetch outside the semaphore
- Prevent AKV overload: proactive background refresh, deduplicated to one call per key per interval
- Reduce Key Vault calls per refresh: cache resolved `IKeyEncryptionKey` so only `UnwrapKey` hits AKV, not `Resolve` + `UnwrapKey`
- Gate all changes behind an opt-in env var for safe rollout — zero behavior change when disabled
- No public API changes. No breaking changes.

**Non-Goals:**
- Changing MDE's type hierarchy or making the `ProtectedDataEncryptionKey` constructor async
- Removing the semaphore entirely (it guards MDE internal state mutation — still needed)
- Per-key semaphores (reduces cross-key contention but same-key still serializes; added complexity for marginal gain)
- Caching the `AeadAes256CbcHmac256EncryptionAlgorithm` at the `EncryptionSettingForProperty` level (would need to be hoisted above the semaphore to reduce contention, which reintroduces ETag validation and Forbidden retry path complexity; however, MDE has a built-in `GetOrCreate` cache that the SDK currently bypasses — see Future Considerations)- Enabling the MDE DEK byte cache (`DataEncryptionKeyCacheTimeToLive`) as a standalone layer (redundant when async prefetch is working — the sync `UnwrapKey` path reads from the prefetch cache, never reaching MDE’s built-in cache)
- CEK rotation handling (CEK replacement requires data migration — re-encrypting every document — and is an offline operation; not a runtime concern for caching)- Supporting key rotation detection at the cache level (CEK rewrap doesn't change plaintext bytes; new CEK policy creates new `EncryptionSettingForProperty` objects)

## Decisions

### Decision 1: Async prefetch in a sealed subclass, not in the base `EncryptionKeyStoreProviderImpl`

**Chosen**: New `CachingEncryptionKeyStoreProviderImpl` (sealed, internal) extends `EncryptionKeyStoreProviderImpl`. Base class stays clean sync-only. `EncryptionCosmosClient` instantiates the subclass when env var is on.

**Rationale**: The base class serves all existing customers with zero behavior change. The subclass adds: prefetch `ConcurrentDictionary`, resolved-client cache, `CancellationTokenSource`, `Cleanup()` method, and overridden `UnwrapKey` that checks prefetch cache first. Separation keeps the risk surface isolated.

**Rejected alternative**: Adding prefetch directly to base class with conditional branches — mixes concerns, harder to reason about lifecycle, risk of unintended behavior when env var is off.

### Decision 2: `ConcurrentDictionary` for prefetch cache (not `MemoryCache` or MDE's `LocalCache`)

**Chosen**: `ConcurrentDictionary<string, byte[]>` with manual TTL tracking (expiry stored alongside value).

**Rationale**: MDE's `LocalCache.GetOrCreate` is get-or-create only — no `Set`, `Remove`, or `Evict` API. `MemoryCache` adds a dependency and has its own concurrency model. `ConcurrentDictionary` is simple, lock-free reads, and we control TTL ourselves.

### Decision 3: Proactive refresh via `Task.Run`, not `Timer`

**Chosen**: On cache access, if within 5 minutes of expiry, fire `Task.Run` to refresh in background. Deduplicate via `ConcurrentDictionary<string, Task>`.

**Rationale**: `Timer` requires managing timer lifecycle per key, disposal ordering, and thread-pool callbacks. `Task.Run` on access is simpler — only fires when the key is actually being used (no wasted refresh for unused keys). Tied to `CancellationTokenSource` for cleanup on dispose.

**Rejected alternative**: `System.Threading.Timer` per key — more deterministic timing but complex lifecycle management (dispose timers on cache eviction, handle timer callbacks after disposal).

## Risks / Trade-offs

- **[Non-risk] Cache coherence on key rotation**: CEK rewrap (same plaintext DEK, new CMK wrapper) is the only runtime rotation — all caches remain valid. CEK replacement is out of scope (requires data migration, offline operation).

- **[Risk] Background refresh lifecycle]: `Task.Run` captures `this`, keeping the provider and all its dependencies (resolver, credential chain) GC-rooted until the task completes. → **Mitigation**: `CancellationTokenSource` cancelled on `Cleanup()`/`Dispose()`. `Interlocked.Exchange` for idempotent dispose. Background tasks observe cancellation token.

- **[Risk] Two copies of plaintext DEK in memory**: PDEK internal field + prefetch `ConcurrentDictionary`. → **Mitigation**: Same process, same threat boundary, same TTL. No new attack surface. Industry standard (Azure Key Vault SDK recommends caching).

- **[Risk] Env var read at construction only**: If an operator enables the env var and restarts the app, old client instances still run without optimization. → **Mitigation**: This is by design — matches SDK `ConfigurationManager` pattern. Client must be reconstructed to pick up changes.

- **[Risk] AKV burst on multi-CEK proactive refresh**: If multiple Client Encryption Keys have similar TTLs (common at startup — all populated in a short window), their proactive refreshes fire simultaneously. Each refresh is deduplicated per key (1 call per CEK), but N CEKs = N concurrent AKV calls. AKV throttles at 4000 ops/vault/10s — unlikely to hit for typical customers (1–10 CEKs), but possible for large deployments. → **Mitigation (recommended)**: Add jitter to the proactive refresh window (random offset within 2–5 minutes before expiry) so CEKs don’t all refresh at the same instant. Consider a concurrency limiter on background AKV calls.

- **[Trade-off] Prefetch adds complexity for correctness**: Async prefetch + background refresh + disposal lifecycle is more complex than a one-liner (`TimeSpan.Zero` → `TimeSpan.FromHours(1)`). But it solves the actual problem — PDEK resolution is async, off the hot path, and doesn’t overload AKV. → **Accept**: Complexity is isolated in a sealed subclass, gated by env var.

## Migration Plan

1. All changes are gated by `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` (off by default).
2. Ship in next SDK release with release notes documenting the env var.
3. Customers experiencing contention enable the env var. No code changes to their app.
4. After bake time (1–2 release cycles), consider enabling by default.
5. **Rollback**: Set env var to false (or unset it). Immediate revert to current behavior on next client construction.

## Performance Benchmarking Strategy

The fix must be validated with quantitative evidence, not just passing tests. The benchmark should reproduce the customer's actual workload — not a synthetic microbenchmark.

**Customer workload profile (from incident investigation):**
- Service running high-concurrency `CosmosFeedIterator<T>` reads with client-side encryption enabled
- Multiple encrypted properties per document (nested objects/arrays → per-leaf-value semaphore acquisition)
- AKV-backed key resolver (`KeyResolver(DefaultTokenCredential)`) with `DefaultAzureCredential` (Managed Identity in production)
- Sustained concurrent load — multiple feed iterators running in parallel
- Failure point: PDEK cache TTL expires (every 1–2 hours) → first thread blocks on sync Key Vault I/O inside semaphore → queued threads' cancellation tokens fire → `OperationCanceledException`
- Cancellation timeout: ~5s (inferred from call stack — Change Feed Processor lease rebalance or request timeout)

**The benchmark must reproduce this pattern**: concurrent feed iterator reads decrypting multi-property documents, crossing a PDEK cache TTL boundary, with realistic Key Vault latency.

**What to measure:**
- **Semaphore hold time** (p50, p95, p99): How long each thread holds the semaphore during `BuildProtectedDataEncryptionKeyAsync`. Baseline (without fix) should show 200ms–2.4s on cache miss. With fix, should drop to microseconds.
- **End-to-end decrypt latency** (p50, p95, p99): Time from `ReadNextAsync` call to decrypted response, under concurrent load.
- **Throughput**: Decrypted documents per second at N concurrent feed iterators.
- **AKV call count**: Total `Resolve` + `UnwrapKey` calls to Key Vault over a fixed workload. Should drop from O(PDEK misses × 2) to O(CEKs × 1 per TTL interval).
- **`OperationCanceledException` rate**: With a 5s cancellation timeout (matching customer pattern), count how many operations are cancelled. Baseline should reproduce the customer's issue; with fix, should be zero.

**Benchmark configuration:**
- Concurrent feed iterators: 1, 10, 50, 100
- Encrypted properties per document: 1, 5, 10
- Documents per page: 10, 50
- CEK count: 1, 5 (to test multi-CEK refresh burst)
- Key Vault latency: simulated via `InMemoryKeyResolver` with configurable delay (0ms, 100ms, 500ms, 2000ms) — 500ms represents typical AKV latency, 2000ms represents cold token + AKV worst case
- PDEK cache state: warm (cache hit) and cold (force TTL expiry to simulate the 2-hour boundary)
- **Key scenario (must reproduce customer failure)**: Start with warm cache under sustained concurrent reads (50+ feed iterators), then force PDEK TTL expiry mid-workload with 500ms simulated AKV latency and 5s cancellation timeout. Baseline should show `OperationCanceledException` cascade; with fix, should show zero cancellations.

**Comparison:**
- Env var off (baseline — current behavior) vs. env var on (with fix)
- Same workload, same concurrency, same simulated Key Vault latency
- Output: CSV with latency percentiles, throughput, AKV call counts, cancellation counts

**Where to run:**
- Unit-level microbenchmarks (in-process, `InMemoryKeyResolver` with configurable delay) for semaphore hold time and throughput
- Emulator E2E benchmarks for end-to-end latency with real Cosmos operations (requires emulator)

## Open Questions

1. Should the resolved-client cache (`IKeyEncryptionKey`) be invalidated on a timer, or only on CMK URL mismatch? The `CryptographyClient` is stateless and long-lived, so indefinite caching until URL change seems safe.
2. What should the prefetch cache TTL be? Should it match PDEK TTL (1–2 hours) or be shorter?
3. **AKV burst on multi-CEK refresh**: If a customer has N distinct Client Encryption Keys all created around the same time, their prefetch caches expire roughly together, causing N concurrent `ResolveAsync` + `UnwrapKeyAsync` calls to AKV in one burst. Should the proactive refresh window include jitter/stagger (e.g., random offset within 2–5 minutes before expiry) to spread calls over time? Should there be a rate limiter (max M concurrent background AKV calls)?
4. **Single vs. dual `ConcurrentDictionary` for prefetch**: The current design uses two dictionaries — `ConcurrentDictionary<string, byte[]>` (result cache) and `ConcurrentDictionary<string, Task>` (inflight deduplication). The result cache serves the sync `UnwrapKey` path (needs `byte[]` immediately, can't `await`). The inflight dictionary is ephemeral (populated only during active AKV calls, removed on completion). An alternative is a single `ConcurrentDictionary<string, Task<byte[]>>` where the sync path calls `.Result` (safe when the task is already completed from a prior prefetch). Trade-off: fewer data structures vs. less explicit intent. Should the implementer choose based on readability?

## Future Considerations

### Use MDE's built-in algorithm cache (`AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate`)

MDE already has a static `LocalCache<Tuple<DataEncryptionKey, EncryptionType>, AeadAes256CbcHmac256EncryptionAlgorithm>` with a `GetOrCreate` method. The SDK currently bypasses it by calling `new AeadAes256CbcHmac256EncryptionAlgorithm(pdek, encType)` directly at `EncryptionSettingForProperty.cs:114`.

Switching to `GetOrCreate` would eliminate a redundant allocation per encrypted leaf value on the hot path (when the PDEK cache hits, the same `ProtectedDataEncryptionKey` reference produces equal `Tuple` keys → algorithm cache hit). `ProtectedDataEncryptionKey` overrides `Equals`/`GetHashCode` with structural equality (`Name` + `KeyEncryptionKey` + `rootKeyHexString`), so the cache key works correctly.

**Why it's not in this change**: The `GetOrCreate` call occurs *after* the semaphore is released — it doesn't reduce semaphore contention. To skip the semaphore entirely, the cache check would need to be hoisted above `BuildProtectedDataEncryptionKeyAsync`, which reintroduces the ETag validation complexity and interaction with the Forbidden retry path. The async prefetch already makes the semaphore hold time microseconds, so the marginal benefit of skipping it entirely is small.

**Low-risk cleanup**: `new` → `GetOrCreate` is a one-line change that saves allocations without changing semantics. Does not conflict with the `injectedAlgorithm` test hook (that path returns before reaching line 114). Could be done independently of this change.
