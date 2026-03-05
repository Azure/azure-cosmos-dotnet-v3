# Client Configuration and Lifecycle

## Purpose

`CosmosClient` is the top-level entry point to the SDK. It manages connection lifecycle, configuration, custom handler pipelines, and authentication.

## Requirements

### Requirement: Client Construction
The SDK SHALL support multiple authentication methods for constructing a CosmosClient.

#### Connection string
**When** `new CosmosClient(connectionString)` is called with a Cosmos DB connection string, the SDK shall connect using the account endpoint and key from the string.

#### Endpoint + key
**When** `new CosmosClient(endpoint, authKeyOrResourceToken)` is called with an account endpoint URI and account key, the SDK shall authenticate with the master key.

#### Endpoint + TokenCredential (AAD)
**When** `new CosmosClient(endpoint, tokenCredential)` is called with an account endpoint and an Azure `TokenCredential` (e.g., `DefaultAzureCredential`), the SDK shall authenticate via Azure Active Directory.

#### Token credential refresh
**Where** `CosmosClientOptions.TokenCredentialBackgroundRefreshInterval` is set, the SDK shall refresh the token in the background at the specified interval while the client is running.

#### Client options
**When** a `CosmosClientOptions` instance with custom settings is passed to any `CosmosClient` constructor, the SDK shall apply all configured options to the client.

### Requirement: Client Lifecycle
The SDK SHALL support proper resource cleanup when a client is disposed.

#### Dispose client
**When** `client.Dispose()` is called, the SDK shall close all connections, release all cached resources, and the client MUST NOT be used after disposal.

#### Singleton pattern
**While** running in a long-running application, the SDK shall support reusing the same `CosmosClient` instance for the application's lifetime, and creating multiple clients to the same account is discouraged.

### Requirement: Region Configuration
The SDK SHALL support configuring preferred regions for multi-region routing.

#### Single preferred region
**Where** `CosmosClientOptions.ApplicationRegion = Regions.WestUS2`, the SDK shall prefer the specified region when routing requests.

#### Ordered region list
**Where** `CosmosClientOptions.ApplicationPreferredRegions = new List<string> { "East US", "West US 2", "Central US" }`, the SDK shall try regions in the specified order for failover when routing requests.

#### Region exclusion per request
**Where** `RequestOptions.ExcludeRegions = new List<string> { "West US 2" }`, the SDK shall skip "West US 2" in the routing preference when routing the request.

### Requirement: Custom Handler Pipeline
The SDK SHALL support inserting custom request handlers into the processing pipeline.

#### Add custom handler
**Where** a class extending `RequestHandler` with overridden `SendAsync` is added to `CosmosClientOptions.CustomHandlers`, the SDK shall invoke the custom handler in the pipeline before the transport handler when requests are processed.

#### Handler ordering
**Where** multiple custom handlers are in `CustomHandlers`, **when** a request is processed, the SDK shall execute handlers in the order they were added.

#### Handler access to request/response
**When** `SendAsync(RequestMessage, CancellationToken)` is invoked on a custom `RequestHandler`, the SDK shall allow the handler to inspect and modify the `RequestMessage` and to inspect and modify the `ResponseMessage` returned from downstream.

### Requirement: Resource Access Shortcuts
The SDK SHALL provide convenient methods to access databases and containers.

#### Get database reference
**When** `client.GetDatabase("mydb")` is called with a known database ID, the SDK shall return a `Database` proxy without making a network call.

#### Get container reference
**When** `client.GetContainer("mydb", "mycontainer")` is called with a known database and container ID, the SDK shall return a `Container` proxy without making a network call.

### Requirement: Account Information
The SDK SHALL support reading account-level properties.

#### Read account
**When** `client.ReadAccountAsync()` is called, the SDK shall return `AccountProperties` with available regions, consistency policy, and account metadata.

### Requirement: Fluent Builder (CosmosClientBuilder)
The SDK SHALL provide a fluent builder for constructing clients.

#### Builder pattern
**When** `new CosmosClientBuilder(endpoint, key)` is used with chained calls to `.WithConnectionModeDirect()`, `.WithApplicationRegion(region)`, `.WithBulkExecution(true)`, and `.Build()`, the SDK shall construct a `CosmosClient` with the specified configuration.

### Requirement: Content Response Control
The SDK SHALL support controlling whether write responses include the resource body.

#### Client-level suppression
**Where** `CosmosClientOptions.EnableContentResponseOnWrite = false`, **when** write operations (Create, Replace, Upsert) are performed, the SDK shall not return response bodies by default and request charge shall be reduced.

#### Per-request override
**Where** `ItemRequestOptions.EnableContentResponseOnWrite = true`, **when** a write operation is performed, the SDK shall include the response body regardless of the client-level setting.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/CosmosClient.cs` — client entry point and lifecycle
- `Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs` — all client configuration
- `Microsoft.Azure.Cosmos/src/Fluent/CosmosClientBuilder.cs` — fluent builder
- `Microsoft.Azure.Cosmos/src/Handler/RequestHandler.cs` — custom handler base class
- `Microsoft.Azure.Cosmos/src/Authorization/` — authentication providers
- `Microsoft.Azure.Cosmos/src/Resource/ClientContextCore.cs` — internal client context
