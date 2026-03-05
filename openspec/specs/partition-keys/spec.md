# Partition Keys

## Purpose

Every Azure Cosmos DB container is partitioned by a partition key, which determines how data is distributed across physical partitions. The .NET SDK provides the `PartitionKey` struct for single-key values and `PartitionKeyBuilder` for hierarchical (multi-hash) partition keys. Understanding partition key semantics is critical because every point operation requires a partition key for routing, and the SDK's auto-extraction behavior differs between typed and stream APIs.

## Public API Surface

### PartitionKey Struct

```csharp
public readonly struct PartitionKey
{
    // Constructors
    public PartitionKey(string partitionKeyValue);
    public PartitionKey(bool partitionKeyValue);
    public PartitionKey(double partitionKeyValue);

    // Static members
    public static readonly PartitionKey None;   // No partition key value
    public static readonly PartitionKey Null;   // Explicit null value

    // Properties
    public bool IsNone { get; }

    // Methods
    public override string ToString();
    public static bool TryParseJsonString(string json, out PartitionKey partitionKey);
}
```

### PartitionKeyBuilder (Hierarchical Partition Keys)

```csharp
public sealed class PartitionKeyBuilder
{
    public PartitionKeyBuilder Add(string val);
    public PartitionKeyBuilder Add(double val);
    public PartitionKeyBuilder Add(bool val);
    public PartitionKeyBuilder AddNullValue();
    public PartitionKeyBuilder AddNoneType();
    public PartitionKey Build();  // Throws ArgumentException if no values added
}
```

### ContainerProperties Partition Key Access

```csharp
// Single partition key path
string path = containerProperties.PartitionKeyPath;  // e.g., "/userId"

// Hierarchical partition key paths
IReadOnlyList<string> paths = containerProperties.PartitionKeyPaths;  // e.g., ["/tenantId", "/userId"]
```

## Behavioral Invariants

### PartitionKey.None vs PartitionKey.Null

| Aspect | `PartitionKey.None` | `PartitionKey.Null` |
|--------|---------------------|---------------------|
| `IsNone` | `true` | `false` |
| Meaning | No partition key value provided or applicable | Explicit `null` value |
| Usage | Legacy or schema-flexible containers | Any container — `null` is a valid PK value |
| Multi-hash support | ❌ Not allowed | ✅ Allowed per component |
| Construction | `PartitionKey.None` | `new PartitionKey((string)null)` |
| ToString | `"None"` | `"null"` (JSON) |

### Partition Key Requirements by Operation

| Operation | Partition Key | Behavior |
|-----------|--------------|----------|
| `CreateItemAsync<T>` | Optional | Auto-extracted from item if null |
| `CreateItemStreamAsync` | Required | Cannot auto-extract from opaque stream |
| `ReadItemAsync<T>` | Required | Routing parameter |
| `ReplaceItemAsync<T>` | Optional | Auto-extracted from item if null |
| `DeleteItemAsync<T>` | Required | Routing parameter |
| `UpsertItemAsync<T>` | Optional | Auto-extracted from item if null |
| `CreateTransactionalBatch` | Required | All items in batch must share same PK |
| Query (via `QueryRequestOptions`) | Optional | Null = cross-partition query |

### Auto-Extraction from Documents

1. When a typed write operation receives `partitionKey=null`, the SDK serializes the item, navigates the JSON tree using the container's partition key path(s), and extracts the value(s).
2. For hierarchical keys, each path level must have a corresponding value in the document.
3. If a path is missing from the document, the extracted value is `Undefined`, which maps to `PartitionKey.None` semantics.
4. If extraction fails due to a stale partition key definition cache, the SDK retries with a refreshed cache via `PartitionKeyMismatchRetryPolicy`.

### Hierarchical (Multi-Hash) Partition Keys

1. Requires `PartitionKeyDefinitionVersion.V2` and `PartitionKind.MultiHash`.
2. Paths are ordered — `PartitionKeyBuilder.Add()` calls correspond to paths in definition order.
3. Point operations MUST provide all path components. Incomplete keys return 400 Bad Request.
4. Query operations support prefix routing: providing only the first N components of an M-level key routes to partitions matching that prefix.
5. `PartitionKeyBuilder.Build()` throws `ArgumentException` if no values were added.

### Partition Key Immutability

1. An item's partition key value is immutable. You cannot change it via Replace or Upsert.
2. To change an item's partition key, delete the item and create a new one with the desired key.

### Supported Value Types

| Type | Constructor | Notes |
|------|------------|-------|
| `string` | `new PartitionKey("value")` | Most common |
| `bool` | `new PartitionKey(true)` | Boolean partition keys |
| `double` | `new PartitionKey(42.0)` | All numeric types as double |
| `null` | `new PartitionKey((string)null)` | Creates `PartitionKey.Null` |

### Equality and Hashing

- `PartitionKey` supports `==`, `!=`, `Equals()`, and `GetHashCode()` based on internal representation.
- Two `PartitionKey` instances with the same value are equal regardless of how they were constructed.

## Configuration

### Container Creation

```csharp
// Single partition key
new ContainerProperties(id: "myContainer", partitionKeyPath: "/userId")

// Hierarchical partition keys
new ContainerProperties(
    id: "myContainer",
    partitionKeyPaths: new[] { "/tenantId", "/userId" })
```

### PartitionKeyDefinition Properties

| Property | Type | Values |
|----------|------|--------|
| `Paths` | `Collection<string>` | 1-3 paths (e.g., `["/tenantId", "/userId"]`) |
| `Kind` | `PartitionKind` | `Hash` (single), `MultiHash` (hierarchical) |
| `Version` | `PartitionKeyDefinitionVersion` | `V1` (default single), `V2` (required for MultiHash) |

## Interactions

- **CRUD Operations**: Every point operation routes by partition key. See `crud-operations` spec.
- **Query**: `QueryRequestOptions.PartitionKey` controls single-partition vs cross-partition routing. See `query-and-linq` spec.
- **Handler Pipeline**: `PartitionKeyRangeHandler` resolves partition key ranges for feed operations. See `handler-pipeline` spec.
- **Batch**: All items in a `TransactionalBatch` must share the same partition key.

## References

- Source: `Microsoft.Azure.Cosmos/src/PartitionKey.cs`
- Source: `Microsoft.Azure.Cosmos/src/PartitionKeyBuilder.cs`
- Source: `Microsoft.Azure.Cosmos/src/Resource/Settings/ContainerProperties.cs`
- Source: `Microsoft.Azure.Cosmos/src/Routing/DocumentAnalyzer.cs`
