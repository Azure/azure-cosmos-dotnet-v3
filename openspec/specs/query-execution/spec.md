# Query Execution

## Purpose

SQL and LINQ query execution against Azure Cosmos DB containers, including cross-partition queries, pagination via FeedIterator, and query optimization features.

## Requirements

### Requirement: SQL Query Execution
The SDK SHALL execute parameterized SQL queries against containers and return results via FeedIterator.

#### Scenario: Single-partition query
- GIVEN a container with items and a query targeting a single partition key
- WHEN `Container.GetItemQueryIterator<T>(queryDefinition, requestOptions: new QueryRequestOptions { PartitionKey = pk })` is called
- THEN results are fetched from a single partition
- AND returned via `FeedIterator<T>` with pagination support

#### Scenario: Cross-partition query
- GIVEN a query without a partition key filter
- WHEN `Container.GetItemQueryIterator<T>(queryDefinition)` is called
- THEN the SDK SHALL fan out the query to all partitions
- AND merge results according to the query's ordering requirements

#### Scenario: Parameterized query
- GIVEN a `QueryDefinition` with parameters
- WHEN `new QueryDefinition("SELECT * FROM c WHERE c.status = @status").WithParameter("@status", "active")` is used
- THEN parameters are safely bound to prevent injection

### Requirement: FeedIterator Pagination
The SDK SHALL provide page-by-page iteration over query results with continuation token support.

#### Scenario: Iterating all pages
- GIVEN a FeedIterator with `HasMoreResults = true`
- WHEN `ReadNextAsync()` is called repeatedly
- THEN each call returns a `FeedResponse<T>` containing a page of items
- AND `HasMoreResults` becomes false when all results are consumed

#### Scenario: Resume with continuation token
- GIVEN a `FeedResponse<T>` with a non-null `ContinuationToken`
- WHEN a new `GetItemQueryIterator<T>` is created with that continuation token
- THEN iteration resumes from where it left off

#### Scenario: Page size control
- GIVEN `QueryRequestOptions.MaxItemCount` is set to N
- WHEN `ReadNextAsync()` is called
- THEN each page contains at most N items (may contain fewer)

### Requirement: Stream-Based Query
The SDK SHALL support stream-based query execution for zero-copy scenarios.

#### Scenario: Stream query
- GIVEN a query
- WHEN `Container.GetItemQueryStreamIterator(queryDefinition)` is called
- THEN results are returned as `ResponseMessage` with raw JSON stream
- AND no deserialization is performed by the SDK

### Requirement: LINQ Query Support
The SDK SHALL support LINQ-to-SQL translation for type-safe queries.

#### Scenario: LINQ query
- GIVEN a container with typed items
- WHEN `Container.GetItemLinqQueryable<T>().Where(x => x.Status == "active").ToFeedIterator()` is called
- THEN the LINQ expression is translated to a Cosmos SQL query
- AND results are returned via `FeedIterator<T>`

### Requirement: Query Configuration
The SDK SHALL support query-level configuration via QueryRequestOptions.

#### Scenario: Max concurrency
- GIVEN `QueryRequestOptions.MaxConcurrency` is set to N
- WHEN a cross-partition query is executed
- THEN at most N partitions are queried concurrently

#### Scenario: Index metrics
- GIVEN `QueryRequestOptions.PopulateIndexMetrics = true`
- WHEN a query is executed
- THEN `FeedResponse<T>.Headers["x-ms-cosmos-index-utilization"]` contains index usage statistics

#### Scenario: Consistency level override
- GIVEN `QueryRequestOptions.ConsistencyLevel` is set
- WHEN a query is executed
- THEN the specified consistency level is used for that query only

### Requirement: ReadMany Optimization
The SDK SHALL provide an optimized multi-item read for known (id, partitionKey) pairs.

#### Scenario: Batch point reads
- GIVEN a list of `(id, partitionKey)` tuples
- WHEN `Container.ReadManyItemsAsync<T>(items)` is called
- THEN all items are returned in a single `FeedResponse<T>`
- AND the operation is optimized as a single server roundtrip where possible

### Requirement: Query Metrics
The SDK SHALL expose server-side query metrics when available.

#### Scenario: Retrieve query metrics
- GIVEN a completed query page
- WHEN `FeedResponse<T>.Diagnostics.GetQueryMetrics()` is called
- THEN `ServerSideCumulativeMetrics` is returned with RU charge, execution time, document count, and index hit metrics

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Query/v3Query/QueryDefinition.cs` — query definition with parameters
- `Microsoft.Azure.Cosmos/src/Query/v3Query/QueryIterator.cs` — query iterator implementation
- `Microsoft.Azure.Cosmos/src/Resource/FeedIterators/FeedIterator.cs` — abstract FeedIterator
- `Microsoft.Azure.Cosmos/src/Linq/` — LINQ-to-SQL translation
- `Microsoft.Azure.Cosmos/src/RequestOptions/QueryRequestOptions.cs` — query configuration
