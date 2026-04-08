# Container and Database Management

## Purpose

The Azure Cosmos DB .NET SDK provides APIs for managing databases and containers (the two top-level resource types in the Cosmos DB resource hierarchy). Databases are units of management for containers; containers are the units of scalability for throughput and storage. This spec covers the CRUD operations for these resources and their configuration properties.

## Public API Surface

### Database Operations

| Method | Returns | Purpose |
|--------|---------|---------|
| `CosmosClient.CreateDatabaseAsync` | `DatabaseResponse` | Create a database |
| `CosmosClient.CreateDatabaseIfNotExistsAsync` | `DatabaseResponse` | Create if not exists (200 if exists, 201 if created) |
| `Database.ReadAsync` | `DatabaseResponse` | Read database properties |
| `Database.DeleteAsync` | `DatabaseResponse` | Delete database and all contents |
| `Database.ReadThroughputAsync` | `ThroughputResponse` | Read provisioned throughput |
| `Database.ReplaceThroughputAsync` | `ThroughputResponse` | Update provisioned throughput |
| `CosmosClient.GetDatabase` | `Database` | Get proxy reference (no network call) |

### Container Operations

| Method | Returns | Purpose |
|--------|---------|---------|
| `Database.CreateContainerAsync` | `ContainerResponse` | Create a container |
| `Database.CreateContainerIfNotExistsAsync` | `ContainerResponse` | Create if not exists |
| `Container.ReadContainerAsync` | `ContainerResponse` | Read container properties |
| `Container.ReplaceContainerAsync` | `ContainerResponse` | Update container settings |
| `Container.DeleteContainerAsync` | `ContainerResponse` | Delete container and all items |
| `Container.ReadThroughputAsync` | `ThroughputResponse` | Read provisioned throughput |
| `Container.ReplaceThroughputAsync` | `ThroughputResponse` | Update provisioned throughput |
| `Database.GetContainer` | `Container` | Get proxy reference (no network call) |

### ContainerProperties Key Settings

| Property | Type | Notes |
|----------|------|-------|
| `Id` | `string` | Container name |
| `PartitionKeyPath` | `string` | Single partition key path (e.g., `"/userId"`) |
| `PartitionKeyPaths` | `IReadOnlyList<string>` | Hierarchical partition key paths |
| `IndexingPolicy` | `IndexingPolicy` | Indexing configuration |
| `DefaultTimeToLive` | `int?` | TTL in seconds (-1 = no expiry, null = disabled) |
| `UniqueKeyPolicy` | `UniqueKeyPolicy` | Unique constraints |
| `ConflictResolutionPolicy` | `ConflictResolutionPolicy` | Multi-region conflict handling |
| `ChangeFeedPolicy` | `ChangeFeedPolicy` | Change feed retention (required for AllVersionsAndDeletes) |
| `ComputedProperties` | `Collection<ComputedProperty>` | Server-computed properties |

### IndexingPolicy

| Property | Type | Notes |
|----------|------|-------|
| `Automatic` | `bool` | Auto-index all properties (default: `true`) |
| `IndexingMode` | `IndexingMode` | `Consistent` (default), `Lazy`, `None` |
| `IncludedPaths` | `Collection<IncludedPath>` | Paths to index |
| `ExcludedPaths` | `Collection<ExcludedPath>` | Paths to exclude |
| `CompositeIndexes` | `Collection<Collection<CompositePath>>` | Multi-property indexes for ORDER BY |
| `SpatialIndexes` | `Collection<SpatialPath>` | Geospatial indexes |
| `VectorIndexes` | `Collection<VectorIndexPath>` | Vector similarity search indexes (Preview) |

## Requirements

### Requirement: Proxy Reference Semantics

The SDK SHALL return lightweight proxy references that do not validate resource existence.

**When** `GetDatabase()` or `GetContainer()` is called, the SDK SHALL return a reference without making network calls. Operations on non-existent resources SHALL return 404.

### Requirement: CreateIfNotExists Idempotency

The SDK SHALL support idempotent create operations.

**When** `CreateDatabaseIfNotExistsAsync` or `CreateContainerIfNotExistsAsync` is called, the SDK SHALL return 200 with the existing resource if it already exists, or 201 if newly created.

### Requirement: Partition Key Immutability

The SDK SHALL enforce that partition keys cannot be changed after container creation.

**When** a container is created with a partition key definition, the SDK SHALL NOT allow the partition key to be changed via `ReplaceContainerAsync` or any other operation.

### Requirement: Indexing Policy Management

The SDK SHALL support configuring indexing policies on containers.

#### Background re-indexing

**When** indexing policy changes are applied via `ReplaceContainerAsync`, the SDK SHALL allow the service to trigger background re-indexing as needed.

#### Vector indexes

**Where** `IndexingPolicy.VectorIndexes` are configured (Preview), the SDK SHALL support creating vector similarity search indexes with types: Flat, QuantizedFlat, DiskANN.

### Requirement: Time-to-Live (TTL)

The SDK SHALL support configuring item expiration via TTL.

#### Container-level TTL

**Where** `ContainerProperties.DefaultTimeToLive` is set to a positive value, **when** items are created without an explicit TTL, the SDK SHALL expire items after the specified duration.

#### Enable without default expiry

**Where** `ContainerProperties.DefaultTimeToLive = -1`, the SDK SHALL enable TTL without a default expiry. Individual items SHALL set their own TTL via the `ttl` property.

#### Item-level TTL override

**While** a container has default TTL enabled, **when** an item is created with `ttl = -1`, the SDK SHALL ensure that item never expires.

### Requirement: Throughput Management

The SDK SHALL support provisioning throughput at database or container level.

**When** `ReadThroughputAsync` or `ReplaceThroughputAsync` is called, the SDK SHALL manage throughput using `ThroughputProperties`, supporting both manual (`CreateManualThroughput()`) and autoscale (`CreateAutoscaleThroughput()`) modes.

## References

- Source: `Microsoft.Azure.Cosmos/src/Resource/Database/`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Container/`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Settings/ContainerProperties.cs`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Settings/IndexingPolicy.cs`