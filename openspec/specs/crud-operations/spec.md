# CRUD Operations

## Purpose

Point operations for creating, reading, updating, replacing, and deleting individual items in Azure Cosmos DB containers. These are the foundational data-plane operations of the SDK.

## Requirements

### Requirement: Create Item
The SDK SHALL create a new item in a container with a specified partition key.

#### Scenario: Successful creation
- GIVEN a container and a valid item with a partition key
- WHEN `Container.CreateItemAsync<T>(item, partitionKey)` is called
- THEN the item is persisted in the container
- AND an `ItemResponse<T>` is returned with status code 201
- AND the response includes the created resource, request charge, and diagnostics

#### Scenario: Conflict on duplicate ID
- GIVEN an item with the same ID and partition key already exists
- WHEN `Container.CreateItemAsync<T>(item, partitionKey)` is called
- THEN a `CosmosException` is thrown with status code 409 (Conflict)

#### Scenario: Create with stream
- GIVEN a valid JSON stream and partition key
- WHEN `Container.CreateItemStreamAsync(stream, partitionKey)` is called
- THEN a `ResponseMessage` is returned without deserialization overhead

### Requirement: Read Item
The SDK SHALL read a single item by its ID and partition key (point read).

#### Scenario: Successful point read
- GIVEN an existing item with known ID and partition key
- WHEN `Container.ReadItemAsync<T>(id, partitionKey)` is called
- THEN the item is returned in an `ItemResponse<T>` with status code 200
- AND the response includes request charge, ETag, and diagnostics

#### Scenario: Item not found
- GIVEN no item exists with the specified ID and partition key
- WHEN `Container.ReadItemAsync<T>(id, partitionKey)` is called
- THEN a `CosmosException` is thrown with status code 404, sub-status code 0

#### Scenario: Conditional read (If-None-Match)
- GIVEN an item with a known ETag
- WHEN `ReadItemAsync` is called with `ItemRequestOptions.IfNoneMatchEtag` set to that ETag
- AND the item has not been modified
- THEN a `CosmosException` is thrown with status code 304 (Not Modified)
- AND no request charge is consumed for the read

### Requirement: Replace Item
The SDK SHALL replace an entire item identified by its ID and partition key.

#### Scenario: Successful replace
- GIVEN an existing item
- WHEN `Container.ReplaceItemAsync<T>(item, id, partitionKey)` is called
- THEN the item is fully replaced with the new content
- AND an `ItemResponse<T>` is returned with status code 200

#### Scenario: Optimistic concurrency with ETag
- GIVEN an item with a known ETag
- WHEN `ReplaceItemAsync` is called with `ItemRequestOptions.IfMatchEtag` set
- AND the item has been modified by another client (ETag mismatch)
- THEN a `CosmosException` is thrown with status code 412 (Precondition Failed)

### Requirement: Upsert Item
The SDK SHALL create or replace an item based on whether it already exists.

#### Scenario: Item does not exist
- GIVEN no item with the specified ID and partition key exists
- WHEN `Container.UpsertItemAsync<T>(item, partitionKey)` is called
- THEN the item is created
- AND an `ItemResponse<T>` is returned with status code 201

#### Scenario: Item already exists
- GIVEN an item with the specified ID and partition key exists
- WHEN `Container.UpsertItemAsync<T>(item, partitionKey)` is called
- THEN the item is replaced with the new content
- AND an `ItemResponse<T>` is returned with status code 200

### Requirement: Delete Item
The SDK SHALL delete an item by its ID and partition key.

#### Scenario: Successful deletion
- GIVEN an existing item
- WHEN `Container.DeleteItemAsync<T>(id, partitionKey)` is called
- THEN the item is removed from the container
- AND an `ItemResponse<T>` is returned with status code 204

#### Scenario: Delete non-existent item
- GIVEN no item with the specified ID and partition key exists
- WHEN `Container.DeleteItemAsync<T>(id, partitionKey)` is called
- THEN a `CosmosException` is thrown with status code 404

### Requirement: Patch Item
The SDK SHALL apply partial modifications to an item without replacing the entire document.

#### Scenario: Add a new property
- GIVEN an existing item
- WHEN `Container.PatchItemAsync<T>(id, partitionKey, patchOperations)` is called with `PatchOperation.Add("/newProp", value)`
- THEN the property is added to the item
- AND an `ItemResponse<T>` is returned with status code 200

#### Scenario: Multiple patch operations
- GIVEN an existing item
- WHEN `PatchItemAsync` is called with multiple `PatchOperation` entries (Add, Remove, Replace, Set, Increment, Move)
- THEN all operations are applied atomically in order

#### Scenario: Conditional patch
- GIVEN an existing item
- WHEN `PatchItemAsync` is called with `PatchItemRequestOptions.FilterPredicate` set to a SQL-like condition
- AND the condition evaluates to false
- THEN a `CosmosException` is thrown with status code 412 (Precondition Failed)

### Requirement: Read Many Items
The SDK SHALL read multiple items by their (ID, partition key) pairs in a single request.

#### Scenario: Successful ReadMany
- GIVEN a list of (id, partitionKey) tuples
- WHEN `Container.ReadManyItemsAsync<T>(items)` is called
- THEN all found items are returned in a `FeedResponse<T>`
- AND missing items are silently omitted from the response

### Requirement: Content Response on Write
The SDK SHALL support controlling whether write operations return the resource body.

#### Scenario: Suppress content on create
- GIVEN `ItemRequestOptions.EnableContentResponseOnWrite = false` or `CosmosClientOptions.EnableContentResponseOnWrite = false`
- WHEN a create, replace, or upsert operation is performed
- THEN the response does not include the resource body
- AND request charge is reduced

### Requirement: Priority-Based Execution
The SDK SHALL support priority levels for point operations.

#### Scenario: Low priority request
- GIVEN `ItemRequestOptions.PriorityLevel = PriorityLevel.Low`
- WHEN a point operation is performed during high load
- THEN the service MAY deprioritize this request in favor of High priority requests

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Resource/Container/Container.cs` — public API definitions
- `Microsoft.Azure.Cosmos/src/Resource/Container/ContainerCore.Items.cs` — implementation
- `Microsoft.Azure.Cosmos/src/Patch/PatchOperation.cs` — patch operation types
- `Microsoft.Azure.Cosmos/src/RequestOptions/ItemRequestOptions.cs` — per-request options
