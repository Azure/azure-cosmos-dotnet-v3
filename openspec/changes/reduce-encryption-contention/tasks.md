## Status Note

The shipped design is **background-refresh-only**. Earlier tasks for `async-dek-prefetch`,
`resolved-client-cache`, and `env-var-feature-gate` were removed after that approach was
rejected in favor of a smaller, safer surface (see `proposal.md` "What was considered and
rejected" and `design.md` for the retrospective). This tasks file reflects only the shipped
work.

## 1. Security Validation — Background Refresh & Cache Writes

- [x] 1.1 **Threat model: plaintext DEK in static cache.** The background worker writes the
  same plaintext AES-256 bytes that the MDE library already caches, to the same static
  `ProtectedDataEncryptionKey.Cache`, with the same TTL. No net-new material is held.
  Deliverable: covered in `design.md` §Security.
- [x] 1.2 **Threat model: background `Task` refresh lifecycle.** Risks reviewed:
  (a) GC rooting of provider/credential chain while worker is alive — mitigated by
  `Dispose()` cancelling the loop; (b) token credential refresh on ThreadPool thread —
  acceptable and matches AKV SDK guidance; (c) cache write after `Dispose()` — worker
  loop terminates on `OperationCanceledException`/`ObjectDisposedException` before any
  further Set is issued. `CancellationTokenSource` is intentionally **not** disposed so
  that in-flight `Task.Delay(ct)` observers cannot race with dispose. `Interlocked`
  isn't required because `Dispose()` is idempotent on `CancellationTokenSource.Cancel()`.
- [x] 1.3 **KEK revocation window.** On 403 during refresh, the worker calls
  `ProtectedDataEncryptionKey.RemoveFromCache(name, kek, encryptedKey)` and removes
  the entry from its tracking dictionary, so the next hot-path access misses and re-runs
  the standard sync flow (which will surface the 403 to the caller). Worst-case stale
  window: bounded by `TTL - refreshSafetyMargin` if the eviction call itself fails.
  Best-effort try/catch around the eviction; on catch we still remove tracking so the
  worker doesn't re-populate the entry via subsequent refreshes.
- [x] 1.4 **Key rotation.** CEK rewrap does not change plaintext DEK bytes, so all cached
  PDEKs remain valid. CMK replacement is out of scope (requires offline data migration).

## 2. `LocalCache` Surface

- [x] 2.1 Add `internal void Set(TKey, TValue)` — writes an entry (indefinite retention;
  `LocalCache` is the MDE-internal `MemoryCache` wrapper).
- [x] 2.2 Add `internal bool Remove(TKey)` — delegates to `MemoryCache.Remove`.
- [x] 2.3 Guarded by existing lock semantics of `MemoryCache`; no new synchronization needed.

## 3. `ProtectedDataEncryptionKey` Cache Surface

- [x] 3.1 Add `internal static void SetInCache(string name, KeyEncryptionKey kek, byte[] encryptedKey, ProtectedDataEncryptionKey pdek)` using cache key `Tuple.Create(name, kek, encryptedKey.ToHexString())` — matches the cache key that `GetOrCreate` builds so subsequent `GetOrCreate` calls hit.
- [x] 3.2 Add `internal static bool RemoveFromCache(string name, KeyEncryptionKey kek, byte[] encryptedKey)` using the identical cache key shape.
- [x] 3.3 Add `internal ProtectedDataEncryptionKey(string name, KeyEncryptionKey kek, byte[] encryptedKey, byte[] rootKey)` — accepts a caller-supplied plaintext rootKey (already unwrapped by the async worker) so `SetInCache` can install a fully-formed PDEK without triggering another sync `UnwrapKey`.
- [x] 3.4 Round-trip coverage: new `ProtectedDataEncryptionKeyCacheRoundTripTests` proves
  `SetInCache → GetOrCreate` is a hit, `RemoveFromCache` evicts, second `SetInCache`
  overwrites, and invalid args are rejected. Uses `DummyKeyEncryptionKey` (echo-UnwrapKey
  semantics) to distinguish cache-hit-returned rootKey from cache-miss-echoed encryptedKey.
  This test is a linchpin — it will fail if any MDE upgrade changes
  `KeyEncryptionKey.Equals`/`GetHashCode` value semantics, alerting us that the cache-key
  contract is broken.

## 4. `PdekCacheRefreshWorker`

### 4.1 Construction / configuration

- [x] 4.1.1 Constructor accepts `EncryptionCosmosClient` (used for `GetClientEncryptionKeyPropertiesAsync` on 403) and the effective `keyCacheTimeToLive` captured at construction. TTL is stored on the worker so the loop never re-reads the process-global `ProtectedDataEncryptionKey.TimeToLive` (which could drift).
- [x] 4.1.2 Scan interval = `min(TTL/10, COSMOS_PDEK_BG_REFRESH_MAX_SCAN_INTERVAL_SECONDS)` (default max 300 s).
- [x] 4.1.3 Refresh window = `COSMOS_PDEK_BG_REFRESH_WINDOW_FRACTION * TTL` (default 0.9 → refresh once we're inside the last 10% of TTL) plus per-key jitter to stagger multi-CEK refreshes.
- [x] 4.1.4 Safety margin = `min(MaxKeyVaultLatencyBudget(5s) + MaxCumulativeBackoff(35s) + scanInterval, TTL/2)`. Refresh threshold is capped at `CreatedAtUtc + TTL - refreshSafetyMargin` so a refresh always has runway to complete before the cache entry expires.
- [x] 4.1.5 Activation gate: worker is only constructed when `TTL >= min-ttl-threshold` (default 1 h; `COSMOS_PDEK_BG_REFRESH_MIN_TTL_SECONDS`). Short-TTL configurations rely on hot-path refresh.

### 4.2 Hot-path integration

- [x] 4.2.1 `EncryptionSettingForProperty.BuildProtectedDataEncryptionKeyAsync` calls `worker?.TrackEntry(name, kek, encryptedKey, databaseRid, containerRid)` after the standard sync build succeeds.
- [x] 4.2.2 `TrackEntry` sets `CreatedAtUtc` only if the entry is new or expired (so refreshes don't reset the age clock) and always updates `LastHotPathTouchUtc` (so prune reflects real usage, not worker activity).

### 4.3 Loop

- [x] 4.3.1 `Task.Run(ScanAndRefreshLoopAsync)` with `Task.Delay(scanInterval, ct)` between scans. Loop catches `OperationCanceledException` and `ObjectDisposedException` as terminal; all other exceptions are logged (per-entry) and swallowed to keep the worker alive.
- [x] 4.3.2 Per entry, if `now >= min(CreatedAtUtc + refreshWindow + jitter, CreatedAtUtc + TTL - refreshSafetyMargin)`, refresh:
  - `ResolveAsync` + `UnwrapKeyAsync` (async, outside semaphore).
  - Build a new `ProtectedDataEncryptionKey` via the rootKey-accepting ctor (no sync UnwrapKey).
  - Acquire the same static semaphore used by the hot path, `SetInCache`, release.
  - Update `CreatedAtUtc = now` on the tracked entry.

### 4.4 Prune

- [x] 4.4.1 Entries whose `LastHotPathTouchUtc + 3*TTL < now` are removed from the tracking dictionary. Using `LastHotPathTouchUtc` (not `CreatedAtUtc`) means the worker's own refreshes do not defeat prune — an entry that stops being read on the hot path will actually age out even if we're still refreshing it.

### 4.5 Failure handling

- [x] 4.5.1 429 (throttling): bounded exponential backoff (1s, 2s, 4s, 8s, 16s → 31 s cumulative), honors AKV `Retry-After` when present, max 5 attempts. `MaxCumulativeBackoff = 35 s` covers this budget.
- [x] 4.5.2 403 (KEK revoked / permission removed): fetch CEK properties, acquire the semaphore, reconstruct the KEK via `KeyEncryptionKey.GetOrCreate`, call `ProtectedDataEncryptionKey.RemoveFromCache`, then remove the tracked entry. Best-effort try/catch; if any step fails we still remove tracking so we don't re-arm SetInCache. The next hot-path access will miss the cache and re-run the standard flow (which will surface the 403 to the caller).
- [x] 4.5.3 Any other exception: log and skip the entry for this scan.

### 4.6 Disposal

- [x] 4.6.1 `Dispose()` calls `cts.Cancel()` but **does not** dispose the CTS — this avoids a `Task.Delay(ct)` observer racing with `Dispose()` and throwing `ObjectDisposedException`. The loop treats ODE as terminal anyway; leaving the CTS undisposed avoids the noise.
- [x] 4.6.2 `EncryptionCosmosClient.Dispose(bool)` disposes the worker (if constructed).
- [x] 4.6.3 Dispose test asserts the worker `Task` completes without faulting: `AreSame(worker.WorkerTask, completed)`, `IsFaulted == false`, `IsCanceled == false`, and awaits the task so any hidden exception surfaces.

## 5. Tests

- [x] 5.1 `LocalCacheTests` — Set/Remove round-trip.
- [x] 5.2 `ProtectedDataEncryptionKeyCacheRoundTripTests` — 4 tests (linchpin coverage; see 3.4).
- [x] 5.3 `PdekCacheRefreshWorkerTests` — activation gate, `TrackEntry` behavior, dispose safety.
- [x] 5.4 Total: **73/73 encryption unit tests passing**.

## 6. Documentation

- [x] 6.1 `design.md` — retains the full alternatives analysis and the shipped-design rationale.
- [x] 6.2 `proposal.md` — rewritten to describe the shipped background-worker design and mark the earlier three specs SUPERSEDED.
- [x] 6.3 `tasks.md` — this file; scoped to shipped work only.
- [ ] 6.4 `changelog.md` — pending: add `Bugs Fixed` bullet ("Client Encryption: Fixes contention on ProtectedDataEncryptionKey cache during Key Vault refresh under concurrent load.") in the PR that ships this.
