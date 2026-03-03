# Bulk and Batch Operations

## Purpose

Transactional batch provides atomic multi-operation execution within a single partition key. Bulk execution mode optimizes throughput for high-volume ingestion and processing scenarios.

## Requirements

### Requirement: Transactional Batch
The SDK SHALL execute multiple operations atomically within a single partition key scope.

#### Scenario: Successful batch execution
- GIVEN a transactional batch with multiple operations on the same partition key
- WHEN `Container.CreateTransactionalBatch(partitionKey).CreateItem(a).UpsertItem(b).DeleteItem(cId).ExecuteAsync()` is called
- THEN all operations succeed atomically
- AND a `TransactionalBatchResponse` is returned with overall status code 200
- AND individual operation results are accessible by index

#### Scenario: Batch failure (atomicity)
- GIVEN a batch where one operation would fail (e.g., creating a duplicate)
- WHEN `ExecuteAsync()` is called
- THEN ALL operations in the batch are rolled back
- AND no partial state is persisted
- AND the response status code indicates the failure

#### Scenario: Mixed operation types
- GIVEN a batch containing Create, Replace, Upsert, Patch, Delete, and Read operations
- WHEN `ExecuteAsync()` is called
- THEN all operations execute in the order they were added

### Requirement: Batch Operation Types
The SDK SHALL support all CRUD operation types within a transactional batch.

#### Scenario: Batch create
- GIVEN `batch.CreateItem<T>(item)`
- WHEN the batch executes
- THEN the item is created if it does not exist

#### Scenario: Batch replace
- GIVEN `batch.ReplaceItem<T>(id, item)`
- WHEN the batch executes
- THEN the existing item is fully replaced

#### Scenario: Batch upsert
- GIVEN `batch.UpsertItem<T>(item)`
- WHEN the batch executes
- THEN the item is created or replaced

#### Scenario: Batch patch
- GIVEN `batch.PatchItem(id, patchOperations)`
- WHEN the batch executes
- THEN partial modifications are applied to the item

#### Scenario: Batch delete
- GIVEN `batch.DeleteItem(id)`
- WHEN the batch executes
- THEN the item is removed

#### Scenario: Batch read
- GIVEN `batch.ReadItem(id)`
- WHEN the batch executes
- THEN the item's current state is returned in the operation result

### Requirement: Batch with Stream
The SDK SHALL support stream-based batch operations for zero-copy performance.

#### Scenario: Stream-based create
- GIVEN `batch.CreateItemStream(stream)`
- WHEN the batch executes
- THEN the item is created from the raw JSON stream without deserialization

### Requirement: Batch Response Inspection
The SDK SHALL provide access to individual operation results within a batch response.

#### Scenario: Access individual results
- GIVEN a completed batch response
- WHEN `response.GetOperationResultAtIndex<T>(index)` is called
- THEN the result for that specific operation is returned with its status code, resource, and ETag

#### Scenario: Check overall success
- GIVEN a completed batch response
- WHEN `response.IsSuccessStatusCode` is checked
- THEN it reflects whether all operations succeeded

### Requirement: Batch Partition Key Constraint
The SDK SHALL enforce that all operations in a batch target the same partition key.

#### Scenario: Single partition key
- GIVEN a batch created with `CreateTransactionalBatch(partitionKey)`
- WHEN operations are added
- THEN all operations are scoped to that partition key
- AND cross-partition batches are not supported

### Requirement: Bulk Execution Mode
The SDK SHALL support a bulk execution mode that batches individual operations for higher throughput.

#### Scenario: Enable bulk mode
- GIVEN `CosmosClientOptions.AllowBulkExecution = true`
- WHEN individual item operations (Create, Upsert, Replace, Delete, Read) are issued concurrently
- THEN the SDK automatically groups operations into server-side batches
- AND throughput is optimized at the cost of per-operation latency

#### Scenario: Bulk with concurrent tasks
- GIVEN bulk execution is enabled
- WHEN hundreds of `CreateItemAsync` calls are made concurrently via `Task.WhenAll`
- THEN the SDK internally batches these into optimal groups by partition key
- AND each batch is sent as a single server request

#### Scenario: Bulk retry behavior
- GIVEN a bulk operation encounters a 429 (Throttled) response
- WHEN the throttled batch is retried
- THEN the SDK uses `BulkExecutionRetryPolicy` for retry logic
- AND respects `MaxRetryAttemptsOnRateLimitedRequests` and `MaxRetryWaitTimeOnRateLimitedRequests`

### Requirement: Batch Optimistic Concurrency
The SDK SHALL support ETag-based concurrency within batch operations.

#### Scenario: Conditional replace in batch
- GIVEN `batch.ReplaceItem(id, item, new TransactionalBatchItemRequestOptions { IfMatchEtag = etag })`
- WHEN the batch executes and the item's ETag does not match
- THEN the entire batch fails with a precondition failure

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Batch/TransactionalBatch.cs` — public batch API
- `Microsoft.Azure.Cosmos/src/Batch/TransactionalBatchResponse.cs` — batch response
- `Microsoft.Azure.Cosmos/src/Batch/BatchAsyncContainerExecutor.cs` — bulk execution orchestrator
- `Microsoft.Azure.Cosmos/src/Batch/ItemBatchOperation.cs` — individual operation representation
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — `AllowBulkExecution` property
