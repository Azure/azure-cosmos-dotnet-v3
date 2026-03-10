# Serialization

## Purpose

The Azure Cosmos DB .NET SDK serializes and deserializes documents for all typed API operations. It supports three serialization backends: Newtonsoft JSON.NET (default), System.Text.Json, and custom implementations via the `CosmosSerializer` abstract class. The serializer choice affects typed CRUD operations, query result deserialization, and LINQ-to-SQL property name translation. Stream APIs bypass serialization entirely.

## Public API Surface

### CosmosSerializer (Abstract Base)

```csharp
public abstract class CosmosSerializer
{
    public abstract T FromStream<T>(Stream stream);   // Deserialize; MUST dispose stream
    public abstract Stream ToStream<T>(T input);      // Serialize; returns readable stream
}
```

### CosmosLinqSerializer (LINQ-Aware Extension)

```csharp
public abstract class CosmosLinqSerializer : CosmosSerializer
{
    public abstract string SerializeMemberName(MemberInfo memberInfo);  // Property name for LINQ queries
}
```

### Registration (Mutually Exclusive)

```csharp
// Option 1: Newtonsoft.Json configuration
CosmosClientOptions options = new()
{
    SerializerOptions = new CosmosSerializationOptions
    {
        IgnoreNullValues = true,
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    }
};

// Option 2: Custom serializer
CosmosClientOptions options = new() { Serializer = new MyCustomSerializer() };

// Option 3: System.Text.Json
CosmosClientOptions options = new()
{
    UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    }
};
```

## Requirements

### Requirement: Serializer Contract

**EARS-SC-1 (FromStream dispose):** When `FromStream<T>` is called on a serializer implementation, the serializer shall dispose the input stream before returning, including on exceptions. If the stream is still readable after `FromStream` returns, the SDK shall throw `InvalidOperationException`.

**EARS-SC-2 (ToStream readable):** When `ToStream<T>` is called on a serializer implementation, the serializer shall return a stream with `CanRead = true` and position at 0. If the returned stream is null or not readable, the SDK shall throw `InvalidOperationException`.

**EARS-SC-3 (Stream pass-through):** When `T` is `Stream`, the SDK shall return the input stream directly without deserialization.

**EARS-SC-4 (Empty stream):** When the input stream is empty and seekable, the SDK shall return `default(T)`.

### Requirement: Serializer Routing

The SDK maintains separate serializers for different type categories:

| Type Category | Serializer Used | Examples |
|--------------|-----------------|---------|
| User types | Custom/configured serializer | Application POCOs, `dynamic`, `Document` |
| SDK internal types | Always default (JSON.NET) | `DatabaseProperties`, `ContainerProperties`, `ThroughputProperties` |
| `PatchSpec` | `PatchOperationsSerializer` | Patch operation payloads |
| `SqlQuerySpec` | `SqlQuerySpecSerializer` | Query definitions with parameters |

**EARS-SR-1 (User type routing):** When a typed API operation targets a user-defined type (application POCOs, `dynamic`, `Document`), the SDK shall use the custom or configured serializer.

**EARS-SR-2 (Internal type routing):** When a typed API operation targets an SDK internal type (`DatabaseProperties`, `ContainerProperties`, `ThroughputProperties`), the SDK shall always use the default JSON.NET serializer, regardless of any custom serializer configured by the user. This ensures SDK resource management works correctly regardless of custom serializer behavior.

**EARS-SR-3 (PatchSpec routing):** When serializing patch operation payloads, the SDK shall use `PatchOperationsSerializer`.

**EARS-SR-4 (SqlQuerySpec routing):** When serializing query definitions with parameters, the SDK shall use `SqlQuerySpecSerializer`.

### Requirement: Serialization Configuration

#### CosmosSerializationOptions (Newtonsoft.Json)

| Property | Type | Default | Maps To |
|----------|------|---------|---------|
| `IgnoreNullValues` | `bool` | `false` | `NullValueHandling.Ignore` / `.Include` |
| `Indented` | `bool` | `false` | `Formatting.Indented` / `.None` |
| `PropertyNamingPolicy` | `CosmosPropertyNamingPolicy` | `Default` | `CamelCasePropertyNamesContractResolver` |

#### CosmosPropertyNamingPolicy

| Value | Effect |
|-------|--------|
| `Default` | No transformation — property names used as-is |
| `CamelCase` | First letter lowercased: `PropertyName` → `propertyName` |

**EARS-CF-1 (Null value handling):** When `IgnoreNullValues` is set to `true`, the Newtonsoft.Json serializer shall use `NullValueHandling.Ignore`; when set to `false`, it shall use `NullValueHandling.Include`.

