## ADDED Requirements

### Requirement: Async prefetch of unwrapped Data Encryption Key bytes outside the semaphore
The system SHALL provide a `PrefetchUnwrapKeyAsync` method that calls `ResolveAsync()` + `UnwrapKeyAsync()` asynchronously and stores the result in a `ConcurrentDictionary<string, byte[]>` prefetch cache. This method SHALL be called before semaphore acquisition in `BuildProtectedDataEncryptionKeyAsync`.

#### Scenario: Prefetch warms cache before semaphore
- **WHEN** `BuildEncryptionAlgorithmForSettingAsync` enters the cold path and calls `PrefetchUnwrapKeyAsync` before acquiring the semaphore
- **THEN** `ResolveAsync` and `UnwrapKeyAsync` SHALL execute asynchronously (yielding the thread), and the result SHALL be stored in the prefetch cache

#### Scenario: Sync UnwrapKey reads from prefetch cache
- **WHEN** the Microsoft Data Encryption library's sync `UnwrapKey` is called inside the semaphore and the prefetch cache has a valid entry for the wrapped key
- **THEN** `UnwrapKey` SHALL return the cached bytes immediately without calling `Resolve()` or `UnwrapKey()` on Key Vault

#### Scenario: Sync fallback on prefetch cache miss
- **WHEN** the Microsoft Data Encryption library's sync `UnwrapKey` is called inside the semaphore and the prefetch cache does NOT have an entry (race condition, prefetch failed, or prefetch not called)
- **THEN** `UnwrapKey` SHALL fall through to the existing sync `Resolve()` + `UnwrapKey()` path (identical to current behavior)

### Requirement: Concurrent prefetch deduplication
The system SHALL deduplicate concurrent prefetch calls for the same wrapped key so that only one async Key Vault call flies per key at a time.

#### Scenario: Multiple threads prefetch same key
- **WHEN** N threads simultaneously call `PrefetchUnwrapKeyAsync` for the same wrapped key (N can be any number of concurrent callers)
- **THEN** only one `ResolveAsync` + `UnwrapKeyAsync` call SHALL be made to Key Vault; all N threads SHALL await the same `Task`
- **NOTE**: The deduplication guarantee is independent of the number of concurrent callers. Test scenarios SHOULD use a representative concurrency level (e.g. 50) but the invariant holds for any N ≥ 2.

### Requirement: Proactive background refresh before time-to-live expiry
The system SHALL schedule a background refresh of the prefetch cache entry when the entry is within the refresh window of its time-to-live expiry (20% of cache time-to-live, capped at 5 minutes maximum), so that the next consumer finds a warm cache.

#### Scenario: Background refresh fires before expiry
- **WHEN** a prefetch cache entry is within the refresh window (20% of time-to-live, max 5 minutes) of expiry and is accessed
- **THEN** the system SHALL initiate a background `Task.Run` that calls `ResolveAsync` + `UnwrapKeyAsync` and updates the cache entry

#### Scenario: Background refresh failure does not crash
- **WHEN** the background refresh call fails (Key Vault down, 429 throttle, network error)
- **THEN** the failure SHALL be caught and logged; the existing cache entry SHALL remain until its time-to-live expires; the sync fallback path SHALL handle the next call
- **NOTE**: A backoff-retry mechanism SHOULD be applied to the background refresh before giving up (e.g. exponential backoff with 1–3 retries). If all retries are exhausted, the existing cache entry continues to serve reads until time-to-live expiry. Once the entry expires (removed from the prefetch cache), the next `UnwrapKey` call inside the semaphore will find no cache hit and fall through to the sync `Resolve()` + `UnwrapKey()` path — no explicit invalidation is needed because time-to-live expiry naturally clears the entry.

### Requirement: Prefetch cache time-to-live matches `ProtectedDataEncryptionKey` cache time-to-live
The prefetch cache entry time-to-live SHALL match the `ProtectedDataEncryptionKey.TimeToLive` value.

#### Scenario: Cache entry expires with `ProtectedDataEncryptionKey`
- **WHEN** the `ProtectedDataEncryptionKey` cache time-to-live (1–2 hours) elapses
- **THEN** the prefetch cache entry for the same key SHALL also be expired, ensuring a fresh Key Vault call on the next cold path

### Requirement: Lifecycle management via CancellationTokenSource
The async prefetch layer SHALL use a `CancellationTokenSource` to cancel in-flight background refresh tasks on disposal.

#### Scenario: Disposal cancels background tasks
- **WHEN** `EncryptionCosmosClient.Dispose()` is called
- **THEN** the `CancellationTokenSource` SHALL be cancelled, all in-flight background refresh tasks SHALL observe cancellation, and the prefetch cache SHALL be cleared

#### Scenario: Double-dispose is safe
- **WHEN** `Dispose()` is called multiple times
- **THEN** the second and subsequent calls SHALL be no-ops (idempotent via `Interlocked.Exchange`)

### Requirement: Prefetch errors do not propagate to callers
The prefetch call in `BuildEncryptionAlgorithmForSettingAsync` SHALL be best-effort.

#### Scenario: Prefetch throws non-cancellation exception
- **WHEN** `PrefetchUnwrapKeyAsync` throws an exception that is not `OperationCanceledException`
- **THEN** the exception SHALL be caught and swallowed; execution SHALL continue to semaphore acquisition and the normal sync path

#### Scenario: Prefetch throws OperationCanceledException
- **WHEN** `PrefetchUnwrapKeyAsync` throws `OperationCanceledException` (caller's token fired)
- **THEN** the exception SHALL propagate to the caller (same as current behavior when `WaitAsync` is cancelled)
