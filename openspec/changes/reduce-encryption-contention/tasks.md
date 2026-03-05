## 1. Security Validation — Background DEK Refresh & Indefinite IKeyEncryptionKey Caching

- [ ] 1.1 **Threat model: plaintext DEK in prefetch cache.** Document what is cached (plaintext AES-256 bytes), where it lives (in-process `ConcurrentDictionary`), and confirm it is the same key material already held by MDE's `ProtectedDataEncryptionKey`. Identify any net-new exposure vs. current state. Deliverable: written assessment in this task item or design.md addendum.
- [ ] 1.2 **Threat model: background `Task.Run` refresh.** Identify risks of a background task calling `ResolveAsync` + `UnwrapKeyAsync` on a timer-like pattern: (a) GC rooting of provider/credential chain while task is in-flight, (b) token credential refresh happening on a ThreadPool thread outside the caller's context, (c) plaintext bytes being written to cache from a background thread after `Dispose()` is called. For each risk, document mitigation (CancellationTokenSource, Interlocked dispose guard, cancellation token observation).
- [ ] 1.3 **Threat model: indefinite `IKeyEncryptionKey` caching.** Confirm the cached `CryptographyClient` contains no secret material (only vault URL + key name + version + HTTP pipeline reference). Identify the invalidation trigger (CMK URL change on key rotation). Document whether an indefinite cache (no TTL, invalidate only on URL mismatch) is safe, or whether a bounded TTL is needed. Check: does the `CryptographyClient` hold a reference to a specific key version, and what happens if the key is disabled/deleted in Key Vault?
- [ ] 1.4 **Key rotation scenario.** Confirm that CEK rewrap (the only runtime rotation) does not change plaintext DEK bytes, so all caches remain valid. CEK replacement is out of scope (requires data migration, offline operation). Document this assumption.
- [ ] 1.5 **Stale key material window.** Quantify the maximum time plaintext DEK bytes from a revoked CMK can remain in memory (prefetch cache TTL + PDEK TTL). CEK replacement is out of scope. Confirm the window is acceptable per Azure Key Vault best practices.
- [ ] 1.6 **Explore: can `AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate` replace `new`?** MDE has a built-in static `LocalCache` for algorithm instances keyed by `(DataEncryptionKey, EncryptionType)`. The SDK bypasses it by calling `new` directly. Evaluate: (a) does `ProtectedDataEncryptionKey.Equals`/`GetHashCode` produce correct cache hits for the same logical key? (b) is there any thread-safety concern with MDE's `LocalCache`? (c) could this be a standalone one-line cleanup independent of this change? Document findings.
- [ ] 1.7 **Review with implementing engineer.** Walk through findings from 1.1–1.6 before proceeding to implementation. Resolve any open questions. Update design.md Risks section if new risks are identified.

## 2. Environment Variable Feature Gate

- [ ] 2.1 Read `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` in `EncryptionCosmosClient` constructor (via `Environment.GetEnvironmentVariable`, case-insensitive `true` check). Store as `internal readonly bool IsOptimisticDecryptionEnabled`.
- [ ] 2.2 Verify env var is read once at construction and cached — subsequent env var changes do not affect existing client instances.
- [ ] 2.3 Add unit test: env var not set → `IsOptimisticDecryptionEnabled` is false.
- [ ] 2.4 Add unit test: env var set to `true` / `True` / `TRUE` → `IsOptimisticDecryptionEnabled` is true.
- [ ] 2.5 Add unit test: env var set to `false` / empty / garbage → `IsOptimisticDecryptionEnabled` is false.

## 3. Resolved Client Cache (`CachingEncryptionKeyStoreProviderImpl`)

- [ ] 4.1 Create `CachingEncryptionKeyStoreProviderImpl` as a sealed internal class extending `EncryptionKeyStoreProviderImpl`.
- [ ] 4.2 Add `ConcurrentDictionary<string, IKeyEncryptionKey> resolvedClientCache` field.
- [ ] 3.3 Override `UnwrapKey`: before calling `Resolve(keyId)`, check `resolvedClientCache.TryGetValue(keyId, out client)`. On hit, use cached client directly for `UnwrapKey`. On miss, call `Resolve`, store result, then call `UnwrapKey`.
- [ ] 3.4 Add unit test: first call for a CMK URL calls `Resolve`; second call skips `Resolve` and calls only `UnwrapKey` (verify via mock).
- [ ] 3.5 Add unit test: different CMK URLs get independent cache entries; resolving one does not affect the other.

## 4. Async DEK Prefetch (`CachingEncryptionKeyStoreProviderImpl`)

