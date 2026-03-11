# Query Execution

## Purpose

SQL and LINQ query execution against Azure Cosmos DB containers, including cross-partition queries, pagination via FeedIterator, and query optimization features.

## Requirements

### Requirement: SQL Query Execution
The SDK SHALL execute parameterized SQL queries against containers and return results via FeedIterator.

#### Single-partition query
**When** `Container.GetItemQueryIterator<T>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = pk })` is called with a query targeting a single partition key, the SDK shall fetch results from a single partition and return them via `FeedIterator<T>` with pagination support.

#### Cross-partition query
**When** `Container.GetItemQueryIterator<T>(queryDefinition)` is called with a query without a partition key filter, the SDK shall fan out the query to all partitions and merge results according to the query's ordering requirements.

#### Parameterized query
**When** `new QueryDefinition("SELECT * FROM c WHERE c.status = @status").WithParameter("@status", "active")` is used, the SDK shall safely bind parameters to prevent injection.

### Requirement: FeedIterator Pagination
The SDK SHALL provide page-by-page iteration over query results with continuation token support.

#### Iterating all pages
**While** a FeedIterator has `HasMoreResults = true`, **when** `ReadNextAsync()` is called repeatedly, the SDK shall return a `FeedResponse<T>` containing a page of items for each call and set `HasMoreResults` to false when all results are consumed.

#### Resume with continuation token
**When** a new `GetItemQueryIterator<T>` is created with a non-null `ContinuationToken` from a previous `FeedResponse<T>`, the SDK shall resume iteration from where it left off.

#### Page size control
**Where** `QueryRequestOptions.MaxItemCount` is set to N, **when** `ReadNextAsync()` is called, the SDK shall return each page containing at most N items (may contain fewer).

### Requirement: Stream-Based Query
The SDK SHALL support stream-based query execution for zero-copy scenarios.

#### Stream query
**When** `Container.GetItemQueryStreamIterator(queryDefinition)` is called, the SDK shall return results as `ResponseMessage` with raw JSON stream and perform no deserialization.

### Requirement: LINQ Query Support
The SDK SHALL support LINQ-to-SQL translation for type-safe queries.

#### LINQ query
**When** `Container.GetItemLinqQueryable<T>().Where(x => x.Status == "active").ToFeedIterator()` is called, the SDK shall translate the LINQ expression to a Cosmos SQL query and return results via `FeedIterator<T>`.

### Requirement: Query Configuration
The SDK SHALL support query-level configuration via QueryRequestOptions.

#### Max concurrency
**Where** `QueryRequestOptions.MaxConcurrency` is set to N, **when** a cross-partition query is executed, the SDK shall query at most N partitions concurrently.

#### Index metrics
**Where** `QueryRequestOptions.PopulateIndexMetrics = true`, **when** a query is executed, the SDK shall populate `FeedResponse<T>.Headers["x-ms-cosmos-index-utilization"]` with index usage statistics.

#### Consistency level override
**Where** `QueryRequestOptions.ConsistencyLevel` is set, **when** a query is executed, the SDK shall use the specified consistency level for that query only.

### Requirement: ReadMany Optimization
The SDK SHALL provide an optimized multi-item read for known (id, partitionKey) pairs.

#### Batch point reads
**When** `Container.ReadManyItemsAsync<T>(items)` is called with a list of `(id, partitionKey)` tuples, the SDK shall return all items in a single `FeedResponse<T>` and optimize the operation as a single server roundtrip where possible.

### Requirement: Query Metrics
The SDK SHALL expose server-side query metrics when available.

#### Retrieve query metrics
**When** `FeedResponse<T>.Diagnostics.GetQueryMetrics()` is called after a completed query page, the SDK shall return `ServerSideCumulativeMetrics` with RU charge, execution time, document count, and index hit metrics.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Query/v3Query/QueryDefinition.cs` — query definition with parameters
- `Microsoft.Azure.Cosmos/src/Query/v3Query/QueryIterator.cs` — query iterator implementation
- `Microsoft.Azure.Cosmos/src/Resource/FeedIterators/FeedIterator.cs` — abstract FeedIterator
- `Microsoft.Azure.Cosmos/src/Linq/` — LINQ-to-SQL translation
- `Microsoft.Azure.Cosmos/src/RequestOptions/QueryRequestOptions.cs` — query configuration
