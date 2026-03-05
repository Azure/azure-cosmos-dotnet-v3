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

## Behavioral Invariants

### Client Lifecycle

1. **Thread-safe**: `CosmosClient` is fully thread-safe and should be shared across threads.
2. **Singleton pattern**: One instance per application lifetime is recommended for optimal connection pooling and cache reuse.
3. **No network validation at construction**: Constructors perform NO network calls. Connectivity issues surface on the first operation.
4. **Immutable after construction**: `ClientOptions` are read-only after the client is created.
5. **Disposal**: Implements `IDisposable`. After disposal, all operations throw errors. `DisposedDateTimeUtc` tracks when disposal occurred.

### Connection Modes

| Aspect | Gateway (`ConnectionMode.Gateway`) | Direct (`ConnectionMode.Direct`) — Default |
|--------|-----------------------------------|-------------------------------------------|
| Protocol | HTTPS (port 443) | TCP/SSL (multiple ports) |
| Routing | Via gateway proxy | Direct to data nodes |
| Throughput | Lower | Higher |
| Latency | Higher | Lower |
| Firewall | Simple (one endpoint) | Complex (multiple ports) |
| Key options | `GatewayModeMaxConnectionLimit`, `WebProxy` | `MaxRequestsPerTcpConnection`, `MaxTcpConnectionsPerEndpoint`, `IdleTcpConnectionTimeout` |

### Region Configuration

- **`ApplicationRegion`** (single string): SDK generates proximity-ordered fallback list. Mutually exclusive with `ApplicationPreferredRegions`.
- **`ApplicationPreferredRegions`** (ordered list): Explicit failover order. Invalid regions are silently ignored but used if later added to account. Mutually exclusive with `ApplicationRegion`.
- **`LimitToEndpoint`** (bool, default `false`): When `true`, disables region auto-discovery. Incompatible with `ApplicationRegion`/`ApplicationPreferredRegions`.

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

## References

- Source: `Microsoft.Azure.Cosmos/src/CosmosClient.cs`
- Source: `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/Fluent/CosmosClientBuilder.cs`
- Source: `Microsoft.Azure.Cosmos/src/ConnectionMode.cs`