- [ ] 4.1 Add `ConcurrentDictionary<string, byte[]> prefetchCache` field to `CachingEncryptionKeyStoreProviderImpl`.
- [ ] 4.2 Add `ConcurrentDictionary<string, Task> inflightPrefetches` field for deduplication.
- [ ] 4.3 Implement `internal async Task PrefetchUnwrapKeyAsync(string keyId, string algorithm, byte[] wrappedKey, CancellationToken ct)`: check prefetch cache → if miss, check inflight → if miss, call `ResolveAsync` + `UnwrapKeyAsync`, store result in prefetch cache with TTL, remove from inflight.
- [ ] 4.4 Update `UnwrapKey` override in `CachingEncryptionKeyStoreProviderImpl`: check prefetch cache first (by `wrappedKey.ToHexString()`). On hit, return immediately. On miss, fall through to resolved-client-cache path then to sync fallback.
- [ ] 4.5 Wire `PrefetchUnwrapKeyAsync` call into `EncryptionSettingForProperty.BuildEncryptionAlgorithmForSettingAsync` — call it BEFORE `SemaphoreSlim.WaitAsync`, wrapped in try/catch (swallow non-cancellation exceptions, propagate `OperationCanceledException`).
- [ ] 4.6 Add unit test: prefetch populates cache → sync `UnwrapKey` returns immediately without Key Vault call.
- [ ] 4.7 Add unit test: prefetch cache miss → sync `UnwrapKey` falls through to sync Resolve + UnwrapKey (existing behavior).
- [ ] 4.8 Add unit test: 50 concurrent threads calling `PrefetchUnwrapKeyAsync` for the same key → only one `ResolveAsync` + `UnwrapKeyAsync` call (deduplication).
- [ ] 4.9 Add unit test: prefetch throws (simulated KV failure) → caller proceeds to semaphore and sync path without error.
- [ ] 4.10 Add unit test: prefetch throws `OperationCanceledException` → exception propagates to caller.

## 5. Proactive Background Refresh

- [ ] 5.1 In `CachingEncryptionKeyStoreProviderImpl`: add `CancellationTokenSource backgroundCts` field.
- [ ] 5.2 On prefetch cache access, if entry is within a jittered window before TTL expiry (random offset within 2–5 minutes before expiry, per key, to stagger multi-CEK refreshes), schedule `Task.Run` to call `ResolveAsync` + `UnwrapKeyAsync` and update the cache entry. Use `backgroundCts.Token`. Deduplicate via `inflightPrefetches`.
- [ ] 5.3 Add `Cleanup()` method: cancel `backgroundCts`, clear `prefetchCache`, clear `resolvedClientCache`, clear `inflightPrefetches`. Use `Interlocked.Exchange(ref disposed, 1)` for idempotent disposal.
- [ ] 5.4 Wire `Cleanup()` into `EncryptionCosmosClient.Dispose(bool disposing)`.
- [ ] 5.5 Add unit test: access a cache entry near TTL expiry → background refresh fires (verify via mock that `ResolveAsync` is called).
- [ ] 5.6 Add unit test: background refresh fails → existing cache entry persists until TTL; no exception propagated.
- [ ] 5.7 Add unit test: `Cleanup()` cancels in-flight background tasks (verify `OperationCanceledException` observed by background task).
- [ ] 5.8 Add unit test: double `Cleanup()` is safe (no `ObjectDisposedException`).

## 6. Integration & Wiring

- [ ] 6.1 In `EncryptionCosmosClient` constructor: if env var is on, instantiate `CachingEncryptionKeyStoreProviderImpl` instead of `EncryptionKeyStoreProviderImpl`.
- [ ] 6.2 Expose `PrefetchUnwrapKeyAsync` on base class as a virtual no-op (`return Task.CompletedTask`) so `EncryptionSettingForProperty` can call it without knowing the concrete type.
- [ ] 6.3 Verify build: `dotnet build Microsoft.Azure.Cosmos.Encryption/src/ -c Release` — 0 errors, 0 warnings.
- [ ] 6.4 Run existing unit tests: `dotnet test Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/ --no-build` — all pass.
- [ ] 6.5 Run emulator E2E tests with `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED=true`: all existing `MdeEncryptionTests` pass.
- [ ] 6.6 Run emulator E2E tests without env var: all existing `MdeEncryptionTests` pass (verify zero behavior change).

## 7. Contract & API Validation

- [ ] 7.1 Verify no public API changes: diff the contracts file before and after. No new public types, methods, or properties.
- [ ] 7.2 Verify `EncryptionKeyStoreProviderImpl` remains `internal`.
- [ ] 7.3 Verify `CachingEncryptionKeyStoreProviderImpl` is `internal sealed`.

## 8. Performance Benchmarking

- [ ] 8.1 Create `InMemoryKeyResolver` test harness with configurable delay per `Resolve`/`UnwrapKey` call and call-count tracking.
- [ ] 8.2 Benchmark: semaphore hold time (p50/p95/p99) at 50 concurrent threads, simulated KV latency 500ms, env var off vs. on. Expect: off = hundreds of ms, on = microseconds.
- [ ] 8.3 Benchmark: end-to-end decrypt throughput (docs/sec) at 10, 50, 100 concurrent feed iterators with 5 encrypted properties per document. Env var off vs. on.
- [ ] 8.4 Benchmark: AKV call count over 1000 decrypt operations with 1 CEK. Env var off (expect 2 per PDEK miss) vs. on (expect 1 per TTL refresh, resolved client cached).
- [ ] 8.5 Benchmark: `OperationCanceledException` count at 50 concurrent threads with 5s cancellation timeout and simulated 2s KV latency. Env var off (expect failures) vs. on (expect zero).
- [ ] 8.6 Benchmark: multi-CEK refresh burst — 5 CEKs with aligned TTLs, verify jitter staggers AKV calls (measure peak concurrent AKV calls).
- [ ] 8.7 Document results in a comparison table (baseline vs. optimized) with all metrics.
