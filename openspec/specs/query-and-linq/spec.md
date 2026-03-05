# Query and LINQ

## Purpose

The Azure Cosmos DB .NET SDK provides SQL query execution and LINQ-to-SQL translation for reading items from containers. Queries return results through the `FeedIterator` pattern, which supports asynchronous pagination with continuation tokens. The SDK handles cross-partition fan-out, query plan generation, and distributed execution transparently.

## Public API Surface

### Container Query Methods

| Method | Parameters | Returns | Purpose |
|--------|-----------|---------|---------|
| `GetItemQueryIterator<T>` | `QueryDefinition`, continuation, options | `FeedIterator<T>` | Typed query with deserialization |
| `GetItemQueryIterator<T>` | `string queryText`, continuation, options | `FeedIterator<T>` | Typed query with inline SQL |
| `GetItemQueryStreamIterator` | `QueryDefinition`, continuation, options | `FeedIterator` | Stream query; raw JSON response |
| `GetItemQueryStreamIterator` | `string queryText`, continuation, options | `FeedIterator` | Stream query with inline SQL |
| `GetItemQueryIterator<T>` | `FeedRange`, `QueryDefinition`, continuation, options | `FeedIterator<T>` | Partition-scoped typed query |
| `GetItemQueryStreamIterator` | `FeedRange`, `QueryDefinition`, continuation, options | `FeedIterator` | Partition-scoped stream query |
| `GetItemLinqQueryable<T>` | `allowSynchronousQueryExecution`, continuation, options, linqSerializerOptions | `IOrderedQueryable<T>` | LINQ provider entry point |
| `GetFeedRangesAsync` | `CancellationToken` | `Task<IReadOnlyList<FeedRange>>` | Get partition ranges for parallel queries |

### FeedIterator Pattern

```csharp
// Typed
public abstract class FeedIterator<T> : IDisposable
{
    public abstract bool HasMoreResults { get; }
    public abstract Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default);
}

// Stream
public abstract class FeedIterator : IDisposable
{
    public abstract bool HasMoreResults { get; }
    public abstract Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default);
}
```

### QueryDefinition

```csharp
public class QueryDefinition
{
    public string QueryText { get; }
    public QueryDefinition WithParameter(string name, object value);      // Replaces if exists
    public QueryDefinition WithParameterStream(string name, Stream value); // For pre-serialized values
    public IReadOnlyList<(string Name, object Value)> GetQueryParameters();
}
```

## Behavioral Invariants

### FeedIterator Lifecycle

1. `HasMoreResults` is `true` initially and remains `true` until the server returns a `null` continuation token or a 304 Not Modified status.
2. Once `HasMoreResults` is `false`, the query is exhausted. No more pages exist.
3. `HasMoreResults` remains `true` after a `ReadNextAsync()` that throws an exception. The caller decides whether to retry.
4. `FeedIterator` implements `IDisposable` and MUST be disposed to avoid resource leaks. Use `using` statements.
5. Pages may be empty (0 items) with a non-null continuation token. This is valid behavior — the caller must continue iterating.

### Parameterized Queries

1. Parameters are name-indexed (e.g., `@status`). Calling `WithParameter` with an existing name replaces the previous value.
2. Parameter values are never parsed as SQL — they prevent SQL injection by design.
3. Supported types: primitives, objects, arrays, and `Stream` (via `WithParameterStream`).

### Cross-Partition vs Single-Partition

1. When `QueryRequestOptions.PartitionKey` is set, the query targets a single partition (fast, cheaper RU cost).
2. When `PartitionKey` is `null`, the SDK executes a cross-partition query (fan-out to all physical partitions, merged results).
3. A `null` `QueryDefinition` or `queryText` is treated as a read feed — returns all items with no WHERE clause.
4. Cross-partition queries have no implicit ordering across partitions. `ORDER BY` requires server-side sorting which may increase RU cost.

