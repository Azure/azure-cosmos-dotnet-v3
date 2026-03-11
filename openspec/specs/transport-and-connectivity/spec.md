# Transport and Connectivity

## Purpose

The SDK supports two transport modes — Gateway (HTTPS) and Direct (TCP) — with configurable connection management, pooling, and endpoint discovery.

## Requirements

### Requirement: Connection Mode Selection
The SDK SHALL support two connection modes with distinct transport characteristics.

#### Gateway mode
**Where** `CosmosClientOptions.ConnectionMode = ConnectionMode.Gateway`, **when** requests are made, the SDK shall route all requests through the Cosmos DB gateway proxy via HTTPS (port 443) and force the protocol to HTTPS regardless of other settings.

#### Direct mode (default)
**Where** `CosmosClientOptions.ConnectionMode = ConnectionMode.Direct` (or not specified, as Direct is the default), **when** requests are made, the SDK shall use direct TCP connections to backend replicas for data-plane requests and use the gateway for metadata requests.

### Requirement: Gateway Mode Configuration
The SDK SHALL support configuring HTTP connection behavior for Gateway mode.

#### Max connection limit
**Where** `CosmosClientOptions.GatewayModeMaxConnectionLimit = 100`, **when** multiple concurrent requests are made in Gateway mode, the SDK shall maintain at most 100 simultaneous HTTP connections (default: 50).

#### Custom HttpClient
**Where** `CosmosClientOptions.HttpClientFactory` is set, **when** the SDK creates HTTP connections, the SDK shall use the provided HttpClient factory and ignore `GatewayModeMaxConnectionLimit` and `WebProxy` settings.

#### Web proxy
**Where** `CosmosClientOptions.WebProxy` is set, **when** requests are made in Gateway mode, the SDK shall route HTTP traffic through the specified proxy.

### Requirement: Direct Mode TCP Configuration
The SDK SHALL support fine-grained TCP connection tuning for Direct mode.

#### Idle connection timeout
**Where** `CosmosClientOptions.IdleTcpConnectionTimeout = TimeSpan.FromMinutes(20)`, **when** a TCP connection is idle for 20 minutes, the SDK shall close it to free resources (minimum: 10 minutes).

#### Connection open timeout
**Where** `CosmosClientOptions.OpenTcpConnectionTimeout = TimeSpan.FromSeconds(10)`, **when** establishing a new TCP connection, the SDK shall time out the connection attempt after 10 seconds (default: 5 seconds).

#### Max requests per connection
**Where** `CosmosClientOptions.MaxRequestsPerTcpConnection = 50`, **when** concurrent requests are sent over TCP, the SDK shall multiplex at most 50 requests per TCP connection (default: 30).

#### Max connections per endpoint
**Where** `CosmosClientOptions.MaxTcpConnectionsPerEndpoint = 32`, **when** connecting to a backend node, the SDK shall establish at most 32 TCP connections to that endpoint.

#### Port reuse mode
**Where** `CosmosClientOptions.PortReuseMode` is set, **when** new TCP connections are created, the SDK shall apply the specified port reuse strategy.

### Requirement: Request Timeout
The SDK SHALL enforce a configurable timeout for individual requests.

#### Default timeout
**While** no timeout is configured, **when** a request is made, the SDK shall time out after 6 seconds (default `RequestTimeout`).

#### Custom timeout
**Where** `CosmosClientOptions.RequestTimeout = TimeSpan.FromSeconds(15)`, **when** a request exceeds 15 seconds, the SDK shall throw a `CosmosException` with status 408 (RequestTimeout).

### Requirement: Endpoint Discovery
The SDK SHALL automatically discover and connect to available service endpoints.

#### Automatic region discovery
**Where** `CosmosClientOptions.LimitToEndpoint = false` (default), **when** the client is initialized, the SDK shall query the account for available regions and populate the endpoint cache.

#### Limit to single endpoint
**Where** `CosmosClientOptions.LimitToEndpoint = true`, **when** the client is initialized, the SDK shall use only the provided endpoint URI, perform no region discovery, and require that `ApplicationRegion` and `ApplicationPreferredRegions` are not set.

#### TCP connection endpoint rediscovery
**Where** `CosmosClientOptions.EnableTcpConnectionEndpointRediscovery = true` (default), **when** a TCP connection is reset, the SDK shall refresh the endpoint address cache and establish connections to newly discovered endpoints.

### Requirement: Custom Initialization Endpoints
The SDK SHALL support custom endpoints for account initialization in geo-failover scenarios.

#### Custom init endpoints
**Where** `CosmosClientOptions.AccountInitializationCustomEndpoints` contains fallback URIs, **if** the primary account endpoint is unreachable during initialization, **then** the SDK shall attempt to initialize from the custom endpoints.

### Requirement: SSL/TLS Configuration
The SDK SHALL support custom certificate validation for Direct mode.

#### Custom certificate validation
**Where** `CosmosClientOptions.ServerCertificateCustomValidationCallback` is set, **when** a TLS connection is established, the SDK shall invoke the custom callback for certificate validation.

### Requirement: Mutual Exclusivity of Settings
The SDK SHALL enforce that conflicting connection settings cannot be used together.

#### WebProxy with HttpClientFactory
**If** both `WebProxy` and `HttpClientFactory` are set, **then** the SDK shall throw an `ArgumentException` when the client is constructed.

#### ApplicationRegion with ApplicationPreferredRegions
**If** both `ApplicationRegion` and `ApplicationPreferredRegions` are set, **then** the SDK shall throw an `ArgumentException` when the client is constructed.

#### LimitToEndpoint with regions
**If** `LimitToEndpoint = true` and either region setting is configured, **then** the SDK shall throw an `ArgumentException` when the client is constructed.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — all connection configuration
- `Microsoft.Azure.Cosmos/src/GatewayStoreModel.cs` — gateway mode transport
- `Microsoft.Azure.Cosmos/src/Handler/TransportHandler.cs` — handler pipeline transport layer
- `Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs` — endpoint discovery and management
- `Microsoft.Azure.Cosmos/src/Routing/LocationCache.cs` — region endpoint caching
