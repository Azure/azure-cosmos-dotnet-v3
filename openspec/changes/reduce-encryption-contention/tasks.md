## 1. Security Validation — Background Data Encryption Key Refresh & Indefinite IKeyEncryptionKey Caching

- [ ] 1.1 **Threat model: plaintext Data Encryption Key in prefetch cache.** Document what is cached (plaintext AES-256 bytes), where it lives (in-process `ConcurrentDictionary`), and confirm it is the same key material already held by the Microsoft Data Encryption library's `ProtectedDataEncryptionKey`. Identify any net-new exposure vs. current state. Deliverable: written assessment in this task item or design.md addendum.
- [ ] 1.2 **Threat model: background `Task.Run` refresh.** Identify risks of a background task calling `ResolveAsync` + `UnwrapKeyAsync` on a timer-like pattern: (a) GC rooting of provider/credential chain while task is in-flight, (b) token credential refresh happening on a ThreadPool thread outside the caller's context, (c) plaintext bytes being written to cache from a background thread after `Dispose()` is called. For each risk, document mitigation (CancellationTokenSource, Interlocked dispose guard, cancellation token observation).
- [ ] 1.3 **Threat model: indefinite `IKeyEncryptionKey` caching.** Confirm the cached `CryptographyClient` contains no secret material (only vault URL + key name + version + HTTP pipeline reference). Identify the invalidation trigger (Customer Master Key URL change on key rotation). Document whether an indefinite cache (no time-to-live, invalidate only on URL mismatch) is safe, or whether a bounded time-to-live is needed. Check: does the `CryptographyClient` hold a reference to a specific key version, and what happens if the key is disabled/deleted in Key Vault?
- [ ] 1.4 **Key rotation scenario.** Confirm that Client Encryption Key rewrap (the only runtime rotation) does not change plaintext Data Encryption Key bytes, so all caches remain valid. Client Encryption Key replacement is out of scope (requires data migration, offline operation). Document this assumption.
- [ ] 1.5 **Stale key material window.** Quantify the maximum time plaintext Data Encryption Key bytes from a revoked Customer Master Key can remain in memory (prefetch cache time-to-live + `ProtectedDataEncryptionKey` cache time-to-live). Client Encryption Key replacement is out of scope. Confirm the window is acceptable per Azure Key Vault best practices.
- [ ] 1.6 **Explore: can `AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate` replace `new`?** The Microsoft Data Encryption library has a built-in static `LocalCache` for algorithm instances keyed by `(DataEncryptionKey, EncryptionType)`. The SDK bypasses it by calling `new` directly. Evaluate: (a) does `ProtectedDataEncryptionKey.Equals`/`GetHashCode` produce correct cache hits for the same logical key? (b) is there any thread-safety concern with the Microsoft Data Encryption library's `LocalCache`? (c) could this be a standalone one-line cleanup independent of this change? Document findings.
- [ ] 1.7 **Understand the wrapped Client Encryption Key fetch path from Cosmos DB.** Read `GetClientEncryptionKeyPropertiesAsync` in `EncryptionCosmosClient` and `EncryptionSettingForProperty`. Understand: (a) how the wrapped Client Encryption Key is fetched (gateway-cached read via `AsyncCache`), (b) when it refreshes (force refresh on Forbidden retry, ETag-based staleness check), (c) whether anything in its caching design (AsyncCache pattern, ETag invalidation, gateway cache headers) can inform or be reused for the prefetch cache design. Document any applicable patterns.
- [ ] 1.8 **Review with implementing engineer.** Walk through findings from 1.1–1.9 before proceeding to implementation. Resolve any open questions. Update design.md Risks section if new risks are identified.
- [ ] 1.9 **Operation-type contention analysis.** Investigate whether specific operation types (read/decrypt vs. write/encrypt, point reads vs. feed iterators, Change Feed Processor vs. manual iteration) trigger disproportionate semaphore contention. Map the call path from each operation type to `BuildEncryptionAlgorithmForSettingAsync` and document the semaphore acquisition frequency per operation type. Identify if any operation type can bypass the semaphore (e.g. if the algorithm is already cached at a higher level).

## 2. Environment Variable Feature Gate

