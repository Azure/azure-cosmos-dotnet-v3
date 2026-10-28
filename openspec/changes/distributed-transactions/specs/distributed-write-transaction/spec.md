## ADDED Requirements

### Requirement: Supported write operations span multiple partitions and containers

`DistributedWriteTransaction` SHALL support Create, Replace, Delete, Upsert, and Patch operations. Each operation SHALL independently specify its target database, container, partition key, and document id, allowing operations to target any combination of partitions and containers within the same database account.

#### Scenario: Operations target different partition keys

- **WHEN** a `DistributedWriteTransaction` is built with operations targeting different partition keys within the same container
- **THEN** `CommitTransactionAsync` SHALL commit all operations atomically

#### Scenario: Operations target different containers

- **WHEN** a `DistributedWriteTransaction` is built with operations targeting different containers within the same database account
- **THEN** `CommitTransactionAsync` SHALL commit all operations atomically

### Requirement: Per-operation ETag is returned on success

After a successful commit, the response SHALL expose an ETag for each write operation, reflecting the server-assigned version of the written document.

#### Scenario: ETag returned for each committed operation

- **WHEN** `CommitTransactionAsync` completes with `IsSuccessStatusCode = true`
- **THEN** each operation's result SHALL carry a non-null `ETag` that can be used as `IfMatchEtag` on a subsequent `DistributedWriteTransaction` for optimistic concurrency

### Requirement: Per-operation optimistic concurrency via IfMatchEtag

Each operation in a `DistributedWriteTransaction` SHALL support an `IfMatchEtag` precondition. If the document's current ETag does not match, the entire transaction SHALL be rolled back.

#### Scenario: ETag precondition fails on one operation

- **WHEN** `CommitTransactionAsync` is called and one operation's `IfMatchEtag` does not match the document's current ETag
- **THEN** the service SHALL roll back all operations and the failed operation's result SHALL carry `StatusCode = 412 Precondition Failed`
- **AND** all other operations SHALL carry `StatusCode = 424 Failed Dependency`



`DistributedWriteTransaction` SHALL atomically commit all staged write operations across any partitions and containers within the same database account. If any operation fails, the service SHALL roll back all operations in the transaction.

#### Scenario: All operations succeed

- **WHEN** `CommitTransactionAsync` is called and all operations are valid and commit successfully
- **THEN** the SDK SHALL return a `DistributedTransactionResponse` with `IsSuccessStatusCode = true` and `StatusCode = 200`

#### Scenario: One operation fails — all are rolled back

- **WHEN** `CommitTransactionAsync` is called and at least one operation fails (e.g., ETag mismatch, conflict)
- **THEN** the service SHALL roll back all operations and the SDK SHALL return a `DistributedTransactionResponse` with `IsSuccessStatusCode = false`
- **AND** the failed operation's result SHALL carry the specific failure status code (e.g., 412 Precondition Failed, 409 Conflict)
- **AND** all other operations SHALL carry `StatusCode = 424 Failed Dependency`

### Requirement: Session tokens are merged into ISessionContainer after commit

After a commit completes (success or failure), the SDK SHALL merge per-operation session tokens from the response into the client's `ISessionContainer`.

#### Scenario: Session tokens merged after commit

- **WHEN** `CommitTransactionAsync` completes and the response contains per-operation `sessionToken` values
- **THEN** the SDK SHALL call `ISessionContainer.SetSessionToken` for each operation's response token so that subsequent reads on any affected partition observe the committed writes

### Requirement: Commits are idempotent via a per-call idempotency token

Each `CommitTransactionAsync` call SHALL auto-generate a unique idempotency token and send it as `x-ms-idempotency-token`. The token SHALL be exposed on the response so callers can safely replay a commit whose outcome is unknown.

#### Scenario: Retry with the same idempotency token

- **WHEN** a caller retries `CommitTransactionAsync` using the `IdempotencyToken` from a previous response
- **THEN** the service SHALL return the same result as the original commit without applying the operations a second time

### Requirement: Write transactions are not supported in Direct mode

The SDK SHALL route every `CommitTransactionAsync` request through the Gateway or Thin Client endpoint. Direct mode is not supported for distributed transactions.

#### Scenario: Direct mode client commits a write transaction

- **WHEN** the `CosmosClient` is configured with Direct connectivity mode
- **THEN** the SDK SHALL override connectivity to Gateway mode and route `CommitTransactionAsync` to the Gateway write-transaction endpoint

### Requirement: CommitTransactionAsync is blocked on accounts with multiple write regions

`CommitTransactionAsync` SHALL throw `NotSupportedException` if the account is configured with multiple write regions.

#### Scenario: Multi-write-region account

- **WHEN** the Cosmos DB account has `UseMultipleWriteLocations = true`
- **THEN** `CommitTransactionAsync` SHALL throw `NotSupportedException` before sending any request

### Requirement: Operation count and payload size are validated before the wire call

The SDK SHALL reject a `CommitTransactionAsync` call before dispatch if the staged operations exceed the supported limits.

#### Scenario: Exceeds maximum operation count

- **WHEN** more than 100 operations have been staged on the transaction
- **THEN** `CommitTransactionAsync` SHALL throw `ArgumentException` before sending any request

#### Scenario: Exceeds maximum payload size

- **WHEN** the total serialized request body would exceed 2 MB
- **THEN** `CommitTransactionAsync` SHALL throw `ArgumentException` before sending any request

### Requirement: Throttled requests are automatically retried

When the Gateway returns 429 (Too Many Requests), the SDK SHALL automatically retry after the `x-ms-retry-after-ms` interval, up to the configured maximum retry count.

#### Scenario: 429 response triggers retry

- **WHEN** `CommitTransactionAsync` receives a 429 response
- **THEN** the SDK SHALL wait for `x-ms-retry-after-ms` and retry the commit with the same idempotency token

### Requirement: CommitTransactionAsync is instrumented with OpenTelemetry

The SDK SHALL emit an OpenTelemetry span for each `CommitTransactionAsync` call.

#### Scenario: Successful commit span

- **WHEN** `CommitTransactionAsync` completes successfully
- **THEN** the SDK SHALL emit a span with `db.cosmosdb.operation_type = "CommitDistributedTransaction"`, `db.cosmosdb.status_code = 200`, and `db.cosmosdb.request_charge` set to the total RU charge for the transaction
