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

## Behavioral Invariants

1. **Proxy references don't validate existence**: `GetDatabase()` and `GetContainer()` return lightweight references without network calls. Operations on non-existent resources return 404.
2. **`CreateIfNotExists` idempotency**: Returns 200 with existing resource if it already exists, 201 if newly created.
3. **Partition key is immutable**: Cannot be changed after container creation.
4. **IndexingPolicy changes**: Some indexing policy changes trigger background re-indexing. Others (like adding vector indexes) may require container recreation.
5. **TTL behavior**: Setting `DefaultTimeToLive = -1` enables TTL without a default expiry. Individual items can set their own TTL via the `ttl` property.
6. **Throughput**: Can be provisioned at database level (shared) or container level (dedicated). Use `ThroughputProperties.CreateAutoscaleThroughput()` for autoscale.

## References

- Source: `Microsoft.Azure.Cosmos/src/Resource/Database/`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Container/`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Settings/ContainerProperties.cs`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Settings/IndexingPolicy.cs`
