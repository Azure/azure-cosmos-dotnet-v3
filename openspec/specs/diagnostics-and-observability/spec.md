# Diagnostics and Observability

## Purpose

The Azure Cosmos DB .NET SDK provides comprehensive diagnostics and observability through three layers: `CosmosDiagnostics` for per-request diagnostic information, a hierarchical `Trace` system for structured request telemetry, and OpenTelemetry integration for distributed tracing and metrics. Together these enable debugging latency issues, understanding retry behavior, and monitoring SDK health in production.

## Public API Surface

### CosmosDiagnostics

Every response exposes diagnostics:

```csharp
ItemResponse<T> response = await container.ReadItemAsync<T>(id, pk);
CosmosDiagnostics diagnostics = response.Diagnostics;

TimeSpan elapsed = diagnostics.GetClientElapsedTime();
IReadOnlyList<(string regionName, Uri uri)> regions = diagnostics.GetContactedRegions();
string fullDiagnostics = diagnostics.ToString();  // JSON — lazy materialized
```

| Method | Returns | Notes |
|--------|---------|-------|
| `GetClientElapsedTime()` | `TimeSpan` | End-to-end client-side elapsed time |
| `GetStartTimeUtc()` | `DateTime?` | Request start time in UTC |
| `GetContactedRegions()` | `IReadOnlyList<(string, Uri)>` | Unique regions contacted (no ordering guarantee) |
| `GetFailedRequestCount()` | `int` | Count of failed sub-requests (retries) |
| `GetQueryMetrics()` | `ServerSideCumulativeMetrics` | Server-side query metrics (query operations only; null for others) |
| `ToString()` | `string` | Full JSON diagnostic tree — lazy materialized on first call |

### OpenTelemetry Activity Sources

| Source Name | Purpose |
|-------------|---------|
| `Azure.Cosmos.Operation` | Operation-level activities (spans) with semantic conventions |
| `Azure-Cosmos-Operation-Request-Diagnostics` | EventSource for events with request diagnostics JSON |

### OpenTelemetry Meters

| Meter Name | Purpose |
|------------|---------|
| `Azure.Cosmos.Client.Operation` | Operation-level metrics (duration, payload sizes) |
| `Azure.Cosmos.Client.Request` | Network-level request metrics |

## Requirements

### Requirement: Diagnostics Lifecycle

**Scenario: Lazy materialization of diagnostics**
When `ToString()` is called on a `CosmosDiagnostics` instance,
the system shall serialize the trace tree to JSON only at that point, avoiding allocation overhead for operations where diagnostics are not inspected.

**Scenario: Diagnostics always attached to responses**
While the SDK is processing any operation,
`ResponseMessage.Diagnostics` shall always be available, defaulting to `NoOpTrace` if no trace was captured.

**Scenario: Immutability after materialization**
When the trace tree is serialized,
the system shall freeze the tree (walked state set recursively) before serialization to ensure consistency.

**Scenario: Thread-safe region tracking**
While multiple threads are recording diagnostics concurrently,
`TraceSummary` shall use `HashSet` under lock for region deduplication and `Interlocked.Increment` for failure counting to guarantee thread safety.

### Requirement: OpenTelemetry Distributed Tracing

**Scenario: Distributed tracing disabled by default in GA**
When the SDK is built as a GA release,
`CosmosClientTelemetryOptions.DisableDistributedTracing` shall default to `true`.

**Scenario: Distributed tracing enabled by default in Preview**
When the SDK is built as a Preview release,
`CosmosClientTelemetryOptions.DisableDistributedTracing` shall default to `false`.

**Scenario: No overhead when tracing is disabled**
While distributed tracing is disabled,
the system shall not create any `Activity` objects.

