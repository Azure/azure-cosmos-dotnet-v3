# CRUD Operations

## Purpose

Point operations for creating, reading, updating, replacing, and deleting individual items in Azure Cosmos DB containers. These are the foundational data-plane operations of the SDK.

## Requirements

### Requirement: Create Item
The SDK SHALL create a new item in a container with a specified partition key.

#### Successful creation
**When** `Container.CreateItemAsync<T>(item, partitionKey)` is called with a valid item and partition key, the SDK shall persist the item in the container, return an `ItemResponse<T>` with status code 201, and include the created resource, request charge, and diagnostics in the response.

#### Conflict on duplicate ID
**If** an item with the same ID and partition key already exists when `Container.CreateItemAsync<T>(item, partitionKey)` is called, **then** the SDK shall throw a `CosmosException` with status code 409 (Conflict).

#### Create with stream
**When** `Container.CreateItemStreamAsync(stream, partitionKey)` is called with a valid JSON stream and partition key, the SDK shall return a `ResponseMessage` without deserialization overhead.

### Requirement: Read Item
The SDK SHALL read a single item by its ID and partition key (point read).

#### Successful point read
**When** `Container.ReadItemAsync<T>(id, partitionKey)` is called for an existing item with known ID and partition key, the SDK shall return the item in an `ItemResponse<T>` with status code 200, including request charge, ETag, and diagnostics.

#### Item not found
**If** no item exists with the specified ID and partition key when `Container.ReadItemAsync<T>(id, partitionKey)` is called, **then** the SDK shall throw a `CosmosException` with status code 404, sub-status code 0.

#### Conditional read (If-None-Match)
**Where** `ItemRequestOptions.IfNoneMatchEtag` is set to a known ETag, **when** `ReadItemAsync` is called and the item has not been modified, the SDK shall throw a `CosmosException` with status code 304 (Not Modified) and consume no request charge for the read.

### Requirement: Replace Item
The SDK SHALL replace an entire item identified by its ID and partition key.

#### Successful replace
**When** `Container.ReplaceItemAsync<T>(item, id, partitionKey)` is called for an existing item, the SDK shall fully replace the item with the new content and return an `ItemResponse<T>` with status code 200.

#### Optimistic concurrency with ETag
**If** `ReplaceItemAsync` is called with `ItemRequestOptions.IfMatchEtag` set and the item has been modified by another client (ETag mismatch), **then** the SDK shall throw a `CosmosException` with status code 412 (Precondition Failed).

### Requirement: Upsert Item
The SDK SHALL create or replace an item based on whether it already exists.

#### Item does not exist
**When** `Container.UpsertItemAsync<T>(item, partitionKey)` is called and no item with the specified ID and partition key exists, the SDK shall create the item and return an `ItemResponse<T>` with status code 201.

#### Item already exists
**When** `Container.UpsertItemAsync<T>(item, partitionKey)` is called and an item with the specified ID and partition key already exists, the SDK shall replace the item with the new content and return an `ItemResponse<T>` with status code 200.

### Requirement: Delete Item
The SDK SHALL delete an item by its ID and partition key.

#### Successful deletion
**When** `Container.DeleteItemAsync<T>(id, partitionKey)` is called for an existing item, the SDK shall remove the item from the container and return an `ItemResponse<T>` with status code 204.

#### Delete non-existent item
**If** no item with the specified ID and partition key exists when `Container.DeleteItemAsync<T>(id, partitionKey)` is called, **then** the SDK shall throw a `CosmosException` with status code 404.

### Requirement: Patch Item
The SDK SHALL apply partial modifications to an item without replacing the entire document.

#### Add a new property
**When** `Container.PatchItemAsync<T>(id, partitionKey, patchOperations)` is called with `PatchOperation.Add("/newProp", value)` for an existing item, the SDK shall add the property to the item and return an `ItemResponse<T>` with status code 200.

#### Multiple patch operations
**When** `PatchItemAsync` is called with multiple `PatchOperation` entries (Add, Remove, Replace, Set, Increment, Move) for an existing item, the SDK shall apply all operations atomically in order.

#### Conditional patch
**If** `PatchItemAsync` is called with `PatchItemRequestOptions.FilterPredicate` set to a SQL-like condition and the condition evaluates to false, **then** the SDK shall throw a `CosmosException` with status code 412 (Precondition Failed).

### Requirement: Read Many Items
The SDK SHALL read multiple items by their (ID, partition key) pairs in a single request.

#### Successful ReadMany
**When** `Container.ReadManyItemsAsync<T>(items)` is called with a list of (id, partitionKey) tuples, the SDK shall return all found items in a `FeedResponse<T>` and silently omit missing items from the response.

### Requirement: Content Response on Write
The SDK SHALL support controlling whether write operations return the resource body.

#### Suppress content on create
**Where** `ItemRequestOptions.EnableContentResponseOnWrite = false` or `CosmosClientOptions.EnableContentResponseOnWrite = false`, **when** a create, replace, or upsert operation is performed, the SDK shall not include the resource body in the response and shall reduce the request charge.

### Requirement: Priority-Based Execution
The SDK SHALL support priority levels for point operations.

#### Low priority request
**Where** `ItemRequestOptions.PriorityLevel = PriorityLevel.Low`, **when** a point operation is performed during high load, the service MAY deprioritize this request in favor of High priority requests.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Resource/Container/Container.cs` — public API definitions
- `Microsoft.Azure.Cosmos/src/Resource/Container/ContainerCore.Items.cs` — implementation
- `Microsoft.Azure.Cosmos/src/Patch/PatchOperation.cs` — patch operation types
- `Microsoft.Azure.Cosmos/src/RequestOptions/ItemRequestOptions.cs` — per-request options
