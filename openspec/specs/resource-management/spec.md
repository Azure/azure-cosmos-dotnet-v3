# Resource Management

## Purpose

The SDK provides management operations for databases, containers, throughput, indexing policies, and container-level features like vector search, full-text search, and conflict resolution.

## Requirements

### Requirement: Database Management
The SDK SHALL support creating, reading, and deleting databases.

#### Scenario: Create database
- GIVEN a Cosmos DB account
- WHEN `CosmosClient.CreateDatabaseAsync("mydb")` is called
- THEN a new database is created
- AND a `DatabaseResponse` is returned with status code 201

#### Scenario: Create if not exists
- GIVEN a database name
- WHEN `CosmosClient.CreateDatabaseIfNotExistsAsync("mydb")` is called
- THEN the database is created if it doesn't exist (201)
- OR the existing database reference is returned (200)

#### Scenario: Read database
- GIVEN an existing database
- WHEN `database.ReadAsync()` is called
- THEN `DatabaseProperties` are returned with the database metadata

#### Scenario: Delete database
- GIVEN an existing database
- WHEN `database.DeleteAsync()` is called
- THEN the database and all its containers are permanently deleted

### Requirement: Container Management
The SDK SHALL support creating, reading, replacing, and deleting containers.

#### Scenario: Create container
- GIVEN a database
- WHEN `database.CreateContainerAsync(new ContainerProperties("mycontainer", "/partitionKey"))` is called
- THEN a new container is created with the specified partition key
- AND a `ContainerResponse` is returned with status code 201

#### Scenario: Create with throughput
- GIVEN `ThroughputProperties.CreateManualThroughput(400)` or `ThroughputProperties.CreateAutoscaleThroughput(4000)`
- WHEN `CreateContainerAsync(properties, throughput)` is called
- THEN the container is created with the specified throughput mode

#### Scenario: Replace container properties
- GIVEN an existing container
- WHEN `container.ReplaceContainerAsync(updatedProperties)` is called
- THEN the container properties are updated (e.g., indexing policy, default TTL)

#### Scenario: Delete container
- GIVEN an existing container
- WHEN `container.DeleteContainerAsync()` is called
- THEN the container and all its items are permanently deleted

### Requirement: Throughput Management
The SDK SHALL support reading and updating throughput for databases and containers.

#### Scenario: Read throughput
- GIVEN a container with provisioned throughput
- WHEN `container.ReadThroughputAsync()` is called
- THEN `ThroughputProperties` is returned with current RU/s settings

#### Scenario: Replace throughput
- GIVEN a container with manual throughput
- WHEN `container.ReplaceThroughputAsync(ThroughputProperties.CreateManualThroughput(800))` is called
- THEN the throughput is updated to 800 RU/s

#### Scenario: Autoscale throughput
- GIVEN a container configured with autoscale
- WHEN `container.ReadThroughputAsync()` is called
- THEN the response includes `AutoscaleMaxThroughput` and current provisioned throughput

### Requirement: Indexing Policy Configuration
The SDK SHALL support configuring indexing policies on containers.

#### Scenario: Default indexing
- GIVEN no indexing policy is specified
- WHEN a container is created
- THEN all properties are automatically indexed (default behavior)

#### Scenario: Custom included paths
- GIVEN `IndexingPolicy.IncludedPaths` contains specific paths
- WHEN the container is created or updated
- THEN only the specified paths are indexed

#### Scenario: Excluded paths
- GIVEN `IndexingPolicy.ExcludedPaths` contains specific paths
- WHEN items are ingested
- THEN the excluded paths are not indexed

#### Scenario: Composite indexes
- GIVEN `IndexingPolicy.CompositeIndexes` is configured
- WHEN queries with multi-property ORDER BY are executed
- THEN the composite index is used for efficient sorting

#### Scenario: Spatial indexes
- GIVEN spatial index paths are configured
- WHEN geospatial queries (ST_DISTANCE, ST_WITHIN, etc.) are executed
- THEN the spatial index is used for efficient geo queries

