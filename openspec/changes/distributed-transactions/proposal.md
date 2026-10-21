## Why

Azure Cosmos DB provides full ACID transactions within a single logical partition via `TransactionalBatch` and stored procedures. Applications that need atomicity across multiple partitions or containers must hand-roll compensating patterns (Saga, event sourcing) at significant complexity cost. Two gaps exist today within a single database account:

1. **Cross-partition write atomicity** — there is no API to atomically commit mutations spanning multiple partition keys or containers. Any scenario (ledger transfers, order+inventory, multi-entity state machines) requires application-level compensating logic that is error-prone and non-portable across languages.

2. **Consistent cross-partition reads** — `ReadManyItemsAsync` fans out parallel queries per partition range with no snapshot guarantee, making it vulnerable to read skew: two items returned in the same call can reflect different logical instants (e.g., one item seen before a concurrent transfer and another after it).

The Cosmos DB Gateway exposes a distributed transaction coordinator at `POST /operations/dtc`. The same endpoint handles both atomic writes and consistent snapshot reads — the `operationType` field in the JSON request body (`"Write"` or `"Read"`) selects the behavior.

## What Changes

- Add `CosmosClient.CreateDistributedWriteTransaction()` returning a new `DistributedWriteTransaction` builder.
- Add `CosmosClient.CreateDistributedReadTransaction()` returning a new `DistributedReadTransaction` builder.
- `DistributedWriteTransaction` accumulates Create, Replace, Delete, Upsert, and Patch operations across any partitions and containers within the same account, then commits them atomically via `CommitTransactionAsync()`.
- `DistributedReadTransaction` accumulates ReadItem operations across any partitions and containers, then executes them as a single consistent server-side snapshot via `CommitTransactionAsync()`.
- New wire operations: `CommitDistributedTransaction` (reused for both write and read transactions; the distinction is in the request body).
- Session tokens are merged into the client's `ISessionContainer` after every commit or execute.
- Per-operation ETags are returned in both write and read responses, enabling ETag-gated follow-up writes.

## Capabilities

### New Capabilities

- `distributed-write-transaction`: Adds `CosmosClient.CreateDistributedWriteTransaction()`, `DistributedWriteTransaction`, `DistributedTransactionResponse`, and related types for cross-partition, cross-container atomic writes within the same database account.
- `distributed-read-transaction`: Adds `CosmosClient.CreateDistributedReadTransaction()`, `DistributedReadTransaction`, and related types for cross-partition, cross-container consistent snapshot reads within the same database account. Reuses `DistributedTransactionResponse` as the shared response type for both write and read transactions.

### Modified Capabilities

- `client-and-configuration`: `CosmosClient` gains two new factory methods: `CreateDistributedWriteTransaction()` and `CreateDistributedReadTransaction()`.

## Impact

- **Public API surface**: New abstract classes `DistributedWriteTransaction` and `DistributedReadTransaction`; a single shared `DistributedTransactionResponse` response type for both (with `IdempotencyToken` as `Guid?`, non-null for writes, null for reads); new options classes; two new factory methods on `CosmosClient`. Initially behind `#if INTERNAL`; promoted to public at GA.
- **Wire protocol**: Uses `POST /operations/dtc` for reads and writes.
- **Gateway routing**: `GatewayStoreModel`, `GatewayStoreClient`, and `RequestInvokerHandler` gain awareness of both operation types; `x-ms-session-token` is suppressed for both (per-partition tokens travel in the body).
- **Existing behavior**: No change for any existing API. Both features are entirely additive.
- **Tests**: New unit tests (serialization, response parsing, session token merge) and emulator integration tests for both write and read transactions.
