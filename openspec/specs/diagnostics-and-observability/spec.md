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
string summaryDiagnostics = diagnostics.ToString(DiagnosticsVerbosity.Summary);  // Compacted summary
```

| Method | Returns | Notes |
|--------|---------|-------|
| `GetClientElapsedTime()` | `TimeSpan` | End-to-end client-side elapsed time |
| `GetStartTimeUtc()` | `DateTime?` | Request start time in UTC |
| `GetContactedRegions()` | `IReadOnlyList<(string, Uri)>` | Unique regions contacted (no ordering guarantee) |
| `GetFailedRequestCount()` | `int` | Count of failed sub-requests (retries) |
| `GetQueryMetrics()` | `ServerSideCumulativeMetrics` | Server-side query metrics (query operations only; null for others) |
| `ToString()` | `string` | Full JSON diagnostic tree — lazy materialized on first call |
| `ToString(DiagnosticsVerbosity)` | `string` | Diagnostics at the requested verbosity level |

### DiagnosticsVerbosity Enum

```csharp
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Controls the level of detail in CosmosDiagnostics serialized output.
    /// </summary>
    public enum DiagnosticsVerbosity
    {
        /// <summary>
        /// Full diagnostic output with all individual request traces.
        /// This is the default and preserves backward compatibility.
        /// </summary>
        Detailed = 0,

        /// <summary>
        /// Compacted diagnostic output optimized for log size constraints.
        /// Groups requests by region. Keeps first and last request in full detail.
        /// Deduplicates middle requests by (StatusCode, SubStatusCode) with
        /// aggregate statistics (count, total RU, min/max/P50 latency).
        /// Respects MaxDiagnosticsSummarySizeBytes limit.
        /// </summary>
        Summary = 1,
    }
}
```

### CosmosClientOptions — Diagnostics Verbosity

```csharp
public class CosmosClientOptions
{
    /// <summary>
    /// Gets or sets the default verbosity for CosmosDiagnostics serialization.
    /// Default: <see cref="DiagnosticsVerbosity.Detailed"/>.
    /// Can also be set via the AZURE_COSMOS_DIAGNOSTICS_VERBOSITY environment variable.
    /// </summary>
    public DiagnosticsVerbosity DiagnosticsVerbosity { get; set; } = DiagnosticsVerbosity.Detailed;