**Scenario: Semantic conventions compliance**
When distributed tracing is enabled,
activities shall follow [OpenTelemetry Cosmos DB semantic conventions](https://opentelemetry.io/docs/specs/semconv/database/cosmosdb/).

**Scenario: Activity kind selection**
When an operation uses Gateway mode,
the activity kind shall be `ActivityKind.Internal`.
When an operation uses Direct mode,
the activity kind shall be `ActivityKind.Client`.

### Requirement: Telemetry Events

When distributed tracing is enabled, three event types are emitted:

| Event | Level | Trigger | Payload |
|-------|-------|---------|---------|
| **FailedRequest** | Error | Non-success status code (≥300, excluding 404/0, 304/0, 409/0, 412/0) | Full diagnostics JSON |
| **LatencyOverThreshold** | Warning | Latency exceeds threshold OR RU/payload exceeds configured limits | Full diagnostics JSON |
| **Exception** | Error | Any exception during operation | Exception diagnostics |

**Scenario: Failed request event emission**
When an operation returns a non-success status code (≥300, excluding 404/0, 304/0, 409/0, 412/0),
the system shall emit a **FailedRequest** event at Error level with the full diagnostics JSON as payload.

**Scenario: Latency over threshold event emission**
When an operation's latency exceeds the configured threshold, or the RU charge or payload size exceeds configured limits,
the system shall emit a **LatencyOverThreshold** event at Warning level with the full diagnostics JSON as payload.

**Scenario: Exception event emission**
When any exception occurs during an operation,
the system shall emit an **Exception** event at Error level with the exception diagnostics as payload.

### Requirement: Threshold-Based Diagnostics

```csharp
CosmosClientTelemetryOptions telemetryOptions = new()
{
    DisableDistributedTracing = false,
    CosmosThresholdOptions = new CosmosThresholdOptions
    {
        PointOperationLatencyThreshold = TimeSpan.FromSeconds(1),      // Default: 1s
        NonPointOperationLatencyThreshold = TimeSpan.FromSeconds(3),   // Default: 3s
        RequestChargeThreshold = 100.0,   // Optional: RU threshold
        PayloadSizeThresholdInBytes = 1024 * 1024  // Optional: 1MB threshold
    }
};
```

**Scenario: Default point operation latency threshold**
If no custom threshold is configured for point operations,
the system shall use a default latency threshold of 1 second.

**Scenario: Default non-point operation latency threshold**
If no custom threshold is configured for non-point operations,
the system shall use a default latency threshold of 3 seconds.

**Scenario: Per-request threshold override**
When `RequestOptions.CosmosThresholdOptions` is set on an individual request,
the system shall use the per-request thresholds instead of the client-level thresholds for that operation.

## Configuration

### CosmosClientTelemetryOptions

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `DisableDistributedTracing` | `bool` | `true` (GA) / `false` (Preview) | Master switch for OpenTelemetry |
| `DisableSendingMetricsToService` | `bool` | `true` | Opt-in for Microsoft telemetry collection |
| `CosmosThresholdOptions` | `CosmosThresholdOptions` | defaults | Latency/RU/size thresholds for events |
| `QueryTextMode` | `QueryTextMode` | `None` | `None`, `Parameterized`, `All` — include queries in traces |
| `IsClientMetricsEnabled` | `bool` | `false` | Client-side metrics (Preview) |

### Activity Attributes

Key attributes on OpenTelemetry activities:

| Attribute | Value |
|-----------|-------|
| `db.system.name` | `"cosmosdb"` |
| `db.operation.name` | Operation type (e.g., `read_item`, `query_items`) |
| `db.namespace` | Database name |
| `db.collection.name` | Container name |
| `db.response.status_code` | HTTP status code |
| `db.cosmosdb.sub_status_code` | Cosmos sub-status code |
| `db.cosmosdb.consistency_level` | Effective consistency level |
| `network.protocol.name` | `"https"` or `"rntbd"` |
| `cloud.region` | Contacted regions |

## Trace Hierarchy

The SDK captures diagnostics in a tree of `ITrace` nodes:

```
Root Trace (operation name)
├── Child: Authorization
├── Child: Transport
│   ├── Data: ClientSideRequestStatistics
│   └── Data: RegionsContacted
├── Child: Retry (if retried)
│   └── Child: Transport (retry attempt)
└── Data: CpuHistory (system usage)
```

- **Root trace**: Created per operation via `Trace.GetRootTrace()`
- **Children**: Added via `trace.StartChild()` — share the parent's `TraceSummary`
- **Data**: Key-value pairs added via `trace.AddDatum()` (latencies, statistics, metadata)
- **Summary**: Thread-safe aggregate of regions contacted and failure count across all nodes

## Interactions

- **Handler Pipeline**: `DiagnosticsHandler` (#3) captures CPU/system usage. `TelemetryHandler` (#4) collects operation telemetry. See `handler-pipeline` spec.
- **Retry Policies**: Each retry attempt adds a child trace node, making retries visible in diagnostics. See `retry-and-failover` spec.

## References

- Source: `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnostics.cs`
- Source: `Microsoft.Azure.Cosmos/src/Tracing/Trace.cs`
- Source: `Microsoft.Azure.Cosmos/src/Telemetry/OpenTelemetry/`
- Source: `Microsoft.Azure.Cosmos/src/CosmosClientTelemetryOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/CosmosThresholdOptions.cs`
- Design: `docs/observability.md`