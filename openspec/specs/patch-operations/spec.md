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

## Behavioral Invariants

1. **Atomic execution**: All patch operations in a single call execute atomically — all succeed or all fail.
2. **`Set` vs `Add` vs `Replace`**: `Set` creates the property if it doesn't exist (upsert semantics for properties). `Replace` fails if the property doesn't exist. `Add` adds to arrays or creates properties.
3. **`Increment` is atomic**: Server-side atomic increment/decrement without read-modify-write race conditions.
4. **Conditional patches**: `FilterPredicate` evaluates a SQL condition against the current item. If the condition is false, the patch returns 412 Precondition Failed without modifying the item.
5. **Path syntax**: Uses JSON pointer-like path syntax (e.g., `/address/city`, `/tags/0`).
6. **Partition key and id are immutable**: Patch operations cannot modify the `id` or partition key properties.
7. **Max operations**: Up to 10 patch operations per call.

## Interactions

- **CRUD Operations**: Patch is an alternative to Replace for partial updates. See `crud-operations` spec.
- **Batch**: Patch can be included in `TransactionalBatch` via `batch.PatchItem()`. See `batch-and-transactional` spec.
- **Serialization**: Patch values are serialized using the configured serializer. See `serialization` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/Patch/PatchOperation.cs`
- Source: `Microsoft.Azure.Cosmos/src/RequestOptions/PatchItemRequestOptions.cs`