- [ ] 2.1 Read `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED` in `EncryptionCosmosClient` constructor (via `Environment.GetEnvironmentVariable`, case-insensitive `true` check). Store as `internal readonly bool IsOptimisticDecryptionEnabled`.
- [ ] 2.2 Verify environment variable is read once at construction and cached — subsequent environment variable changes do not affect existing client instances.
- [ ] 2.3 Add unit test: environment variable not set → `IsOptimisticDecryptionEnabled` is false.
- [ ] 2.4 Add unit test: environment variable set to `true` / `True` / `TRUE` → `IsOptimisticDecryptionEnabled` is true.
- [ ] 2.5 Add unit test: environment variable set to `false` / empty / garbage → `IsOptimisticDecryptionEnabled` is false.

## 3. Resolved Client Cache (`CachingEncryptionKeyStoreProviderImpl`)

- [ ] 4.1 Create `CachingEncryptionKeyStoreProviderImpl` as a sealed internal class extending `EncryptionKeyStoreProviderImpl`.
- [ ] 4.2 Add `ConcurrentDictionary<string, IKeyEncryptionKey> resolvedClientCache` field.
- [ ] 3.3 Override `UnwrapKey`: before calling `Resolve(keyId)`, check `resolvedClientCache.TryGetValue(keyId, out client)`. On hit, use cached client directly for `UnwrapKey`. On miss, call `Resolve`, store result, then call `UnwrapKey`.
- [ ] 3.4 Add unit test: first call for a Customer Master Key URL calls `Resolve`; second call skips `Resolve` and calls only `UnwrapKey` (verify via mock).
- [ ] 3.5 Add unit test: different Customer Master Key URLs get independent cache entries; resolving one does not affect the other.

## 4. Async Data Encryption Key Prefetch (`CachingEncryptionKeyStoreProviderImpl`)

- [ ] 4.1 Add `ConcurrentDictionary<string, byte[]> prefetchCache` field to `CachingEncryptionKeyStoreProviderImpl`.
- [ ] 4.2 Add `ConcurrentDictionary<string, Task> inflightPrefetches` field for deduplication.
- [ ] 4.3 Implement `internal async Task PrefetchUnwrapKeyAsync(string keyId, string algorithm, byte[] wrappedKey, CancellationToken ct)`: check prefetch cache → if miss, check inflight → if miss, call `ResolveAsync` + `UnwrapKeyAsync`, store result in prefetch cache with time-to-live, remove from inflight.
- [ ] 4.4 Update `UnwrapKey` override in `CachingEncryptionKeyStoreProviderImpl`: check prefetch cache first (by `wrappedKey.ToHexString()`). On hit, return immediately. On miss, fall through to resolved-client-cache path then to sync fallback.
- [ ] 4.5 Wire `PrefetchUnwrapKeyAsync` call into `EncryptionSettingForProperty.BuildEncryptionAlgorithmForSettingAsync` — call it BEFORE `SemaphoreSlim.WaitAsync`, wrapped in try/catch (swallow non-cancellation exceptions, propagate `OperationCanceledException`).
- [ ] 4.6 Add unit test: prefetch populates cache → sync `UnwrapKey` returns immediately without Key Vault call.
- [ ] 4.7 Add unit test: prefetch cache miss → sync `UnwrapKey` falls through to sync Resolve + UnwrapKey (existing behavior).
- [ ] 4.8 Add unit test: 50 concurrent threads calling `PrefetchUnwrapKeyAsync` for the same key → only one `ResolveAsync` + `UnwrapKeyAsync` call (deduplication).
- [ ] 4.9 Add unit test: prefetch throws (simulated Key Vault failure) → caller proceeds to semaphore and sync path without error.
- [ ] 4.10 Add unit test: prefetch throws `OperationCanceledException` → exception propagates to caller.

## 5. Proactive Background Refresh

