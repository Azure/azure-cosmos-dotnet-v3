# Handler Pipeline

## Purpose

The Azure Cosmos DB .NET SDK processes all requests through a chain-of-responsibility handler pipeline. Each handler in the chain has a single responsibility (diagnostics, telemetry, retries, routing, transport) and processes a `RequestMessage`, passing it to the next handler via `InnerHandler`. Understanding the pipeline ordering and handler responsibilities is critical because the order determines which cross-cutting concerns apply to a request and in what sequence.

## Public API Surface

### RequestHandler Base Class

```csharp
public abstract class RequestHandler
{
    public RequestHandler InnerHandler { get; set; }
    public virtual Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken);
}
```

### Custom Handler Injection

```csharp
CosmosClientOptions options = new CosmosClientOptions
{
    CustomHandlers = { new MyLoggingHandler(), new MyMetricsHandler() }
};
```

Custom handlers are inserted after `RequestInvokerHandler` and before `DiagnosticsHandler`. They must inherit from `RequestHandler` and must have `InnerHandler == null` at registration time (the SDK links them).

## Pipeline Ordering

```
RequestInvokerHandler          (#1 - entry point, validates, applies options)
    |
Custom Handlers                (#2 - user-provided, in registration order)
    |
DiagnosticsHandler             (#3 - captures CPU/system usage)
    |
TelemetryHandler               (#4 - collects operation telemetry)
    |
RetryHandler                   (#5 - cross-region + throttle retries)
    |
RouterHandler                  (#6 - routes to point or feed pipeline)
    |-- Point operations ----> TransportHandler
    +-- Feed operations -----> NamedCacheRetryHandler
                                  |
                               PartitionKeyRangeHandler
                                  |
                               TransportHandler
```

## Handler Specifications

### 1. RequestInvokerHandler (Entry Point)

- **File**: `Microsoft.Azure.Cosmos/src/Handler/RequestInvokerHandler.cs`
- **Responsibilities**:
  - Validates client state (returns error if client is disposed)
  - Applies request-level and client-level options (consistency level, priority, throughput bucket)
  - Handles binary encoding negotiation (serialization format headers)
  - Executes availability strategy (hedging) if configured — hedged requests each traverse their own independent pipeline
- **RequestMessage**: Reads `RequestOptions`, writes consistency/priority/serialization headers
- **ResponseMessage**: Converts to cloneable stream if binary encoding enabled; adds excluded regions to diagnostics

### 2. Custom Handlers (User-Provided)

- **Injection**: `CosmosClientOptions.CustomHandlers`
- **Constraints**: Must be stateless. Must inherit `RequestHandler`. `InnerHandler` must be `null` at registration.
- **Position**: After `RequestInvokerHandler`, before `DiagnosticsHandler`
- **Use cases**: Logging, request modification, metrics collection, request signing

### 3. DiagnosticsHandler

