# Client and Configuration

## Purpose

`CosmosClient` is the entry point for all interactions with Azure Cosmos DB. It manages connections, caches, and configuration. The SDK is designed for a single long-lived `CosmosClient` instance per application (singleton pattern) to maximize connection pooling and cache reuse. Configuration is immutable after construction.

## Public API Surface

### CosmosClient Constructors

```csharp
// Connection string
public CosmosClient(string connectionString, CosmosClientOptions clientOptions = null)

// Endpoint + key/resource token
public CosmosClient(string accountEndpoint, string authKeyOrResourceToken, CosmosClientOptions clientOptions = null)

// Endpoint + rotatable credential
public CosmosClient(string accountEndpoint, AzureKeyCredential authKeyOrResourceTokenCredential, CosmosClientOptions clientOptions = null)

// Endpoint + AAD token
public CosmosClient(string accountEndpoint, TokenCredential tokenCredential, CosmosClientOptions clientOptions = null)
```

### CosmosClientBuilder (Fluent API)

```csharp
CosmosClient client = new CosmosClientBuilder("connection-string")
    .WithApplicationPreferredRegions(new List<string> { "East US", "West US" })
    .WithConnectionModeDirect()
    .WithThrottlingRetryOptions(maxWaitTime: TimeSpan.FromSeconds(30), maxAttempts: 9)
    .WithBulkExecution(true)
    .Build();

// Or with pre-warming:
CosmosClient client = await new CosmosClientBuilder("connection-string")
    .BuildAndInitializeAsync(new[] { ("myDb", "myContainer") });
```

### Resource References

```csharp
Database db = cosmosClient.GetDatabase("myDb");           // No network call
Container container = cosmosClient.GetContainer("myDb", "myContainer"); // No network call
```

These return proxy references — they do NOT validate existence. Use `CreateDatabaseIfNotExistsAsync` / `CreateContainerIfNotExistsAsync` to ensure resources exist.

## Requirements

### Requirement: Client Lifecycle

The SDK SHALL manage `CosmosClient` as a thread-safe, long-lived singleton.

#### Thread safety

**When** multiple threads access a `CosmosClient` instance concurrently, the SDK SHALL handle all operations safely without requiring external synchronization.

#### Singleton pattern

**When** creating a `CosmosClient`, the SDK SHALL optimize for a single instance per application lifetime to maximize connection pooling and cache reuse.

#### No network validation at construction

**When** a `CosmosClient` is constructed, the SDK SHALL NOT perform any network calls. Connectivity issues SHALL surface on the first operation.

#### Immutable after construction

**When** a `CosmosClient` is constructed, the SDK SHALL treat `ClientOptions` as read-only. Modifications after construction SHALL NOT be possible.

#### Disposal behavior

**When** a `CosmosClient` is disposed, all subsequent operations SHALL throw errors. The SDK SHALL track disposal via `DisposedDateTimeUtc`.

### Requirement: Connection Modes

The SDK SHALL support two connection modes with distinct characteristics.

| Aspect | Gateway (`ConnectionMode.Gateway`) | Direct (`ConnectionMode.Direct`) - Default |
|--------|-----------------------------------|-------------------------------------------|
| Protocol | HTTPS (port 443) | TCP/SSL (multiple ports) |
| Routing | Via gateway proxy | Direct to data nodes |
| Throughput | Lower | Higher |
| Latency | Higher | Lower |
| Firewall | Simple (one endpoint) | Complex (multiple ports) |
| Key options | `GatewayModeMaxConnectionLimit`, `WebProxy` | `MaxRequestsPerTcpConnection`, `MaxTcpConnectionsPerEndpoint`, `IdleTcpConnectionTimeout` |

### Requirement: Region Configuration

The SDK SHALL support configuring preferred regions for request routing.

#### ApplicationRegion

**Where** `CosmosClientOptions.ApplicationRegion` is set (single string), **when** the client initializes, the SDK SHALL generate a proximity-ordered fallback list. This setting SHALL be mutually exclusive with `ApplicationPreferredRegions`.

#### ApplicationPreferredRegions

