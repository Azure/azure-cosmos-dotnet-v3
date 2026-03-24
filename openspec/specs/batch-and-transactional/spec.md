# Batch and Transactional Operations

## Purpose

The Azure Cosmos DB .NET SDK provides two batching mechanisms: `TransactionalBatch` for atomic multi-operation transactions within a single partition key, and `AllowBulkExecution` for automatic throughput-optimized batching of individual operations. TransactionalBatch guarantees all-or-nothing semantics; bulk execution optimizes throughput at the cost of latency.

## Public API Surface

### TransactionalBatch (Atomic)

```csharp
TransactionalBatch batch = container.CreateTransactionalBatch(new PartitionKey("pk-value"))
    .CreateItem(item1)
    .CreateItem(item2)
    .ReplaceItem("id3", updatedItem)
    .DeleteItem("id4")
    .ReadItem("id5");

using TransactionalBatchResponse response = await batch.ExecuteAsync();
```

### Batch Operations

| Method | Parameters | Purpose |
|--------|-----------|---------|
| `CreateItem<T>` | `T item, TransactionalBatchItemRequestOptions` | Add create operation |
| `CreateItemStream` | `Stream payload, TransactionalBatchItemRequestOptions` | Create (stream) |
| `ReadItem` | `string id, TransactionalBatchItemRequestOptions` | Add read operation |
| `ReplaceItem<T>` | `string id, T item, TransactionalBatchItemRequestOptions` | Add replace |
| `ReplaceItemStream` | `string id, Stream payload, TransactionalBatchItemRequestOptions` | Replace (stream) |
| `UpsertItem<T>` | `T item, TransactionalBatchItemRequestOptions` | Add upsert |
| `UpsertItemStream` | `Stream payload, TransactionalBatchItemRequestOptions` | Upsert (stream) |
| `DeleteItem` | `string id, TransactionalBatchItemRequestOptions` | Add delete |
| `PatchItem` | `string id, IReadOnlyList<PatchOperation>, TransactionalBatchPatchItemRequestOptions` | Add patch |

### Bulk Execution

```csharp
CosmosClientOptions options = new CosmosClientOptions { AllowBulkExecution = true };
CosmosClient client = new CosmosClient("connection-string", options);

// Individual operations are automatically batched by the SDK
List<Task> tasks = items.Select(item =>
    container.CreateItemAsync(item, new PartitionKey(item.Pk))).ToList();
await Task.WhenAll(tasks);
```

## Requirements

### Requirement: TransactionalBatch Atomicity

The SDK SHALL execute all operations in a TransactionalBatch atomically.

#### All-or-nothing execution

**When** `TransactionalBatch.ExecuteAsync()` is called, **if** ANY operation fails, the SDK SHALL roll back the ENTIRE batch. Zero operations SHALL be committed.

#### Same partition key constraint

**When** creating a TransactionalBatch, all items in the batch SHALL share the same partition key (specified at `CreateTransactionalBatch`).

#### Ordered execution

**When** a TransactionalBatch is executed, the SDK SHALL execute operations in submission order (`x-ms-cosmos-batch-ordered: true`).

#### No exceptions on failure

**When** a TransactionalBatch fails, `ExecuteAsync` SHALL NOT throw exceptions. The caller SHALL check `response.IsSuccessStatusCode` to determine success or failure.

#### Failed dependency status

**When** one operation causes the batch to fail, subsequent operations SHALL return status code 424 (Failed Dependency). The failing operation SHALL return the actual error code.

### Requirement: TransactionalBatch Limits

The SDK SHALL enforce server-side limits on batch operations.

| Limit | Value |
|-------|-------|
| Max operations per batch | 100 (server-enforced) |
| Max payload size | 2 MB (server-enforced) |

### Requirement: TransactionalBatch Response

The SDK SHALL provide per-operation results in the batch response.

#### Response structure

**When** a TransactionalBatch completes, the response SHALL implement `IReadOnlyList<TransactionalBatchOperationResult>` and `IDisposable`, providing per-operation `StatusCode`, `ETag`, and typed results via `GetOperationResultAtIndex<T>`.

#### Multi-Status promotion

**If** the response is 207 (Multi-Status), the SDK SHALL promote the status to the first failing operation's error code.

### Requirement: Per-Operation Request Options

The SDK SHALL support per-operation configuration within a batch.

| Option | Type | Effect |
|--------|------|--------|
| `IfMatchEtag` | `string` | Conditional operation (412 on mismatch) |
| `IfNoneMatchEtag` | `string` | Conditional read |
| `EnableContentResponseOnWrite` | `bool?` | Skip response payload |
| `IndexingDirective` | `IndexingDirective?` | Include/Exclude indexing |

### Requirement: Bulk Execution

The SDK SHALL support automatic throughput-optimized batching via `AllowBulkExecution`.

#### Automatic batching

**Where** `CosmosClientOptions.AllowBulkExecution = true`, **when** individual item operations are called, the SDK SHALL automatically group operations by partition key range and send them as server batches.

#### Non-atomic execution

**While** bulk execution is enabled, individual operations SHALL be independent. Some MAY succeed while others fail (unlike TransactionalBatch).

#### Per-partition-key-range streaming

**While** bulk execution is enabled, each partition key range SHALL have its own `BatchAsyncStreamer` that accumulates operations and flushes when full or when a timer expires.

#### Individual options respected

**While** bulk execution is enabled, each operation's `ItemRequestOptions` (ETags, consistency, etc.) SHALL be honored within the batch.

### TransactionalBatch vs Bulk Execution

| Aspect | TransactionalBatch | Bulk Execution |
|--------|-------------------|----------------|
| Atomicity | All-or-nothing | Independent operations |
| Max operations | 100 | Unlimited (auto-batched) |
| Partition key | All same PK | Any/multiple PKs |
| Latency | Low (single round-trip) | Higher (batching delay) |
| Throughput | Single request | Optimized (parallel batches) |
| API | Explicit builder pattern | Implicit (normal CRUD calls) |
| Ordering | Ordered execution | Unordered |
| Configuration | Per-operation | `CosmosClientOptions.AllowBulkExecution = true` |

## Interactions

- **Partition Keys**: All TransactionalBatch items must share the same partition key. See `partition-keys` spec.
- **CRUD Operations**: Batch uses the same operation semantics as individual CRUD. See `crud-operations` spec.
- **Retry Policies**: Batch requests are retried at the batch level (not per-operation). See `retry-and-failover` spec.
- **Serialization**: Typed batch operations use the container's serializer. Internal wire format uses HybridRow binary serialization.

## References

- Source: `Microsoft.Azure.Cosmos/src/Batch/TransactionalBatch.cs`
- Source: `Microsoft.Azure.Cosmos/src/Batch/BatchCore.cs`
- Source: `Microsoft.Azure.Cosmos/src/Batch/BatchAsyncContainerExecutor.cs`
- Source: `Microsoft.Azure.Cosmos/src/Batch/TransactionalBatchResponse.cs`