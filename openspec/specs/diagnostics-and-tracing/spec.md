# Diagnostics and Tracing

## Purpose

The SDK provides comprehensive diagnostics, distributed tracing, and metrics to enable observability of Cosmos DB operations for debugging, monitoring, and performance analysis.

## Requirements

### Requirement: CosmosDiagnostics
The SDK SHALL capture diagnostics for every operation and expose them on response objects.

#### Scenario: Access diagnostics
- GIVEN any Cosmos DB operation completes (success or failure)
- WHEN `response.Diagnostics` is accessed
- THEN a `CosmosDiagnostics` object is returned containing operation-level metadata

#### Scenario: Client elapsed time
- GIVEN a completed operation
- WHEN `response.Diagnostics.GetClientElapsedTime()` is called
- THEN the total client-side elapsed time is returned as a `TimeSpan`

#### Scenario: Contacted regions
- GIVEN a completed operation
- WHEN `response.Diagnostics.GetContactedRegions()` is called
- THEN an `IReadOnlyList<(string regionName, Uri endpoint)>` is returned listing all regions contacted

#### Scenario: Failed request count
- GIVEN a completed operation that involved retries
- WHEN `response.Diagnostics.GetFailedRequestCount()` is called
- THEN the number of failed attempts (before the final result) is returned

#### Scenario: Lazy materialization
- GIVEN a completed operation
- WHEN `response.Diagnostics.ToString()` is called
- THEN the full diagnostic JSON is materialized on demand (not eagerly computed)
- AND the output includes the complete trace tree with all request details

### Requirement: Trace Tree
The SDK SHALL maintain a hierarchical trace tree for each operation capturing all internal steps.

#### Scenario: Trace hierarchy
- GIVEN an operation like `ReadItemAsync`
- WHEN the operation completes
- THEN the trace tree contains the top-level operation node
- AND child nodes for each internal step (routing, transport, retry, serialization)
- AND each node records name, start time, duration, component, and level

#### Scenario: Trace data
- GIVEN a request was sent to the backend
- WHEN the trace is examined
- THEN `ClientSideRequestStatisticsTraceDatum` contains per-request details including:
  - Status code and sub-status code
  - Request charge (RU)
  - Region and endpoint
  - Request and response timestamps
  - Transport-specific details (Direct: RNTBD stats, Gateway: HTTP stats)

### Requirement: OpenTelemetry Distributed Tracing
The SDK SHALL emit OpenTelemetry-compatible traces for operation-level observability.

#### Scenario: Enable distributed tracing
- GIVEN `CosmosClientTelemetryOptions.DisableDistributedTracing = false` (default)
- WHEN operations are performed
- THEN the SDK emits activities on the `Azure.Cosmos.Operation` ActivitySource

#### Scenario: Trace attributes
- GIVEN distributed tracing is enabled
- WHEN an operation activity is recorded
- THEN it includes standard attributes: `db.system`, `db.name`, `db.collection.name`, `db.cosmosdb.operation_type`, `db.cosmosdb.status_code`, `db.cosmosdb.sub_status_code`, `db.cosmosdb.request_charge`

#### Scenario: Latency threshold events
- GIVEN a latency threshold is configured via `CosmosThresholdOptions`
- WHEN an operation exceeds the threshold
- THEN a `LatencyOverThreshold` event is emitted on the activity with full diagnostics

#### Scenario: Failed request events
- GIVEN a request fails with a non-retryable error
- WHEN the operation completes
- THEN a `FailedRequest` event is emitted on the activity

#### Scenario: Request-level diagnostics source
- GIVEN an application subscribes to `Azure-Cosmos-Operation-Request-Diagnostics`
- WHEN operations are performed
- THEN detailed per-request diagnostics are emitted as events

### Requirement: Client Metrics
The SDK SHALL emit OpenTelemetry-compatible metrics for quantitative monitoring.

#### Scenario: Enable metrics
- GIVEN `CosmosClientTelemetryOptions.IsClientMetricsEnabled = true`
- WHEN operations are performed
- THEN histograms are emitted on the `Azure.Cosmos.Client.Operation` meter

#### Scenario: Operation duration histogram
- GIVEN metrics are enabled
- WHEN operations complete
- THEN `db.client.cosmosdb.request.duration` records operation latency in seconds
- AND uses histogram buckets: 0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1, 5, 10

#### Scenario: Request/response body size
- GIVEN metrics are enabled
- WHEN operations complete
- THEN `db.client.cosmosdb.request.body.size` and `db.client.cosmosdb.response.body.size` record payload sizes

#### Scenario: Service duration (Direct mode only)
- GIVEN metrics are enabled and Direct mode is used
- WHEN operations complete
- THEN `db.client.cosmosdb.request.service_duration` records backend processing time

### Requirement: Threshold-Based Diagnostics
The SDK SHALL support configurable thresholds for automatic diagnostics capture.

#### Scenario: Configure thresholds
- GIVEN `CosmosThresholdOptions` is configured with latency thresholds per operation type
- WHEN an operation exceeds its threshold
- THEN diagnostics are automatically captured and emitted as telemetry events

#### Scenario: Per-request threshold override
- GIVEN `RequestOptions.CosmosThresholdOptions` is set
- WHEN the request exceeds the per-request threshold
- THEN the per-request threshold takes precedence over the client-level threshold

### Requirement: Diagnostics in Exceptions
The SDK SHALL include diagnostics in exception objects.

#### Scenario: Exception diagnostics
- GIVEN an operation throws a `CosmosException`
- WHEN `exception.Diagnostics` is accessed
- THEN full diagnostics are available including all retry attempts and failure details

### Requirement: Query Metrics
The SDK SHALL expose server-side query metrics when available.

#### Scenario: Access query metrics
- GIVEN a query operation completes
- WHEN `response.Diagnostics.GetQueryMetrics()` is called
- THEN `ServerSideCumulativeMetrics` is returned with server-side execution time, RU, and document count

## Key Source Files
- `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnostics.cs` — abstract diagnostics base
- `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosTraceDiagnostics.cs` — trace-based implementation
- `Microsoft.Azure.Cosmos/src/Tracing/Trace.cs` — hierarchical trace node
- `Microsoft.Azure.Cosmos/src/Tracing/TraceData/ClientSideRequestStatisticsTraceDatum.cs` — per-request stats
- `Microsoft.Azure.Cosmos/src/Telemetry/OpenTelemetry/` — OpenTelemetry integration
- `Microsoft.Azure.Cosmos/src/CosmosClientTelemetryOptions.cs` — telemetry configuration
