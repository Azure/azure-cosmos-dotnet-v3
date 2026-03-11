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
    public QueryDefinition WithParameter(string name, object value);
    public QueryDefinition WithParameterStream(string name, Stream value);
    public IReadOnlyList<(string Name, object Value)> GetQueryParameters();
}
```

## Requirements

### Requirement: FeedIterator Lifecycle

The SDK SHALL manage query results through the FeedIterator pattern with specific lifecycle guarantees.

#### HasMoreResults initial state

**When** a FeedIterator is created, `HasMoreResults` SHALL be `true` and SHALL remain `true` until the server returns a `null` continuation token or 304 Not Modified.

#### Exhaustion semantics

**When** `HasMoreResults` becomes `false`, the query SHALL be exhausted with no more pages available.

#### Exception resilience

**When** `ReadNextAsync()` throws an exception, `HasMoreResults` SHALL remain `true`. The caller SHALL decide whether to retry.

#### Disposal requirement

**When** a FeedIterator is no longer needed, it SHALL be disposed to avoid resource leaks. The SDK SHALL implement `IDisposable`.

#### Empty pages

**When** a page is returned with 0 items but a non-null continuation token, the SDK SHALL treat this as valid behavior. The caller SHALL continue iterating.

### Requirement: Parameterized Queries

The SDK SHALL support parameterized queries for safe SQL execution.

#### Parameter replacement

**When** `WithParameter` is called with an existing parameter name, the SDK SHALL replace the previous value.

#### SQL injection prevention

**When** parameter values are provided, the SDK SHALL NOT parse them as SQL. Parameters SHALL prevent SQL injection by design.

#### Supported parameter types

**When** parameters are added, the SDK SHALL support primitives, objects, arrays, and `Stream` (via `WithParameterStream`).

### Requirement: Cross-Partition vs Single-Partition Queries

The SDK SHALL support both single-partition and cross-partition query execution.

#### Single-partition routing

**Where** `QueryRequestOptions.PartitionKey` is set, **when** a query is executed, the SDK SHALL target a single partition for faster execution and lower RU cost.

#### Cross-partition fan-out

**Where** `QueryRequestOptions.PartitionKey` is null, **when** a query is executed, the SDK SHALL execute a cross-partition query, fanning out to all physical partitions and merging results.

#### Null query as read feed

**When** `QueryDefinition` or `queryText` is `null`, the SDK SHALL treat this as a read feed, returning all items with no WHERE clause.

#### Cross-partition ordering

**When** a cross-partition query uses `ORDER BY`, the SDK SHALL perform server-side sorting, which MAY increase RU cost. No implicit ordering SHALL be guaranteed across partitions.

### Requirement: FeedRange-Based Parallelism

The SDK SHALL support partition-scoped parallel queries via FeedRange.

#### FeedRange per physical partition

**When** `GetFeedRangesAsync()` is called, the SDK SHALL return one `FeedRange` per physical partition. Ranges SHALL be mutually exclusive.

#### Independent parallel queries

**When** a query is scoped to a `FeedRange`, the SDK SHALL execute it independently against that partition.

#### Range-specific continuation tokens

**When** continuation tokens are generated for FeedRange queries, they SHALL be range-specific and not interchangeable between ranges.

#### Transparent split handling

**When** a physical partition splits during iteration, the SDK SHALL handle it transparently.

### Requirement: Continuation Tokens

The SDK SHALL manage opaque, version-bound continuation tokens for query resumption.

#### Opaque tokens

**When** a continuation token is returned, callers SHALL NOT parse or construct tokens manually. The SDK SHALL treat them as opaque.

#### Version and container binding

**When** a continuation token is used, the SDK SHALL validate it is compatible with the current container and SDK version. Tokens from different containers or SDK versions SHALL be invalid.

#### Token size control

**Where** `QueryRequestOptions.ResponseContinuationTokenLimitInKb` is set, the SDK SHALL limit the maximum token size accordingly.

#### Options snapshot at creation

**When** a FeedIterator is created, `QueryRequestOptions` SHALL be copied at creation time. Modifying options after creation SHALL have no effect.

### Requirement: LINQ Provider

The SDK SHALL support LINQ-to-SQL translation for type-safe queries.

#### Queryable interface

**When** `GetItemLinqQueryable<T>()` is called, the SDK SHALL return an `IOrderedQueryable<T>` backed by `CosmosLinqQuery<T>`.

#### Supported operators

**When** LINQ expressions are built, the SDK SHALL support: `Where`, `Select`, `OrderBy`, `OrderByDescending`, `ThenBy`, `Take`, `Skip`, `Distinct`, `Count`, `Sum`, `Average`, `Min`, `Max`, `Join`, `GroupJoin`, `OfType<T>`.

#### Lazy expression building

**When** LINQ expressions are composed, the SDK SHALL NOT execute them until materialization (e.g., `ToFeedIterator()` or enumeration).

#### Async execution (recommended)

**When** executing LINQ queries, callers SHOULD call `.ToFeedIterator()` to get a `FeedIterator<T>` and iterate with `ReadNextAsync()`.

#### Synchronous execution

**Where** `allowSynchronousQueryExecution=true`, **when** `.ToList()` or direct enumeration is used, the SDK SHALL execute the query synchronously, blocking the calling thread.

#### Non-translatable expressions

**When** a LINQ expression contains non-translatable methods (custom methods, `ToString()`, etc.), the SDK SHALL fail at query execution time, not at expression-build time.

## Configuration

### QueryRequestOptions

| Property | Type | Effect |
|----------|------|--------|
| `MaxItemCount` | `int?` | Page size hint; -1 = dynamic; 0 is invalid |
| `MaxConcurrency` | `int?` | Parallelism for cross-partition; -1 = auto |
| `MaxBufferedItemCount` | `int?` | Client-side buffer during parallel execution |
| `PartitionKey` | `PartitionKey?` | Single-partition routing (null = cross-partition) |
| `EnableScanInQuery` | `bool?` | Allow scans when indexes do not cover query |
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