**Where** `CosmosClientOptions.ApplicationPreferredRegions` is set (ordered list), **when** requests are routed, the SDK SHALL follow the explicit failover order. Invalid regions SHALL be silently ignored but used if later added to the account.

#### LimitToEndpoint

**Where** `CosmosClientOptions.LimitToEndpoint = true`, **when** the client initializes, the SDK SHALL disable region auto-discovery. This setting SHALL be incompatible with `ApplicationRegion`/`ApplicationPreferredRegions`.

### Requirement: Proxy Reference Semantics

The SDK SHALL return lightweight proxy references for database and container access.

**When** `GetDatabase()` or `GetContainer()` is called, the SDK SHALL return a reference without making network calls. Operations on non-existent resources SHALL return 404.

## Configuration

### CosmosClientOptions Key Properties

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `ConnectionMode` | `ConnectionMode` | `Direct` | Gateway or Direct |
| `ApplicationRegion` | `string` | `null` | Single preferred region |
| `ApplicationPreferredRegions` | `IReadOnlyList<string>` | `null` | Ordered region list |
| `LimitToEndpoint` | `bool` | `false` | Disable region discovery |
| `ConsistencyLevel` | `ConsistencyLevel?` | `null` | Can only weaken account default |
| `MaxRetryAttemptsOnRateLimitedRequests` | `int?` | 9 | HTTP 429 retry attempts |
| `MaxRetryWaitTimeOnRateLimitedRequests` | `TimeSpan?` | 30 seconds | Max cumulative retry wait |
| `AllowBulkExecution` | `bool` | `false` | Automatic request batching |
| `EnableContentResponseOnWrite` | `bool?` | `null` | Skip response payload on writes |
| `RequestTimeout` | `TimeSpan` | 6 seconds | Per-request timeout |
| `GatewayModeMaxConnectionLimit` | `int` | 50 | Gateway HTTP connection pool |
| `MaxRequestsPerTcpConnection` | `int?` | 30 | Direct: concurrent requests per TCP connection |
| `MaxTcpConnectionsPerEndpoint` | `int?` | 65,535 | Direct: max TCP connections per backend |
| `IdleTcpConnectionTimeout` | `TimeSpan?` | indefinite | Direct: close idle connections (min 10 min) |
| `OpenTcpConnectionTimeout` | `TimeSpan?` | 5 seconds | Direct: TCP establishment timeout |
| `EnableTcpConnectionEndpointRediscovery` | `bool` | `true` | Direct: refresh addresses on TCP reset |
| `AvailabilityStrategy` | `AvailabilityStrategy` | `null` | Cross-region hedging |
| `CustomHandlers` | `Collection<RequestHandler>` | empty | Pipeline interceptors |
| `ApplicationName` | `string` | `null` | User-agent suffix |

### Serializer Configuration (Mutually Exclusive)

Only ONE of these can be set:
- `SerializerOptions` — `CosmosSerializationOptions` (Newtonsoft.Json config)
- `Serializer` — `CosmosSerializer` (custom implementation)
- `UseSystemTextJsonSerializerWithOptions` — `JsonSerializerOptions` (System.Text.Json)

See `serialization` spec for details.

## Interactions

- **Handler Pipeline**: Client constructs the handler pipeline at initialization. See `handler-pipeline` spec.
- **Retry Policies**: `MaxRetryAttemptsOnRateLimitedRequests` and `MaxRetryWaitTimeOnRateLimitedRequests` configure `ResourceThrottleRetryPolicy`. See `retry-and-failover` spec.
- **Hedging**: `AvailabilityStrategy` configures cross-region hedging. See `cross-region-hedging` spec.
- **Serialization**: Serializer configuration affects all typed APIs. See `serialization` spec.
- **Transport**: Connection mode and TCP settings affect transport behavior. See `transport-and-connectivity` spec.
- **Consistency**: `ConsistencyLevel` affects read guarantees. See `consistency-and-session` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/CosmosClient.cs`
- Source: `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/Fluent/CosmosClientBuilder.cs`
- Source: `Microsoft.Azure.Cosmos/src/ConnectionMode.cs`