### Requirement: Vector Embedding Policy
The SDK SHALL support configuring vector embedding policies for vector search.

#### Scenario: Configure vector embedding
- GIVEN `ContainerProperties.VectorEmbeddingPolicy` is set with vector paths, dimensions, data type, and distance function
- WHEN the container is created
- THEN vector search is enabled for the specified paths

#### Scenario: Vector index types
- GIVEN `VectorIndexPath` with `VectorIndexType` (Flat, QuantizedFlat, DiskANN)
- WHEN configured in the indexing policy
- THEN the appropriate vector index is created

### Requirement: Full-Text Search Policy
The SDK SHALL support configuring full-text search policies.

#### Scenario: Configure full-text paths
- GIVEN `ContainerProperties.FullTextPolicy` with `FullTextPath` entries
- WHEN the container is created
- THEN full-text search is enabled for the specified paths

### Requirement: Time-to-Live (TTL)
The SDK SHALL support configuring item expiration via TTL.

#### Scenario: Container-level TTL
- GIVEN `ContainerProperties.DefaultTimeToLive = 3600` (seconds)
- WHEN items are created without an explicit TTL
- THEN items expire 3600 seconds after their last modification

#### Scenario: Item-level TTL override
- GIVEN a container with default TTL enabled
- WHEN an item is created with a `ttl` property set to a specific value
- THEN that item's TTL overrides the container default

#### Scenario: Disable TTL for specific item
- GIVEN a container with default TTL
- WHEN an item is created with `ttl = -1`
- THEN that item never expires

### Requirement: Conflict Resolution Policy
The SDK SHALL support configuring conflict resolution for multi-region writes.

#### Scenario: Last-writer-wins (default)
- GIVEN `ConflictResolutionPolicy.Mode = ConflictResolutionMode.LastWriterWins`
- WHEN concurrent writes conflict
- THEN the write with the highest `_ts` (or custom conflict resolution path) wins

#### Scenario: Custom stored procedure
- GIVEN `ConflictResolutionPolicy.Mode = ConflictResolutionMode.Custom` with a stored procedure path
- WHEN conflicts occur
- THEN the specified stored procedure is invoked to resolve the conflict

### Requirement: Stored Procedures, UDFs, and Triggers
The SDK SHALL support managing server-side programmability artifacts.

#### Scenario: Create stored procedure
- GIVEN JavaScript function body
- WHEN `container.Scripts.CreateStoredProcedureAsync(properties)` is called
- THEN the stored procedure is registered in the container

#### Scenario: Execute stored procedure
- GIVEN a registered stored procedure
- WHEN `container.Scripts.ExecuteStoredProcedureAsync<T>(sprocId, partitionKey, parameters)` is called
- THEN the stored procedure executes server-side within the partition scope

#### Scenario: Create user-defined function
- GIVEN JavaScript function body
- WHEN `container.Scripts.CreateUserDefinedFunctionAsync(properties)` is called
- THEN the UDF is registered and available for use in queries

#### Scenario: Create trigger
- GIVEN trigger properties with type (Pre/Post) and operation
- WHEN `container.Scripts.CreateTriggerAsync(properties)` is called
- THEN the trigger is registered and fires on the specified operation type

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Resource/Database/Database.cs` — database management API
- `Microsoft.Azure.Cosmos/src/Resource/Container/Container.cs` — container management API
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ContainerProperties.cs` — container properties
- `Microsoft.Azure.Cosmos/src/Resource/Settings/IndexingPolicy.cs` — indexing configuration
- `Microsoft.Azure.Cosmos/src/Resource/Settings/VectorEmbeddingPolicy.cs` — vector search config
- `Microsoft.Azure.Cosmos/src/Resource/Settings/FullTextPolicy.cs` — full-text search config
- `Microsoft.Azure.Cosmos/src/Resource/Settings/ConflictResolutionPolicy.cs` — conflict resolution
- `Microsoft.Azure.Cosmos/src/Resource/Scripts/Scripts.cs` — stored procedures, UDFs, triggers