    /// <summary>
    /// Gets or sets the maximum size in bytes for Summary mode diagnostic output.
    /// If the summary output exceeds this limit, a truncated indicator is returned.
    /// Default: 8192 (8 KB). Minimum: 4096 (4 KB).
    /// Can also be set via the AZURE_COSMOS_DIAGNOSTICS_MAX_SUMMARY_SIZE environment variable.
    /// </summary>
    public int MaxDiagnosticsSummarySizeBytes { get; set; } = 8192;
}
```

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

### Requirement: Diagnostics Verbosity

**Scenario: Default verbosity is Detailed**
If no explicit verbosity is configured,
the system shall default to `DiagnosticsVerbosity.Detailed`, preserving full backward-compatible trace output.

**Scenario: Parameterless ToString always returns Detailed**
When `ToString()` (parameterless) is called on a `CosmosDiagnostics` instance,
the system shall always return the full `Detailed` trace output, regardless of `CosmosClientOptions.DiagnosticsVerbosity` setting.

**Scenario: In-memory trace tree unchanged by verbosity**
When any verbosity mode is selected,
the system shall leave the in-memory `ITrace` tree and `ClientSideRequestStatisticsTraceDatum` data unchanged. Compaction shall only occur at serialization time.

**Scenario: Summary mode region grouping**
When `ToString(DiagnosticsVerbosity.Summary)` is called,
the system shall group all `StoreResponseStatistics` and `HttpResponseStatistics` entries by region. Entries with a null or empty region shall be grouped under `"Unknown"`.

**Scenario: Summary mode first/last preservation**
When a region group contains two or more requests,
the system shall preserve full details of the chronologically first and last request in that region.

**Scenario: Summary mode single request region**
When a region group contains exactly one request,
the system shall include only the first request with full details and omit the last request.

**Scenario: Summary mode aggregated groups**
When a region group contains more than two requests,
the system shall aggregate the middle entries (all except first and last) by `(StatusCode, SubStatusCode)`, providing `Count`, `TotalRequestCharge`, `MinDurationMs`, `MaxDurationMs`, `P50DurationMs`, and `AvgDurationMs` for each group.

**Scenario: Summary mode with mixed Direct and Gateway requests**
When the trace tree contains both `StoreResponseStatistics` (Direct mode) and `HttpResponseStatistics` (Gateway mode),
the system shall collect and treat both uniformly in the summary. Both transport types shall appear in the same region groups and aggregated groups.

**Scenario: Summary mode size enforcement**
When the serialized summary JSON exceeds `MaxDiagnosticsSummarySizeBytes`,
the system shall fall back to a minimal truncated output containing `TotalDurationMs`, `TotalRequestCount`, `Truncated: true`, and a message directing users to use Detailed mode.

**Scenario: Summary mode size under limit**
When the serialized summary JSON fits within `MaxDiagnosticsSummarySizeBytes`,
the system shall return the full summary JSON as-is.

**Scenario: MaxDiagnosticsSummarySizeBytes minimum validation**
When `CosmosClientOptions.MaxDiagnosticsSummarySizeBytes` is set below 4096,
the system shall reject the value (minimum: 4096 bytes).

**Scenario: Verbosity precedence**
When determining verbosity for serialization,
the system shall apply this precedence (highest to lowest):
1. Explicit `ToString(DiagnosticsVerbosity)` parameter
2. `CosmosClientOptions.DiagnosticsVerbosity` (set in code or populated from env var)
3. Default: `DiagnosticsVerbosity.Detailed`

**Scenario: Environment variable configuration**
When the `AZURE_COSMOS_DIAGNOSTICS_VERBOSITY` environment variable is set and no code-level value overrides it,
the system shall use the environment variable value to populate `CosmosClientOptions.DiagnosticsVerbosity`. Valid values: `"Detailed"`, `"Summary"`.

**Scenario: Code-level value overrides environment variable**
When `CosmosClientOptions.DiagnosticsVerbosity` is explicitly set in code,
the system shall use the code-set value regardless of the `AZURE_COSMOS_DIAGNOSTICS_VERBOSITY` environment variable.

**Scenario: Summary mode region ordering**
When multiple regions are present in the summary output,
the system shall order region groups deterministically (alphabetically by region name).

**Scenario: Summary mode caching**
When `ToString(DiagnosticsVerbosity)` is called multiple times with the same verbosity on the same `CosmosDiagnostics` instance,
the system shall cache and return the same serialized string to avoid redundant computation.

### Requirement: Summary JSON Format

The summary mode output shall conform to this structure:

```json
{
  "Summary": {
    "DiagnosticsVerbosity": "Summary",
    "TotalDurationMs": 1234.5,
    "TotalRequestCharge": 245.5,
    "TotalRequestCount": 60,
    "RegionsSummary": [
      {
        "Region": "West US 2",
        "RequestCount": 50,
        "TotalRequestCharge": 200.0,
        "First": {
          "StatusCode": 429,
          "SubStatusCode": 3200,
          "RequestCharge": 0.0,
          "DurationMs": 5,
          "Region": "West US 2",
          "Endpoint": "https://account-westus2.documents.azure.com",
          "RequestStartTimeUtc": "2026-02-26T21:00:00.000Z",
          "OperationType": "Read",
          "ResourceType": "Document"
        },
        "Last": { },
        "AggregatedGroups": [
          {
            "StatusCode": 429,
            "SubStatusCode": 3200,
            "Count": 48,
            "TotalRequestCharge": 0.0,
            "MinDurationMs": 3,
            "MaxDurationMs": 45,
            "P50DurationMs": 12,
            "AvgDurationMs": 15.3
          }
        ]
      }
    ]
  }
}
```

**Scenario: Truncated output format**
When summary output is truncated due to size limits,
the system shall emit:

```json
{
  "Summary": {
    "DiagnosticsVerbosity": "Summary",
    "TotalDurationMs": 1234.5,
    "TotalRequestCount": 60,
    "Truncated": true,
    "Message": "Summary output truncated to fit size limit. Set DiagnosticsVerbosity to Detailed for full diagnostics."
  }
}
```

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

### CosmosClientOptions — Diagnostics Verbosity

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `DiagnosticsVerbosity` | `DiagnosticsVerbosity` | `Detailed` | Controls serialization detail level |
| `MaxDiagnosticsSummarySizeBytes` | `int` | `8192` (8 KB) | Max bytes for summary output. Minimum: 4096 |

### Diagnostics Verbosity Environment Variables

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `AZURE_COSMOS_DIAGNOSTICS_VERBOSITY` | `string` | `"Detailed"` | Default verbosity when not set in code. Values: `"Detailed"`, `"Summary"` |
| `AZURE_COSMOS_DIAGNOSTICS_MAX_SUMMARY_SIZE` | `int` | `8192` | Max bytes for summary output. Minimum: 4096 |

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
- Source: `Microsoft.Azure.Cosmos/src/Diagnostics/DiagnosticsVerbosity.cs`
- Source: `Microsoft.Azure.Cosmos/src/Diagnostics/DiagnosticsSummaryWriter.cs`
- Source: `Microsoft.Azure.Cosmos/src/Tracing/Trace.cs`
- Source: `Microsoft.Azure.Cosmos/src/Telemetry/OpenTelemetry/`
- Source: `Microsoft.Azure.Cosmos/src/CosmosClientTelemetryOptions.cs`
- Source: `Microsoft.Azure.Cosmos/src/CosmosThresholdOptions.cs`
- Design: `docs/observability.md`
- Cross-SDK reference: [Azure/azure-sdk-for-rust#3592](https://github.com/Azure/azure-sdk-for-rust/pull/3592) — Rust SDK `DiagnosticsContext` with `Summary` and `Detailed` modes