- **File**: `Microsoft.Azure.Cosmos/src/Handler/DiagnosticsHandler.cs`
- **Responsibilities**: Captures CPU and system usage statistics during request execution (best-effort — failures don't break the request)
- **Behavior**: Adds `CpuHistoryTraceDatum` to the request's `Trace` on completion

### 4. TelemetryHandler

- **File**: `Microsoft.Azure.Cosmos/src/Handler/TelemetryHandler.cs`
- **Responsibilities**: Collects operation telemetry (latency, payload size, regions contacted, request charge, status codes) for service monitoring
- **Behavior**: Non-blocking, exception-safe. Filters by resource type via `ClientTelemetryOptions.AllowedResourceTypes`. Runs collection asynchronously on background thread.

### 5. RetryHandler

- **File**: `Microsoft.Azure.Cosmos/src/Handler/RetryHandler.cs`
- **Responsibilities**: Cross-region retries and throttle retries. Uses a stack of pluggable retry policies.
- **Retry policy stack** (in order of application):
  1. `ResetSessionTokenRetryPolicy` — session token mismatch
  2. `ClientRetryPolicy` — cross-region failover (DNS failures, 410, 503, 403/3, 403/1008, 404/1002)
  3. `ResourceThrottleRetryPolicy` — rate limiting (429)
- **Behavior**: Loops on `ShouldRetryAsync` while response is non-success. Respects backoff delay and `CancellationToken`. See `retry-and-failover` spec for detailed retry logic.

### 6. RouterHandler

- **File**: `Microsoft.Azure.Cosmos/src/Handler/RouterHandler.cs`
- **Responsibilities**: Routes requests to one of two sub-pipelines based on operation type.
- **Decision**: Reads `request.IsPartitionKeyRangeHandlerRequired`:
  - **`false`** (point operations) -> `TransportHandler` directly
  - **`true`** (feed operations: queries, change feed, read feed) -> `NamedCacheRetryHandler` -> `PartitionKeyRangeHandler` -> `TransportHandler`

### 7. NamedCacheRetryHandler (Feed Pipeline Only)

- **File**: `Microsoft.Azure.Cosmos/src/Handler/NamedCacheRetryHandler.cs`
- **Responsibilities**: Retries feed operations when partition key range cache becomes stale due to partition splits or container recreation.

### 8. PartitionKeyRangeHandler (Feed Pipeline Only)

- **File**: `Microsoft.Azure.Cosmos/src/Handler/PartitionKeyRangeHandler.cs`
- **Responsibilities**: Resolves the target physical partition(s) for feed operations. Distributes cross-partition queries across partition key ranges.

### 9. TransportHandler (Leaf)

- **File**: `Microsoft.Azure.Cosmos/src/Handler/TransportHandler.cs`
- **Responsibilities**:
  - Converts `RequestMessage` to `DocumentServiceRequest`
  - Obtains authorization token
  - Invokes transport layer: `GatewayStoreModel` (HTTP/Gateway mode) or `ServerStoreModel` (TCP/Direct mode)
  - Converts exceptions to `ResponseMessage`
  - Captures client-side request statistics

## Requirements

### Requirement: Handler Statelessness

The SDK SHALL ensure all handlers are stateless.

**When** a handler processes a request, the SDK SHALL NOT store request-specific state between calls. Each invocation of `SendAsync` SHALL be independent.

### Requirement: Pipeline Immutability

The SDK SHALL ensure the pipeline is immutable after client creation.

**When** `CosmosClient` is constructed, the SDK SHALL link all handlers during construction. **After** construction, the pipeline SHALL NOT be modifiable.

### Requirement: Custom Handler Ordering

The SDK SHALL execute custom handlers before SDK handlers (except `RequestInvokerHandler`).

**When** custom handlers are registered via `CosmosClientOptions.CustomHandlers`, the SDK SHALL insert them after `RequestInvokerHandler` (position #1) and before `DiagnosticsHandler` (position #3). Custom handlers SHALL see the raw request before retries, diagnostics, or telemetry processing.

### Requirement: Independent Pipeline Per Hedge

The SDK SHALL provide each hedged request its own independent pipeline.

**When** an availability strategy (hedging) is active, the SDK SHALL execute each parallel request through its own handler pipeline instance, including independent retry behavior.

### Requirement: Feed Operation Routing

The SDK SHALL route feed operations through additional handlers.

**When** `request.IsPartitionKeyRangeHandlerRequired` is `true` (queries, change feed, read feed), the SDK SHALL route the request through `NamedCacheRetryHandler` and `PartitionKeyRangeHandler` before reaching `TransportHandler`. Point operations SHALL go directly to `TransportHandler`.

### Requirement: Exception Conversion

The SDK SHALL convert handler exceptions to `ResponseMessage`.

**When** a handler throws an unhandled exception, the SDK SHALL catch the exception and wrap it in a `ResponseMessage` with an appropriate status code. Handlers SHALL NOT propagate unhandled exceptions up the pipeline.

## Interactions

- **Retry Policies**: `RetryHandler` delegates to policies defined in `retry-and-failover` spec.
- **Availability Strategy**: `RequestInvokerHandler` executes hedging from `cross-region-hedging` spec.
- **Transport**: `TransportHandler` connects to Gateway or Direct mode based on `CosmosClientOptions.ConnectionMode`.
- **Diagnostics**: Each handler can add data to `RequestMessage.Trace` for diagnostic output.

## References

- Source: `Microsoft.Azure.Cosmos/src/Handler/` (all handler files)
- Source: `Microsoft.Azure.Cosmos/src/ClientPipelineBuilder.cs`
- Design: `docs/SdkDesign.md` (Handler Pipeline section)