## ADDED Requirements

### Requirement: Proactive PDEK cache background refresh

The system SHALL implement a background worker that monitors `ProtectedDataEncryptionKey` cache entries and proactively refreshes them before TTL expiry, preventing cache misses under concurrent load.

#### Scenario: Background worker activated for TTL >= 1 hour
- **WHEN** `WithEncryption` is called with `keyCacheTimeToLive >= TimeSpan.FromHours(1)` (or `null`, which defaults to 1 hour)
- **THEN** `EncryptionCosmosClient` SHALL start a `PdekCacheRefreshWorker` background thread that monitors PDEK cache entries for approaching expiry

#### Scenario: Background worker NOT activated for TTL < 1 hour
- **WHEN** `WithEncryption` is called with `keyCacheTimeToLive < TimeSpan.FromHours(1)` (e.g. 10 minutes, or `TimeSpan.Zero`)
- **THEN** `EncryptionCosmosClient` SHALL NOT create a background worker — zero CPU overhead, behavior identical to current baseline

#### Scenario: Background worker NOT activated for TimeSpan.Zero (no caching)
- **WHEN** `WithEncryption` is called with `keyCacheTimeToLive = TimeSpan.Zero`
- **THEN** no background worker SHALL be created; every operation rebuilds the PDEK synchronously (existing test behavior preserved, KEK revocation detected immediately)

### Requirement: Refresh window triggers proactive refresh

The background worker SHALL identify cache entries within the refresh window and enqueue them for refresh.

#### Scenario: Entry within refresh window is enqueued
- **WHEN** a PDEK cache entry has elapsed >= 80% of its TTL (e.g. for TTL = 1 hour, the entry is 48+ minutes old)
- **THEN** the worker SHALL enqueue that entry for refresh

#### Scenario: Entry outside refresh window is not enqueued
- **WHEN** a PDEK cache entry has elapsed < 80% of its TTL
- **THEN** the worker SHALL NOT enqueue it; no Key Vault calls are made for this entry

#### Scenario: Per-entry jitter staggers multi-CEK refreshes
- **WHEN** multiple PDEK cache entries reach the refresh window at approximately the same time (common at startup when all entries are populated together)
- **THEN** the system SHALL apply per-entry jitter (random offset within the refresh window) so entries are NOT all enqueued at the exact same scan cycle

### Requirement: Serial refresh queue with 429 backoff

All refresh operations SHALL be processed serially through a queue, with proper throttle handling for Azure Key Vault rate limits.

#### Scenario: Serial processing — one Key Vault call at a time
- **WHEN** multiple entries are enqueued for refresh
- **THEN** the refresh thread SHALL dequeue and process them one at a time — at most one `ResolveAsync` + `UnwrapKeyAsync` call in-flight from the background worker at any moment

#### Scenario: Successful refresh updates PDEK cache
- **WHEN** the refresh thread calls `ResolveAsync(kekId)` + `UnwrapKeyAsync(wrappedDek)` and both succeed
- **THEN** the system SHALL rebuild the `ProtectedDataEncryptionKey` via `GetOrCreate` (under the semaphore) and the cache entry SHALL have a renewed TTL

#### Scenario: HTTP 429 triggers backoff and retry
- **WHEN** `ResolveAsync` or `UnwrapKeyAsync` returns HTTP 429 (Too Many Requests)
- **THEN** the refresh thread SHALL read the `Retry-After` header; if present, sleep for that duration; if absent, use exponential backoff (1s, 2s, 4s, 8s, max 30s). The same entry SHALL be retried after the backoff period.

#### Scenario: Transient failure (5xx, timeout) skips entry
- **WHEN** `ResolveAsync` or `UnwrapKeyAsync` fails with a transient error (5xx, network timeout) that is NOT a 429
- **THEN** the refresh thread SHALL log the failure, skip the entry (do NOT retry immediately), and proceed to the next queued item. The skipped entry SHALL be re-evaluated on the next scan cycle.

#### Scenario: 403 (KEK revoked) invalidates cache entry
- **WHEN** `ResolveAsync` or `UnwrapKeyAsync` returns HTTP 403 (Forbidden — KEK revoked or access removed)
- **THEN** the refresh thread SHALL invalidate/remove the PDEK cache entry, ensuring the next hot-path access triggers the existing `BuildEncryptionAlgorithmForSettingAsync` force-refresh flow (which detects revocation and throws "needs to be rewrapped")

