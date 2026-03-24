# Patch Operations

## Purpose

The Azure Cosmos DB .NET SDK supports partial document updates via JSON patch operations. Instead of replacing an entire item, patch allows modifying specific properties atomically with lower RU cost. The SDK provides a `PatchOperation` builder and supports conditional patches via filter predicates.

## Public API Surface

### Container Patch Methods

```csharp
// Typed
Task<ItemResponse<T>> PatchItemAsync<T>(
    string id, PartitionKey partitionKey,
    IReadOnlyList<PatchOperation> patchOperations,
    PatchItemRequestOptions requestOptions = null,
    CancellationToken cancellationToken = default);

// Stream
Task<ResponseMessage> PatchItemStreamAsync(
    string id, PartitionKey partitionKey,
    IReadOnlyList<PatchOperation> patchOperations,
    PatchItemRequestOptions requestOptions = null,
    CancellationToken cancellationToken = default);
```

### PatchOperation Types

| Factory Method | Parameters | Purpose |
|---------------|-----------|---------|
| `PatchOperation.Add(path, value)` | JSON path, value | Add property (or array element) |
| `PatchOperation.Remove(path)` | JSON path | Remove property |
| `PatchOperation.Replace(path, value)` | JSON path, value | Replace existing property value |
| `PatchOperation.Set(path, value)` | JSON path, value | Set property (create if not exists) |
| `PatchOperation.Increment(path, value)` | JSON path, long/double | Atomic increment/decrement |
| `PatchOperation.Move(from, path)` | Source path, target path | Move property to new location |

### PatchItemRequestOptions

| Property | Type | Effect |
|----------|------|--------|
| `FilterPredicate` | `string` | SQL WHERE clause for conditional patch (e.g., `"from c where c.status = 'active'"`) |
| `IfMatchEtag` | `string` | Conditional patch; 412 if ETag mismatch |
| `EnableContentResponseOnWrite` | `bool?` | Skip response payload |

## Requirements

### Requirement: Atomic Execution

The SDK SHALL execute all patch operations in a single call atomically.

**When** `PatchItemAsync` is called with multiple `PatchOperation` entries, all operations SHALL succeed or all SHALL fail. No partial application SHALL occur.

### Requirement: Set vs Add vs Replace Semantics

The SDK SHALL differentiate Set, Add, and Replace operations.

#### Set (upsert semantics)

**When** `PatchOperation.Set(path, value)` is used, the SDK SHALL create the property if it does not exist, or replace it if it does.

#### Replace (strict)

**When** `PatchOperation.Replace(path, value)` is used, the SDK SHALL fail if the property does not exist.

#### Add

**When** `PatchOperation.Add(path, value)` is used, the SDK SHALL add the property or array element at the specified path.

### Requirement: Atomic Increment

The SDK SHALL support server-side atomic increment/decrement.

**When** `PatchOperation.Increment(path, value)` is used, the SDK SHALL perform a server-side atomic increment without read-modify-write race conditions.

### Requirement: Conditional Patches

The SDK SHALL support conditional patches via filter predicates.

**Where** `PatchItemRequestOptions.FilterPredicate` is set to a SQL condition, **if** the condition evaluates to false against the current item, the SDK SHALL return 412 (Precondition Failed) without modifying the item.

### Requirement: Path Syntax

The SDK SHALL use JSON pointer-like path syntax for patch operations (e.g., `/address/city`, `/tags/0`).

### Requirement: Immutable Fields

The SDK SHALL prevent modification of immutable fields via patch.

**When** a patch operation targets the `id` or partition key properties, the SDK SHALL reject the operation.

### Requirement: Operation Limit

The SDK SHALL enforce a maximum of 10 patch operations per call.

## Interactions

- **CRUD Operations**: Patch is an alternative to Replace for partial updates. See `crud-operations` spec.
- **Batch**: Patch can be included in `TransactionalBatch` via `batch.PatchItem()`. See `batch-and-transactional` spec.
- **Serialization**: Patch values are serialized using the configured serializer. See `serialization` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/Patch/PatchOperation.cs`
- Source: `Microsoft.Azure.Cosmos/src/RequestOptions/PatchItemRequestOptions.cs`