# Serialization

## Purpose

The SDK's serialization layer handles conversion between .NET objects and JSON for Cosmos DB operations, supporting multiple JSON libraries and custom serializers.

## Requirements

### Requirement: Default Serializer
The SDK SHALL use Newtonsoft.Json (JSON.NET) as the default serializer.

#### Scenario: Default behavior
- GIVEN no custom serializer is configured
- WHEN items are created, read, or queried
- THEN `CosmosJsonDotNetSerializer` is used for serialization and deserialization

### Requirement: Custom Serializer
The SDK SHALL support plugging in a custom serializer that extends `CosmosSerializer`.

#### Scenario: Custom serializer via options
- GIVEN `CosmosClientOptions.Serializer` is set to a custom `CosmosSerializer` implementation
- WHEN items are serialized or deserialized
- THEN the custom serializer's `ToStream<T>()` and `FromStream<T>()` methods are used

#### Scenario: CosmosSerializer contract
- GIVEN a class extending `CosmosSerializer`
- WHEN it is provided to the client
- THEN it MUST implement `Stream ToStream<T>(T input)` and `T FromStream<T>(Stream stream)`

### Requirement: System.Text.Json Support
The SDK SHALL support System.Text.Json as an alternative serializer.

#### Scenario: Enable System.Text.Json
- GIVEN `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions` is set to a `JsonSerializerOptions` instance
- WHEN items are serialized or deserialized
- THEN `CosmosSystemTextJsonSerializer` is used

### Requirement: Serialization Options
The SDK SHALL support configuring serialization behavior via `CosmosSerializationOptions`.

#### Scenario: Ignore null values
- GIVEN `CosmosClientOptions.SerializerOptions = new CosmosSerializationOptions { IgnoreNullValues = true }`
- WHEN an item with null properties is serialized
- THEN null properties are omitted from the JSON output

#### Scenario: Indented output
- GIVEN `CosmosSerializationOptions.Indented = true`
- WHEN an item is serialized
- THEN the JSON output is formatted with indentation

#### Scenario: Camel case property names
- GIVEN `CosmosSerializationOptions.PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase`
- WHEN an item with PascalCase property names is serialized
- THEN property names are converted to camelCase in the JSON output

### Requirement: Mutual Exclusivity of Serializer Settings
The SDK SHALL enforce that only one serializer configuration is active.

#### Scenario: Multiple serializer settings
- GIVEN two or more of `Serializer`, `SerializerOptions`, and `UseSystemTextJsonSerializerWithOptions` are set
- WHEN the client is constructed
- THEN an `ArgumentException` is thrown

### Requirement: LINQ Serialization Support
The SDK SHALL support custom member name resolution for LINQ queries via `CosmosLinqSerializer`.

#### Scenario: LINQ with custom serializer
- GIVEN a `CosmosLinqSerializer` implementation that maps `MyProperty` to `my_property`
- WHEN a LINQ query references `x.MyProperty`
- THEN the generated SQL uses `c["my_property"]`

### Requirement: Stream Operations
The SDK SHALL support stream-based operations that bypass serialization.

#### Scenario: Stream create
- GIVEN `Container.CreateItemStreamAsync(stream, partitionKey)` is called
- WHEN the operation is processed
- THEN the raw stream is sent directly to the service without SDK-side serialization

#### Scenario: Stream read
- GIVEN `Container.ReadItemStreamAsync(id, partitionKey)` is called
- WHEN the response is received
- THEN a `ResponseMessage` is returned with the raw JSON stream
- AND no deserialization is performed

### Requirement: Internal vs User Serialization
The SDK SHALL separate internal serialization (system properties, metadata) from user content serialization.

#### Scenario: System properties
- GIVEN an item response from the service
- WHEN system properties (`id`, `_rid`, `_ts`, `_etag`, `_self`) are deserialized
- THEN the SDK uses its internal serializer regardless of the user-configured serializer

#### Scenario: User content
- GIVEN an item response from the service
- WHEN the user's item content is deserialized
- THEN the user-configured serializer is used

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosSerializer.cs` — abstract base serializer
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosJsonDotNetSerializer.cs` — default JSON.NET serializer
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosSystemTextJsonSerializer.cs` — System.Text.Json serializer
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosSerializerCore.cs` — core serialization logic
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosLinqSerializer.cs` — LINQ member name resolution
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — serializer configuration properties
