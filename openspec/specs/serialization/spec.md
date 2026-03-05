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

## Behavioral Invariants

### Serializer Contract

1. **`FromStream<T>` must dispose the stream**: The serializer implementation is responsible for disposing the input stream, including on exceptions. The SDK validates this and throws `InvalidOperationException` if the stream is still readable after `FromStream` returns.
2. **`ToStream<T>` must return a readable stream**: The returned stream must have `CanRead = true` and position at 0. The SDK validates this and throws `InvalidOperationException` if the stream is null or not readable.
3. **Stream pass-through**: If `T` is `Stream`, the input stream is returned directly without deserialization.
4. **Empty stream handling**: Returns `default(T)` for empty seekable streams.

### Serializer Routing

The SDK maintains separate serializers for different type categories:

| Type Category | Serializer Used | Examples |
|--------------|-----------------|---------|
| User types | Custom/configured serializer | Application POCOs, `dynamic`, `Document` |
| SDK internal types | Always default (JSON.NET) | `DatabaseProperties`, `ContainerProperties`, `ThroughputProperties` |
| `PatchSpec` | `PatchOperationsSerializer` | Patch operation payloads |
| `SqlQuerySpec` | `SqlQuerySpecSerializer` | Query definitions with parameters |

**Key rule**: Custom serializers are NEVER used for SDK internal types. This ensures SDK resource management works correctly regardless of custom serializer behavior.

### Configuration Options

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

### LINQ Property Name Translation

1. `CosmosLinqSerializer.SerializeMemberName(MemberInfo)` is called during LINQ-to-SQL translation to determine the JSON property name for `SELECT`, `WHERE`, and `ORDER BY` clauses.
2. **System.Text.Json serializer** respects `[JsonPropertyName]` and `JsonSerializerOptions.PropertyNamingPolicy`.
3. **Newtonsoft.Json serializer** respects `[JsonProperty]` attribute and contract resolver naming.
4. Custom `CosmosLinqSerializer` implementations must use `PropertyNamingPolicy = Default` (validated internally).

### Operations Using Serialization

**Typed APIs (use serializer)**:
- `CreateItemAsync<T>`, `ReadItemAsync<T>`, `ReplaceItemAsync<T>`, `UpsertItemAsync<T>`
- `GetItemQueryIterator<T>`, `GetItemLinqQueryable<T>`
- `TransactionalBatch` typed operations
- Stored procedure parameter serialization

**Stream APIs (bypass serializer)**:
- `CreateItemStreamAsync`, `ReadItemStreamAsync`, `ReplaceItemStreamAsync`
- `GetItemQueryStreamIterator`, `GetChangeFeedStreamIterator`

### Partition Key Extraction

When auto-extracting partition key from typed items (e.g., `CreateItemAsync<T>(item, partitionKey: null)`):
1. The item is serialized using the configured serializer.
2. Partition key extraction always uses `JToken.FromObject()` (Newtonsoft) on the deserialized object.
3. This means partition key paths must be deserializable via `JToken.FromObject()` even when using a custom serializer.

### Max Depth Protection

The default JSON.NET serializer sets `MaxDepth = 64` to prevent denial-of-service attacks via deeply nested JSON (GHSA-5crp-9r3c-p9vr).

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
