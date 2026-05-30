# Distributed Transactions (DTX) — Comprehensive Test Plan

> Generated from the exhaustive DTX test plan spreadsheet.
> 
> **Level** classifies each test as `Unit`, `E2E`, or `Unit, E2E` (both).
> **Implemented** indicates whether the test currently exists in the codebase.

---

## 1. Positive Test Cases (Happy Path)

### 1.1 Write Transaction — Operation Building

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CreateItem with valid Container, partitionKey, id, typed resource | Operation added, returns `this` for chaining | Unit | ✅ Yes |
| CreateItemStream with valid stream payload | Operation added with stream body | Unit | ✅ Yes |
| ReplaceItem with valid parameters and existing item | Operation added with Replace OperationType | Unit | ✅ Yes |
| ReplaceItemStream with valid stream | Operation added | Unit | ✅ Yes |
| DeleteItem with valid parameters | Operation added, no resource body | Unit | ✅ Yes |
| PatchItem with valid patch operations list | Operation added with Patch OperationType | Unit | ✅ Yes |
| PatchItemStream with valid stream | Operation added | Unit | ✅ Yes |
| UpsertItem with valid parameters | Operation added with Upsert OperationType | Unit | ✅ Yes |
| UpsertItemStream with valid stream | Operation added | Unit | ✅ Yes |
| Chain multiple operations of different types (fluent API) | All operations added in order with correct indices (0, 1, 2…) | Unit | ✅ Yes |
| Add 50+ operations to a single transaction | All added successfully | Unit | ❌ No |
| Operations with DistributedTransactionRequestOptions (SessionToken set) | Options preserved per-operation | Unit | ❌ No |
| Operations with IfMatchEtag set (optimistic concurrency) | ETag serialized in request body | Unit | ✅ Yes |
| Operations with hierarchical/multi-value partition keys | Correct partition key JSON serialization | Unit | ❌ No |
| Operations targeting Containers from different databases | Allowed, stored correctly | Unit | ❌ No |
| Operations targeting different Containers in same database | Allowed, stored correctly | Unit | ❌ No |
| Operations with Unicode characters in Container.Id, Container.Database.Id, item id | Correctly stored and round-tripped | Unit | ❌ No |
| CreateItem with complex nested object (arrays, nulls, nested objects) | Serialized correctly via CosmosSerializer | Unit | ❌ No |
| Operations with all supported data types (int, double, bool, string, null, array, object) | Full fidelity serialization | Unit | ❌ No |

### 1.2 Read Transaction — Operation Building

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| ReadItem with valid Container, partitionKey, id | Operation added, returns `this` | Unit | ✅ Yes |
| ReadItem with DistributedTransactionRequestOptions (SessionToken) | Options stored per-operation | Unit | ✅ Yes |
| ReadItem with IfNoneMatchEtag for conditional read | ETag serialized | Unit | ✅ Yes |
| Chain multiple ReadItem calls (fluent API) | All added in order with correct indices | Unit | ✅ Yes |
| ReadItem targeting multiple containers | Allowed | Unit | ❌ No |
| ReadItem targeting multiple databases | Allowed | Unit | ❌ No |
| ReadItem with hierarchical partition key | Correct PK serialization | Unit | ❌ No |
| Large number of reads (50+) in single transaction | All accepted | Unit | ❌ No |

### 1.3 Commit — Success Flow

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CommitTransactionAsync on write transaction with single operation | Returns DistributedTransactionResponse with 2xx status | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| CommitTransactionAsync on write transaction with multiple operations | All results present, all 2xx | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| CommitTransactionAsync on read transaction with single item | Returns response with resource body | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| CommitTransactionAsync on read transaction with multiple items | All resources returned | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| CommitTransactionAsync with CancellationToken (not cancelled) | Completes normally | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Response.IsSuccessStatusCode = true on success | Correct boolean | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Response.StatusCode = 200 OK | Correct code | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Response.Count matches number of operations | Exact match | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Response.ActivityId is non-empty | Populated | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Response.RequestCharge > 0 | Positive value | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Response.IdempotencyToken is valid GUID (write) | Non-empty GUID | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Response.Diagnostics is non-null | CosmosDiagnostics present | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Response.Headers populated | Contains standard headers | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Indexer `response[i]` returns correct OperationResult | Matches by position | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| GetOperationResultAtIndex<T>(i) deserializes typed resource | Correct typed object | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| GetEnumerator() iterates all results in order | Full iteration | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Each OperationResult.StatusCode is 2xx | Success per-op | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Each OperationResult.ETag populated (for create/replace/upsert) | Version tag present | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Each OperationResult.SessionToken populated | Per-partition token | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Each OperationResult.RequestCharge > 0 | Per-op cost | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Each OperationResult.ResourceStream readable (non-delete ops) | Stream with content | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 1.4 End-to-End Write Scenarios (Integration)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Two CreateItem in same container, same partition | 201 Created for both | E2E | ✅ Yes |
| Two CreateItem in same container, different partitions | 201 Created, cross-partition atomic | E2E | ❌ No |
| CreateItem across different Containers (same DB) | 201 Created for all | E2E | ✅ Yes |
| CreateItem across Containers in different databases | 201 Created for all | E2E | ❌ No |
| Mixed: Create + Replace + Delete + Patch + Upsert in one transaction | All succeed atomically | E2E | ✅ Yes |
| ReplaceItem with pre-existing item | 200 OK, item updated, new ETag | E2E | ❌ No |
| DeleteItem with pre-existing item | 200/204, item removed | E2E | ❌ No |
| PatchItem: add, remove, set, replace, increment operations | 200 OK, item patched correctly | E2E | ❌ No |
| UpsertItem creating new item | 201 Created | E2E | ❌ No |
| UpsertItem updating existing item | 200 OK, item replaced | E2E | ❌ No |
| Large batch (25+ operations) across multiple partitions | All succeed | E2E | ❌ No |
| Write with IfMatchEtag matching current version | Succeeds | E2E | ❌ No |
| Verify items readable after commit (read-your-own-writes) | Data persisted | E2E | ❌ No |

### 1.5 End-to-End Read Scenarios (Integration)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Read single existing item | 200 OK, resource body returned | E2E | ✅ Yes |
| Read multiple items from same partition | All returned correctly | E2E | ❌ No |
| Read items from different partitions | Consistent snapshot returned | E2E | ❌ No |
| Read items from different Containers | All returned | E2E | ❌ No |
| Read with IfNoneMatchEtag matching current (unchanged) | 304 NotModified, no body | E2E | ❌ No |
| Read with IfNoneMatchEtag not matching (item changed) | 200 OK, full resource | E2E | ❌ No |
| Read returns per-operation session tokens | Tokens usable for future requests | E2E | ❌ No |
| Read returns per-operation ETag | Current version | E2E | ❌ No |

### 1.6 ETag — Positive Scenarios

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| ReplaceItem with correct IfMatchEtag (item unchanged since read) | 200 OK, replaced | E2E | ❌ No |
| DeleteItem with correct IfMatchEtag | 200/204, deleted | E2E | ❌ No |
| PatchItem with correct IfMatchEtag | 200 OK, patched | E2E | ❌ No |
| UpsertItem with IfMatchEtag matching existing item | 200 OK, updated | E2E | ❌ No |
| ReplaceItem with IfMatchEtag = `"*"` (wildcard match any) | 200 OK | E2E | ❌ No |
| Response ETag after Create — new version assigned | Non-null ETag | E2E | ❌ No |
| Response ETag after Replace — updated version | Different from original | E2E | ❌ No |
| Response ETag after Patch — updated version | Different from original | E2E | ❌ No |
| Use returned ETag from one transaction in subsequent transaction | Second succeeds | E2E | ❌ No |
| ReadItem with IfNoneMatchEtag matching → 304 NotModified | No body, ETag echoed | E2E | ❌ No |
| ReadItem with stale IfNoneMatchEtag → 200 OK with new data | Full resource + new ETag | E2E | ❌ No |

### 1.7 RU — Positive Scenarios

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Successful transaction — response.RequestCharge > 0 | Aggregate cost present | E2E | ❌ No |
| Single-op transaction — aggregate equals per-op RU | Values match | E2E | ❌ No |
| Multi-op transaction — aggregate ≈ sum of per-op RUs | Sum relationship holds | E2E | ❌ No |
| Each operation type reports per-op RequestCharge > 0 | All positive | E2E | ❌ No |
| Fractional RU values (e.g., 3.57) preserved | No rounding | E2E | ❌ No |
| RequestCharge accessible via response.Headers and response.RequestCharge | Same value both paths | E2E | ❌ No |
| Per-op RU accessible via response[i].RequestCharge | Correct per-op | E2E | ❌ No |
| GetOperationResultAtIndex<T>(i).RequestCharge matches untyped | Same value | E2E | ❌ No |

### 1.8 Session Consistency — Positive Scenarios

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| After write commit, session-consistency read sees new data | Read-your-own-writes | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Session tokens from response merged into SessionContainer | Global state updated | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Session tokens usable in subsequent point reads | Consistency maintained | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Token format `partitionKeyRangeId:lsn` assembled correctly | Correct string | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Multiple operations merge tokens for different partitions | All partitions updated | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Correct collectionRid and collectionFullName passed to SetSessionToken | Exact values | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Session tokens merged on EVERY retry attempt (even failures) | Ensures monotonic progress even during retries | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| Session token merge exception is swallowed (logged, does not fail committed transaction) | Transaction result preserved despite merge error | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |
| LSN-only token without partitionKeyRangeId → skipped with trace warning | Token not mergeable; warning emitted | Unit, E2E | ✅ Yes - Unit, ❌ No - E2E |

### 1.9 Idempotency — Positive Scenarios

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Write transaction generates new Guid IdempotencyToken per commit | Non-empty GUID | Unit | ✅ Yes |
| Token sent in request headers as `idempotency-token` | Header present | Unit | ✅ Yes |
| Retries reuse same idempotency token | Same GUID across attempts | Unit | ✅ Yes |
| Different transaction instances produce different tokens | Unique GUIDs | Unit | ❌ No |
| Server echoes idempotency token in response | Matches request | Unit | ❌ No |

### 1.10 Retry — Positive Scenarios

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Success on first attempt — no retry | Single server call | Unit | ✅ Yes |
| Outer-loop retriable failure (isRetriable=true in body) then success on 2nd attempt | Returns success | Unit | ✅ Yes |
| Inner-loop 449/5352 (no body) retried then succeeds | Transparent to caller, within 10-attempt coordinator budget | Unit | ❌ No |
| Inner-loop 500/5411 (no body) retried then succeeds | Transparent to caller, within 9-attempt infra budget | Unit | ❌ No |
| Outer-loop succeeds on the last allowed attempt (10th) | Returns success after 9 prior failures | Unit | ❌ No |
| Inner-loop coordinator retry succeeds on the last allowed attempt (10th) | Returns success after 9 prior failures | Unit | ❌ No |
| Inner-loop infra retry succeeds on the last allowed attempt (9th) | Returns success after 8 prior failures | Unit | ❌ No |
| Diagnostics accumulated across all retry attempts | Single diagnostics object | Unit | ✅ Yes |

