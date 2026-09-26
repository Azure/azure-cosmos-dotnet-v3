## 1. Setup

- [ ] 1.1 Create a git worktree for the feature branch (e.g., `users/<alias>/distributed-transactions`) to work in isolation from the main working directory

## 2. Write Transaction — Core (already implemented, internal)

- [x] 2.1 `DistributedTransaction` abstract base class (`src/DistributedTransaction/DistributedTransaction.cs`)
- [x] 2.2 `DistributedWriteTransaction` abstract class with 9 write operation methods (`src/DistributedTransaction/DistributedWriteTransaction.cs`)
- [x] 2.3 `DistributedWriteTransactionCore` sealed concrete implementation (`src/DistributedTransaction/DistributedWriteTransactionCore.cs`)
- [x] 2.4 `DistributedTransactionOperation` operation model (`src/DistributedTransaction/DistributedTransactionOperation.cs`)
- [x] 2.5 `DistributedTransactionRequestOptions` per-operation options class (`src/DistributedTransaction/DistributedTransactionRequestOptions.cs`)
- [x] 2.6 `DistributedTransactionConstants` routing helpers (`src/DistributedTransaction/DistributedTransactionConstants.cs`)
- [x] 2.7 `DistributedTransactionSerializer` JSON serializer for `POST /operations/dtc` body (`src/DistributedTransaction/DistributedTransactionSerializer.cs`)
- [x] 2.8 `DistributedTransactionServerRequest` request builder (`src/DistributedTransaction/DistributedTransactionServerRequest.cs`)
- [x] 2.9 `DistributedTransactionResponse` response parser with MultiStatus promotion and IDisposable (`src/DistributedTransaction/DistributedTransactionResponse.cs`)
- [x] 2.10 `DistributedTransactionOperationResult` per-operation result type (`src/DistributedTransaction/DistributedTransactionOperationResult.cs`)
- [x] 2.11 `DistributedTransactionCommitter` orchestrator: RID resolution → serialization → HTTP dispatch → session merge (`src/DistributedTransaction/DistributedTransactionCommitter.cs`)
- [x] 2.12 Gateway routing: `GatewayStoreModel`, `GatewayStoreClient`, `RequestInvokerHandler` updated for `CommitDistributedTransaction`
- [x] 2.13 `CosmosClient.CreateDistributedWriteTransaction()` entry point (`src/CosmosClient.cs`)
- [x] 2.14 Unit tests: serializer, response parser, committer (RID resolution, session merge) (`tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/`)
- [x] 2.15 Emulator integration tests with mock DTC handler (`tests/Microsoft.Azure.Cosmos.EmulatorTests/DistributedTransaction/DistributedTransactionTests.cs`)

## 3. Write Transaction — Gaps (not yet implemented)

