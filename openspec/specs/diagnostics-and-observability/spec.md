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

## Behavioral Invariants

### Diagnostics Lifecycle

1. **Lazy materialization**: `ToString()` serializes the trace tree to JSON only when called. This avoids allocation overhead for operations where diagnostics are not inspected.
2. **Attached to every response**: `ResponseMessage.Diagnostics` is always available, defaulting to `NoOpTrace` if no trace was captured.
3. **Immutable once materialized**: The trace tree is frozen (walked state set recursively) before serialization to ensure consistency.
4. **Thread-safe region tracking**: `TraceSummary` uses `HashSet` under lock for region deduplication and `Interlocked.Increment` for failure counting.

### OpenTelemetry Integration

1. **Disabled by default (GA builds)**: `CosmosClientTelemetryOptions.DisableDistributedTracing` defaults to `true` in GA, `false` in Preview.
2. **No overhead when disabled**: When distributed tracing is disabled, no `Activity` objects are created.
3. **Semantic conventions**: Activities follow [OpenTelemetry Cosmos DB semantic conventions](https://opentelemetry.io/docs/specs/semconv/database/cosmosdb/).
4. **Activity kind**: `ActivityKind.Internal` for Gateway mode, `ActivityKind.Client` for Direct mode.

### Events

Three event types are emitted when distributed tracing is enabled:

| Event | Level | Trigger | Payload |
|-------|-------|---------|---------|
| **FailedRequest** | Error | Non-success status code (≥300, excluding 404/0, 304/0, 409/0, 412/0) | Full diagnostics JSON |
| **LatencyOverThreshold** | Warning | Latency exceeds threshold OR RU/payload exceeds configured limits | Full diagnostics JSON |
| **Exception** | Error | Any exception during operation | Exception diagnostics |

### Threshold Configuration

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

Thresholds can also be overridden per-request via `RequestOptions.CosmosThresholdOptions`.

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
