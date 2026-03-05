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

## Behavioral Invariants

### TransactionalBatch Atomicity

1. **All-or-nothing**: If ANY operation fails, the ENTIRE batch is rolled back. Zero operations are committed.
2. **Same partition key**: All items in a batch MUST share the same partition key (specified at `CreateTransactionalBatch`).
3. **Ordered execution**: Operations execute in submission order (`x-ms-cosmos-batch-ordered: true`).
4. **No exceptions on failure**: `ExecuteAsync` does NOT throw exceptions for batch failures. Check `response.IsSuccessStatusCode`.
5. **Failed dependency**: When one operation causes the batch to fail, subsequent operations return status code 424 (Failed Dependency). The failing operation returns the actual error code.

### TransactionalBatch Limits

| Limit | Value |
|-------|-------|
| Max operations per batch | 100 (server-enforced) |
| Max payload size | 2 MB (server-enforced) |

### TransactionalBatchResponse

```csharp
response.IsSuccessStatusCode   // true if all operations succeeded
response.StatusCode            // Overall status (200 = success, or first error code)
response.RequestCharge         // Total RU consumption
response[0].StatusCode         // Per-operation status
response[0].ETag               // Per-operation ETag
response.GetOperationResultAtIndex<T>(0).Resource  // Deserialized result
```

- Implements `IReadOnlyList<TransactionalBatchOperationResult>` and `IDisposable`.
- If response is 207 (Multi-Status), the status is promoted to the first failing operation's error code.

### Per-Operation Request Options

| Option | Type | Effect |
|--------|------|--------|
| `IfMatchEtag` | `string` | Conditional operation (412 on mismatch) |
| `IfNoneMatchEtag` | `string` | Conditional read |
| `EnableContentResponseOnWrite` | `bool?` | Skip response payload |
| `IndexingDirective` | `IndexingDirective?` | Include/Exclude indexing |

### Bulk Execution (`AllowBulkExecution`)

1. **Automatic batching**: The SDK groups individual item operations by partition key range and sends them as server batches.
2. **Throughput over latency**: Designed for high-volume scenarios (thousands of operations/second). Individual operation latency may increase due to batching overhead.
3. **Not atomic**: Unlike `TransactionalBatch`, individual operations in bulk execution are independent. Some may succeed while others fail.
4. **Per-partition-key-range streaming**: Each partition key range has its own `BatchAsyncStreamer` that accumulates operations and flushes when full or when a timer expires.
5. **Individual options respected**: Each operation's `ItemRequestOptions` (ETags, consistency, etc.) are honored within the batch.

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
