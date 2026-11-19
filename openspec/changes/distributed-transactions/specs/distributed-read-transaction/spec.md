## ADDED Requirements

### Requirement: Supported read operations span multiple partitions and containers

`DistributedReadTransaction` SHALL support ReadItem operations. Each operation SHALL independently specify its target database, container, partition key, and document id, allowing reads from any combination of partitions and containers within the same database account.

#### Scenario: Operations target different partition keys

- **WHEN** a `DistributedReadTransaction` is built with ReadItem operations targeting different partition keys within the same container
- **THEN** `CommitTransactionAsync` SHALL return all items as a consistent snapshot

#### Scenario: Operations target different containers

- **WHEN** a `DistributedReadTransaction` is built with ReadItem operations targeting different containers within the same database account
- **THEN** `CommitTransactionAsync` SHALL return all items as a consistent snapshot

### Requirement: Per-item ETag is returned in the response

The response SHALL expose an ETag for each successfully read item, reflecting the document version that was read.

#### Scenario: ETag returned for each read item

- **WHEN** `CommitTransactionAsync` completes with `IsSuccessStatusCode = true`
- **THEN** each operation's result SHALL carry a non-null `ETag` that can be used as `IfMatchEtag` on a subsequent `DistributedWriteTransaction` for conditional writes (optimistic CAS pattern)

### Requirement: Conditional reads via IfNoneMatchEtag return 304 when unchanged

Each ReadItem operation in a `DistributedReadTransaction` SHALL support an optional `IfNoneMatchETag` precondition. If the document's current ETag matches, the server SHALL return 304 Not Modified for that operation with no response body.

#### Scenario: Document is unchanged — 304 returned

- **WHEN** `CommitTransactionAsync` is called and one operation specifies an `IfNoneMatchETag` that matches the document's current ETag
- **THEN** that operation's result SHALL carry `StatusCode = 304` and a null resource body

#### Scenario: Document has changed — full response returned

- **WHEN** `CommitTransactionAsync` is called and one operation specifies an `IfNoneMatchETag` that does not match the document's current ETag
- **THEN** that operation's result SHALL carry `StatusCode = 200` and the full document body



`DistributedReadTransaction` SHALL execute all staged ReadItem operations against a single consistent server-side snapshot, guaranteeing that no two results reflect different logical instants (no read skew).

#### Scenario: All items read from the same snapshot

- **WHEN** `CommitTransactionAsync` is called with multiple ReadItem operations targeting different partitions or containers
- **THEN** the service SHALL return all items as they existed at the same logical point in time
- **AND** the SDK SHALL return a `DistributedTransactionResponse` with `IsSuccessStatusCode = true` and `StatusCode = 200`

#### Scenario: One item not found

- **WHEN** `CommitTransactionAsync` is called and one of the staged items does not exist in the container
- **THEN** the SDK SHALL return a `DistributedTransactionResponse` with `StatusCode = 207`
- **AND** the missing item's result SHALL carry `StatusCode = 404`
- **AND** all other items' results SHALL carry their actual response status codes

### Requirement: Session tokens are merged into ISessionContainer after execution

After execution completes, the SDK SHALL merge per-operation session tokens from the response into the client's `ISessionContainer`.

#### Scenario: Session tokens merged after successful execution

- **WHEN** `CommitTransactionAsync` completes and the response contains per-operation `sessionToken` values
- **THEN** the SDK SHALL merge each response token into `ISessionContainer` for the corresponding partition

### Requirement: Read transactions are not supported in Direct mode

The SDK SHALL route every `CommitTransactionAsync` request through the Gateway or Thin Client endpoint. Direct mode is not supported for distributed transactions.

#### Scenario: Direct mode client executes a read transaction

- **WHEN** the `CosmosClient` is configured with Direct connectivity mode
- **THEN** the SDK SHALL override connectivity to Gateway mode and route `CommitTransactionAsync` to the Gateway read-transaction endpoint

### Requirement: Read transactions are idempotent and safe to retry unconditionally

`CommitTransactionAsync` SHALL be safe to retry on any transient failure without risk of duplicate side effects. No idempotency token is required.

#### Scenario: Network failure triggers automatic retry

- **WHEN** `CommitTransactionAsync` experiences a transient failure (408 Request Timeout or 503 Service Unavailable)
- **THEN** the SDK SHALL automatically retry the request

### Requirement: CommitTransactionAsync is blocked on accounts with multiple write regions

`CommitTransactionAsync` SHALL throw `NotSupportedException` if the account is configured with multiple write regions.

#### Scenario: Multi-write-region account

- **WHEN** the Cosmos DB account has `UseMultipleWriteLocations = true`
- **THEN** `CommitTransactionAsync` SHALL throw `NotSupportedException` before sending any request

### Requirement: Consistency level controls snapshot freshness

The `ConsistencyLevel` set in `DistributedReadTransactionRequestOptions` SHALL determine how fresh the server-side snapshot is.

#### Scenario: Strong consistency provides globally latest snapshot

- **WHEN** `CommitTransactionAsync` is called with `ConsistencyLevel = Strong`
- **THEN** the service SHALL return all items from the globally latest committed version with no stale reads

#### Scenario: Session consistency provides per-partition monotonic reads

- **WHEN** `CommitTransactionAsync` is called with `ConsistencyLevel = Session` and per-operation session tokens are populated from the client's `ISessionContainer`
- **THEN** the service SHALL return items that are at least as fresh as the provided session token for each partition

### Requirement: Operation count is validated before the wire call

The SDK SHALL reject a `CommitTransactionAsync` call before dispatch if the staged operations exceed the supported limit.

#### Scenario: Exceeds maximum operation count

- **WHEN** more than 100 ReadItem operations have been staged on the transaction
- **THEN** `CommitTransactionAsync` SHALL throw `ArgumentException` before sending any request

### Requirement: Throttled requests are automatically retried

When the Gateway returns 429 (Too Many Requests), the SDK SHALL automatically retry after the `x-ms-retry-after-ms` interval.

#### Scenario: 429 response triggers retry

- **WHEN** `CommitTransactionAsync` receives a 429 response
- **THEN** the SDK SHALL wait for `x-ms-retry-after-ms` and retry the request

### Requirement: CommitTransactionAsync is instrumented with OpenTelemetry

The SDK SHALL emit an OpenTelemetry span for each `CommitTransactionAsync` call.

#### Scenario: Successful execution span

- **WHEN** `CommitTransactionAsync` completes successfully
- **THEN** the SDK SHALL emit a span with operation name `commit_distributed_read_transaction`, `db.cosmosdb.status_code = 200`, and `db.cosmosdb.request_charge` set to the total RU charge for all read operations