### Requirement: KEK revocation detection remains functional

The background refresh approach SHALL NOT interfere with the existing KEK revocation detection mechanism.

#### Scenario: DEK byte cache remains disabled
- **GIVEN** the final approach does NOT enable the Data Encryption Key byte cache
- **THEN** `DataEncryptionKeyCacheTimeToLive` SHALL remain `TimeSpan.Zero` in `EncryptionKeyStoreProviderImpl`
- **AND** every PDEK cache miss (including background refresh) SHALL call the resolver, allowing 403 detection

#### Scenario: VerifyKekRevokeHandling test passes
- **GIVEN** the test sets `ProtectedDataEncryptionKey.TimeToLive = TimeSpan.Zero`
- **WHEN** the test revokes the KEK and performs an operation
- **THEN** the operation SHALL detect revocation immediately (no background worker is active because TTL < 1 hour) and throw the expected "needs to be rewrapped" error

### Requirement: Lifecycle management via CancellationTokenSource

The background refresh worker SHALL be tied to the `EncryptionCosmosClient` lifecycle and cleanly shut down on disposal.

#### Scenario: Dispose cancels background worker
- **WHEN** `EncryptionCosmosClient.Dispose()` is called
- **THEN** the `CancellationTokenSource` SHALL be cancelled, the background worker loop SHALL exit, and any in-flight `ResolveAsync`/`UnwrapKeyAsync` call SHALL observe the cancellation token

#### Scenario: Double-dispose is safe
- **WHEN** `Dispose()` is called multiple times
- **THEN** the second and subsequent calls SHALL be no-ops (idempotent via `Interlocked.Exchange`)

#### Scenario: Worker exits gracefully on cancellation
- **WHEN** the cancellation token is triggered between queue items
- **THEN** the worker SHALL stop dequeuing and exit its loop without throwing

### Requirement: No public API changes

There SHALL be no new public classes, methods, properties, or parameters exposed. The background refresh is entirely internal and activated automatically based on the existing `keyCacheTimeToLive` parameter.

#### Scenario: Public API surface unchanged
- **WHEN** the encryption package is built with background refresh implemented
- **THEN** the public API contract (as captured in the contracts file) SHALL be identical to the current version

### Requirement: No environment variable gating

Unlike the rejected Approach B, the background refresh SHALL NOT be gated by an environment variable. Activation is purely determined by the `keyCacheTimeToLive` parameter value.

#### Scenario: No environment variable needed
- **WHEN** a customer upgrades to the new SDK version and uses `WithEncryption` with default parameters
- **THEN** the background refresh SHALL be active automatically (since default TTL = 1 hour >= 1 hour threshold)
- **AND** no environment variable configuration SHALL be required

### Requirement: Semaphore acquisition during background refresh

The background refresh worker SHALL acquire `EncryptionKeyCacheSemaphore` when calling `ProtectedDataEncryptionKey.GetOrCreate`, consistent with the hot-path contract.

#### Scenario: Background refresh acquires semaphore
- **WHEN** the refresh thread rebuilds a `ProtectedDataEncryptionKey` via `GetOrCreate`
- **THEN** it SHALL acquire `EncryptionKeyCacheSemaphore` before the call and release it after
- **NOTE**: This means the semaphore is held for Key Vault I/O duration during refresh, but this occurs proactively (before TTL expiry) when no concurrent hot-path threads are waiting for the same entry

#### Scenario: Semaphore timeout prevents deadlock
- **WHEN** the refresh thread attempts to acquire `EncryptionKeyCacheSemaphore` and it is held by a hot-path thread for an extended period
- **THEN** the refresh thread SHOULD use a timeout (e.g. 30 seconds) and skip the entry if the semaphore cannot be acquired, re-evaluating on the next scan cycle

### Requirement: Scan interval based on TTL

The monitor thread SHALL scan for entries approaching expiry at a frequency derived from the configured TTL.

#### Scenario: Scan interval calculation
- **WHEN** the background worker is configured with a `keyCacheTimeToLive` value
- **THEN** the scan interval SHALL be `min(ttl * 0.1, 5 minutes)` — for TTL = 1 hour, scan every 5 minutes (capped); for TTL = 2 hours, scan every 5 minutes (capped)

#### Scenario: Worker sleeps between scans
- **WHEN** no entries are approaching expiry after a scan
- **THEN** the worker SHALL sleep for the scan interval duration before the next scan, consuming negligible CPU