- [ ] 3.1 Populate outbound session tokens from `ISessionContainer` before dispatch in `DistributedTransactionCommitter` (each op's `sessionToken` field is currently always null before sending)
- [ ] 3.2 Add op-count and total-body-size guards in `DistributedWriteTransactionCore` (reject > 100 ops or > 2 MB before the wire call)
- [ ] 3.3 Wrap `CommitTransactionAsync` in 429-retry logic using the SDK's `RetryHandler`
- [ ] 3.4 Wrap `CommitTransactionAsync` in `OperationHelperAsync` with `OpenTelemetryConstants.Operations.CommitDistributedTransaction = "commit_distributed_transaction"`; add the constant to `OpenTelemetryConstants.cs`
- [ ] 3.5 Remove dead code: `DistributedTransactionRequest` class is never instantiated
- [ ] 3.6 Add emulator end-to-end tests against the real `/operations/dtc` endpoint (no mock handler)
- [ ] 3.7 Add single write-region guard: throw `NotSupportedException` if account has multiple write regions
- [ ] 3.8 Promote all write transaction types from `#if INTERNAL` to public; update API contract baseline with `UpdateContracts.ps1`
- [ ] 3.9 Public documentation and changelog entry

## 4. Read Transaction — Core Infrastructure

- [ ] 4.1 `DistributedReadTransaction` abstract class with `ReadItem()` and `CommitTransactionAsync()` methods (`src/DistributedTransaction/DistributedReadTransaction.cs`)
- [ ] 4.2 `DistributedReadTransactionRequestOptions` options class with `ConsistencyLevel`, `ReadConsistencyStrategy` (preview), and `SessionToken` (`src/DistributedTransaction/DistributedReadTransactionRequestOptions.cs`)
- [ ] 4.3 `DistributedReadTransactionOperation` model: `database`, `container`, `id`, `partitionKey`, `sessionToken`, `ifNoneMatchETag`, `index` (`src/DistributedTransaction/DistributedReadTransactionOperation.cs`)
- [ ] 4.4 `DistributedReadTransactionCore` sealed concrete implementation (`src/DistributedTransaction/DistributedReadTransactionCore.cs`)

## 5. Read Transaction — Wire Protocol

- [ ] 5.1 `DistributedReadTransactionSerializer`: serializes read ops to JSON body; omits `resourceBody`; adds `ifNoneMatchETag` (`src/DistributedTransaction/DistributedReadTransactionSerializer.cs`)
- [ ] 5.2 `DistributedReadTransactionServerRequest`: builds `RequestMessage` reusing `OperationType.CommitDistributedTransaction` and `ResourceType.DistributedTransactionBatch`; sets the read endpoint URL and `UseGatewayMode = true` to override Direct mode (Gateway and Thin Client modes are supported; Direct mode is not) (`src/DistributedTransaction/DistributedReadTransactionServerRequest.cs`)
- [ ] 5.3 Extend `DistributedTransactionResponse` for read transaction support: change `IdempotencyToken` from `Guid` to `Guid?` (null when returned from a read commit); add `GetItem<T>(index)` typed deserialization helper on the shared response type (`src/DistributedTransaction/DistributedTransactionResponse.cs`)

## 6. Read Transaction — Orchestration & Gateway Integration

- [ ] 6.1 `DistributedReadTransactionCommitter`: RID resolution → outbound session token population → serialize → HTTP dispatch → parse response → merge session tokens (`src/DistributedTransaction/DistributedReadTransactionCommitter.cs`)
- [ ] 6.2 Verify no gateway routing changes are required: `GatewayStoreModel` session-token stripping and `GatewayStoreClient`/`RequestInvokerHandler` HTTP method selection already apply correctly via the reused `OperationType.CommitDistributedTransaction`
- [ ] 6.3 `CosmosClient.CreateDistributedReadTransaction()` entry point, guarded by `#if INTERNAL` (`src/CosmosClient.cs`)

## 7. Read Transaction — Reliability & Observability

- [ ] 7.1 Wrap `CommitTransactionAsync` in 429-retry logic (reads are safe to retry unconditionally)
- [ ] 7.2 Wrap `CommitTransactionAsync` in `OperationHelperAsync` with a new `OpenTelemetryConstants.Operations.CommitDistributedReadTransaction = "commit_distributed_read_transaction"` constant; add it to `OpenTelemetryConstants.cs` (OTel span names are independent of `OperationType`)
- [ ] 7.3 Add op-count and total-body-size guards (reject > 100 ops or > 2 MB before the wire call)
- [ ] 7.4 Add single write-region guard: throw `NotSupportedException` if account has multiple write regions

## 8. Read Transaction — Tests

- [ ] 8.1 Unit tests — serializer: every field name matches server model; null `sessionToken`; `ifNoneMatchETag` present/absent (`tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedReadTransactionSerializerTests.cs`)
- [ ] 8.2 Unit tests — `DistributedTransactionResponse` with read transaction: 200 all-success; 207 mixed; malformed JSON; count mismatch; `GetItem<T>()` deserialization; `IdempotencyToken` is null (`tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedReadTransactionResponseTests.cs`)
- [ ] 8.3 Unit tests — session tokens: outbound populated from `ISessionContainer`; inbound merged back after execution (`tests/Microsoft.Azure.Cosmos.Tests/DistributedTransaction/DistributedReadTransactionCommitterTests.cs`)
- [ ] 8.4 Emulator integration tests: write known items; execute `DistributedReadTransaction` at Strong consistency; assert snapshot guarantee; test 304 for `ifNoneMatchETag`; test 404 for missing item; test session token round-trip (`tests/Microsoft.Azure.Cosmos.EmulatorTests/DistributedTransaction/DistributedReadTransactionTests.cs`)

## 9. Public Promotion (both features)

- [ ] 9.1 Remove all `#if INTERNAL` guards from write and read transaction types
- [ ] 9.2 Run `UpdateContracts.ps1` to capture new public types in API contract baseline files
- [ ] 9.3 Add both features to `changelog.md` and public documentation
- [ ] 9.4 Add code samples to `Microsoft.Azure.Cosmos.Samples/`

## 10. Cross-SDK Implementations

- [ ] 10.1 Java SDK: implement `DistributedWriteTransaction` and `DistributedReadTransaction`
- [ ] 10.2 Python SDK: implement async `DistributedWriteTransaction` and `DistributedReadTransaction`
- [ ] 10.3 JavaScript/Node.js SDK: implement Promise-based `DistributedWriteTransaction` and `DistributedReadTransaction`
- [ ] 10.4 Go SDK: implement context-based `DistributedWriteTransaction` and `DistributedReadTransaction`
