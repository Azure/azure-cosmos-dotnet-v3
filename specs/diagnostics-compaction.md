# CosmosDiagnostics Compaction — Summary Mode

## 1. Problem Statement

`CosmosDiagnostics.ToString()` produces a JSON trace that grows **unboundedly** with retries. Each retry attempt creates a new child `ITrace` node containing a full `ClientSideRequestStatisticsTraceDatum` with complete `StoreResponseStatistics` and `HttpResponseStatistics` entries. In pathological scenarios (sustained 429 throttling, transient failures, cross-region failovers), a single operation's diagnostics can grow to hundreds of KB.

**Impact:**
- **Log truncation** — monitoring systems (Application Insights, Azure Monitor, etc.) silently drop oversized log entries
- **Memory pressure** — large diagnostic strings increase GC overhead, especially at high throughput
- **Readability** — operators cannot quickly extract signal from noise when hundreds of identical retry entries are listed

**Example scenario:** A point read that encounters 50 retries due to 429 throttling in West US 2, then fails over to East US 2 with 10 more retries, produces ~60 full `StoreResponseStatistics` entries in the trace tree. With summary mode, this compacts to: first request + last request + 1 aggregated group per region.

## 2. Design Overview

Introduce a **`DiagnosticsVerbosity`** concept (modeled after [Azure/azure-sdk-for-rust#3592](https://github.com/Azure/azure-sdk-for-rust/pull/3592)) that controls how `CosmosDiagnostics.ToString()` serializes trace data:

| Mode | Behavior | Use Case |
|------|----------|----------|
| **Detailed** (default) | Current behavior — full trace tree output | Debugging, development |
| **Summary** | Region-grouped compaction with first/last + aggregated middle | Production logging, size-constrained environments |

**Key design principle:** The in-memory representation (`ITrace` tree, `ClientSideRequestStatisticsTraceDatum`) stays **unchanged**. Compaction only happens at **serialization time** in the `TraceJsonWriter` path. This preserves full programmatic access to diagnostics data while reducing serialized output size.

## 3. Public API Surface

### 3.1 DiagnosticsVerbosity Enum (New)

```csharp
// File: Microsoft.Azure.Cosmos/src/Diagnostics/DiagnosticsVerbosity.cs
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

### 3.2 CosmosClientOptions — Client-Wide Default

```csharp
// File: Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs
public class CosmosClientOptions
{
    // ... existing properties ...

    /// <summary>
    /// Gets or sets the default verbosity for CosmosDiagnostics serialization.
    /// Default: <see cref="DiagnosticsVerbosity.Detailed"/>.
    /// This is a client-level configuration value. Pass it to
    /// <see cref="CosmosDiagnostics.ToString(DiagnosticsVerbosity)"/> when serializing diagnostics.
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

### 3.3 CosmosDiagnostics — Serialization Overloads

The parameterless `ToString()` continues to return full detailed output (backward compatible). New overloads accept an explicit `DiagnosticsVerbosity` parameter — no mutable state is stored on the diagnostics object, avoiding thread-safety concerns.

```csharp
// File: Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnostics.cs
public abstract class CosmosDiagnostics
{
    // ... existing members (ToString(), GetContactedRegions(), etc. unchanged) ...

    /// <summary>
    /// Returns the string representation of diagnostics using the specified verbosity.
    /// When <paramref name="verbosity"/> is <see cref="DiagnosticsVerbosity.Summary"/>,
    /// produces a compacted region-grouped summary. When <see cref="DiagnosticsVerbosity.Detailed"/>,
    /// produces the full trace output (same as parameterless <see cref="ToString()"/>).
    /// </summary>
    /// <param name="verbosity">The verbosity level to use for serialization.</param>
    /// <returns>A JSON string with diagnostics at the requested verbosity level.</returns>
    public abstract string ToString(DiagnosticsVerbosity verbosity);
}
```

### 3.4 Environment Variables

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `AZURE_COSMOS_DIAGNOSTICS_VERBOSITY` | string | `"Detailed"` | Default verbosity when not set in code. Values: `"Detailed"`, `"Summary"` |
| `AZURE_COSMOS_DIAGNOSTICS_MAX_SUMMARY_SIZE` | int | `8192` | Max bytes for summary output. Minimum: 4096 |

**Precedence order** (highest to lowest):
1. Explicit `ToString(DiagnosticsVerbosity)` parameter
2. `CosmosClientOptions.DiagnosticsVerbosity` (set in code, or populated from `AZURE_COSMOS_DIAGNOSTICS_VERBOSITY` environment variable)
3. Default: `DiagnosticsVerbosity.Detailed`

> **Note:** The parameterless `ToString()` always returns `Detailed` output for backward compatibility. To use `Summary` mode, callers must explicitly pass `DiagnosticsVerbosity.Summary` to the overload.

## 4. Summary Mode JSON Format

### 4.1 Detailed Mode (unchanged — current behavior)

```json
{
  "Summary": {
    "DirectCalls": { "(200, 0)": 1 },
    "GatewayCalls": {},
    "RegionsContacted": 1
  },
  "name": "ReadItemAsync",
  "id": "...",
  "caller info": { ... },
  "start time": "...",
  "duration in milliseconds": 45.2,
  "data": { ... },
  "children": [
    { /* full child trace for attempt 1 */ },
    { /* full child trace for attempt 2 */ },
    { /* ... one per retry ... */ }
  ]
}
```

### 4.2 Summary Mode (new)

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
        "Last": {
          "StatusCode": 200,
          "SubStatusCode": 0,
          "RequestCharge": 5.0,
          "DurationMs": 12,
          "Region": "West US 2",
          "Endpoint": "https://account-westus2.documents.azure.com",
          "RequestStartTimeUtc": "2026-02-26T21:00:05.000Z",
          "OperationType": "Read",
          "ResourceType": "Document"
        },
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
      },
      {
        "Region": "East US 2",
        "RequestCount": 10,
        "TotalRequestCharge": 45.5,
        "First": { ... },
        "Last": { ... },
        "AggregatedGroups": [ ... ]
      }
    ]
  }
}
```

### 4.3 Truncated Output (when summary exceeds max size)

If the summary JSON exceeds `MaxDiagnosticsSummarySizeBytes`, fall back to a minimal truncated indicator:

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

## 5. Summary Compaction Algorithm

### 5.1 Data Collection

Walk the `ITrace` tree (same traversal as `SummaryDiagnostics.CollectSummaryFromTraceTree()`) to collect all `StoreResponseStatistics` and `HttpResponseStatistics` entries from every `ClientSideRequestStatisticsTraceDatum` in the trace hierarchy.

### 5.2 Region Grouping

Group collected entries by `Region` (string). Entries with a null/empty region are grouped under `"Unknown"`.

### 5.3 Per-Region Summary

For each region group (ordered chronologically by request start time):

1. **First**: Full details of the chronologically first request
2. **Last**: Full details of the chronologically last request (omitted if only 1 request)
3. **Middle entries** (all except first and last): Group by `(StatusCode, SubStatusCode)`:
   - **Count**: Number of requests in this group
   - **TotalRequestCharge**: Sum of RU charges
   - **MinDurationMs / MaxDurationMs / P50DurationMs / AvgDurationMs**: Latency statistics

### 5.4 Size Enforcement

1. Serialize the summary JSON
2. If `serializedBytes <= MaxDiagnosticsSummarySizeBytes` → return as-is
3. If `serializedBytes > MaxDiagnosticsSummarySizeBytes` → return truncated output (§4.3)

### 5.5 Handling Both Direct and Gateway Requests

Both `StoreResponseStatistics` (direct mode) and `HttpResponseStatistics` (gateway mode) are collected and treated uniformly in the summary. The aggregated groups include entries from both transport paths. An optional `"TransportType"` field (`"Direct"` / `"Gateway"`) can be included in aggregated groups if needed to distinguish.

## 6. Implementation Plan — Files to Create/Modify

### 6.1 New Files

| File | Description |
|------|-------------|
| `Microsoft.Azure.Cosmos/src/Diagnostics/DiagnosticsVerbosity.cs` | `DiagnosticsVerbosity` enum |
| `Microsoft.Azure.Cosmos/src/Diagnostics/DiagnosticsSummaryWriter.cs` | Summary computation and JSON serialization logic |

### 6.2 Modified Files

| File | Change |
|------|--------|
| `CosmosClientOptions.cs` | Add `DiagnosticsVerbosity` and `MaxDiagnosticsSummarySizeBytes` properties with validation |
| `CosmosDiagnostics.cs` | Add `ToString(DiagnosticsVerbosity)` abstract overload |
| `CosmosTraceDiagnostics.cs` | Implement `ToString(DiagnosticsVerbosity)` overload; delegate to `DiagnosticsSummaryWriter` when verbosity is `Summary` |
| `TraceWriter.TraceJsonWriter.cs` | Add summary serialization path that delegates to `DiagnosticsSummaryWriter` when verbosity is `Summary` |
| `SummaryDiagnostics.cs` | Extend `CollectSummaryFromTraceTree()` to support region-grouped collection with ordering |
| `ClientSideRequestStatisticsTraceDatum.cs` | Ensure `StoreResponseStatistics` and `HttpResponseStatistics` lists are accessible for summary computation |

### 6.3 Contract/Baseline Updates

| File | Change |
|------|--------|
| `ContractEnforcementTests.cs` baseline | Update public API contract for new enum and properties |

## 7. Work Items

### WI-1: DiagnosticsVerbosity Enum & Options Plumbing
**Scope:** Create the enum, add `DiagnosticsVerbosity` and `MaxDiagnosticsSummarySizeBytes` properties to `CosmosClientOptions`, add `ToString(DiagnosticsVerbosity)` abstract overload to `CosmosDiagnostics`, add environment variable support.
**Acceptance:** `ToString(verbosity)` overloads compile and delegate correctly. Parameterless `ToString()` is unchanged (always `Detailed`). No behavioral change yet.

### WI-2: Summary Computation Engine
**Scope:** Implement `DiagnosticsSummaryWriter` — the core logic that walks the trace tree, collects stats, groups by region, computes first/last/aggregated groups, and produces the summary JSON structure.
**Acceptance:** Given an `ITrace` tree, produces the correct summary JSON. Unit-testable in isolation.

### WI-3: Summary Serialization Integration
**Scope:** Implement `CosmosTraceDiagnostics.ToString(DiagnosticsVerbosity)`. When `Summary`, delegate to `DiagnosticsSummaryWriter`. Implement size enforcement and truncated output fallback. Parameterless `ToString()` remains unchanged.
**Acceptance:** `ToString(DiagnosticsVerbosity.Summary)` returns compact summary JSON. `ToString()` (parameterless) continues to return full `Detailed` trace.

### WI-4: Contract Updates & Public API Validation
**Scope:** Update `ContractEnforcementTests` baselines for new public API surface. Ensure the new enum and properties appear in contracts.
**Acceptance:** All contract tests pass. Public API is correctly documented.

### WI-5: Unit Tests
**Scope:** Comprehensive unit tests for the summary engine covering:
- Single region, single request (no deduplication)
- Single region, many retries (deduplication with first/last)
- Multi-region failover (separate region groups)
- Mixed Direct + Gateway requests
- Edge cases: 0 requests, 1 request, 2 requests (first + last, no middle)
- Percentile computation (P50 on odd/even counts)
- Size enforcement / truncation
- Environment variable precedence
- Verbosity flow from options → diagnostics

### WI-6: Integration Tests (Emulator)
**Scope:** Emulator-based integration tests verifying:
- Summary mode produces valid, parseable JSON for real operations
- Summary mode output size is bounded
- Round-trip: create scenario with retries → verify summary captures correct counts
- Hedging/failover: multi-region scenarios produce correct region grouping

### WI-7: Baseline / Golden-File Tests
**Scope:** Create baseline JSON files for summary mode output (similar to existing `EndToEndTraceWriterBaselineTests`). Verify serialization stability across code changes.

## 8. Testing Plan

### 8.1 Unit Tests

| Test | Description |
|------|-------------|
| `DiagnosticsVerbosity_DefaultIsDetailed` | Verify enum default |
| `CosmosClientOptions_DiagnosticsVerbosity_DefaultValue` | Verify options default |
| `CosmosClientOptions_MaxSummarySizeBytes_Validation` | Min 4096 enforced |
| `CosmosClientOptions_DiagnosticsVerbosity_EnvVarFallback` | Env var populates `CosmosClientOptions.DiagnosticsVerbosity` when not set in code |
| `CosmosClientOptions_DiagnosticsVerbosity_CodeOverridesEnvVar` | Code-set value takes precedence over env var |
| `ToString_Overload_UsesSummary_WhenExplicit` | `ToString(Summary)` produces summary output |
| `Summary_SingleRegion_SingleRequest` | No deduplication, first only |
| `Summary_SingleRegion_TwoRequests` | First + last, no middle |
| `Summary_SingleRegion_ManyRetries_429` | First + last + 1 aggregated group |
| `Summary_MultiRegion_Failover` | Separate region summaries |
| `Summary_MixedStatusCodes` | Multiple aggregated groups per region |
| `Summary_DirectAndGateway_Combined` | Both transport types in summary |
| `Summary_P50_OddCount` | Percentile on odd-sized collection |
| `Summary_P50_EvenCount` | Percentile on even-sized collection |
| `Summary_P50_SingleItem` | Percentile with 1 item |
| `Summary_SizeEnforcement_UnderLimit` | Summary fits within max size |
| `Summary_SizeEnforcement_OverLimit_Truncated` | Falls back to truncated output |
| `Summary_EmptyTrace` | No requests produces minimal output |
| `Summary_RegionOrdering_Deterministic` | Regions sorted alphabetically |
| `Detailed_Mode_Unchanged` | Existing detailed output is byte-for-byte identical |
| `ToString_Parameterless_AlwaysDetailed` | Parameterless `ToString()` always returns `Detailed` output regardless of `CosmosClientOptions` setting |

### 8.2 Integration Tests (Emulator)

| Test | Description |
|------|-------------|
| `ReadItem_SummaryMode_ProducesValidJson` | Real read → summary JSON parses correctly |
| `ReadItem_SummaryMode_SizeWithinLimit` | Summary output ≤ configured max bytes |
| `QueryItems_SummaryMode_MultipleRequests` | Query with continuations → summary compacts |
| `BulkOperations_SummaryMode_HighRetryCount` | Simulate throttling → verify compaction |
| `CrossRegion_SummaryMode_RegionGroups` | Multi-region → separate region summaries (requires fault injection or mock) |

### 8.3 Baseline Tests

Create golden-file baseline JSONs for summary mode output and add to existing `EndToEndTraceWriterBaselineTests` pattern. This ensures serialization format stability.

### 8.4 Performance / Size Validation

| Scenario | Detailed Size | Expected Summary Size | Reduction |
|----------|--------------|----------------------|-----------|
| 1 request, no retries | ~2 KB | ~1 KB | ~50% |
| 10 retries, same region | ~20 KB | ~2 KB | ~90% |
| 50 retries, 2 regions | ~100 KB | ~3 KB | ~97% |
| 100 retries, 3 regions | ~200 KB | ~4 KB | ~98% |

These should be validated as part of unit tests to ensure compaction effectiveness.

## 9. Rollout & Migration

### 9.1 Backward Compatibility

- **Default is `Detailed`** — no behavioral change for existing users
- **No breaking changes** — `ToString()` output format only changes when `Summary` is explicitly opted into
- **Programmatic API unchanged** — `GetContactedRegions()`, `GetFailedRequestCount()`, etc. continue to work from the full in-memory trace regardless of verbosity

### 9.2 Recommended Rollout

1. Ship with `Detailed` as default in initial release
2. Document `Summary` mode in SDK documentation and changelog
3. Consider making `Summary` the default in a future major version after customer feedback

### 9.3 Preview vs GA

The `DiagnosticsVerbosity` enum and related options should ship as **GA** (non-preview) since it's an additive, backward-compatible feature with no impact when not opted into.

## 10. Open Questions

1. **Should `AggregatedGroups` include an `AvgDurationMs` field?** The Rust SDK only includes min/max/P50. Adding avg is cheap to compute but adds to the output size. _Decision: Include avg. It's a single field and provides useful signal._

2. **Should the summary include the `children` trace tree at all?** Currently proposed as replacing the entire trace output. An alternative is to emit the summary _alongside_ a truncated trace tree (e.g., first + last children only). _Decision: Summary replaces the full trace. The `First` and `Last` entries in each region summary provide the detailed bookends._

3. **Gateway vs Direct distinction in aggregated groups.** Should each `AggregatedGroup` indicate whether it's from Direct or Gateway transport? _Decision: Defer. The `StatusCode/SubStatusCode` combination is usually sufficient. Can add a `TransportType` field later if needed._

4. **Caching.** The Rust SDK caches serialized JSON per verbosity level via `OnceLock`. Should the .NET SDK cache the summary JSON? _Decision: Yes, use `Lazy<string>` or similar. `ToString()` may be called multiple times (logging, telemetry, etc.)._

5. **Thread safety.** `CosmosDiagnostics.Verbosity` as a settable property on a potentially shared object needs consideration. _Decision: Use a volatile field or make it thread-safe. The property is set once from `CosmosClientOptions` during response creation and read during serialization. The `ToString(DiagnosticsVerbosity)` overload avoids mutating state entirely._

## 11. References

- **Rust SDK PR:** [Azure/azure-sdk-for-rust#3592](https://github.com/Azure/azure-sdk-for-rust/pull/3592) — `DiagnosticsContext` with `Summary` and `Detailed` modes
- **Current .NET diagnostics:** `Microsoft.Azure.Cosmos/src/Diagnostics/` and `Microsoft.Azure.Cosmos/src/Tracing/`
- **Existing summary:** `SummaryDiagnostics.cs` — aggregates `(StatusCode, SubStatusCode)` counts (foundation to build on)
- **Trace tree:** `ITrace` → `Trace` with recursive children and `ClientSideRequestStatisticsTraceDatum` data
