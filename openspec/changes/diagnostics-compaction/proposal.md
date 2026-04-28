# Diagnostics Compaction — Proposal

## Problem

`CosmosDiagnostics.ToString()` produces a JSON trace that grows **unboundedly** with retries. Each retry attempt creates a new child `ITrace` node containing a full `ClientSideRequestStatisticsTraceDatum` with complete `StoreResponseStatistics` and `HttpResponseStatistics` entries. In pathological scenarios (sustained 429 throttling, transient failures, cross-region failovers), a single operation's diagnostics can grow to hundreds of KB.

**Impact:**
- **Log truncation** — monitoring systems (Application Insights, Azure Monitor, etc.) silently drop oversized log entries
- **Memory pressure** — large diagnostic strings increase GC overhead, especially at high throughput
- **Readability** — operators cannot quickly extract signal from noise when hundreds of identical retry entries are listed

**Example scenario:** A point read that encounters 50 retries due to 429 throttling in West US 2, then fails over to East US 2 with 10 more retries, produces ~60 full `StoreResponseStatistics` entries in the trace tree. With summary mode, this compacts to: first request + last request + 1 aggregated group per region.

## Proposed Approach

Introduce a **`DiagnosticsVerbosity`** concept (modeled after [Azure/azure-sdk-for-rust#3592](https://github.com/Azure/azure-sdk-for-rust/pull/3592)) that controls how `CosmosDiagnostics.ToString()` serializes trace data:

| Mode | Behavior | Use Case |
|------|----------|----------|
| **Detailed** (default) | Current behavior — full trace tree output | Debugging, development |
| **Summary** | Region-grouped compaction with first/last + aggregated middle | Production logging, size-constrained environments |

**Key design principle:** The in-memory representation (`ITrace` tree, `ClientSideRequestStatisticsTraceDatum`) stays **unchanged**. Compaction only happens at **serialization time** in the `TraceJsonWriter` path. This preserves full programmatic access to diagnostics data while reducing serialized output size.

## SDK Area

- **Primary:** Diagnostics
- **Secondary:** Client-config (new options properties)

## Preview vs GA

The `DiagnosticsVerbosity` enum and related options should ship as **GA** (non-preview) since it's an additive, backward-compatible feature with no impact when not opted into.

## Backward Compatibility

- **Default is `Detailed`** — no behavioral change for existing users
- **No breaking changes** — `ToString()` output format only changes when `Summary` is explicitly opted into
- **Programmatic API unchanged** — `GetContactedRegions()`, `GetFailedRequestCount()`, etc. continue to work from the full in-memory trace regardless of verbosity

## Rollout Strategy

1. Ship with `Detailed` as default in initial release
2. Document `Summary` mode in SDK documentation and changelog
3. Consider making `Summary` the default in a future major version after customer feedback

## Non-Goals

- Changing the in-memory `ITrace` tree structure
- Modifying the `Detailed` mode output format
- Adding new programmatic APIs beyond `ToString(DiagnosticsVerbosity)` overload
- Per-request verbosity override via `RequestOptions` (can be added later)

## Resolved Questions

1. **Should `AggregatedGroups` include an `AvgDurationMs` field?** The Rust SDK only includes min/max/P50. Adding avg is cheap to compute but adds to the output size. _Decision: Include avg. It's a single field and provides useful signal._

2. **Should the summary include the `children` trace tree at all?** Currently proposed as replacing the entire trace output. An alternative is to emit the summary _alongside_ a truncated trace tree (e.g., first + last children only). _Decision: Summary replaces the full trace. The `First` and `Last` entries in each region summary provide the detailed bookends._

3. **Gateway vs Direct distinction in aggregated groups.** Should each `AggregatedGroup` indicate whether it's from Direct or Gateway transport? _Decision: Defer. The `StatusCode/SubStatusCode` combination is usually sufficient. Can add a `TransportType` field later if needed._

4. **Caching.** The Rust SDK caches serialized JSON per verbosity level via `OnceLock`. Should the .NET SDK cache the summary JSON? _Decision: Yes, use `Lazy<string>` or similar. `ToString()` may be called multiple times (logging, telemetry, etc.)._

5. **Thread safety.** `CosmosDiagnostics.Verbosity` as a settable property on a potentially shared object needs consideration. _Decision: Use the `ToString(DiagnosticsVerbosity)` overload which avoids mutating state entirely. The property is set once from `CosmosClientOptions` during response creation and read during serialization._

## References

- **Rust SDK PR:** [Azure/azure-sdk-for-rust#3592](https://github.com/Azure/azure-sdk-for-rust/pull/3592) — `DiagnosticsContext` with `Summary` and `Detailed` modes
- **Current .NET diagnostics:** `Microsoft.Azure.Cosmos/src/Diagnostics/` and `Microsoft.Azure.Cosmos/src/Tracing/`
- **Existing summary:** `SummaryDiagnostics.cs` — aggregates `(StatusCode, SubStatusCode)` counts (foundation to build on)
- **Trace tree:** `ITrace` → `Trace` with recursive children and `ClientSideRequestStatisticsTraceDatum` data
- **Related spec:** `openspec/specs/diagnostics-and-observability/spec.md`
