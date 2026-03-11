# Diagnostics and Tracing

## Purpose

The SDK provides comprehensive diagnostics, distributed tracing, and metrics to enable observability of Cosmos DB operations for debugging, monitoring, and performance analysis.

## Requirements

### Requirement: CosmosDiagnostics
The SDK SHALL capture diagnostics for every operation and expose them on response objects.

#### Access diagnostics
**When** any Cosmos DB operation completes (success or failure) and `response.Diagnostics` is accessed, the SDK shall return a `CosmosDiagnostics` object containing operation-level metadata.

#### Client elapsed time
**When** `response.Diagnostics.GetClientElapsedTime()` is called after a completed operation, the SDK shall return the total client-side elapsed time as a `TimeSpan`.

#### Contacted regions
**When** `response.Diagnostics.GetContactedRegions()` is called after a completed operation, the SDK shall return an `IReadOnlyList<(string regionName, Uri endpoint)>` listing all regions contacted.

#### Failed request count
**While** a completed operation involved retries, **when** `response.Diagnostics.GetFailedRequestCount()` is called, the SDK shall return the number of failed attempts before the final result.

#### Lazy materialization
**When** `response.Diagnostics.ToString()` is called after a completed operation, the SDK shall materialize the full diagnostic JSON on demand (not eagerly computed) and include the complete trace tree with all request details.

### Requirement: Trace Tree
The SDK SHALL maintain a hierarchical trace tree for each operation capturing all internal steps.

#### Trace hierarchy
**When** an operation like `ReadItemAsync` completes, the SDK shall produce a trace tree containing the top-level operation node, child nodes for each internal step (routing, transport, retry, serialization), and each node shall record name, start time, duration, component, and level.

#### Trace data
**When** a request is sent to the backend, the SDK shall include `ClientSideRequestStatisticsTraceDatum` in the trace containing per-request details including: status code and sub-status code, request charge (RU), region and endpoint, request and response timestamps, and transport-specific details (Direct: RNTBD stats, Gateway: HTTP stats).

### Requirement: OpenTelemetry Distributed Tracing
The SDK SHALL emit OpenTelemetry-compatible traces for operation-level observability.

#### Enable distributed tracing
**Where** `CosmosClientTelemetryOptions.DisableDistributedTracing = false` (default), **when** operations are performed, the SDK shall emit activities on the `Azure.Cosmos.Operation` ActivitySource.

#### Trace attributes
**While** distributed tracing is enabled, **when** an operation activity is recorded, the SDK shall include standard attributes: `db.system`, `db.name`, `db.collection.name`, `db.cosmosdb.operation_type`, `db.cosmosdb.status_code`, `db.cosmosdb.sub_status_code`, `db.cosmosdb.request_charge`.

#### Latency threshold events
**Where** a latency threshold is configured via `CosmosThresholdOptions`, **when** an operation exceeds the threshold, the SDK shall emit a `LatencyOverThreshold` event on the activity with full diagnostics.

#### Failed request events
**If** a request fails with a non-retryable error, **then** the SDK shall emit a `FailedRequest` event on the activity when the operation completes.

#### Request-level diagnostics source
**Where** an application subscribes to `Azure-Cosmos-Operation-Request-Diagnostics`, **when** operations are performed, the SDK shall emit detailed per-request diagnostics as events.

### Requirement: Client Metrics
The SDK SHALL emit OpenTelemetry-compatible metrics for quantitative monitoring.

#### Enable metrics
**Where** `CosmosClientTelemetryOptions.IsClientMetricsEnabled = true`, **when** operations are performed, the SDK shall emit histograms on the `Azure.Cosmos.Client.Operation` meter.

#### Operation duration histogram
**While** metrics are enabled, **when** operations complete, the SDK shall record operation latency in seconds via `db.client.cosmosdb.request.duration` using histogram buckets: 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10.

#### Request/response body size
**While** metrics are enabled, **when** operations complete, the SDK shall record payload sizes via `db.client.cosmosdb.request.body.size` and `db.client.cosmosdb.response.body.size`.

#### Service duration (Direct mode only)
**While** metrics are enabled and Direct mode is used, **when** operations complete, the SDK shall record backend processing time via `db.client.cosmosdb.request.service_duration`.

### Requirement: Threshold-Based Diagnostics
The SDK SHALL support configurable thresholds for automatic diagnostics capture.

#### Configure thresholds
**Where** `CosmosThresholdOptions` is configured with latency thresholds per operation type, **when** an operation exceeds its threshold, the SDK shall automatically capture diagnostics and emit them as telemetry events.

#### Per-request threshold override
**Where** `RequestOptions.CosmosThresholdOptions` is set, **when** the request exceeds the per-request threshold, the SDK shall use the per-request threshold taking precedence over the client-level threshold.

### Requirement: Diagnostics in Exceptions
The SDK SHALL include diagnostics in exception objects.

#### Exception diagnostics
**If** an operation throws a `CosmosException`, **then** the SDK shall provide full diagnostics via `exception.Diagnostics` including all retry attempts and failure details.

### Requirement: Query Metrics
The SDK SHALL expose server-side query metrics when available.

#### Access query metrics
**When** `response.Diagnostics.GetQueryMetrics()` is called after a query operation completes, the SDK shall return `ServerSideCumulativeMetrics` with server-side execution time, RU, and document count.

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnostics.cs` — abstract diagnostics base
- `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosTraceDiagnostics.cs` — trace-based implementation
- `Microsoft.Azure.Cosmos/src/Tracing/Trace.cs` — hierarchical trace node
- `Microsoft.Azure.Cosmos/src/Tracing/TraceData/ClientSideRequestStatisticsTraceDatum.cs` — per-request stats
- `Microsoft.Azure.Cosmos/src/Telemetry/OpenTelemetry/` — OpenTelemetry integration
- `Microsoft.Azure.Cosmos/src/CosmosClientTelemetryOptions.cs` — telemetry configuration
