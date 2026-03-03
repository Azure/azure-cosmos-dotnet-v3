# Client Configuration and Lifecycle

## Purpose

`CosmosClient` is the top-level entry point to the SDK. It manages connection lifecycle, configuration, custom handler pipelines, and authentication.

## Requirements

### Requirement: Client Construction
The SDK SHALL support multiple authentication methods for constructing a CosmosClient.

#### Scenario: Connection string
- GIVEN a Cosmos DB connection string
- WHEN `new CosmosClient(connectionString)` is called
- THEN the client connects using the account endpoint and key from the string

#### Scenario: Endpoint + key
- GIVEN an account endpoint URI and account key
- WHEN `new CosmosClient(endpoint, authKeyOrResourceToken)` is called
- THEN the client authenticates with the master key

#### Scenario: Endpoint + TokenCredential (AAD)
- GIVEN an account endpoint and an Azure `TokenCredential` (e.g., `DefaultAzureCredential`)
- WHEN `new CosmosClient(endpoint, tokenCredential)` is called
- THEN the client authenticates via Azure Active Directory

#### Scenario: Token credential refresh
- GIVEN `CosmosClientOptions.TokenCredentialBackgroundRefreshInterval` is set
- WHEN the client is running
- THEN the token is refreshed in the background at the specified interval

#### Scenario: Client options
- GIVEN a `CosmosClientOptions` instance with custom settings
- WHEN passed to any `CosmosClient` constructor
- THEN all configured options are applied to the client

### Requirement: Client Lifecycle
The SDK SHALL support proper resource cleanup when a client is disposed.

#### Scenario: Dispose client
- GIVEN a `CosmosClient` instance
- WHEN `client.Dispose()` is called
- THEN all connections are closed
- AND all cached resources are released
- AND the client MUST NOT be used after disposal

#### Scenario: Singleton pattern
- GIVEN a long-running application
- WHEN the application creates a `CosmosClient`
- THEN it SHOULD reuse the same instance for the application's lifetime
- AND creating multiple clients to the same account is discouraged

### Requirement: Region Configuration
The SDK SHALL support configuring preferred regions for multi-region routing.

#### Scenario: Single preferred region
- GIVEN `CosmosClientOptions.ApplicationRegion = Regions.WestUS2`
- WHEN requests are routed
- THEN the SDK prefers the specified region

#### Scenario: Ordered region list
- GIVEN `CosmosClientOptions.ApplicationPreferredRegions = new List<string> { "East US", "West US 2", "Central US" }`
- WHEN requests are routed
- THEN regions are tried in the specified order for failover

#### Scenario: Region exclusion per request
- GIVEN `RequestOptions.ExcludeRegions = new List<string> { "West US 2" }`
- WHEN the request is routed
- THEN "West US 2" is skipped in the routing preference

### Requirement: Custom Handler Pipeline
The SDK SHALL support inserting custom request handlers into the processing pipeline.

#### Scenario: Add custom handler
- GIVEN a class extending `RequestHandler` with overridden `SendAsync`
- AND added to `CosmosClientOptions.CustomHandlers`
- WHEN requests are processed
- THEN the custom handler is invoked in the pipeline before the transport handler

#### Scenario: Handler ordering
- GIVEN multiple custom handlers in `CustomHandlers`
- WHEN a request is processed
- THEN handlers execute in the order they were added

#### Scenario: Handler access to request/response
- GIVEN a custom `RequestHandler`
- WHEN `SendAsync(RequestMessage, CancellationToken)` is invoked
- THEN the handler can inspect and modify the `RequestMessage`
- AND can inspect and modify the `ResponseMessage` returned from downstream

### Requirement: Resource Access Shortcuts
The SDK SHALL provide convenient methods to access databases and containers.

#### Scenario: Get database reference
- GIVEN a known database ID
- WHEN `client.GetDatabase("mydb")` is called
- THEN a `Database` proxy is returned (no network call)

#### Scenario: Get container reference
- GIVEN a known database and container ID
- WHEN `client.GetContainer("mydb", "mycontainer")` is called
- THEN a `Container` proxy is returned (no network call)

### Requirement: Account Information
The SDK SHALL support reading account-level properties.

#### Scenario: Read account
- GIVEN a connected client
- WHEN `client.ReadAccountAsync()` is called
- THEN `AccountProperties` is returned with available regions, consistency policy, and account metadata

### Requirement: Fluent Builder (CosmosClientBuilder)
The SDK SHALL provide a fluent builder for constructing clients.

#### Scenario: Builder pattern
- GIVEN `new CosmosClientBuilder(endpoint, key)`
- WHEN `.WithConnectionModeDirect()`, `.WithApplicationRegion(region)`, `.WithBulkExecution(true)`, `.Build()` are chained
- THEN a `CosmosClient` is constructed with the specified configuration

### Requirement: Content Response Control
The SDK SHALL support controlling whether write responses include the resource body.

#### Scenario: Client-level suppression
- GIVEN `CosmosClientOptions.EnableContentResponseOnWrite = false`
- WHEN write operations (Create, Replace, Upsert) are performed
- THEN response bodies are not returned by default
- AND request charge is reduced

#### Scenario: Per-request override
- GIVEN `ItemRequestOptions.EnableContentResponseOnWrite = true`
- WHEN a write operation is performed
- THEN the response body is included regardless of client-level setting

## Key Source Files
- `Microsoft.Azure.Cosmos/src/CosmosClient.cs` — client entry point and lifecycle
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — all client configuration
- `Microsoft.Azure.Cosmos/src/Fluent/CosmosClientBuilder.cs` — fluent builder
- `Microsoft.Azure.Cosmos/src/Handler/RequestHandler.cs` — custom handler base class
- `Microsoft.Azure.Cosmos/src/Authorization/` — authentication providers
- `Microsoft.Azure.Cosmos/src/Resource/ClientContextCore.cs` — internal client context
