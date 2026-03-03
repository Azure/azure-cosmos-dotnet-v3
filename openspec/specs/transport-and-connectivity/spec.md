# Transport and Connectivity

## Purpose

The SDK supports two transport modes — Gateway (HTTPS) and Direct (TCP) — with configurable connection management, pooling, and endpoint discovery.

## Requirements

### Requirement: Connection Mode Selection
The SDK SHALL support two connection modes with distinct transport characteristics.

#### Scenario: Gateway mode
- GIVEN `CosmosClientOptions.ConnectionMode = ConnectionMode.Gateway`
- WHEN requests are made
- THEN all requests are routed through the Cosmos DB gateway proxy via HTTPS (port 443)
- AND the protocol is forced to HTTPS regardless of other settings

#### Scenario: Direct mode (default)
- GIVEN `CosmosClientOptions.ConnectionMode = ConnectionMode.Direct` (or not specified, as Direct is the default)
- WHEN requests are made
- THEN data-plane requests use direct TCP connections to backend replicas
- AND metadata requests still use the gateway

### Requirement: Gateway Mode Configuration
The SDK SHALL support configuring HTTP connection behavior for Gateway mode.

#### Scenario: Max connection limit
- GIVEN `CosmosClientOptions.GatewayModeMaxConnectionLimit = 100`
- WHEN multiple concurrent requests are made in Gateway mode
- THEN at most 100 simultaneous HTTP connections are maintained (default: 50)

#### Scenario: Custom HttpClient
- GIVEN `CosmosClientOptions.HttpClientFactory` is set
- WHEN the SDK creates HTTP connections
- THEN it uses the provided HttpClient factory
- AND `GatewayModeMaxConnectionLimit` and `WebProxy` settings are ignored

#### Scenario: Web proxy
- GIVEN `CosmosClientOptions.WebProxy` is set
- WHEN requests are made in Gateway mode
- THEN HTTP traffic is routed through the specified proxy

### Requirement: Direct Mode TCP Configuration
The SDK SHALL support fine-grained TCP connection tuning for Direct mode.

#### Scenario: Idle connection timeout
- GIVEN `CosmosClientOptions.IdleTcpConnectionTimeout = TimeSpan.FromMinutes(20)`
- WHEN a TCP connection is idle for 20 minutes
- THEN it is closed to free resources (minimum: 10 minutes)

#### Scenario: Connection open timeout
- GIVEN `CosmosClientOptions.OpenTcpConnectionTimeout = TimeSpan.FromSeconds(10)`
- WHEN establishing a new TCP connection
- THEN the connection attempt times out after 10 seconds (default: 5 seconds)

#### Scenario: Max requests per connection
- GIVEN `CosmosClientOptions.MaxRequestsPerTcpConnection = 50`
- WHEN concurrent requests are sent over TCP
- THEN at most 50 requests are multiplexed per TCP connection (default: 30)

#### Scenario: Max connections per endpoint
- GIVEN `CosmosClientOptions.MaxTcpConnectionsPerEndpoint = 32`
- WHEN connecting to a backend node
- THEN at most 32 TCP connections are established to that endpoint

#### Scenario: Port reuse mode
- GIVEN `CosmosClientOptions.PortReuseMode` is set
- WHEN new TCP connections are created
- THEN the specified port reuse strategy is applied

### Requirement: Request Timeout
The SDK SHALL enforce a configurable timeout for individual requests.

#### Scenario: Default timeout
- GIVEN no timeout is configured
- WHEN a request is made
- THEN it times out after 6 seconds (default `RequestTimeout`)

#### Scenario: Custom timeout
- GIVEN `CosmosClientOptions.RequestTimeout = TimeSpan.FromSeconds(15)`
- WHEN a request exceeds 15 seconds
- THEN a `CosmosException` with status 408 (RequestTimeout) is thrown

### Requirement: Endpoint Discovery
The SDK SHALL automatically discover and connect to available service endpoints.

#### Scenario: Automatic region discovery
- GIVEN `CosmosClientOptions.LimitToEndpoint = false` (default)
- WHEN the client is initialized
- THEN the SDK queries the account for available regions
- AND populates the endpoint cache

#### Scenario: Limit to single endpoint
- GIVEN `CosmosClientOptions.LimitToEndpoint = true`
- WHEN the client is initialized
- THEN only the provided endpoint URI is used
- AND no region discovery is performed
- AND `ApplicationRegion` and `ApplicationPreferredRegions` MUST NOT be set

#### Scenario: TCP connection endpoint rediscovery
- GIVEN `CosmosClientOptions.EnableTcpConnectionEndpointRediscovery = true` (default)
- WHEN a TCP connection is reset
- THEN the SDK refreshes the endpoint address cache
- AND establishes connections to newly discovered endpoints

### Requirement: Custom Initialization Endpoints
The SDK SHALL support custom endpoints for account initialization in geo-failover scenarios.

#### Scenario: Custom init endpoints
- GIVEN `CosmosClientOptions.AccountInitializationCustomEndpoints` contains fallback URIs
- WHEN the primary account endpoint is unreachable during initialization
- THEN the SDK attempts to initialize from the custom endpoints

### Requirement: SSL/TLS Configuration
The SDK SHALL support custom certificate validation for Direct mode.

#### Scenario: Custom certificate validation
- GIVEN `CosmosClientOptions.ServerCertificateCustomValidationCallback` is set
- WHEN a TLS connection is established
- THEN the custom callback is invoked for certificate validation

### Requirement: Mutual Exclusivity of Settings
The SDK SHALL enforce that conflicting connection settings cannot be used together.

#### Scenario: WebProxy with HttpClientFactory
- GIVEN both `WebProxy` and `HttpClientFactory` are set
- WHEN the client is constructed
- THEN an `ArgumentException` is thrown

#### Scenario: ApplicationRegion with ApplicationPreferredRegions
- GIVEN both `ApplicationRegion` and `ApplicationPreferredRegions` are set
- WHEN the client is constructed
- THEN an `ArgumentException` is thrown

#### Scenario: LimitToEndpoint with regions
- GIVEN `LimitToEndpoint = true` and either region setting is configured
- WHEN the client is constructed
- THEN an `ArgumentException` is thrown

## Key Source Files
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — all connection configuration
- `Microsoft.Azure.Cosmos/src/GatewayStoreModel.cs` — gateway mode transport
- `Microsoft.Azure.Cosmos/src/Handler/TransportHandler.cs` — handler pipeline transport layer
- `Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs` — endpoint discovery and management
- `Microsoft.Azure.Cosmos/src/Routing/LocationCache.cs` — region endpoint caching