**EARS-CF-2 (Indentation):** When `Indented` is set to `true`, the Newtonsoft.Json serializer shall use `Formatting.Indented`; when set to `false`, it shall use `Formatting.None`.

**EARS-CF-3 (Camel case naming):** When `PropertyNamingPolicy` is set to `CamelCase`, the Newtonsoft.Json serializer shall use `CamelCasePropertyNamesContractResolver`, lowercasing the first letter of property names (`PropertyName` → `propertyName`). When set to `Default`, property names shall be used as-is.

### Requirement: LINQ Property Name Translation

**EARS-LQ-1 (Member name resolution):** When the SDK translates a LINQ expression to SQL, it shall call `CosmosLinqSerializer.SerializeMemberName(MemberInfo)` to determine the JSON property name for `SELECT`, `WHERE`, and `ORDER BY` clauses.

**EARS-LQ-2 (System.Text.Json attributes):** When the System.Text.Json serializer is configured, the LINQ translator shall respect `[JsonPropertyName]` attributes and `JsonSerializerOptions.PropertyNamingPolicy`.

**EARS-LQ-3 (Newtonsoft.Json attributes):** When the Newtonsoft.Json serializer is configured, the LINQ translator shall respect `[JsonProperty]` attributes and contract resolver naming.

**EARS-LQ-4 (Custom LINQ serializer naming policy):** When a custom `CosmosLinqSerializer` implementation is registered, the SDK shall validate that `PropertyNamingPolicy` is set to `Default` and reject other values.

### Requirement: Serialization Scope

**Typed APIs (use serializer)**:
- `CreateItemAsync<T>`, `ReadItemAsync<T>`, `ReplaceItemAsync<T>`, `UpsertItemAsync<T>`
- `GetItemQueryIterator<T>`, `GetItemLinqQueryable<T>`
- `TransactionalBatch` typed operations
- Stored procedure parameter serialization

**Stream APIs (bypass serializer)**:
- `CreateItemStreamAsync`, `ReadItemStreamAsync`, `ReplaceItemStreamAsync`
- `GetItemQueryStreamIterator`, `GetChangeFeedStreamIterator`

**EARS-SS-1 (Typed API serialization):** When a typed API operation (`CreateItemAsync<T>`, `ReadItemAsync<T>`, `ReplaceItemAsync<T>`, `UpsertItemAsync<T>`, `GetItemQueryIterator<T>`, `GetItemLinqQueryable<T>`, `TransactionalBatch` typed operations, stored procedure parameter serialization) is invoked, the SDK shall use the configured serializer to serialize and/or deserialize the payload.

**EARS-SS-2 (Stream API bypass):** When a stream API operation (`CreateItemStreamAsync`, `ReadItemStreamAsync`, `ReplaceItemStreamAsync`, `GetItemQueryStreamIterator`, `GetChangeFeedStreamIterator`) is invoked, the SDK shall bypass the serializer entirely and pass the raw stream.

### Requirement: Partition Key Extraction

**EARS-PK-1 (Auto-extraction serialization):** When auto-extracting a partition key from a typed item (e.g., `CreateItemAsync<T>(item, partitionKey: null)`), the SDK shall first serialize the item using the configured serializer.

**EARS-PK-2 (JToken extraction):** When extracting the partition key value after serialization, the SDK shall always use `JToken.FromObject()` (Newtonsoft) on the deserialized object, regardless of the configured serializer.

**EARS-PK-3 (Cross-serializer compatibility):** Because partition key extraction always uses `JToken.FromObject()`, partition key paths shall be deserializable via `JToken.FromObject()` even when using a custom serializer.

### Requirement: Max Depth Protection

**EARS-MD-1 (Depth limit):** The default JSON.NET serializer shall set `MaxDepth = 64` to prevent denial-of-service attacks via deeply nested JSON (GHSA-5crp-9r3c-p9vr).

## Interactions

- **CRUD Operations**: All typed CRUD operations use the configured serializer. See `crud-operations` spec.
- **Query**: `FeedIterator<T>` deserializes results; LINQ uses `SerializeMemberName` for property translation. See `query-and-linq` spec.
- **Client Configuration**: Serializer is configured via `CosmosClientOptions`. See `client-and-configuration` spec.
- **Partition Keys**: Auto-extraction depends on serialization. See `partition-keys` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/Serializer/CosmosSerializer.cs`
- Source: `Microsoft.Azure.Cosmos/src/Serializer/CosmosLinqSerializer.cs`
- Source: `Microsoft.Azure.Cosmos/src/Serializer/CosmosJsonDotNetSerializer.cs`
- Source: `Microsoft.Azure.Cosmos/src/Serializer/CosmosSystemTextJsonSerializer.cs`
- Source: `Microsoft.Azure.Cosmos/src/Serializer/CosmosSerializerCore.cs`
- Source: `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs`