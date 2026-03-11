# Transport and Connectivity

## Purpose

The Azure Cosmos DB .NET SDK supports two transport modes — Gateway (HTTPS) and Direct (TCP) — with configurable connection management, pooling, and endpoint discovery. The transport layer is the bottom of the handler pipeline stack and is responsible for converting `RequestMessage` objects into network calls to the Cosmos DB service.

## Public API Surface

### Connection Mode Configuration

```csharp
// Gateway mode (HTTPS through Cosmos DB gateway proxy)
CosmosClientOptions gatewayOptions = new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Gateway,
    GatewayModeMaxConnectionLimit = 100
};

// Direct mode (TCP to backend replicas — default)
CosmosClientOptions directOptions = new CosmosClientOptions
{
    ConnectionMode = ConnectionMode.Direct,
    IdleTcpConnectionTimeout = TimeSpan.FromMinutes(20),
    OpenTcpConnectionTimeout = TimeSpan.FromSeconds(10),
    MaxRequestsPerTcpConnection = 50,
    MaxTcpConnectionsPerEndpoint = 32
};
```

### Endpoint and Region Configuration

```csharp
CosmosClientOptions options = new CosmosClientOptions
{
    ApplicationPreferredRegions = new List<string> { "East US", "West US" },
    // OR: ApplicationRegion = "East US",
    LimitToEndpoint = false,  // default — enables region discovery
    EnableTcpConnectionEndpointRediscovery = true,  // default
    AccountInitializationCustomEndpoints = new HashSet<Uri>
    {
        new Uri("https://fallback-endpoint.documents.azure.com:443/")
    }
};
```

## Requirements

### Requirement: Connection Mode Selection

The SDK SHALL support two connection modes with distinct transport characteristics.

#### Gateway mode

**Where** `CosmosClientOptions.ConnectionMode = ConnectionMode.Gateway`, **when** requests are made, the SDK SHALL route all requests through the Cosmos DB gateway proxy via HTTPS (port 443) and force the protocol to HTTPS regardless of other settings.

#### Direct mode (default)

**Where** `CosmosClientOptions.ConnectionMode = ConnectionMode.Direct` (or not specified), **when** requests are made, the SDK SHALL use direct TCP connections to backend replicas for data-plane requests and use the gateway for metadata requests.

### Requirement: Gateway Mode Configuration

The SDK SHALL support configuring HTTP connection behavior for Gateway mode.

#### Max connection limit

**Where** `CosmosClientOptions.GatewayModeMaxConnectionLimit` is set (default: 50), **when** multiple concurrent requests are made in Gateway mode, the SDK SHALL maintain at most that many simultaneous HTTP connections.

#### Custom HttpClient

**Where** `CosmosClientOptions.HttpClientFactory` is set, **when** the SDK creates HTTP connections, the SDK SHALL use the provided HttpClient factory and ignore `GatewayModeMaxConnectionLimit` and `WebProxy` settings.

#### Web proxy

**Where** `CosmosClientOptions.WebProxy` is set, **when** requests are made in Gateway mode, the SDK SHALL route HTTP traffic through the specified proxy.

### Requirement: Direct Mode TCP Configuration

The SDK SHALL support fine-grained TCP connection tuning for Direct mode.

| Property | Default | Minimum | Description |
|----------|---------|---------|-------------|
| `IdleTcpConnectionTimeout` | — | 10 minutes | Close idle TCP connections after this duration |
| `OpenTcpConnectionTimeout` | 5 seconds | — | Timeout for establishing new TCP connections |
| `MaxRequestsPerTcpConnection` | 30 | — | Max multiplexed requests per TCP connection |
| `MaxTcpConnectionsPerEndpoint` | — | — | Max TCP connections to a single backend node |
| `PortReuseMode` | — | — | TCP port reuse strategy |

### Requirement: Request Timeout

The SDK SHALL enforce a configurable timeout for individual requests.

#### Default timeout

**While** no timeout is configured, **when** a request is made, the SDK SHALL time out after 6 seconds (default `RequestTimeout`).

#### Custom timeout

**Where** `CosmosClientOptions.RequestTimeout` is set, **when** a request exceeds the configured duration, the SDK SHALL throw a `CosmosException` with status 408 (RequestTimeout).

### Requirement: Endpoint Discovery

The SDK SHALL automatically discover and connect to available service endpoints.

#### Automatic region discovery

**Where** `CosmosClientOptions.LimitToEndpoint = false` (default), **when** the client is initialized, the SDK SHALL query the account for available regions and populate the endpoint cache.

#### Limit to single endpoint

**Where** `CosmosClientOptions.LimitToEndpoint = true`, **when** the client is initialized, the SDK SHALL use only the provided endpoint URI, perform no region discovery, and require that `ApplicationRegion` and `ApplicationPreferredRegions` are not set.

#### TCP connection endpoint rediscovery

**Where** `CosmosClientOptions.EnableTcpConnectionEndpointRediscovery = true` (default), **when** a TCP connection is reset, the SDK SHALL refresh the endpoint address cache and establish connections to newly discovered endpoints.

### Requirement: Custom Initialization Endpoints

The SDK SHALL support custom endpoints for account initialization in geo-failover scenarios.

#### Custom init endpoints

**Where** `CosmosClientOptions.AccountInitializationCustomEndpoints` contains fallback URIs, **if** the primary account endpoint is unreachable during initialization, **then** the SDK SHALL attempt to initialize from the custom endpoints.

### Requirement: SSL/TLS Configuration

The SDK SHALL support custom certificate validation for Direct mode.

#### Custom certificate validation

**Where** `CosmosClientOptions.ServerCertificateCustomValidationCallback` is set, **when** a TLS connection is established, the SDK SHALL invoke the custom callback for certificate validation.

### Requirement: Mutual Exclusivity of Settings

The SDK SHALL enforce that conflicting connection settings cannot be used together.

| Conflict | Behavior |
|----------|----------|
| `WebProxy` + `HttpClientFactory` | `ArgumentException` at client construction |
| `ApplicationRegion` + `ApplicationPreferredRegions` | `ArgumentException` at client construction |
| `LimitToEndpoint = true` + either region setting | `ArgumentException` at client construction |

## Interactions

- **Handler Pipeline**: `TransportHandler` is the leaf handler that invokes `GatewayStoreModel` (Gateway) or `ServerStoreModel` (Direct). See `handler-pipeline` spec.
- **Retry Policies**: Transport-level errors (timeouts, connection resets) trigger retry policies. See `retry-and-failover` spec.
- **Cross-Region Hedging**: Hedged requests may target different regional endpoints. See `cross-region-hedging` spec.
- **Diagnostics**: Transport-level statistics are captured in `ClientSideRequestStatisticsTraceDatum`. See `diagnostics-and-observability` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/GatewayStoreModel.cs`
- Source: `Microsoft.Azure.Cosmos/src/Handler/TransportHandler.cs`
- Source: `Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs`
- Source: `Microsoft.Azure.Cosmos/src/Routing/LocationCache.cs`