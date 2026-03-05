# Serialization

## Purpose

The SDK's serialization layer handles conversion between .NET objects and JSON for Cosmos DB operations, supporting multiple JSON libraries and custom serializers.

## Requirements

### Requirement: Default Serializer
The SDK SHALL use Newtonsoft.Json (JSON.NET) as the default serializer.

#### Default behavior
**While** no custom serializer is configured, **when** items are created, read, or queried, the SDK shall use `CosmosJsonDotNetSerializer` for serialization and deserialization.

### Requirement: Custom Serializer
The SDK SHALL support plugging in a custom serializer that extends `CosmosSerializer`.

#### Custom serializer via options
**Where** `CosmosClientOptions.Serializer` is set to a custom `CosmosSerializer` implementation, **when** items are serialized or deserialized, the SDK shall use the custom serializer's `ToStream<T>()` and `FromStream<T>()` methods.

#### CosmosSerializer contract
**When** a class extending `CosmosSerializer` is provided to the client, the SDK shall require it to implement `Stream ToStream<T>(T input)` and `T FromStream<T>(Stream stream)`.

### Requirement: System.Text.Json Support
The SDK SHALL support System.Text.Json as an alternative serializer.

#### Enable System.Text.Json
**Where** `CosmosClientOptions.UseSystemTextJsonSerializerWithOptions` is set to a `JsonSerializerOptions` instance, **when** items are serialized or deserialized, the SDK shall use `CosmosSystemTextJsonSerializer`.

### Requirement: Serialization Options
The SDK SHALL support configuring serialization behavior via `CosmosSerializationOptions`.

#### Ignore null values
**Where** `CosmosClientOptions.SerializerOptions = new CosmosSerializationOptions { IgnoreNullValues = true }`, **when** an item with null properties is serialized, the SDK shall omit null properties from the JSON output.

#### Indented output
**Where** `CosmosSerializationOptions.Indented = true`, **when** an item is serialized, the SDK shall format the JSON output with indentation.

#### Camel case property names
**Where** `CosmosSerializationOptions.PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase`, **when** an item with PascalCase property names is serialized, the SDK shall convert property names to camelCase in the JSON output.

### Requirement: Mutual Exclusivity of Serializer Settings
The SDK SHALL enforce that only one serializer configuration is active.

#### Multiple serializer settings
**If** two or more of `Serializer`, `SerializerOptions`, and `UseSystemTextJsonSerializerWithOptions` are set, **then** the SDK shall throw an `ArgumentException` when the client is constructed.

### Requirement: LINQ Serialization Support
The SDK SHALL support custom member name resolution for LINQ queries via `CosmosLinqSerializer`.

#### LINQ with custom serializer
**Where** a `CosmosLinqSerializer` implementation maps `MyProperty` to `my_property`, **when** a LINQ query references `x.MyProperty`, the SDK shall generate SQL using `c["my_property"]`.

### Requirement: Stream Operations
The SDK SHALL support stream-based operations that bypass serialization.

#### Stream create
**When** `Container.CreateItemStreamAsync(stream, partitionKey)` is called, the SDK shall send the raw stream directly to the service without SDK-side serialization.

#### Stream read
**When** `Container.ReadItemStreamAsync(id, partitionKey)` is called, the SDK shall return a `ResponseMessage` with the raw JSON stream and perform no deserialization.

### Requirement: Internal vs User Serialization
The SDK SHALL separate internal serialization (system properties, metadata) from user content serialization.

#### System properties
**When** system properties (`id`, `_rid`, `_ts`, `_etag`, `_self`) are deserialized from an item response, the SDK shall use its internal serializer regardless of the user-configured serializer.

#### User content
**When** the user's item content is deserialized from an item response, the SDK shall use the user-configured serializer.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosSerializer.cs` — abstract base serializer
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosJsonDotNetSerializer.cs` — default JSON.NET serializer
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosSystemTextJsonSerializer.cs` — System.Text.Json serializer
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosSerializerCore.cs` — core serialization logic
- `Microsoft.Azure.Cosmos/src/Serializer/CosmosLinqSerializer.cs` — LINQ member name resolution
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — serializer configuration properties
