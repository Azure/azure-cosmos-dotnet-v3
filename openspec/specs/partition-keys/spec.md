# Partition Keys

## Purpose

Every Azure Cosmos DB container is partitioned by a partition key, which determines how data is distributed across physical partitions. The .NET SDK provides the `PartitionKey` struct for single-key values and `PartitionKeyBuilder` for hierarchical (multi-hash) partition keys. Understanding partition key semantics is critical because every point operation requires a partition key for routing, and the SDK's auto-extraction behavior differs between typed and stream APIs.

## Public API Surface

### PartitionKey Struct

```csharp
public readonly struct PartitionKey
{
    public PartitionKey(string partitionKeyValue);
    public PartitionKey(bool partitionKeyValue);
    public PartitionKey(double partitionKeyValue);

    public static readonly PartitionKey None;   // No partition key value
    public static readonly PartitionKey Null;   // Explicit null value

    public bool IsNone { get; }
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
string path = containerProperties.PartitionKeyPath;  // e.g., "/userId"
IReadOnlyList<string> paths = containerProperties.PartitionKeyPaths;  // e.g., ["/tenantId", "/userId"]
```

## Requirements

### Requirement: PartitionKey.None vs PartitionKey.Null

The SDK SHALL distinguish between `PartitionKey.None` and `PartitionKey.Null`.

| Aspect | `PartitionKey.None` | `PartitionKey.Null` |
|--------|---------------------|---------------------|
| `IsNone` | `true` | `false` |
| Meaning | No partition key value provided or applicable | Explicit `null` value |
| Usage | Legacy or schema-flexible containers | Any container — `null` is a valid PK value |
| Multi-hash support | Not allowed | Allowed per component |
| Construction | `PartitionKey.None` | `new PartitionKey((string)null)` |
| ToString | `"None"` | `"null"` (JSON) |

### Requirement: Partition Key Per Operation

The SDK SHALL enforce partition key requirements per operation type.

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

### Requirement: Auto-Extraction from Documents

The SDK SHALL automatically extract partition key values from typed items.

#### Typed write auto-extraction

**When** a typed write operation receives `partitionKey=null`, the SDK SHALL serialize the item, navigate the JSON tree using the container's partition key path(s), and extract the value(s).

#### Hierarchical key extraction

**When** a container uses hierarchical partition keys, the SDK SHALL extract each path level's corresponding value from the document.

#### Missing path handling

**If** a path is missing from the document, the SDK SHALL extract the value as `Undefined`, which maps to `PartitionKey.None` semantics.

#### Stale cache retry

**If** extraction fails due to a stale partition key definition cache, the SDK SHALL retry with a refreshed cache via `PartitionKeyMismatchRetryPolicy`.

### Requirement: Hierarchical (Multi-Hash) Partition Keys

The SDK SHALL support hierarchical partition keys with `PartitionKeyBuilder`.

#### Version and kind requirements

**When** hierarchical partition keys are used, the container SHALL require `PartitionKeyDefinitionVersion.V2` and `PartitionKind.MultiHash`.

#### Ordered path components

**When** building a hierarchical partition key via `PartitionKeyBuilder`, `Add()` calls SHALL correspond to paths in definition order.

#### Complete key for point operations

**When** a point operation is performed with a hierarchical partition key, all path components SHALL be provided. Incomplete keys SHALL return 400 Bad Request.

#### Prefix routing for queries

**When** a query operation provides only the first N components of an M-level hierarchical key, the SDK SHALL route to partitions matching that prefix.

#### Empty builder validation

**When** `PartitionKeyBuilder.Build()` is called with no values added, the SDK SHALL throw `ArgumentException`.

### Requirement: Partition Key Immutability

The SDK SHALL enforce that an item's partition key value is immutable.

**When** a Replace or Upsert operation is performed, the SDK SHALL NOT allow changing the item's partition key value. To change an item's partition key, the item SHALL be deleted and recreated with the desired key.

### Requirement: Supported Value Types

The SDK SHALL support the following partition key value types.

| Type | Constructor | Notes |
|------|------------|-------|
| `string` | `new PartitionKey("value")` | Most common |
| `bool` | `new PartitionKey(true)` | Boolean partition keys |
| `double` | `new PartitionKey(42.0)` | All numeric types as double |
| `null` | `new PartitionKey((string)null)` | Creates `PartitionKey.Null` |

### Requirement: Equality and Hashing

The SDK SHALL support value-based equality for `PartitionKey`.

**When** two `PartitionKey` instances have the same value, the SDK SHALL consider them equal via `==`, `!=`, `Equals()`, and `GetHashCode()`, regardless of how they were constructed.

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