### FeedRange-Based Parallelism

1. `GetFeedRangesAsync()` returns one `FeedRange` per physical partition. Ranges are mutually exclusive (no item duplication).
2. Each `FeedRange` can be queried independently in parallel via `GetItemQueryIterator<T>(feedRange, ...)`.
3. Continuation tokens are FeedRange-specific and not interchangeable between ranges.
4. If a physical partition splits during iteration, the SDK handles it transparently.

### Continuation Tokens

1. Continuation tokens are opaque — never parse or construct them manually.
2. Tokens are version-bound and container-bound. A token from one container or SDK version is invalid for another.
3. Tokens can be persisted within a session for resumption: pass a saved token to `GetItemQueryIterator(queryDef, continuationToken: savedToken)`.
4. `QueryRequestOptions.ResponseContinuationTokenLimitInKb` controls maximum token size.
5. `QueryRequestOptions` are copied at iterator creation time. Modifying options after creation has no effect.

### LINQ Provider

1. `GetItemLinqQueryable<T>()` returns an `IOrderedQueryable<T>` backed by `CosmosLinqQuery<T>`.
2. **Supported operators**: `Where`, `Select`, `OrderBy`, `OrderByDescending`, `ThenBy`, `Take`, `Skip`, `Distinct`, `Count`, `Sum`, `Average`, `Min`, `Max`, `Join`, `GroupJoin`, `OfType<T>`.
3. LINQ expressions are lazily built — no execution occurs until materialization.
4. **Async execution (recommended)**: Call `.ToFeedIterator()` to get a `FeedIterator<T>`, then iterate with `ReadNextAsync()`.
5. **Synchronous execution**: Only available when `allowSynchronousQueryExecution=true`. Calling `.ToList()` or enumerating directly blocks the thread.
6. Non-translatable expressions (custom methods, `ToString()`, etc.) fail at query time with an exception, not at expression-build time.

## Configuration

### QueryRequestOptions

| Property | Type | Effect |
|----------|------|--------|
| `MaxItemCount` | `int?` | Page size hint; -1 = dynamic; 0 is invalid |
| `MaxConcurrency` | `int?` | Parallelism for cross-partition; -1 = auto |
| `MaxBufferedItemCount` | `int?` | Client-side buffer during parallel execution |
| `PartitionKey` | `PartitionKey?` | Single-partition routing (null = cross-partition) |
| `EnableScanInQuery` | `bool?` | Allow scans when indexes don't cover query |
| `EnableOptimisticDirectExecution` | `bool` | Try direct execution before query plan |
| `PopulateIndexMetrics` | `bool?` | Return index usage stats |
| `ConsistencyLevel` | `ConsistencyLevel?` | Override account default |
| `SessionToken` | `string` | Session consistency token |
| `ResponseContinuationTokenLimitInKb` | `int?` | Max continuation token size |

## Interactions

- **Handler Pipeline**: Query requests flow through the full handler pipeline. For cross-partition queries, `PartitionKeyRangeHandler` distributes across partitions. See `handler-pipeline` spec.
- **Partition Keys**: Single-partition queries require a partition key in `QueryRequestOptions`. See `partition-keys` spec.
- **Serialization**: `FeedIterator<T>` uses the container's serializer for deserialization. See `serialization` spec.
- **Retry**: Query page fetches are retried per `retry-and-failover` spec policies.

## References

- Source: `Microsoft.Azure.Cosmos/src/Resource/Container/Container.cs`
- Source: `Microsoft.Azure.Cosmos/src/Query/v3Query/QueryDefinition.cs`
- Source: `Microsoft.Azure.Cosmos/src/Resource/FeedIterators/FeedIterator.cs`
- Source: `Microsoft.Azure.Cosmos/src/Linq/CosmosLinqQuery.cs`
- Source: `Microsoft.Azure.Cosmos/src/RequestOptions/QueryRequestOptions.cs`
