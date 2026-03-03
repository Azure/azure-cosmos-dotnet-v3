# Partitioning

## Purpose

Partitioning is the fundamental data distribution mechanism in Azure Cosmos DB. The SDK handles partition key routing, hierarchical partition keys, and feed ranges for parallel processing.

## Requirements

### Requirement: Partition Key Specification
The SDK SHALL support specifying partition keys for all item-level operations.

#### Scenario: Single partition key
- GIVEN a container with a partition key path `/tenantId`
- WHEN `new PartitionKey("tenant-1")` is provided to an operation
- THEN the request is routed to the correct physical partition

#### Scenario: Partition key from item
- GIVEN an item with the partition key property populated
- WHEN `Container.CreateItemAsync(item)` is called without an explicit partition key
- THEN the SDK extracts the partition key from the item using the container's partition key definition

#### Scenario: None partition key
- GIVEN an item that does not have a partition key property (or it is null)
- WHEN `PartitionKey.None` is used
- THEN the item is stored in the system-defined "none" partition

### Requirement: Hierarchical Partition Keys
The SDK SHALL support hierarchical (multi-level) partition keys.

#### Scenario: Build hierarchical key
- GIVEN a container with partition key paths `["/tenantId", "/userId", "/sessionId"]`
- WHEN `new PartitionKeyBuilder().Add("tenant-1").Add("user-1").Add("session-1").Build()` is called
- THEN a hierarchical partition key is created for routing

#### Scenario: Partial partition key for queries
- GIVEN a hierarchical partition key container
- WHEN a query is executed with only the first level partition key (e.g., tenantId only)
- THEN the query fans out to all sub-partitions under that prefix

#### Scenario: Full hierarchical key for point operations
- GIVEN a hierarchical partition key container
- WHEN a point operation (Read, Replace, Delete) is performed
- THEN the full hierarchical key MUST be provided

### Requirement: Feed Ranges
The SDK SHALL support FeedRange for parallel processing across partitions.

#### Scenario: Get all feed ranges
- GIVEN a container
- WHEN `Container.GetFeedRangesAsync()` is called
- THEN a list of `FeedRange` objects is returned, each representing a partition key range

#### Scenario: Use feed range for change feed
- GIVEN a `FeedRange` from `GetFeedRangesAsync()`
- WHEN `ChangeFeedStartFrom.Beginning(feedRange)` is used
- THEN only changes within that partition range are returned

#### Scenario: Use feed range for query
- GIVEN a `FeedRange`
- WHEN a query is executed with `QueryRequestOptions.FeedRange` set
- THEN the query only executes against the specified partition range

#### Scenario: FeedRange from partition key
- GIVEN a partition key value
- WHEN `FeedRange.FromPartitionKey(partitionKey)` is called
- THEN a `FeedRange` scoped to that single logical partition is returned

### Requirement: Partition Key Range Caching
The SDK SHALL cache partition key range information to avoid repeated metadata lookups.

#### Scenario: Cache hit
- GIVEN a partition key range has been resolved previously
- WHEN a new request targets the same partition
- THEN the cached routing information is used without a metadata call

#### Scenario: Cache invalidation on 410 Gone
- GIVEN the service returns 410 (Gone) with sub-status 1002 (PartitionKeyRangeGone)
- WHEN the SDK handles this response
- THEN the partition key range cache is invalidated
- AND the operation is retried with refreshed routing

#### Scenario: Partition split
- GIVEN a physical partition is split by the service
- WHEN a request targets the old partition range
- THEN the SDK receives a 410 response
- AND refreshes the routing map to discover the new partition ranges
- AND retries the operation against the correct new partition

### Requirement: Cross-Partition Operations
The SDK SHALL support operations that span multiple partitions.

#### Scenario: Cross-partition query
- GIVEN a query without a partition key filter
- WHEN the query is executed
- THEN the SDK fans out the query to all partitions
- AND merges results according to the query's ORDER BY requirements

#### Scenario: Change feed across all partitions
- GIVEN `ChangeFeedStartFrom.Beginning()` without a FeedRange
- WHEN the change feed iterator is used
- THEN changes from all partitions are returned

## Key Source Files
- `Microsoft.Azure.Cosmos/src/PartitionKey.cs` — partition key value type
- `Microsoft.Azure.Cosmos/src/PartitionKeyBuilder.cs` — hierarchical partition key builder
- `Microsoft.Azure.Cosmos/src/FeedRange/FeedRange.cs` — feed range abstraction
- `Microsoft.Azure.Cosmos/src/Routing/PartitionKeyRangeCache.cs` — range caching
- `Microsoft.Azure.Cosmos/src/Routing/CollectionRoutingMap.cs` — partition routing
- `Microsoft.Azure.Cosmos/src/Routing/PartitionRoutingHelper.cs` — routing utilities