- [ ] 5.1 In `CachingEncryptionKeyStoreProviderImpl`: add `CancellationTokenSource backgroundCts` field.
- [ ] 5.2 On prefetch cache access, if entry is within the refresh window before time-to-live expiry (20% of cache time-to-live, capped at 5 minutes maximum, with per-key jitter to stagger multi-Client Encryption Key refreshes), schedule `Task.Run` to call `ResolveAsync` + `UnwrapKeyAsync` and update the cache entry. Use `backgroundCts.Token`. Deduplicate via `inflightPrefetches`. Enforce a concurrency limiter (`SemaphoreSlim`, e.g. max 3 concurrent background refresh calls) to prevent unbounded fan-out with many Client Encryption Keys.
- [ ] 5.3 Add `Cleanup()` method: cancel `backgroundCts`, clear `prefetchCache`, clear `resolvedClientCache`, clear `inflightPrefetches`. Use `Interlocked.Exchange(ref disposed, 1)` for idempotent disposal.
- [ ] 5.4 Wire `Cleanup()` into `EncryptionCosmosClient.Dispose(bool disposing)`.
- [ ] 5.5 Add unit test: access a cache entry near time-to-live expiry → background refresh fires (verify via mock that `ResolveAsync` is called).
- [ ] 5.6 Add unit test: background refresh fails → existing cache entry persists until time-to-live; no exception propagated.
- [ ] 5.7 Add unit test: `Cleanup()` cancels in-flight background tasks (verify `OperationCanceledException` observed by background task).
- [ ] 5.8 Add unit test: double `Cleanup()` is safe (no `ObjectDisposedException`).
- [ ] 5.9 Add unit test: concurrency limiter caps background refresh tasks (e.g. 10 Client Encryption Keys trigger refresh simultaneously, verify only max 3 concurrent Azure Key Vault calls via mock).

## 6. Integration & Wiring

- [ ] 6.1 In `EncryptionCosmosClient` constructor: if environment variable is on, instantiate `CachingEncryptionKeyStoreProviderImpl` instead of `EncryptionKeyStoreProviderImpl`.
- [ ] 6.2 Expose `PrefetchUnwrapKeyAsync` on base class as a virtual no-op (`return Task.CompletedTask`) so `EncryptionSettingForProperty` can call it without knowing the concrete type.
- [ ] 6.3 Verify build: `dotnet build Microsoft.Azure.Cosmos.Encryption/src/ -c Release` — 0 errors, 0 warnings.
- [ ] 6.4 Run existing unit tests: `dotnet test Microsoft.Azure.Cosmos.Encryption/tests/Microsoft.Azure.Cosmos.Encryption.Tests/ --no-build` — all pass.
- [ ] 6.5 Run emulator end-to-end tests with `AZURE_COSMOS_ENCRYPTION_OPTIMISTIC_DECRYPTION_ENABLED=true`: all existing `MdeEncryptionTests` pass.
- [ ] 6.6 Run emulator end-to-end tests without environment variable: all existing `MdeEncryptionTests` pass (verify zero behavior change).

## 7. Contract & API Validation

- [ ] 7.1 Verify no public API changes: diff the contracts file before and after. No new public types, methods, or properties.
- [ ] 7.2 Verify `EncryptionKeyStoreProviderImpl` remains `internal`.
- [ ] 7.3 Verify `CachingEncryptionKeyStoreProviderImpl` is `internal sealed`.

## 8. Performance Benchmarking

- [ ] 8.1 Create `InMemoryKeyResolver` test harness with configurable delay per `Resolve`/`UnwrapKey` call and call-count tracking.
- [ ] 8.2 Benchmark: semaphore hold time (p50/p95/p99) at 50 concurrent threads, simulated Key Vault latency 500ms, environment variable off vs. on. Expect: off = hundreds of ms, on = microseconds.
- [ ] 8.3 Benchmark: end-to-end decrypt throughput (docs/sec) at 10, 50, 100 concurrent feed iterators with 5 encrypted properties per document. Environment variable off vs. on.
- [ ] 8.4 Benchmark: Azure Key Vault call count over 1000 decrypt operations with 1 Client Encryption Key. Environment variable off (expect 2 per `ProtectedDataEncryptionKey` cache miss) vs. on (expect 1 per time-to-live refresh, resolved client cached).
- [ ] 8.5 Benchmark: `OperationCanceledException` count at 50 concurrent threads with 5s cancellation timeout and simulated 2s Key Vault latency. Environment variable off (expect failures) vs. on (expect zero).
- [ ] 8.6 Benchmark: multi-Client Encryption Key refresh burst — 5 Client Encryption Keys with aligned time-to-live values, verify jitter + concurrency limiter staggers Azure Key Vault calls (measure peak concurrent Azure Key Vault calls; should not exceed concurrency limit).
- [ ] 8.7 Document results in a comparison table (baseline vs. optimized) with all metrics.
- [ ] 8.8 **Customer thread pool investigation.** Gather information about the impacted customer's thread pool configuration (min/max threads, thread pool starvation indicators) to inform benchmark concurrency levels and validate that the fix is effective under their actual thread pool constraints.
