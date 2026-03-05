# Resource Management

## Purpose

The SDK provides management operations for databases, containers, throughput, indexing policies, and container-level features like vector search, full-text search, and conflict resolution.

## Requirements

### Requirement: Database Management
The SDK SHALL support creating, reading, and deleting databases.

#### Create database
**When** `CosmosClient.CreateDatabaseAsync("mydb")` is called, the SDK shall create a new database and return a `DatabaseResponse` with status code 201.

#### Create if not exists
**When** `CosmosClient.CreateDatabaseIfNotExistsAsync("mydb")` is called, the SDK shall create the database if it doesn't exist (201) or return the existing database reference (200).

#### Read database
**When** `database.ReadAsync()` is called on an existing database, the SDK shall return `DatabaseProperties` with the database metadata.

#### Delete database
**When** `database.DeleteAsync()` is called on an existing database, the SDK shall permanently delete the database and all its containers.

### Requirement: Container Management
The SDK SHALL support creating, reading, replacing, and deleting containers.

#### Create container
**When** `database.CreateContainerAsync(new ContainerProperties("mycontainer", "/partitionKey"))` is called, the SDK shall create a new container with the specified partition key and return a `ContainerResponse` with status code 201.

#### Create with throughput
**When** `CreateContainerAsync(properties, throughput)` is called with `ThroughputProperties.CreateManualThroughput(400)` or `ThroughputProperties.CreateAutoscaleThroughput(4000)`, the SDK shall create the container with the specified throughput mode.

#### Replace container properties
**When** `container.ReplaceContainerAsync(updatedProperties)` is called on an existing container, the SDK shall update the container properties (e.g., indexing policy, default TTL).

#### Delete container
**When** `container.DeleteContainerAsync()` is called on an existing container, the SDK shall permanently delete the container and all its items.

### Requirement: Throughput Management
The SDK SHALL support reading and updating throughput for databases and containers.

#### Read throughput
**When** `container.ReadThroughputAsync()` is called on a container with provisioned throughput, the SDK shall return `ThroughputProperties` with current RU/s settings.

#### Replace throughput
**When** `container.ReplaceThroughputAsync(ThroughputProperties.CreateManualThroughput(800))` is called on a container with manual throughput, the SDK shall update the throughput to 800 RU/s.

#### Autoscale throughput
**While** a container is configured with autoscale, **when** `container.ReadThroughputAsync()` is called, the SDK shall return the response including `AutoscaleMaxThroughput` and current provisioned throughput.

### Requirement: Indexing Policy Configuration
The SDK SHALL support configuring indexing policies on containers.

#### Default indexing
**When** a container is created without specifying an indexing policy, the SDK shall automatically index all properties (default behavior).

#### Custom included paths
**Where** `IndexingPolicy.IncludedPaths` contains specific paths, **when** the container is created or updated, the SDK shall index only the specified paths.

#### Excluded paths
**Where** `IndexingPolicy.ExcludedPaths` contains specific paths, **when** items are ingested, the SDK shall not index the excluded paths.

#### Composite indexes
**Where** `IndexingPolicy.CompositeIndexes` is configured, **when** queries with multi-property ORDER BY are executed, the SDK shall use the composite index for efficient sorting.

#### Spatial indexes
**Where** spatial index paths are configured, **when** geospatial queries (ST_DISTANCE, ST_WITHIN, etc.) are executed, the SDK shall use the spatial index for efficient geo queries.

### Requirement: Vector Embedding Policy
The SDK SHALL support configuring vector embedding policies for vector search.

#### Configure vector embedding
**Where** `ContainerProperties.VectorEmbeddingPolicy` is set with vector paths, dimensions, data type, and distance function, **when** the container is created, the SDK shall enable vector search for the specified paths.

#### Vector index types
**Where** `VectorIndexPath` with `VectorIndexType` (Flat, QuantizedFlat, DiskANN) is configured in the indexing policy, the SDK shall create the appropriate vector index.

### Requirement: Full-Text Search Policy
The SDK SHALL support configuring full-text search policies.

#### Configure full-text paths
**Where** `ContainerProperties.FullTextPolicy` is set with `FullTextPath` entries, **when** the container is created, the SDK shall enable full-text search for the specified paths.

### Requirement: Time-to-Live (TTL)
The SDK SHALL support configuring item expiration via TTL.

#### Container-level TTL
**Where** `ContainerProperties.DefaultTimeToLive = 3600` (seconds), **when** items are created without an explicit TTL, the SDK shall expire items 3600 seconds after their last modification.

#### Item-level TTL override
**While** a container has default TTL enabled, **when** an item is created with a `ttl` property set to a specific value, the SDK shall use that item's TTL overriding the container default.

#### Disable TTL for specific item
**While** a container has default TTL, **when** an item is created with `ttl = -1`, the SDK shall ensure that item never expires.

### Requirement: Conflict Resolution Policy
The SDK SHALL support configuring conflict resolution for multi-region writes.

#### Last-writer-wins (default)
**Where** `ConflictResolutionPolicy.Mode = ConflictResolutionMode.LastWriterWins`, **when** concurrent writes conflict, the SDK shall resolve the conflict so the write with the highest `_ts` (or custom conflict resolution path) wins.

#### Custom stored procedure
**Where** `ConflictResolutionPolicy.Mode = ConflictResolutionMode.Custom` with a stored procedure path, **when** conflicts occur, the SDK shall invoke the specified stored procedure to resolve the conflict.

### Requirement: Stored Procedures, UDFs, and Triggers
The SDK SHALL support managing server-side programmability artifacts.

#### Create stored procedure
**When** `container.Scripts.CreateStoredProcedureAsync(properties)` is called with a JavaScript function body, the SDK shall register the stored procedure in the container.

#### Execute stored procedure
**When** `container.Scripts.ExecuteStoredProcedureAsync<T>(sprocId, partitionKey, parameters)` is called with a registered stored procedure, the SDK shall execute the stored procedure server-side within the partition scope.

#### Create user-defined function
**When** `container.Scripts.CreateUserDefinedFunctionAsync(properties)` is called with a JavaScript function body, the SDK shall register the UDF and make it available for use in queries.

#### Create trigger
**When** `container.Scripts.CreateTriggerAsync(properties)` is called with trigger properties specifying type (Pre/Post) and operation, the SDK shall register the trigger and fire it on the specified operation type.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Resource/Database/Database.cs` — database management API
- `Microsoft.Azure.Cosmos/src/Resource/Container/Container.cs` — container management API
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ContainerProperties.cs` — container properties
- `Microsoft.Azure.Cosmos/src/Resource/Settings/IndexingPolicy.cs` — indexing configuration
- `Microsoft.Azure.Cosmos/src/Resource/Settings/VectorEmbeddingPolicy.cs` — vector search config
- `Microsoft.Azure.Cosmos/src/Resource/Settings/FullTextPolicy.cs` — full-text search config
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ConflictResolutionPolicy.cs` — conflict resolution
- `Microsoft.Azure.Cosmos/src/Resource/Scripts/Scripts.cs` — stored procedures, UDFs, triggers