---

## 2. Negative Test Cases (Input Validation)

### 2.1 Null / Invalid Arguments — Write Operations

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CreateItem with null Container | `ArgumentNullException` | Unit | ✅ Yes |
| CreateItem with Container from a different CosmosClient | `ArgumentException` | Unit | ✅ Yes |
| CreateItem with null id | `ArgumentNullException` | Unit | ❌ No |
| CreateItem with empty/whitespace id | `ArgumentNullException` / `ArgumentException` | Unit | ❌ No |
| CreateItem with null resource (typed) | `ArgumentNullException` | Unit | ✅ Yes |
| CreateItemStream with null stream | `ArgumentNullException` | Unit | ✅ Yes |
| ReplaceItem with null resource | `ArgumentNullException` | Unit | ❌ No |
| ReplaceItemStream with null stream | `ArgumentNullException` | Unit | ✅ Yes |
| PatchItem with null patch operations | `ArgumentNullException` | Unit | ✅ Yes |
| PatchItem with empty patch operations list | `ArgumentNullException` or validation error | Unit | ✅ Yes |
| PatchItemStream with null stream | `ArgumentNullException` | Unit | ✅ Yes |
| UpsertItem with null resource | `ArgumentNullException` | Unit | ❌ No |
| UpsertItemStream with null stream | `ArgumentNullException` | Unit | ✅ Yes |
| DeleteItem with null Container | `ArgumentNullException` | Unit | ❌ No |
| DeleteItem with null id | `ArgumentNullException` | Unit | ❌ No |

### 2.2 Null / Invalid Arguments — Read Operations

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| ReadItem with null Container | `ArgumentNullException` | Unit | ✅ Yes |
| ReadItem with Container from a different CosmosClient | `ArgumentException` | Unit | ✅ Yes |
| ReadItem with null id | `ArgumentNullException` | Unit | ✅ Yes |
| ReadItem with empty/whitespace id | `ArgumentNullException` | Unit | ✅ Yes |

### 2.3 Single-Use Violation (Double-Commit)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Second CommitTransactionAsync after successful first | `InvalidOperationException` | Unit | ✅ Yes |
| Second CommitTransactionAsync after failed first (server error) | `InvalidOperationException` | Unit | ✅ Yes |
| Second CommitTransactionAsync after first threw network exception | `InvalidOperationException` | Unit | ❌ No |
| Second CommitTransactionAsync after first was cancelled | `InvalidOperationException` | Unit | ✅ Yes |
| Add operations after CommitTransactionAsync called (write) | `InvalidOperationException` | Unit | ❌ No |
| ReadItem after CommitTransactionAsync called (read) | `InvalidOperationException` | Unit | ❌ No |
| CommitTransactionAsync with zero operations | Defined error (empty transaction) | Unit | ❌ No |

### 2.4 Response Access Violations

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Access indexer after Response.Dispose | `ObjectDisposedException` | Unit | ✅ Yes |
| GetOperationResultAtIndex after Dispose | `ObjectDisposedException` | Unit | ✅ Yes |
| Access ResourceStream after Dispose | `ObjectDisposedException` | Unit | ❌ No |
| GetOperationResultAtIndex with out-of-range index | `ArgumentOutOfRangeException` | Unit | ✅ Yes |
| GetOperationResultAtIndex with negative index | `ArgumentOutOfRangeException` | Unit | ❌ No |
| Access transaction methods after client disposed | `ObjectDisposedException` | Unit | ❌ No |

### 2.5 Serialization Edge Cases

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Operation with negative index (internal corruption) | `ArgumentOutOfRangeException` | Unit | ❌ No |
| Stream that throws IOException on read during serialization | Exception propagated cleanly | Unit | ❌ No |
| Resource that fails custom CosmosSerializer | Exception propagated | Unit | ❌ No |
| Empty stream as resource body | Valid JSON with empty/null body or error | Unit | ❌ No |

### 2.6 Invalid ETag Usage

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| IfMatchEtag with empty string | Defined behavior (server interprets) | Unit | ❌ No |
| IfMatchEtag with malformed value | Server returns 412 or processes | Unit | ❌ No |
| IfNoneMatchEtag with empty string | Defined behavior | Unit | ❌ No |
| Write op with only IfNoneMatchEtag set (wrong direction) | Not serialized — write ETag property reads from IfMatchEtag only | Unit | ✅ Yes |
| Read op with only IfMatchEtag set (wrong direction) | Not serialized — read ETag property reads from IfNoneMatchEtag only | Unit | ✅ Yes |
| Both IfMatchEtag and IfNoneMatchEtag set on write | Only IfMatchEtag serialized (as `"ifMatch"` field) | Unit | ❌ No |
| Both IfMatchEtag and IfNoneMatchEtag set on read | Only IfNoneMatchEtag serialized (as `"ifMatch"` field) | Unit | ❌ No |

---

## 3. Failure Test Cases (Server-Side)

### 3.1 Application-Level Failures (4xx)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CreateItem with duplicate id → 409 Conflict | Entire transaction fails atomically | Unit, E2E | ❌ No - Unit, ✅ Yes - E2E |
| ReplaceItem with non-existent item → 404 NotFound | Transaction fails | Unit, E2E | ❌ No - Unit, ✅ Yes - E2E |
| DeleteItem with non-existent item → 404 NotFound | Transaction fails | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| PatchItem on non-existent item → 404 NotFound | Transaction fails | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ReadItem for non-existent item → 404 NotFound per operation | Transaction reports error | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ReplaceItem with stale IfMatchEtag → 412 PreconditionFailed | Transaction fails atomically | Unit, E2E | ❌ No - Unit, ✅ Yes - E2E |
| DeleteItem with stale IfMatchEtag → 412 PreconditionFailed | Transaction fails | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| PatchItem with stale IfMatchEtag → 412 PreconditionFailed | Transaction fails | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| UpsertItem with stale IfMatchEtag (existing item) → 412 | Transaction fails | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Write to non-existent container → error | Propagated | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Write to non-existent database → error | Propagated | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Read from non-existent container → error | Propagated | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Read from non-existent database → error | Propagated | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Read with invalid partition key → error | Propagated | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Transaction exceeds request size limit → 413 | RequestEntityTooLarge | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| **[E2E]** Emulator returns real 449/5352 race conflict under concurrent commits | SDK retries via inner loop and surfaces final outcome | E2E | ❌ No |
| **[E2E]** Emulator returns real 429/3200 under provisioned throughput exhaustion | SDK honors RetryAfter, succeeds after backoff | E2E | ❌ No |

### 3.2 Coordinator/Infrastructure Failures (5xx, 4xx retriable)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| 449/5352 (Coordinator Race Conflict) no body → inner retries | Up to 10 retries with max(RetryAfter, 1s) delay | Unit | ❌ No |
| 449/5352 with body → defers to outer loop (`ShouldDeferDtxThrottleToOuterLoop`) | No inner retry; body's `isRetriable` controls outer retry | Unit | ❌ No |
| 449/5352 exhausts inner budget (10) → failure returned | Non-retriable after 10 attempts | Unit | ❌ No |
| 429/3200 (Ledger Throttled) no body → ResourceThrottleRetryPolicy handles | Uses server RetryAfter header | Unit | ❌ No |
| 429/3200 with body → defers to outer loop (`ShouldDeferDtxThrottleToOuterLoop`) | No inner retry; outer loop uses `isRetriable` | Unit | ❌ No |
| 429/3200 exhausts throttle retries → 429 to caller | Throttle error surfaced | Unit | ❌ No |
| 500/5411 (Ledger Failure) no body → infra retry | Exponential backoff (100ms base, 5s cap), up to 9 retries | Unit | ❌ No |
| 500/5411 exhausts infra budget → 500 returned | Error surfaced | Unit | ❌ No |
| 500/5412 (Account Config Failure) → infra retry | Shares budget with 5411/5413 | Unit | ❌ No |
| 500/5413 (Dispatch Failure) → infra retry | Shares budget | Unit | ❌ No |
| Mix of 5411/5412/5413 → single shared budget (9 total) | Budget counted together | Unit | ❌ No |
| 408 (Request Timeout) no body → coordinator retry | Up to 10 with max(RetryAfter, 1s) delay | Unit | ❌ No |
| 408 with body → defers to outer loop | Body present triggers deferral; no inner retry | Unit | ❌ No |
| 408 exhausts coordinator budget → timeout error surfaced | Failure after 10 attempts | Unit | ❌ No |
| 449 without sub-status 5352 (plain 449) → not matched by DTX classifier | Falls through; standard CRP `RetryWith` handling | Unit | ❌ No |
| 429 without sub-status 3200 (standard throttle) → not matched by DTX classifier | Standard throttle policy handles it | Unit | ❌ No |
| 500 without sub-status (plain 500) → not matched by DTX infra classifier | Falls through to default `NoRetry()` | Unit | ❌ No |
| 452/5421 (HLC Clock Skew Aborted) | Inner DTX classifier does not match (no handler for 452); returns `NoRetry()` — falls through to outer body-based loop; retried only if `isRetriable=true` in body | Unit | ❌ No |
| 500 with unrecognized sub-status → inner classifier returns `NoRetry()` | Falls through to outer loop / default policy; not auto-retried by inner DTX classifier | Unit | ❌ No |
| isRetriable=true in response body → outer loop retries | Up to 10 outer retries with exponential backoff | Unit | ❌ No |
| isRetriable=false in response body → no retry | Returned immediately | Unit | ❌ No |
| Outer loop `RetryAfter` from headers: max(serverHint, computedBackoff) | Server hint wins when larger | Unit | ❌ No |

### 3.3 Network/Transport Failures

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Network timeout during commit | Handled by inner retry policy | E2E | ❌ No |
| Connection reset mid-request | Retried or exception propagated | E2E | ❌ No |
| DNS resolution failure | Exception propagated | E2E | ❌ No |
| TLS handshake failure | Exception propagated | E2E | ❌ No |
| Gateway returns empty response (no body) | Handled by retry logic | E2E | ❌ No |

### 3.4 Response Parsing Failures

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Null response content with success status code | Synthetic 500 InternalServerError | Unit | ✅ Yes |
| Null response content with error status code | Pads results with error status | Unit | ✅ Yes |
| Malformed JSON body with success status | Synthetic 500 | Unit | ✅ Yes |
| Malformed JSON body with error status | Pads results with error | Unit | ✅ Yes |
| Fewer operation results than operations (success) | Synthetic 500 | Unit | ✅ Yes |
| Fewer operation results than operations (error) | Pads missing results | Unit | ✅ Yes |
| More operation results than expected | Handled gracefully | Unit | ❌ No |
| Missing `operationResponses` field entirely | Error/500 | Unit | ❌ No |
| Out-of-order indices in response | Handled correctly | Unit | ❌ No |
| Duplicate indices in response | No crash | Unit | ❌ No |
| Negative index in response | No crash | Unit | ❌ No |
| Extra unexpected JSON fields | Ignored gracefully | Unit | ❌ No |
| Empty `operationResponses` array with 200 status | Count mismatch → 500 | Unit | ❌ No |
| Invalid JSON element type in operationResponses array (e.g., string instead of object) | Synthetic 500 on success status | Unit | ❌ No |
| `operationResponses` key in PascalCase ("OperationResponses") | Parsed successfully (case-insensitive) | Unit | ✅ Yes |
| `resourceBody` key in PascalCase ("ResourceBody") | Deserialized correctly (case-insensitive) | Unit | ✅ Yes |
| `diagnosticString` key in PascalCase ("DiagnosticString") | Deserialized correctly (case-insensitive) | Unit | ✅ Yes |
| `statusCode` field is non-number JSON type (string "200") | JsonException thrown; 500 returned on success path | Unit | ❌ No |
| `subStatusCode` field exceeds UInt32 range | JsonException thrown | Unit | ❌ No |
| `requestCharge` field is non-number JSON type | JsonException thrown | Unit | ❌ No |
| `index` field is non-number JSON type | JsonException thrown | Unit | ❌ No |
| Response JSON with `requestCharge: null` | Defaults to 0 | Unit | ❌ No |
| Response JSON missing `requestCharge` field | Defaults to 0 | Unit | ❌ No |
| Response JSON with `requestCharge: -1` (invalid) | Handled gracefully | Unit | ❌ No |

### 3.5 MultiStatus (207) Response Handling

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| 207 with first op failed, rest successful | Promotes first failure status to response | Unit | ✅ Yes |
| 207 with first op successful, later one failed | Skips successes, promotes first failure | Unit | ✅ Yes |
| 207 with all ops failed | Promotes first failure status | Unit | ✅ Yes |
| 207 with all ops successful | Promotes to success | Unit | ✅ Yes |
| 207 with mixed 4xx and 5xx per-op errors | Promotes first non-success | Unit | ❌ No |
| 207 with 424 FailedDependency (cascading failure) | Skipped during promotion | Unit | ❌ No |

### 3.6 RID Resolution Failures

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Container does not exist during RID resolution (404) | Exception propagated | E2E | ❌ No |
| Network failure during RID resolution | Exception propagated | E2E | ❌ No |
| Service returns empty/null ContainerProperties | Error handling | E2E | ❌ No |
| Service returns ContainerProperties without ResourceId | Error handling | E2E | ❌ No |

### 3.7 ETag Conflict Scenarios

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Multiple ops — first succeeds, second has stale ETag | Entire transaction fails (atomicity) | E2E | ❌ No |
| IfMatchEtag set but item doesn't exist (Replace) | 404 takes precedence over ETag | E2E | ❌ No |
| Concurrent writes both with same IfMatchEtag | Exactly one succeeds, other 412 | E2E | ❌ No |
| Response ETag after Delete | Null or absent (resource gone) | E2E | ❌ No |
| ETag from failed (412) response — is current ETag returned? | Verify server behavior | E2E | ❌ No |

### 3.8 Response Body Preservation (Regression from PR #5904)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Non-success status (e.g., 452) with per-operation JSON body → body NOT disposed before parsing | Per-operation results contain real per-op status codes, not envelope status stamped onto all | Unit | ❌ No |
| 449/5352 with JSON body containing per-op 412 PreconditionFailed → body preserved | SDK surfaces actual 412 per-op status, not 449 for all operations | Unit | ❌ No |
| DTX response bypasses `CosmosExceptionFactory.Create` path (which disposes body) | Lightweight exception used; body stream remains readable for parsing | Unit | ❌ No |
| `GatewayStoreClient.ParseResponseAsync` does not throw for DTX non-success responses | Body preserved; response flows to DTX response parser intact | Unit | ❌ No |
| Response with body AND coordinator-retriable status (449/5352) → inner loop defers to outer | Body triggers `ShouldDeferDtxThrottleToOuterLoop` path; no inner retry | Unit | ❌ No |

### 3.9 408 Endpoint Cache Poisoning Guard

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DTX request returns 408 → endpoint NOT marked unavailable | `isDtxRequest` guard prevents routing cache poisoning (408 = "transaction in-progress", not endpoint failure) | Unit | ❌ No |
| Non-DTX request returns 408 → endpoint marked unavailable (normal behavior) | Standard CRP behavior unchanged | Unit | ❌ No |
| DTX 408 does not trigger partition key range unavailability marking | Only coordinator retry path engaged | Unit | ❌ No |

### 3.10 `isRetriable` Field Edge Cases

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| `isRetriable` field missing from response JSON | Defaults to `false` — no retry | Unit | ❌ No |
| `isRetriable` field = string "true" (wrong type) | Treated as falsy — no retry | Unit | ❌ No |
| `isRetriable` field = integer 1 (wrong type) | Treated as falsy — no retry | Unit | ❌ No |
| `isRetriable` field = null | Defaults to `false` — no retry | Unit | ❌ No |
| `isRetriable: true` with error status → outer loop retries | Up to 10 retries | Unit | ❌ No |
| `isRetriable: false` with error status → no retry, returned immediately | Single attempt only | Unit | ❌ No |

### 3.11 Response Stream Handling

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Non-seekable response content stream → buffered to MemoryStream before parsing | Parse succeeds; no seek-related exceptions | Unit | ❌ No |
| `ResourceStream` from operation result is non-writable (MemoryStream snapshot) | Writes throw `NotSupportedException` | Unit | ❌ No |
| `ResourceStream` is readable multiple times (MemoryStream, resettable position) | Position can be reset; re-read succeeds | Unit | ❌ No |
| `diagnosticString` truncated to 256 chars for logging | Long server diagnostics don't bloat traces | Unit | ❌ No |
| Empty operations list sent to server (zero operations) | Server returns defined behavior; SDK handles empty `operationResponses` | Unit | ❌ No |

---

## 4. Feature Composability

### 4.1 Cross-Partition Operations

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Write transaction spanning 2 partitions in same container | Atomic commit across both | E2E | ❌ No |
| Write transaction spanning 5+ partitions in same container | All-or-nothing | E2E | ❌ No |
| Write transaction spanning partitions across different containers | Atomic | E2E | ❌ No |
| Read transaction spanning partitions across containers and databases | Consistent snapshot | E2E | ❌ No |
| Write + verify isolation: items on different partitions not visible until commit | Snapshot isolation | E2E | ❌ No |

### 4.2 Cross-Container Operations

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Create items in 3 different containers atomically | All created or none | E2E | ❌ No |
| Replace in container A + Delete in container B | Atomic | E2E | ❌ No |
| Read from container A + Read from container B | Consistent cross-container snapshot | E2E | ❌ No |
| Containers with different partition key definitions | All work correctly | E2E | ❌ No |
| Containers with different indexing policies | No impact on transaction semantics | E2E | ❌ No |

### 4.3 Cross-Database Operations

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Create items in containers in 2 different databases | Atomic | E2E | ❌ No |
| Mix of operations across databases | All-or-nothing | E2E | ❌ No |
| RID resolution handles cross-database correctly | Each resolved independently | E2E | ❌ No |

### 4.4 Mixed Operation Types in Single Transaction

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Create + Replace same item in one transaction | Server determines validity | E2E | ❌ No |
| Delete + Create same id (re-create) | Server determines order semantics | E2E | ❌ No |
| Upsert + Patch same item | Server determines validity | E2E | ❌ No |
| Multiple patches on same item | Server determines validity | E2E | ❌ No |

### 4.5 Interaction with Session Consistency

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Write commit → immediate session-consistent read (same client) | Sees committed data | E2E | ❌ No |
| Write commit → read with returned session token | Consistent | E2E | ❌ No |
| Read transaction with per-op SessionToken from prior write | Sees latest writes | E2E | ❌ No |
| Multiple sequential transactions — session tokens chain correctly | Monotonic consistency | E2E | ❌ No |
| Transaction from client A, read from client B with session token | Client B sees data with correct token | E2E | ❌ No |

### 4.6 Interaction with Idempotency

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Server receives duplicate idempotency token (retry scenario) | De-duplicated, same result returned | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| New transaction instance → new token → no de-duplication | Treated as new request | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Idempotency token preserved across all retry attempts | Same GUID | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Read transaction idempotency behavior (if applicable) | Defined semantics | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 4.7 Interaction with Client Configuration

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Client with custom CosmosSerializer — transactions use it | Correct serialization | E2E | ❌ No |
| Client with Gateway connection mode — transactions work | Always uses Gateway (forced) | E2E | ❌ No |
| Client with Direct connection mode — transactions still use Gateway | Forced gateway mode | E2E | ❌ No |
| Client with custom retry policy — does it interact with DTX retries? | Verify no conflicts | E2E | ❌ No |
| Client with ApplicationRegion set — transactions route correctly | Correct region | E2E | ❌ No |
| Client with multiple preferred regions (failover) — transactions respect | Regional routing | E2E | ❌ No |
| Client with consistency level set (session, eventual, strong) — transaction isolation unaffected | Snapshot isolation always | E2E | ❌ No |

### 4.8 Interaction with Throughput/Autoscale

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Transaction on fixed-throughput container | RU charged correctly | E2E | ❌ No |
| Transaction on autoscale container | RU metered, may scale up | E2E | ❌ No |
| Transaction against serverless account | Works (if supported) or clear error | E2E | ❌ No |
| Transaction exceeds per-partition RU limit | Throttled (429) | E2E | ❌ No |

---

## 5. Long-Haul Correctness

### 5.1 Data Integrity Over Time

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Execute 10,000 transactions over 24 hours — verify zero data loss | All committed data verifiable | E2E | ❌ No |
| Sequential write → read cycles for hours — no drift | Consistent every time | E2E | ❌ No |
| Continuous Create+Replace+Delete cycle — item count remains correct | Exact count | E2E | ❌ No |
| Long-running session token chain (1000s of transactions) — no token corruption | Monotonically increasing | E2E | ❌ No |
| Verify ETag monotonicity over many updates to same item | Always increasing/changing | E2E | ❌ No |

### 5.2 Retry Correctness Over Extended Periods

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Intermittent 449/5352 over hours — all eventually succeed | 100% success rate | E2E | ❌ No |
| Periodic 429 throttling — transactions complete after backoff | No permanent failures | E2E | ❌ No |
| Random infrastructure failures (500/5411-5413) — self-healing | Recovers after retries | E2E | ❌ No |
| Retry jitter doesn't degenerate over time (Random seeding) | Uniform distribution | E2E | ❌ No |

### 5.3 Resource Stability

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Memory usage stable after 100,000 transactions | No growth trend | E2E | ❌ No |
| No handle/connection leaks over extended runs | OS resources stable | E2E | ❌ No |
| Session token container doesn't grow unbounded | Bounded size | E2E | ❌ No |
| Diagnostics objects properly collected after use | GC healthy | E2E | ❌ No |
| Thread pool not exhausted after many async operations | Threads returned | E2E | ❌ No |

### 5.4 Atomicity Under Sustained Load

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| 1000 concurrent transaction streams — no partial writes ever | Atomic guarantee holds | E2E | ❌ No |
| Verify invariants (e.g., balance sum) after 10,000 transactions | Invariant preserved | E2E | ❌ No |
| Kill/restart client mid-transaction — no orphaned state | Clean recovery | E2E | ❌ No |

---

## 6. Stress Tests

### 6.1 Concurrency Stress

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| 16 threads calling CommitTransactionAsync on same instance | Exactly 1 succeeds | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| 1000 threads racing on same transaction instance | Still exactly 1 succeeds | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| 100 concurrent independent transactions from same client | All succeed, no deadlocks | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| 500 concurrent transactions from same client | No corruption, bounded latency | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Rapid transaction create-commit-dispose cycle (tight loop) | No resource leaks | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Concurrent session token merges from parallel transactions | No corruption in SessionContainer | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 6.2 Payload Stress

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Transaction with maximum document size items (2MB each) | Succeeds or clear size error | E2E | ❌ No |
| Transaction with 100+ operations | Succeeds or defined limit error | E2E | ❌ No |
| Transaction with maximum total payload size | Succeeds or clear error | E2E | ❌ No |
| Very long item ids (maximum allowed length) | Accepted | E2E | ❌ No |
| Deeply nested JSON (100 levels) as resource | Serialization handles | E2E | ❌ No |
| Resource with 10,000 properties | Serialized correctly | E2E | ❌ No |

### 6.3 Retry Storm

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| All requests return 449/5352 for extended period | Budgets enforced, bounded time | Unit | ❌ No |
| All requests return 429/3200 with large RetryAfter | Respects hints, bounded | Unit | ❌ No |
| All requests return 500/5411 — infra budget exhaustion | Returns error after 9 retries | Unit | ❌ No |
| Mixed retriable errors saturating both inner budgets | Total attempts bounded | Unit | ❌ No |
| 100 transactions all retrying simultaneously | No thundering herd collapse | Unit | ❌ No |
| Verify jitter prevents synchronized retry waves | Desynchronized delays | Unit | ❌ No |

### 6.4 Memory Stress

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Create 10,000 transactions without committing (just add ops) | Memory bounded | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Commit 10,000 transactions sequentially — GC pressure | No OOM | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Large response bodies (many operations, large resources) | Parsed without OOM | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Repeated Response creation without explicit Dispose (DistributedTransactionResponse implements IDisposable) | GC reclaims; no permanent leak even when caller forgets Dispose | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

---

## 7. Performance Tests

### 7.1 Latency Benchmarks

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Single-op write transaction P50/P95/P99 latency | Baseline established | E2E | ❌ No |
| Multi-op (5 ops) write transaction latency vs single-op | Sub-linear overhead | E2E | ❌ No |
| Multi-op (25 ops) write transaction latency | Acceptable scaling | E2E | ❌ No |
| Read transaction latency (single item) | Lower than write | E2E | ❌ No |
| Cross-partition transaction latency vs same-partition | Overhead measured | E2E | ❌ No |
| Cross-container transaction latency | Overhead measured | E2E | ❌ No |
| Latency with IfMatchEtag (conditional) vs unconditional | Minimal overhead | E2E | ❌ No |

### 7.2 Throughput Benchmarks

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Max transactions/second (single-op writes) | Throughput ceiling | E2E | ❌ No |
| Max transactions/second (5-op writes) | Throughput ceiling | E2E | ❌ No |
| Sustained throughput over 10 minutes | No degradation | E2E | ❌ No |
| Throughput with concurrent clients (10, 50, 100) | Linear or near-linear scaling | E2E | ❌ No |
| Read transaction throughput vs write | Reads faster | E2E | ❌ No |

### 7.3 RU Efficiency

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Transaction RU vs equivalent individual operations | Transaction overhead measured | E2E | ❌ No |
| RU scaling with operation count | Linear or sub-linear | E2E | ❌ No |
| RU for read-only transactions vs point reads | Overhead measured | E2E | ❌ No |
| RU for large documents vs small | Proportional | E2E | ❌ No |
| RU for Patch (single field) vs Replace (whole doc) | Patch cheaper | E2E | ❌ No |

### 7.4 Serialization Performance

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Request serialization time for 1/5/25/100 operations | Sub-linear preferred | Unit | ❌ No |
| Response deserialization time for large payloads | Bounded | Unit | ❌ No |
| Memory allocation during serialization | Minimized (pooling?) | Unit | ❌ No |
| CreateBodyStream reuse (pre-serialized) vs re-serialize | Pre-serialized faster on retries | Unit | ❌ No |

### 7.5 Retry Overhead

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| First-attempt-success path — zero retry overhead | No unnecessary allocations | Unit | ❌ No |
| Single retry — overhead (delay + re-send) | Matches expected backoff | Unit | ❌ No |
| Backoff timing accuracy (actual delay vs computed) | Within tolerance | Unit | ❌ No |

---

## 8. Security Tests

### 8.1 Authorization & Authentication

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Transaction with valid auth token | Succeeds | E2E | ❌ No |
| Transaction with expired auth token | 401 Unauthorized | E2E | ❌ No |
| Transaction with invalid/malformed auth token | 401 Unauthorized | E2E | ❌ No |
| Transaction with token lacking required permissions | 403 Forbidden | E2E | ❌ No |
| Transaction with read-only token attempting writes | 403 Forbidden | E2E | ❌ No |
| Token refresh during retry loop (token expires between retries) | Refreshed token used | E2E | ❌ No |
| AAD-based auth with transactions | Works correctly | E2E | ❌ No |
| Resource token (scoped to container) with cross-container transaction | 403 or appropriate denial | E2E | ❌ No |

### 8.2 Data Isolation

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Transaction cannot access other account's data | Isolation enforced | E2E | ❌ No |
| Transaction cannot read uncommitted data from other transactions | Snapshot isolation | E2E | ❌ No |
| Response does not leak data from other partitions | Only requested data returned | E2E | ❌ No |
| Session tokens are scoped — cannot be used cross-account | Rejected | E2E | ❌ No |
| Idempotency tokens are account-scoped | No cross-account replay | E2E | ❌ No |

### 8.3 Input Sanitization

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Reserved/escape characters in item id, database, container names (`'`, `"`, `;`, `--`, etc.) | Passed as literal data; no SQL/command parsing on the gateway path | E2E | ❌ No |
| Script-like content in resource body fields | Stored as data, not executed by server or SDK | E2E | ❌ No |
| Extremely long strings (buffer overflow attempt) | Bounded, no crash | E2E | ❌ No |
| Null bytes in strings | Handled (rejected or escaped) | E2E | ❌ No |
| Control characters in field values | Stored or rejected cleanly | E2E | ❌ No |
| Malicious JSON (deeply nested, huge arrays) | Bounded parsing, no DoS | E2E | ❌ No |

### 8.4 Transport Security

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| All transaction requests use HTTPS | TLS enforced | E2E | ❌ No |
| Certificate validation enabled | Man-in-middle rejected | E2E | ❌ No |
| Request body not logged in plaintext (sensitive data) | Secure logging | E2E | ❌ No |

---

## 9. Observability Tests

### 9.1 Diagnostics

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Successful commit — Diagnostics populated with timing info | Non-null, meaningful data | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Failed commit — Diagnostics contain failure details | Error info present | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Retried commit — Diagnostics show all attempts | Each attempt visible | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Diagnostics accessible on all response types (2xx, 4xx, 5xx) | Always available | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Diagnostics include endpoint/region information | Routing info present | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Diagnostics serializable to string (ToString) | Readable output | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Diagnostics include request/response size | Payload metrics | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 9.2 Activity ID & Correlation

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| ActivityId populated in every response | Non-empty string | E2E | ❌ No |
| ActivityId unique per transaction attempt | Different per call | E2E | ❌ No |
| ActivityId traceable in server-side logs | Correlates to backend | E2E | ❌ No |
| ActivityId preserved in Diagnostics | Accessible programmatically | E2E | ❌ No |

### 9.3 OpenTelemetry Integration

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| OTel span created for CommitDistributedTransaction (write) | Correct operation name | Unit | ❌ No |
| OTel span created for CommitDistributedReadTransaction (read) | Correct operation name | Unit | ❌ No |
| OTel span attributes include db.system, db.operation | Standard attributes | Unit | ❌ No |
| OTel span status reflects success/failure | Correct status code | Unit | ❌ No |
| OTel span duration includes retry time | Total elapsed | Unit | ❌ No |
| OTel span includes RequestCharge attribute | RU visible in traces | Unit | ❌ No |
| OTel span includes StatusCode attribute | HTTP status present | Unit | ❌ No |
| Nested spans for retries (if applicable) | Retry attempts visible | Unit | ❌ No |
| TraceComponent set correctly (DistributedTransaction) | Correct component name | Unit | ❌ No |

### 9.4 Logging & Warnings

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Trace warning emitted when session token merge skips (missing pkRangeId) | Warning logged | Unit | ❌ No |
| No excessive logging on success path | Clean logs | Unit | ❌ No |
| Retry attempts logged with attempt number | Debugging aid | Unit | ❌ No |
| Non-fatal errors (e.g., session merge failure) logged at appropriate level | Warning not Error | Unit | ❌ No |
| Sensitive data (tokens, keys) not logged | Secure | Unit | ❌ No |

### 9.5 Request Metrics

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| RequestCharge (aggregate) always reported | Even on failure | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Per-op RequestCharge parsed from response | All ops have RU | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Retry count accessible (via diagnostics or metrics) | Debugging aid | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Elapsed time measurable from Diagnostics | Latency visible | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Response size measurable | Bandwidth visible | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

---

## 10. Miscellaneous

