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

## Requirements

### Requirement: Partition Key Handling

1. **When** the caller invokes a Read or Delete operation, the SDK **shall** require a partition key parameter.
2. **When** the caller invokes a typed Create, Replace, or Upsert operation with a `null` partition key, the SDK **shall** extract the partition key from the serialized item using the container's partition key path definition.
3. **When** the caller invokes a stream Create, Replace, or Upsert operation, the SDK **shall** require a partition key parameter (the SDK cannot extract from an opaque stream without the caller's serializer context).
4. **Where** auto-extraction fails due to a stale partition key definition cache, the SDK **shall** refresh the cache and retry extraction via `PartitionKeyMismatchRetryPolicy`.

### Requirement: Conditional Operations (ETags)

| Operation | `IfMatchEtag` | `IfNoneMatchEtag` |
|-----------|--------------|-------------------|
| Create | Ignored | N/A |
| Read | N/A | Supported (returns 304 if match) |
| Replace | Supported (412 on mismatch) | N/A |
| Upsert | Respected only during replace phase | Respected only during replace phase |
| Delete | Supported (412 on mismatch) | N/A |

1. **When** a Replace or Delete request includes `IfMatchEtag` and the server-side ETag does not match, the SDK **shall** return status code 412 (Precondition Failed).
2. **When** a Read request includes `IfNoneMatchEtag` and the server-side ETag matches, the SDK **shall** return status code 304 (Not Modified).
3. **When** an Upsert request includes `IfMatchEtag` or `IfNoneMatchEtag`, the SDK **shall** apply the condition only during the replace phase of the upsert.
4. **When** a Create request includes `IfMatchEtag`, the SDK **shall** ignore it.

### Requirement: Response Behavior

1. **When** `EnableContentResponseOnWrite` is set to `false` on `ItemRequestOptions`, write operations (Create, Replace, Upsert) **shall** return `null` for `Resource`, reducing network payload. Delete **shall** always return `null` for `Resource`.
2. **When** an Upsert operation succeeds, the SDK **shall** return status code 201 if the item was created or 200 if the item was replaced. Callers MUST check `StatusCode` to distinguish the outcome.
3. **When** a Delete operation succeeds, `Resource` **shall** always be `null` and the content stream **shall** be empty, regardless of options.
4. **When** a ReadMany operation is invoked, the SDK **shall** return partial results. Items not found **shall** be silently omitted — the operation **shall not** fail if some items are missing.

### Requirement: Error Handling

| Code | Substatus | Meaning | Operations |
|------|-----------|---------|-----------|
| 400 | — | Bad request (invalid PK, malformed item) | All |
| 404 | 0 | Item not found | Read, Replace, Delete |
| 409 | 0 | Conflict (item already exists) | Create |
| 412 | 0 | Precondition failed (ETag mismatch) | Replace, Delete, Upsert (replace phase) |
| 413 | — | Item exceeds size limit (2 MB) | Create, Replace, Upsert |
| 429 | — | Rate limited | All (handled by retry policy) |

1. **When** the request payload is invalid (bad partition key, malformed item), the SDK **shall** return status code 400.
2. **When** a Read, Replace, or Delete targets an item that does not exist, the SDK **shall** return status code 404.
3. **When** a Create targets an item whose id already exists in the partition, the SDK **shall** return status code 409.
4. **When** a write operation exceeds the 2 MB item size limit, the SDK **shall** return status code 413.
5. **When** the service returns 429 (rate limited), the SDK **shall** retry the request according to the configured retry policy.

### Requirement: Server-Side Triggers

1. **When** `ItemRequestOptions.PreTriggers` or `PostTriggers` are specified on a write operation, the SDK **shall** include the trigger names in the request so they execute server-side.
2. **Where** triggers are configured, they **shall** execute within the same transaction as the operation.

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