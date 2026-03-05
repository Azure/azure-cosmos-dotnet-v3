# Bulk and Batch Operations

## Purpose

Transactional batch provides atomic multi-operation execution within a single partition key. Bulk execution mode optimizes throughput for high-volume ingestion and processing scenarios.

## Requirements

### Requirement: Transactional Batch
The SDK SHALL execute multiple operations atomically within a single partition key scope.

#### Successful batch execution
**When** `Container.CreateTransactionalBatch(partitionKey).CreateItem(a).UpsertItem(b).DeleteItem(cId).ExecuteAsync()` is called with multiple operations on the same partition key, the SDK shall execute all operations atomically, return a `TransactionalBatchResponse` with overall status code 200, and provide individual operation results accessible by index.

#### Batch failure (atomicity)
**If** any operation in a transactional batch would fail (e.g., creating a duplicate), **then** the SDK shall roll back ALL operations in the batch, persist no partial state, and return a response status code indicating the failure.

#### Mixed operation types
**When** `ExecuteAsync()` is called on a batch containing Create, Replace, Upsert, Patch, Delete, and Read operations, the SDK shall execute all operations in the order they were added.

### Requirement: Batch Operation Types
The SDK SHALL support all CRUD operation types within a transactional batch.

#### Batch create
**When** the batch executes with `batch.CreateItem<T>(item)`, the SDK shall create the item if it does not exist.

#### Batch replace
**When** the batch executes with `batch.ReplaceItem<T>(id, item)`, the SDK shall fully replace the existing item.

#### Batch upsert
**When** the batch executes with `batch.UpsertItem<T>(item)`, the SDK shall create or replace the item.

#### Batch patch
**When** the batch executes with `batch.PatchItem(id, patchOperations)`, the SDK shall apply partial modifications to the item.

#### Batch delete
**When** the batch executes with `batch.DeleteItem(id)`, the SDK shall remove the item.

#### Batch read
**When** the batch executes with `batch.ReadItem(id)`, the SDK shall return the item's current state in the operation result.

### Requirement: Batch with Stream
The SDK SHALL support stream-based batch operations for zero-copy performance.

#### Stream-based create
**When** the batch executes with `batch.CreateItemStream(stream)`, the SDK shall create the item from the raw JSON stream without deserialization.

### Requirement: Batch Response Inspection
The SDK SHALL provide access to individual operation results within a batch response.

#### Access individual results
**When** `response.GetOperationResultAtIndex<T>(index)` is called on a completed batch response, the SDK shall return the result for that specific operation with its status code, resource, and ETag.

#### Check overall success
**When** `response.IsSuccessStatusCode` is checked on a completed batch response, the SDK shall reflect whether all operations succeeded.

### Requirement: Batch Partition Key Constraint
The SDK SHALL enforce that all operations in a batch target the same partition key.

#### Single partition key
**When** a batch is created with `CreateTransactionalBatch(partitionKey)` and operations are added, the SDK shall scope all operations to that partition key and shall not support cross-partition batches.

### Requirement: Bulk Execution Mode
The SDK SHALL support a bulk execution mode that batches individual operations for higher throughput.

#### Enable bulk mode
**Where** `CosmosClientOptions.AllowBulkExecution = true`, **when** individual item operations (Create, Upsert, Replace, Delete, Read) are issued concurrently, the SDK shall automatically group operations into server-side batches and optimize throughput at the cost of per-operation latency.

#### Bulk with concurrent tasks
**While** bulk execution is enabled, **when** hundreds of `CreateItemAsync` calls are made concurrently via `Task.WhenAll`, the SDK shall internally batch these into optimal groups by partition key and send each batch as a single server request.

#### Bulk retry behavior
**If** a bulk operation encounters a 429 (Throttled) response, **then** the SDK shall use `BulkExecutionRetryPolicy` for retry logic and respect `MaxRetryAttemptsOnRateLimitedRequests` and `MaxRetryWaitTimeOnRateLimitedRequests`.

### Requirement: Batch Optimistic Concurrency
The SDK SHALL support ETag-based concurrency within batch operations.

#### Conditional replace in batch
**If** the item's ETag does not match when executing a batch with `batch.ReplaceItem(id, item, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag })`, **then** the SDK shall fail the entire batch with a precondition failure.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Batch/TransactionalBatch.cs` — public batch API
- `Microsoft.Azure.Cosmos/src/Batch/TransactionalBatchResponse.cs` — batch response
- `Microsoft.Azure.Cosmos/src/Batch/BatchAsyncContainerExecutor.cs` — bulk execution orchestrator
- `Microsoft.Azure.Cosmos/src/Batch/ItemBatchOperation.cs` — individual operation representation
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — `AllowBulkExecution` property