### 10.1 API Surface / Compilation

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Build with PREVIEW defined — all DTX types are public | Accessible externally | Unit | ❌ No |
| Build without PREVIEW — all DTX types are internal | Not accessible | Unit | ❌ No |
| CosmosClient.CreateDistributedWriteTransaction visible under PREVIEW | Method exists | Unit | ❌ No |
| CosmosClient.CreateDistributedReadTransaction visible under PREVIEW | Method exists | Unit | ❌ No |
| DistributedTransactionResponse implements IReadOnlyList<> | Interface usable | Unit | ❌ No |
| External assembly cannot access without PREVIEW | Compilation error | Unit | ❌ No |
| InternalsVisibleTo only test assemblies | No accidental exposure | Unit | ❌ No |
| All public APIs have XML documentation | No warnings | Unit | ❌ No |
| `OperationResult.SessionToken` is internal (not public) | Not accessible from external assembly | Unit | ❌ No |
| `OperationResult.PartitionKeyRangeId` is internal (not public) | Not accessible from external assembly | Unit | ❌ No |
| `OperationResult.SubStatusCodeValue` removed from public API | Not accessible from external assembly | Unit | ❌ No |
| `OperationResult` constructor is internal (not public) | External code cannot instantiate | Unit | ❌ No |
| Manual JSON deserialization is case-insensitive for all known fields | PascalCase/camelCase both work | Unit | ❌ No |

### 10.2 Disposal & Resource Management

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Response.Dispose releases all streams | Resources freed | Unit | ❌ No |
| OperationResult.ResourceStream readable before dispose | Works | Unit | ❌ No |
| Multiple enumerations of response results | Consistent | Unit | ❌ No |
| Dispose is idempotent (double-dispose) | No exception | Unit | ✅ Yes |
| Concurrent Dispose from multiple threads | No exception | Unit | ❌ No |
| GC finalizer if Dispose not explicitly called | No permanent leak | Unit | ❌ No |
| Transaction objects GC-eligible after commit | No rooting | Unit | ❌ No |

### 10.3 Edge Cases & Boundaries

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Transaction with exactly 1 operation | Works (minimum viable) | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Item id with maximum allowed length (255 chars) | Accepted | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Partition key with maximum size | Accepted | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Resource body at maximum document size (2MB) | Accepted or clear error | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Total request payload at service limit | Clear error if exceeded | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Very long Container.Database.Id / Container.Id values | Accepted or clear error | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Empty string values for resource fields | Stored as empty strings | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Null values in resource JSON | Stored as JSON null | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Unicode and emoji in document fields | Full fidelity round-trip | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Integer overflow in RequestCharge parsing | Handled gracefully | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Server returns 2xx success but body says `isRetriable=true` | Outer loop checks `IsSuccessStatusCode` first — exits immediately with success; `IsRetriable` property still reflects body value but does not trigger retry | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| RetryAfter header with zero value | Uses computed backoff | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| RetryAfter header with negative value | Uses computed backoff | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| RetryAfter header malformed (non-numeric) | Falls back to computed | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 10.4 Gateway Mode Enforcement

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Transaction always uses Gateway mode (UseGatewayMode=true) | Verified in request | Unit | ✅ Yes |
| Client configured for Direct mode — transaction still uses Gateway | Forced override | Unit | ❌ No |
| ResourceType = DistributedTransactionBatch in request | Correct constant | Unit | ✅ Yes |
| OperationType = CommitDistributedTransaction (write) | Correct constant | Unit | ✅ Yes |
| OperationType = CommitDistributedReadTransaction (read) | Correct constant | Unit | ✅ Yes |

### 10.5 Server Request Object

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CreateAsync materializes typed resources to streams | Bodies serialized | Unit | ❌ No |
| CreateAsync converts partition keys to JSON | Correct format | Unit | ❌ No |
| CreateBodyStream returns independent MemoryStream each call (re-readable on retries) | Separate, identical content | Unit | ✅ Yes |
| IdempotencyToken is valid Guid | Non-empty | Unit | ❌ No |
| CreateBodyStream called before CreateAsync | Error or empty | Unit | ❌ No |

### 10.6 Cancellation Behavior

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CancellationToken cancelled before commit starts (pre-cancelled) | Immediate `OperationCanceledException` | Unit | ✅ Yes |
| CancellationToken cancelled during RID resolution | `OperationCanceledException` | Unit | ❌ No |
| CancellationToken cancelled during server request | `OperationCanceledException` | Unit | ❌ No |
| CancellationToken cancelled during retry backoff delay | `OperationCanceledException` | Unit | ✅ Yes |
| CancellationToken cancelled between retries | `OperationCanceledException` | Unit | ❌ No |
| Long-running commit with short timeout | Cancelled appropriately | Unit | ❌ No |
| Dispose CancellationTokenSource during commit | `OperationCanceledException` or `ObjectDisposedException` | Unit | ❌ No |

### 10.7 Backward Compatibility

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Existing non-DTX operations unaffected by DTX code | No regression | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| SDK without PREVIEW flag — no DTX surface, no overhead | Clean separation | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Older server versions that don't support DTX — clear error | Not silent failure | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Package size impact of DTX code | Minimal overhead | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

---

## 11. Failover & Regional Resilience

### 11.1 Region Failover During Transaction

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Primary region fails mid-commit → client retries on secondary | Transaction eventually succeeds or clean error | E2E | ❌ No |
| Write region failover during retry backoff | New region used on next attempt | E2E | ❌ No |
| Read region unavailable during read transaction | Fails over or errors cleanly | E2E | ❌ No |
| DNS failover during RID resolution | Retried or propagated | E2E | ❌ No |
| Partial network partition (one partition reachable, another not) | Atomic — all-or-nothing | E2E | ❌ No |
| Region failover with session consistency — tokens still valid? | Token refers to new region's LSN | E2E | ❌ No |
| Multi-region write account — transaction targets correct write region | Correct routing | E2E | ❌ No |
| Account with single write region — transaction routes there | Not sent to read replicas | E2E | ❌ No |
| Gateway endpoint switch mid-retry | New endpoint used seamlessly | E2E | ❌ No |

### 11.2 Service Degradation

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| High latency on one partition (>30s) — transaction timeout | Cancellation or timeout surfaced | E2E | ❌ No |
| Intermittent packet loss (10% drop rate) | Retries succeed eventually | E2E | ❌ No |
| Service returning 503 ServiceUnavailable | Retried by base policy or propagated | E2E | ❌ No |
| Transient DNS failures (resolve on 2nd try) | Transparent retry | E2E | ❌ No |
| SSL certificate rotation during long-running retry loop | Connection re-established | E2E | ❌ No |

### 11.3 Region Failover Retry Budget (`MaxDtxRegionFailoverRetryCount` = 30, `DtxRegionFailoverBackoff` = 1s)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Endpoint failure during DTX commit triggers region-failover retry path (CRP `ShouldRetryOnEndpointFailureAsync`) | Uses 30-retry DTX-specific budget, not the standard `MaxRetryCount` | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Successive endpoint failures up to 30 — budget exhausts on the 31st | Returns `NoRetry`; failure surfaced to caller | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Endpoint failure path uses 1s backoff between region attempts | `DtxRegionFailoverBackoff` honored | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Endpoint failure budget is **separate** from inner coordinator (10) / infra (9) / outer body (10) budgets | Counters don't interfere | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| 30-retry endpoint-failure budget intentionally exceeds outer-loop 30s wall-clock cap | Wall-clock cap may end the commit before region-failover budget is exhausted | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Region failover with `enableEndpointDiscovery=false` | Falls through to `NoRetry` immediately | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

---

## 12. Determinism & Ordering Guarantees

### 12.1 Operation Ordering

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Operations always executed in the order they were added | Index 0, 1, 2… matches add order | Unit | ❌ No |
| Response results maintain same ordering as request | result[i] matches operation[i] | Unit | ❌ No |
| Serialization preserves add-order in JSON `operations` array | JSON array order = add order | Unit | ❌ No |
| After retry, operation order unchanged | Same order on every attempt | Unit | ❌ No |
| 207 MultiStatus — per-op results index-correlated with request | Correct mapping | Unit | ❌ No |
| Parallel RID resolution does not reorder operations | Order preserved | Unit | ❌ No |

### 12.2 Idempotency Determinism

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Same idempotency token replayed → same result | De-duplicated by server | E2E | ❌ No |
| Idempotency window expiry — same token after expiry treated as new | Server behavior defined | E2E | ❌ No |
| Token uniqueness across process restarts (GUID-based) | Globally unique | E2E | ❌ No |
| Race: two retries with same token arrive simultaneously | Server de-duplicates | E2E | ❌ No |

### 12.3 Response Determinism

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Same transaction committed twice (different instances) produces same data | Consistent writes | E2E | ❌ No |
| Reading same item in two separate read transactions | Same snapshot if no writes between | E2E | ❌ No |
| ETag for unchanged item is stable across reads | Same value | E2E | ❌ No |
| RequestCharge for identical operation is deterministic (±tolerance) | Consistent RU cost | E2E | ❌ No |

---

## 13. Timeout & Deadline Handling

### 13.1 Client-Side Timeouts

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CosmosClientOptions.RequestTimeout exceeded during commit | Timeout exception or cancellation | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Custom per-request timeout via CancellationToken | Honored | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Timeout during RID resolution phase | Clean exception | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Timeout during serialization (large payload) | Possible? If so, handled | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Timeout during response parsing | Clean exception | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Timeout with retry in progress — does remaining budget get timeout? | Bounded total time | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 13.2 Server-Side Timeouts

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Server returns 408 RequestTimeout | Retried by inner loop | E2E | ❌ No |
| Server takes >5min to respond (gateway timeout) | Client-side timeout fires first | E2E | ❌ No |
| Server timeout on one partition in multi-partition transaction | Entire transaction fails atomically | E2E | ❌ No |
| Repeated server timeouts exhausting retry budget | Final timeout error returned | E2E | ❌ No |

### 13.3 End-to-End Deadline

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Total retry time bounded (max inner + outer backoffs) | Calculable upper bound | Unit | ❌ No |
| Outer-loop wall-clock cap (`DtxWriteForbiddenRetryWindow` = 30s) caps the outer body-based retry loop even when attempt budget remains | Loop exits at ~30s elapsed | Unit | ❌ No |
| Worst case: 10 outer × (10 inner coordinator + 9 inner infra) retries | Time bounded by max delays and the 30s outer wall-clock cap | Unit | ❌ No |
| User can set reasonable deadline and transaction completes or cancels within it | Responsive to cancellation | Unit | ❌ No |
| No infinite hang scenarios (all paths have timeout/budget) | Verified | Unit | ❌ No |

---

## 14. Data Type & Encoding Edge Cases

