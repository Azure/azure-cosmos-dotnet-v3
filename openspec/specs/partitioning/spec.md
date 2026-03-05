# Partitioning

## Purpose

Partitioning is the fundamental data distribution mechanism in Azure Cosmos DB. The SDK handles partition key routing, hierarchical partition keys, and feed ranges for parallel processing.

## Requirements

### Requirement: Partition Key Specification
The SDK SHALL support specifying partition keys for all item-level operations.

#### Single partition key
**When** `new PartitionKey("tenant-1")` is provided to an operation on a container with partition key path `/tenantId`, the SDK shall route the request to the correct physical partition.

#### Partition key from item
**When** `Container.CreateItemAsync(item)` is called without an explicit partition key, the SDK shall extract the partition key from the item using the container's partition key definition.

#### None partition key
**When** `PartitionKey.None` is used for an item that does not have a partition key property (or it is null), the SDK shall store the item in the system-defined "none" partition.

### Requirement: Hierarchical Partition Keys
The SDK SHALL support hierarchical (multi-level) partition keys.

#### Build hierarchical key
**When** `new PartitionKeyBuilder().Add("tenant-1").Add("user-1").Add("session-1").Build()` is called for a container with partition key paths `["/tenantId", "/userId", "/sessionId"]`, the SDK shall create a hierarchical partition key for routing.

#### Partial partition key for queries
**While** using a hierarchical partition key container, **when** a query is executed with only the first level partition key (e.g., tenantId only), the SDK shall fan out the query to all sub-partitions under that prefix.

#### Full hierarchical key for point operations
**While** using a hierarchical partition key container, **when** a point operation (Read, Replace, Delete) is performed, the full hierarchical key MUST be provided.

### Requirement: Feed Ranges
The SDK SHALL support FeedRange for parallel processing across partitions.

#### Get all feed ranges
**When** `Container.GetFeedRangesAsync()` is called, the SDK shall return a list of `FeedRange` objects, each representing a partition key range.

#### Use feed range for change feed
**When** `ChangeFeedStartFrom.Beginning(feedRange)` is used with a `FeedRange` from `GetFeedRangesAsync()`, the SDK shall return only changes within that partition range.

#### Use feed range for query
**When** a query is executed with `QueryRequestOptions.FeedRange` set to a specific `FeedRange`, the SDK shall execute the query only against the specified partition range.

#### FeedRange from partition key
**When** `FeedRange.FromPartitionKey(partitionKey)` is called, the SDK shall return a `FeedRange` scoped to that single logical partition.

### Requirement: Partition Key Range Caching
The SDK SHALL cache partition key range information to avoid repeated metadata lookups.

#### Cache hit
**While** a partition key range has been resolved previously, **when** a new request targets the same partition, the SDK shall use the cached routing information without a metadata call.

#### Cache invalidation on 410 Gone
**If** the service returns 410 (Gone) with sub-status 1002 (PartitionKeyRangeGone), **then** the SDK shall invalidate the partition key range cache and retry the operation with refreshed routing.

#### Partition split
**If** a physical partition is split by the service and a request targets the old partition range, **then** the SDK shall handle the 410 response, refresh the routing map to discover the new partition ranges, and retry the operation against the correct new partition.

### Requirement: Cross-Partition Operations
The SDK SHALL support operations that span multiple partitions.

#### Cross-partition query
**When** a query without a partition key filter is executed, the SDK shall fan out the query to all partitions and merge results according to the query's ORDER BY requirements.

#### Change feed across all partitions
**When** `ChangeFeedStartFrom.Beginning()` is used without a FeedRange, the SDK shall return changes from all partitions via the change feed iterator.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/PartitionKey.cs` — partition key value type
- `Microsoft.Azure.Cosmos/src/PartitionKeyBuilder.cs` — hierarchical partition key builder
- `Microsoft.Azure.Cosmos/src/FeedRange/FeedRange.cs` — feed range abstraction
- `Microsoft.Azure.Cosmos/src/Routing/PartitionKeyRangeCache.cs` — range caching
- `Microsoft.Azure.Cosmos/src/Routing/CollectionRoutingMap.cs` — partition routing
- `Microsoft.Azure.Cosmos/src/Routing/PartitionRoutingHelper.cs` — routing utilities
