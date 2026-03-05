# CRUD Operations

## Purpose

The Azure Cosmos DB .NET SDK provides item-level CRUD (Create, Read, Replace, Upsert, Delete) operations on containers. Every data-plane interaction with items flows through these APIs, which exist in two variants: a typed API that serializes/deserializes to `T` and a stream API that works with raw `Stream` payloads.

## Public API Surface

### Container Item Methods

| Method | Returns | Success Code | Purpose |
|--------|---------|-------------|---------|
| `CreateItemAsync<T>` | `ItemResponse<T>` | 201 | Create a new item |
| `CreateItemStreamAsync` | `ResponseMessage` | 201 | Create (stream) |
| `ReadItemAsync<T>` | `ItemResponse<T>` | 200 | Read by id + partition key |
| `ReadItemStreamAsync` | `ResponseMessage` | 200 | Read (stream) |
| `ReplaceItemAsync<T>` | `ItemResponse<T>` | 200 | Full item replacement |
| `ReplaceItemStreamAsync` | `ResponseMessage` | 200 | Replace (stream) |
| `UpsertItemAsync<T>` | `ItemResponse<T>` | 200 or 201 | Create-or-replace |
| `UpsertItemStreamAsync` | `ResponseMessage` | 200 or 201 | Upsert (stream) |
| `DeleteItemAsync<T>` | `ItemResponse<T>` | 204 | Delete by id + partition key |
| `DeleteItemStreamAsync` | `ResponseMessage` | 204 | Delete (stream) |
| `ReadManyItemsAsync<T>` | `FeedResponse<T>` | 200 | Batch read multiple items |
| `ReadManyItemsStreamAsync` | `ResponseMessage` | 200 | Batch read (stream) |

### Typed vs Stream API

| Aspect | Typed (`ItemResponse<T>`) | Stream (`ResponseMessage`) |
|--------|--------------------------|---------------------------|
| Error handling | Throws `CosmosException` on failure | Returns status code; caller checks `IsSuccessStatusCode` |
| Partition key | Optional for writes (auto-extracted from item) | Mandatory parameter |
| Deserialization | Automatic via container serializer | None; raw stream |
| Use case | Application code | Performance-critical paths, proxying |

## Behavioral Invariants

### Partition Key Requirements

1. **Read, Delete**: Partition key is always a required parameter.
2. **Create, Replace, Upsert (typed)**: Partition key is optional. If `null`, the SDK extracts it from the serialized item using the container's partition key path definition.
3. **Create, Replace, Upsert (stream)**: Partition key is a required parameter (SDK cannot extract from opaque stream without the caller's serializer context).
4. If auto-extraction fails due to a stale partition key definition cache, the SDK refreshes the cache and retries extraction via `PartitionKeyMismatchRetryPolicy`.

### Conditional Operations (ETags)

| Operation | `IfMatchEtag` | `IfNoneMatchEtag` |
|-----------|--------------|-------------------|
| Create | Ignored | N/A |
| Read | N/A | Supported (returns 304 if match) |
| Replace | Supported (412 on mismatch) | N/A |
| Upsert | Respected only during replace phase | Respected only during replace phase |
| Delete | Supported (412 on mismatch) | N/A |

### Response Behavior

1. **`EnableContentResponseOnWrite`**: When set to `false` on `ItemRequestOptions`, write operations (Create, Replace, Upsert) return `null` for `Resource`, reducing network payload. Delete always returns `null` for `Resource`.
2. **Upsert status code differentiation**: Callers MUST check `StatusCode` to determine if the item was created (201) or replaced (200).
3. **Delete responses**: `Resource` is always `null` and the content stream is empty, regardless of options.
4. **ReadMany**: Returns partial results. Items not found are silently omitted — the operation does not fail if some items are missing.

### Error Status Codes

| Code | Substatus | Meaning | Operations |
|------|-----------|---------|-----------|
| 400 | — | Bad request (invalid PK, malformed item) | All |
| 404 | 0 | Item not found | Read, Replace, Delete |
| 409 | 0 | Conflict (item already exists) | Create |
| 412 | 0 | Precondition failed (ETag mismatch) | Replace, Delete, Upsert (replace phase) |
| 413 | — | Item exceeds size limit (2 MB) | Create, Replace, Upsert |
| 429 | — | Rate limited | All (handled by retry policy) |

### Triggers

- `ItemRequestOptions.PreTriggers` and `PostTriggers` allow specifying stored procedure triggers for write operations.
- Triggers execute server-side within the same transaction as the operation.

## Configuration

### ItemRequestOptions

| Property | Type | Effect |
|----------|------|--------|
| `IfMatchEtag` | `string` | Conditional write; 412 if ETag doesn't match |
| `IfNoneMatchEtag` | `string` | Conditional read; 304 if ETag matches |
| `EnableContentResponseOnWrite` | `bool?` | `false` = null Resource on writes |
| `IndexingDirective` | `IndexingDirective?` | Include/Exclude from indexing |
| `ConsistencyLevel` | `ConsistencyLevel?` | Override account default |
| `SessionToken` | `string` | For session consistency |
| `PreTriggers` / `PostTriggers` | `IEnumerable<string>` | Server-side trigger names |

## Interactions

- **Handler Pipeline**: All CRUD operations flow through the full handler pipeline (`RequestInvokerHandler` → ... → `TransportHandler`). See `handler-pipeline` spec.
- **Retry Policies**: Transient failures (429, 503, etc.) are retried per the `retry-and-failover` spec.
- **Serialization**: Typed APIs use the container's `CosmosSerializer`. See `serialization` spec.
- **Partition Keys**: Routing depends on partition key. See `partition-keys` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/Resource/Container/ContainerCore.Items.cs`
- Source: `Microsoft.Azure.Cosmos/src/RequestOptions/ItemRequestOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Response.cs`
- Design: `docs/SdkDesign.md`