### 14.1 Partition Key Edge Cases

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Partition key = empty string `""` | Accepted (valid PK) | E2E | ❌ No |
| Partition key = very long string (max size) | Accepted | E2E | ❌ No |
| Partition key = numeric value (int, double) | Serialized correctly | E2E | ❌ No |
| Partition key = boolean (true/false) | Serialized correctly | E2E | ❌ No |
| Partition key = null (None partition key) | `PartitionKey.None` handled | E2E | ❌ No |
| Hierarchical partition key (2-level) | Correct array serialization | E2E | ❌ No |
| Hierarchical partition key (3-level) | Correct array serialization | E2E | ❌ No |
| Partition key with Unicode/emoji | Correct encoding | E2E | ❌ No |
| Partition key with special JSON characters (`"`, `\`, `/`) | Properly escaped | E2E | ❌ No |
| Multiple operations with different partition key types (string vs number) | Each correct | E2E | ❌ No |
| PartitionKey.Undefined behavior | Handled or rejected | E2E | ❌ No |

### 14.2 Item ID Edge Cases

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| ID = single character `"a"` | Accepted | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID = maximum length (255 chars) | Accepted | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID = 256 chars (exceeds max) | Rejected cleanly | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID with forward slash `/` | Accepted (URL-encoded by SDK?) | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID with backslash `\` | Accepted or rejected? | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID with `#` and `?` characters | Handled (URL-unsafe) | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID with tab/newline characters | Rejected or encoded | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID with null byte `\0` | Rejected | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID with only spaces (but not empty after trim) | Accepted as literal | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID with Unicode combining characters | Correct handling | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID = same value across operations (duplicate id same PK) | Server determines conflict | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| ID = same value across operations (same id, different PKs) | Different items, both valid | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 14.3 Resource Body Edge Cases

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Resource with `id` field matching operation id | Consistent | E2E | ❌ No |
| Resource with `id` field NOT matching operation id | Which takes precedence? | E2E | ❌ No |
| Resource body = `{}` (empty object) | Accepted | E2E | ❌ No |
| Resource with only `id` field | Accepted (minimal doc) | E2E | ❌ No |
| Resource with field name = `""` (empty key) | Stored as-is or rejected? | E2E | ❌ No |
| Resource with duplicate JSON keys | Last-wins or error? | E2E | ❌ No |
| Resource with very large string field (1MB+) | Within doc size limit | E2E | ❌ No |
| Resource with binary-like data (base64 encoded) | Stored as string | E2E | ❌ No |
| Resource with Date/DateTime as string vs number | Stored as provided type | E2E | ❌ No |
| Resource with int64 max value | Precision preserved | E2E | ❌ No |
| Resource with `double.NaN` / `double.PositiveInfinity` / `double.NegativeInfinity` | Serializer throws or produces non-standard JSON that server rejects — never silently corrupts data | E2E | ❌ No |
| Resource with deeply nested object (50 levels) | Accepted within limits | E2E | ❌ No |
| Resource with array of 10,000 elements | Accepted within size limit | E2E | ❌ No |
| Patch operations on nested fields (`/address/city`) | Correct path resolution | E2E | ❌ No |
| Patch with `increment` on non-numeric field | Server error | E2E | ❌ No |
| Patch with `add` to non-existent path | Creates path or errors? | E2E | ❌ No |

### 14.4 Session Token Edge Cases

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Session token = empty string | Treated as no token? | Unit | ❌ No |
| Session token = extremely long string | Passed through to server | Unit | ❌ No |
| Session token from different account/container | Server rejects or ignores | Unit | ❌ No |
| Session token with future LSN (ahead of server) | Server behavior (wait or ignore?) | Unit | ❌ No |
| Malformed session token (no colon separator) | Handled gracefully in merge | Unit | ❌ No |
| Session token with non-numeric LSN | Merge fails gracefully | Unit | ❌ No |
| Null partitionKeyRangeId in response | Skipped with trace warning | Unit | ❌ No |
| Empty string partitionKeyRangeId in response | Skipped with trace warning | Unit | ❌ No |
| Whitespace-only partitionKeyRangeId | Skipped with trace warning | Unit | ❌ No |

### 14.5 Manual JSON Deserialization Edge Cases (per #5896 refactor)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Operation result JSON with all fields in camelCase | Parsed correctly | Unit | ❌ No |
| Operation result JSON with all fields in PascalCase | Parsed correctly (case-insensitive) | Unit | ❌ No |
| Operation result JSON with mixed case ("Index", "statusCode") | Each parsed independently via case-insensitive match | Unit | ❌ No |
| `index` field as string ("0" instead of 0) | JsonException: "must be a JSON number" | Unit | ❌ No |
| `statusCode` field as string ("200") | JsonException: "must be a JSON number" | Unit | ❌ No |
| `subStatusCode` field as negative number | JsonException: "must be a 32-bit unsigned integer" | Unit | ❌ No |
| `subStatusCode` field exceeds UInt32.MaxValue | JsonException from TryGetUInt32 | Unit | ❌ No |
| `requestCharge` field as string ("1.5") | JsonException: "must be a JSON number" | Unit | ❌ No |
| `requestCharge` field as integer (no decimal) | Parsed via TryGetDouble — succeeds | Unit | ❌ No |
| `etag` field as non-string (number) | Skipped (ValueKind != String check) — ETag remains null | Unit | ❌ No |
| `sessionToken` field as non-string (array) | Skipped — SessionToken remains null | Unit | ❌ No |
| `partitionKeyRangeId` field as non-string (number) | Skipped — PartitionKeyRangeId remains null | Unit | ❌ No |
| Operation result JSON is a JSON array instead of object | JsonException: "must be a JSON object" | Unit | ❌ No |
| Operation result JSON is a JSON string instead of object | JsonException: "must be a JSON object" | Unit | ❌ No |
| Operation result JSON is `null` | JsonException or handled gracefully | Unit | ❌ No |
| All optional fields missing (only `index` and `statusCode` present) | Defaults: ETag=null, RU=0, SessionToken=null, ResourceStream=null | Unit | ❌ No |
| Unknown/extra JSON fields in operation result | Ignored (no enumeration of all props) | Unit | ❌ No |

---

## 15. Retry Timing & Backoff Edge Cases

### 15.1 Outer Loop Backoff (Body-Based Retry)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Attempt 0: delay = max(serverHint, 1s * 2^0 ± jitter) = ~1s | ~1s | Unit | ✅ Yes |
| Attempt 1: delay = max(serverHint, 1s * 2^1 ± jitter) = ~2s | ~2s | Unit | ✅ Yes |
| Attempt 2: delay = ~4s | Correct | Unit | ✅ Yes |
| Attempt 3: delay = ~8s | Correct | Unit | ✅ Yes |
| Attempt 4: delay = ~16s | Correct | Unit | ✅ Yes |
| Attempt 5+: delay = ~32s (capped at maxExp=5) | Does not exceed ~32s | Unit | ✅ Yes |
| Jitter range: 0.75x to 1.25x of computed delay | Verified statistically | Unit | ❌ No |
| Server RetryAfter = 60s, computed = 2s → uses 60s | Max wins | Unit | ❌ No |
| Server RetryAfter = 0.1s, computed = 8s → uses 8s | Max wins | Unit | ❌ No |
| Thread-local Random seeding — no collisions across threads | Each thread independent | Unit | ❌ No |

### 15.2 Inner Loop Backoff (Infrastructure Failures)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Infra attempt 0: delay = 100ms (base) | Correct | Unit | ❌ No |
| Infra attempt 1: delay = 200ms | Correct | Unit | ❌ No |
| Infra attempt 2: delay = 400ms | Correct | Unit | ❌ No |
| Infra attempt 3: delay = 800ms | Correct | Unit | ❌ No |
| Infra attempt 4: delay = 1600ms | Correct | Unit | ❌ No |
| Infra attempt 5: delay = 3200ms | Correct | Unit | ❌ No |
| Infra attempt 6+: delay capped at 5000ms (maxBackoff) | Does not exceed 5s | Unit | ❌ No |
| Budget = 9 total infra retries across all sub-statuses | Shared counter | Unit | ❌ No |

### 15.3 Inner Loop Coordinator Retry

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| 449/5352 with RetryAfter=2s → uses 2s | Header respected | Unit | ❌ No |
| 449/5352 without RetryAfter → uses 1000ms default | Default applied | Unit | ❌ No |
| 408 with RetryAfter=500ms → uses 500ms | Header respected | Unit | ❌ No |
| 408 without RetryAfter → uses 1000ms default | Default applied | Unit | ❌ No |
| Budget = 10 coordinator retries | Enforced | Unit | ❌ No |
| 429/3200 deferred to ResourceThrottleRetryPolicy | Not counted in DTX budget | Unit | ❌ No |

### 15.4 Outer Loop Wall-Clock Window (`DtxWriteForbiddenRetryWindow` = 30s)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Retriable failure where cumulative elapsed time stays under 30s | Continues retrying within budget | Unit | ❌ No |
| Retriable failure where cumulative elapsed time exceeds 30s mid-loop | Loop exits and returns last response even if attempt count < 10 | Unit | ❌ No |
| Server returns very large `RetryAfter` (e.g., 60s) on first attempt → next sleep alone would breach 30s | Window check fires on the following iteration; loop exits with last response | Unit | ❌ No |
| Loop exit due to wall-clock window emits trace warning naming elapsed time and threshold | Warning logged with `elapsed > DtxWriteForbiddenRetryWindow` shape | Unit | ❌ No |
| Wall-clock window applies only to outer (body-based) loop, not inner (envelope) loop | Inner loop budgets unchanged | Unit | ❌ No |
| Cancellation during the wall-clock-bounded loop | `OperationCanceledException` propagates (cancellation honored before window check) | Unit | ❌ No |

### 15.5 Retry Amplification Prevention (Regression from commit 0267ade11)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| 449/5352 + body present → no inner retry (prevents 10 outer × 10 inner = 100 wire requests) | Deferred to outer loop immediately | Unit | ❌ No |
| 429/3200 + body present → no inner retry (prevents throttle × outer amplification) | `ShouldDeferDtxThrottleToOuterLoop` returns true | Unit | ❌ No |
| 408 + body present → no inner retry | Body indicates server processed; defer to outer | Unit | ❌ No |
| Worst-case wire request count: max 10 outer attempts (not 10 × 10 = 100) | Total ≤ 10 server round-trips per commit | Unit | ❌ No |
| 500/5411 infra retry does NOT defer when body is absent | Inner retry proceeds (up to 9) | Unit | ❌ No |
| JsonException during `operationResponses` parsing → `isRetriable` forced to `false` | Prevents retry-on-corrupt-body infinite loop | Unit | ❌ No |
| Partially-parsed response streams disposed on JsonException before returning error | No resource leak on parse failure | Unit | ❌ No |
| Inner coordinator budget (10) is separate from inner infra budget (9) | Different counters; one exhausting doesn't affect the other | Unit | ❌ No |

---

## 16. Transactional Semantics & Isolation

### 16.1 Atomicity Proof Tests

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| **[E2E]** Concurrent transactions touching overlapping items — verify atomicity on emulator | Either both commit serially or one fails atomically; no partial writes visible | E2E | ❌ No |
| **[E2E]** Read-your-write within same client after DTX commit (Session consistency) | Subsequent read sees committed state | E2E | ❌ No |
| 5-item Create — kill connection after send, before ack → no items exist | All-or-nothing | E2E | ❌ No |
| 3-item Create with 1 conflict → verify other 2 not created | Atomic rollback | E2E | ❌ No |
| Replace + Delete — if Delete fails, Replace not visible | Atomic | E2E | ❌ No |
| Large batch (25 ops) — inject one failure — verify zero committed | Atomic | E2E | ❌ No |
| Verify via separate client (no session token) that partial state never visible | External observer sees none | E2E | ❌ No |

### 16.2 Snapshot Isolation Proof Tests

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Start read txn, concurrent write commits, read txn sees pre-write state | Snapshot at start | E2E | ❌ No |
| Two read txns started at different times see different snapshots | Point-in-time correct | E2E | ❌ No |
| Write txn isolation: concurrent reader doesn't see in-flight writes | No dirty reads | E2E | ❌ No |
| Phantom reads: new item inserted during read txn not visible | No phantoms | E2E | ❌ No |
| Read-your-own-writes within same session after write commit | Visible with session token | E2E | ❌ No |

### 16.3 Conflict Resolution

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Two write txns targeting same item — one 409/412 | Deterministic winner | E2E | ❌ No |
| High contention: 10 concurrent txns updating same item | Exactly 1 wins, 9 fail | E2E | ❌ No |
| Write-write conflict on different fields of same item | Entire item is the conflict unit | E2E | ❌ No |
| Write-delete conflict (one txn writes, another deletes same item) | One wins | E2E | ❌ No |
| Conflict detection with ETag vs without ETag | Both paths work | E2E | ❌ No |

---

## 17. SDK Upgrade & Migration

### 17.1 Version Compatibility

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| **[E2E]** Emulator version that does NOT support DTX — clear actionable error | Single error surfaced; no infinite retry | E2E | ❌ No |
| **[E2E]** Emulator + DTX-capable build round-trip | Full happy path works against emulator's DTX implementation | E2E | ❌ No |
| Upgrade SDK from non-DTX version to DTX-enabled version | No breaking changes to existing code | E2E | ❌ No |
| Downgrade SDK from DTX to non-DTX version | DTX code not compiled, no errors for non-DTX users | E2E | ❌ No |
| Mixed SDK versions in same application (unlikely but possible via deps) | No binary conflicts | E2E | ❌ No |
| Server version that predates DTX support — clear error message | Not silent failure, actionable error | E2E | ❌ No |
| Account not enrolled in DTX preview — clear error on commit | Descriptive error | E2E | ❌ No |

### 17.2 API Contract Stability

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Public API surface matches documented spec | No undocumented members | Unit | ❌ No |
| No breaking changes from previous preview version (if applicable) | Backward compatible | Unit | ❌ No |
| Response JSON contract stable (field names, types) | No silent schema changes | Unit | ❌ No |
| Error codes stable between SDK versions | Same mapping | Unit | ❌ No |

---

## 18. Chaos & Fault Injection

### 18.1 Simulated Faults

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Inject random 449/5352 on 30% of requests | All eventually succeed | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject random 500/5411 on 50% of requests | Success after retries or budget exhaustion | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject 429/3200 for first N attempts then success | Throttle policy recovers | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject increasing latency (1s, 2s, 4s, 8s) | Timeout fires before runaway | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject response body corruption (random bytes) | Parsed as malformed → 500 | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject empty response body on success status | Synthetic 500 | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject wrong operation count in response | Count mismatch → 500 | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject session token merge failure (exception in SetSessionToken) | Swallowed, transaction succeeds | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject RID resolution failure on first attempt, success on retry | Propagated (no retry at RID level currently?) | Unit, E2E | ❌ No - Unit, ❌ No - E2E |
| Inject CancellationToken cancellation at random points | Always surfaces OperationCanceledException | Unit, E2E | ❌ No - Unit, ❌ No - E2E |

### 18.2 Resource Exhaustion

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Low memory condition during serialization | OutOfMemoryException or graceful error | E2E | ❌ No |
| Thread pool exhaustion (all threads blocked) | Timeout, not deadlock | E2E | ❌ No |
| Socket exhaustion (many concurrent transactions) | Connection pool error | E2E | ❌ No |
| Disk full (if temp files used) | Clean error | E2E | ❌ No |
| GC pressure during retry loop | Delays but no corruption | E2E | ❌ No |

---

## 19. Documentation & Contract Verification

### 19.1 Public API Documentation

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| All public classes have XML `<summary>` doc | No CS1591 warnings | Unit | ❌ No |
| All public methods have `<param>` and `<returns>` docs | Complete | Unit | ❌ No |
| All public properties have `<summary>` docs | Complete | Unit | ❌ No |
| Exception documentation (`<exception>`) matches actual behavior | Accurate | Unit | ❌ No |
| Code samples in docs compile and run | Verified | Unit | ❌ No |
| Changelog entry present for new feature | Under `### Unreleased` | Unit | ❌ No |

### 19.2 Contract Tests (Behavioral)

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DistributedTransactionResponse fulfills IReadOnlyList<> contract | Count, indexer, enumerator all work | Unit | ❌ No |
| DistributedTransactionResponse.Dispose fulfills IDisposable contract | Resources released | Unit | ❌ No |
| RequestOptions inheritance — all base properties accessible | No shadowing | Unit | ❌ No |
| Method chaining (fluent API) — every op returns `this` | Chain compiles | Unit | ❌ No |
| Null-safety: all public methods document null handling | Consistent | Unit | ❌ No |

### 19.3 Error Message Quality

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| ArgumentNullException includes parameter name | `paramName` set | Unit | ❌ No |
| InvalidOperationException for double-commit has clear message | Explains single-use | Unit | ❌ No |
| ObjectDisposedException identifies disposed object | Type name included | Unit | ❌ No |
| Server error response includes ErrorMessage from server | Diagnostic info present | Unit | ❌ No |
| 404 for non-existent container includes container name | Actionable | Unit | ❌ No |
| 429 includes RetryAfter suggestion | User knows wait time | Unit | ❌ No |

---

## 20. Interoperability & Ecosystem

### 20.1 Serialization Interop

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Default System.Text.Json serializer works | Round-trip fidelity | E2E | ❌ No |
| Custom Newtonsoft.Json CosmosSerializer works | Round-trip fidelity | E2E | ❌ No |
| Custom serializer with camelCase naming | Fields mapped correctly | E2E | ❌ No |
| Custom serializer with attribute-based mapping ([JsonProperty]) | Correct serialization | E2E | ❌ No |
| Polymorphic types with custom serializer | Deserialized to correct derived type | E2E | ❌ No |
| Record types (C# records) as resources | Serialized/deserialized correctly | E2E | ❌ No |
| Anonymous types as resources | Serialized correctly (if supported) | E2E | ❌ No |
| Stream operations bypass serializer | Raw bytes passed through | E2E | ❌ No |

### 20.2 Framework Compatibility

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| .NET 6 target — builds and runs | Compatible | E2E | ❌ No |
| .NET 8 target — builds and runs | Compatible | E2E | ❌ No |
| .NET Framework 4.7.2 target (if supported) | Compatible or clear minimum | E2E | ❌ No |
| ASP.NET Core DI integration (CosmosClient from DI) | Transactions work | E2E | ❌ No |
| Azure Functions integration | Transactions work in function context | E2E | ❌ No |
| Blazor Server context (synchronization context) | No deadlocks | E2E | ❌ No |

### 20.3 Dependency Interaction

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| No conflicts with common packages (Newtonsoft.Json, etc.) | Clean build | E2E | ❌ No |
| Transactions work with Microsoft.Azure.Cosmos.Encryption extension | Compatible | E2E | ❌ No |
| Transactions work with FaultInjection library | Faults injectable | E2E | ❌ No |
| Change Feed ignores in-flight transaction data | Only committed data surfaced | E2E | ❌ No |

---

## 21. Capacity & Limits

### 21.1 Operation Count Limits

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| 1 operation (minimum) | Works | E2E | ❌ No |
| 10 operations | Works | E2E | ❌ No |
| 25 operations (TransactionalBatch limit) | Works (DTX may have different limit) | E2E | ❌ No |
| 50 operations | Works or clear limit error | E2E | ❌ No |
| 100 operations | Verify limit | E2E | ❌ No |
| 500 operations | Verify limit or error | E2E | ❌ No |
| Exceed documented max operations → clear error | Descriptive limit error | E2E | ❌ No |

### 21.2 Payload Size Limits

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Single item 1KB | Works | E2E | ❌ No |
| Single item 100KB | Works | E2E | ❌ No |
| Single item 1MB | Works | E2E | ❌ No |
| Single item 2MB (Cosmos max doc size) | Works or clear error | E2E | ❌ No |
| Single item >2MB | Rejected with clear error | E2E | ❌ No |
| Total payload 1MB (many small items) | Works | E2E | ❌ No |
| Total payload 4MB (approaching gateway limit) | Works or clear error | E2E | ❌ No |
| Total payload exceeding gateway limit | 413 or clear error | E2E | ❌ No |
| Response payload very large (many items with large bodies) | Parsed correctly | E2E | ❌ No |

### 21.3 Partition & Container Limits

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Operations spanning 2 partitions | Works | E2E | ❌ No |
| Operations spanning 10 partitions | Works | E2E | ❌ No |
| Operations spanning 50+ partitions | Works or documented limit | E2E | ❌ No |
| Operations across 2 containers | Works | E2E | ❌ No |
| Operations across 5 containers | Works | E2E | ❌ No |
| Operations across 10+ containers | Works or documented limit | E2E | ❌ No |
| Operations across 2 databases | Works | E2E | ❌ No |
| Operations across 5+ databases | Works or documented limit | E2E | ❌ No |

---

## 22. Regression & Non-Interference

### 22.1 Non-DTX Operations Unaffected

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Point read performance unchanged with DTX code present | No regression | E2E | ❌ No |
| Point write (CreateItem) performance unchanged | No regression | E2E | ❌ No |
| Query performance unchanged | No regression | E2E | ❌ No |
| TransactionalBatch (existing feature) still works | No interference | E2E | ❌ No |
| Change Feed operations unaffected | No interference | E2E | ❌ No |
| Bulk operations unaffected | No interference | E2E | ❌ No |
| Session token handling for non-DTX ops unchanged | No regression | E2E | ❌ No |
| **[E2E]** Mixed workload — non-DTX CRUD + DTX commits on same CosmosClient against emulator | Both paths succeed; no shared-state corruption | E2E | ❌ No |

### 22.2 Memory/CPU Baseline

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Idle client memory unchanged with DTX preview flag | No overhead at rest | E2E | ❌ No |
| Startup time unchanged with DTX code | No added latency | E2E | ❌ No |
| CPU baseline unchanged when DTX not used | Zero cost abstraction | E2E | ❌ No |
| No new background threads created for DTX | On-demand only | E2E | ❌ No |

---

## 23. Graceful Degradation

### 23.1 Feature Unavailability

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Account not enabled for DTX → commit returns clear error | Actionable error message | E2E | ❌ No |
| Region doesn't support DTX → error with region info | Diagnosable | E2E | ❌ No |
| Service temporarily disables DTX (maintenance) | Retriable or clear unavailable error | E2E | ❌ No |
| DTX endpoint unreachable → timeout and clear error | Not infinite hang | E2E | ❌ No |
| Server returns unexpected response format (future version?) | Graceful parse failure | E2E | ❌ No |

### 23.2 Partial System Availability

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| One target container throttled, others fine → whole txn fails | Atomic (all-or-nothing) | E2E | ❌ No |
| One partition unavailable, others fine → whole txn fails | Atomic failure | E2E | ❌ No |
| Gateway healthy but coordinator down | 5xx with retry | E2E | ❌ No |
| Coordinator healthy but one partition shard down | Transaction fails atomically | E2E | ❌ No |

---

## 24. E2E Emulator Integration

### 24.1 Happy-Path Emulator Round-Trip

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Create transaction with each supported op type (Create/Replace/Upsert/Delete/Patch + stream variants) against emulator | All persisted; per-op result has real ETag, RU, status | E2E | ❌ No |
| Read transaction returns committed items with real ETags | ETags match what a subsequent point-read returns | E2E | ❌ No |
| Mixed typed + stream ops in same write txn | All succeed; round-trip JSON identical | E2E | ❌ No |
| Empty-body ops (Delete) and large-body ops in same txn | Both serialize and persist correctly | E2E | ❌ No |

### 24.2 Cross-Partition Commits

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Write transaction across N distinct logical partitions in one container | Atomic — all visible after commit, none visible if one fails | E2E | ❌ No |
| Write transaction across multiple containers (same DB) | Atomic across containers | E2E | ❌ No |
| Write transaction across multiple databases (if supported) | Either atomic or clear pre-commit validation error | E2E | ❌ No |
| Read transaction across multiple partitions | Consistent snapshot | E2E | ❌ No |

### 24.3 Hierarchical / Sub-Partition Keys

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DTX writes/reads against container with 2-level hierarchical PK | Routed and persisted correctly | E2E | ❌ No |
| DTX writes against container with 3-level hierarchical PK | Routed and persisted correctly | E2E | ❌ No |
| Mixed PK depths across ops in same txn | Behaves per documented contract (succeed or clear error) | E2E | ❌ No |

### 24.4 Indexing Policy Interactions

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DTX writes against container with custom included/excluded paths | Documents queryable per policy after commit | E2E | ❌ No |
| DTX writes against container with composite indexes | Composite queries return committed docs | E2E | ❌ No |
| DTX writes against container with NO indexing (None mode) | Writes succeed; no query-time impact | E2E | ❌ No |
| Lazy indexing mode + DTX | Eventually-queryable per policy | E2E | ❌ No |

### 24.5 TTL Interactions

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DTX writes that set per-item TTL | TTL respected after commit | E2E | ❌ No |
| DTX read of item near expiry | Returns item until expiry; not after | E2E | ❌ No |
| Container default TTL + DTX writes | Inherited TTL applied | E2E | ❌ No |
| DTX read of already-expired item | Returns 404 per normal semantics | E2E | ❌ No |

### 24.6 Container / Database Lifecycle During DTX

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Container deleted between operation-build and commit | Commit fails with clear NotFound | E2E | ❌ No |
| Container recreated (same name, new RID) during outer retry loop | Cache invalidated; commit either succeeds against new RID or fails cleanly | E2E | ❌ No |
| Throughput changed mid-commit | Commit unaffected | E2E | ❌ No |
| Database deleted mid-commit | Commit fails with clear error | E2E | ❌ No |

### 24.7 Throughput Modes

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Manual RU/s container — DTX under throttling produces real 429/3200 | Inner retry loop honors RetryAfter | E2E | ❌ No |
| Autoscale RU/s container — DTX under burst | Eventual success or 429 with RetryAfter | E2E | ❌ No |
| Serverless container — DTX commits | Succeed within serverless RU limits | E2E | ❌ No |

### 24.8 Real Concurrent Conflict

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Two clients commit overlapping write txns simultaneously | One succeeds, one returns ETag conflict (412) or coordinator race (449/5352) | E2E | ❌ No |
| Same item updated by N parallel transactions with IfMatch | Exactly one wins; others fail with 412 | E2E | ❌ No |
| DTX competing with non-DTX point-write on same item | Server-side conflict resolution honored | E2E | ❌ No |

### 24.9 Connection Modes

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| CosmosClient with Gateway mode — DTX commit | Routes via Gateway; succeeds | E2E | ❌ No |
| CosmosClient with Direct mode — DTX commit | DTX forces Gateway path internally; still succeeds | E2E | ❌ No |
| Connection multiplexing — many parallel DTX commits | No connection leak; bounded socket usage | E2E | ❌ No |

### 24.10 Session Consistency Interactions

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DTX commit then read on same CosmosClient — Session | Reads see committed state | E2E | ❌ No |
| DTX commit on client A, read on client B — Session with token propagation | Reads see committed state when session token is propagated | E2E | ❌ No |
| DTX commit on client A, read on client B — no token propagation | Reads may be stale until session converges | E2E | ❌ No |
| Session token returned in DistributedTransactionResponse | Token is consumable in subsequent ops | E2E | ❌ No |

### 24.11 Consistency Levels

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DTX commit under Strong consistency | Behaves per Strong semantics | E2E | ❌ No |
| DTX commit under BoundedStaleness | Behaves per BS semantics | E2E | ❌ No |
| DTX commit under Session | Default path; covered in §24.10 | E2E | ❌ No |
| DTX commit under Eventual / ConsistentPrefix | Behavior documented; no protocol error | E2E | ❌ No |
| Per-request consistency override (if supported) | Override applied for that commit | E2E | ❌ No |

### 24.12 Bulk + DTX Coexistence

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Same CosmosClient with `AllowBulkExecution=true` runs both Bulk and DTX | No shared-state interference | E2E | ❌ No |
| Bulk request in flight while DTX commits | Both complete correctly | E2E | ❌ No |

### 24.13 Real Diagnostics End-to-End

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| `DistributedTransactionResponse.Diagnostics` populated from real commit | Contains region, status, sub-status, RU, latency | E2E | ❌ No |
| OTel span emitted for DTX commit | Span has expected attributes (operation, status, RU) | E2E | ❌ No |
| Diagnostics on retried commit | Shows all attempts with their individual outcomes | E2E | ❌ No |
| Diagnostics on failed commit | Shows terminal error and the path leading to it | E2E | ❌ No |

### 24.14 Emulator Restart Mid-Commit

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Stop emulator while DTX outer-loop is sleeping | SDK either reconnects (if within 30s wall-clock cap) or fails cleanly | E2E | ❌ No |
| Stop+restart emulator within 30s of commit start | SDK may succeed after reconnect | E2E | ❌ No |
| Emulator down for > 30s | Outer wall-clock cap triggers; commit fails with clear error | E2E | ❌ No |

### 24.15 Large Payload Round-Trip

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Single item near 2 MB ceiling in DTX | Persisted; readable; ETag returned | E2E | ❌ No |
| Many medium-sized items totaling near request-size limit | Either succeeds or clear server-side too-large error | E2E | ❌ No |
| Unicode-heavy large payload | Round-trips with full byte fidelity | E2E | ❌ No |

### 24.16 Many-Op Transactions

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Maximum supported op-count transaction against emulator | Commits with per-op results in correct order | E2E | ❌ No |
| One op past the maximum | Clear validation error or server-side too-large | E2E | ❌ No |
| Per-op `RequestCharge` aggregates equal `Response.RequestCharge` from real server | Billing parity holds | E2E | ❌ No |

### 24.17 Mixed Container Schemas

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| DTX touching containers with different PK definitions in one txn (where supported) | Either succeeds atomically or clear pre-commit error | E2E | ❌ No |
| DTX touching containers with different indexing policies | Each container honors its own policy after commit | E2E | ❌ No |
| DTX with mix of TTL-enabled and TTL-disabled containers | Each container honors its own TTL | E2E | ❌ No |

### 24.18 ETag Round-Trip Through Real Commit

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Read returns server ETag, feed into subsequent DTX `IfMatchEtag` | Commit succeeds | E2E | ❌ No |
| Stale ETag feed into DTX `IfMatchEtag` | Server returns 412 PreconditionFailed | E2E | ❌ No |
| `IfNoneMatchEtag` on read returns 304 path | Honored end-to-end | E2E | ❌ No |
| ETag from one DTX op used as `IfMatch` in next DTX | Chained commit succeeds | E2E | ❌ No |

### 24.19 RU Billing Parity

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Aggregate `Response.RequestCharge` equals sum of per-op `RequestCharge` | Parity holds across all op types | E2E | ❌ No |
| Retried commit reports only the final attempt's RU | Not cumulative across attempts | E2E | ❌ No |
| RU charge is non-zero for successful writes | Server-billed and surfaced | E2E | ❌ No |
| RU charge surfaced even for failed commits where server processed work | Visible to caller for billing reconciliation | E2E | ❌ No |

### 24.20 Cancellation Mid-Flight Against Emulator

| Test | Expectation | Level | Implemented |
|------|-------------|-------|-------------|
| Cancel CancellationToken while HTTP request is in flight to emulator | `OperationCanceledException` raised promptly | E2E | ❌ No |
| Cancel CancellationToken during inner-loop backoff sleep | `OperationCanceledException` raised without finishing the sleep | E2E | ❌ No |
| Cancel CancellationToken during outer-loop backoff sleep | Same as above, at outer-loop layer | E2E | ❌ No |
| Cancel after server has accepted commit (race) | Either OCE or committed result — never partial state | E2E | ❌ No |

---
