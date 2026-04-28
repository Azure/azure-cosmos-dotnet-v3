# Diagnostics Compaction — Tasks

## Task 1: DiagnosticsVerbosity Enum & Options Plumbing

**Scope:** Create the enum, add `DiagnosticsVerbosity` and `MaxDiagnosticsSummarySizeBytes` properties to `CosmosClientOptions`, add `ToString(DiagnosticsVerbosity)` abstract overload to `CosmosDiagnostics`, add environment variable support.

**Acceptance:** `ToString(verbosity)` overloads compile and delegate correctly. Parameterless `ToString()` is unchanged (always `Detailed`). No behavioral change yet.

**Spec requirements:** Diagnostics Verbosity (default verbosity, parameterless ToString, environment variable configuration, code-level override, verbosity precedence)

## Task 2: Summary Computation Engine

**Scope:** Implement `DiagnosticsSummaryWriter` — the core logic that walks the trace tree, collects stats, groups by region, computes first/last/aggregated groups, and produces the summary JSON structure.

**Acceptance:** Given an `ITrace` tree, produces the correct summary JSON. Unit-testable in isolation.

**Spec requirements:** Summary mode region grouping, first/last preservation, single request region, aggregated groups, mixed Direct and Gateway, region ordering

## Task 3: Summary Serialization Integration

**Scope:** Implement `CosmosTraceDiagnostics.ToString(DiagnosticsVerbosity)`. When `Summary`, delegate to `DiagnosticsSummaryWriter`. Implement size enforcement and truncated output fallback. Implement caching. Parameterless `ToString()` remains unchanged.

**Acceptance:** `ToString(DiagnosticsVerbosity.Summary)` returns compact summary JSON. `ToString()` (parameterless) continues to return full `Detailed` trace.

**Spec requirements:** In-memory trace tree unchanged, size enforcement, size under limit, summary mode caching, Summary JSON Format, truncated output format

## Task 4: Contract Updates & Public API Validation

**Scope:** Update `ContractEnforcementTests` baselines for new public API surface. Ensure the new enum and properties appear in contracts.

**Acceptance:** All contract tests pass. Public API is correctly documented.

## Task 5: Unit Tests

**Scope:** Comprehensive unit tests for the summary engine.

| Test | Description | Spec Requirement |
|------|-------------|------------------|
| `DiagnosticsVerbosity_DefaultIsDetailed` | Verify enum default | Default verbosity is Detailed |
| `CosmosClientOptions_DiagnosticsVerbosity_DefaultValue` | Verify options default | Default verbosity is Detailed |
| `CosmosClientOptions_MaxSummarySizeBytes_Validation` | Min 4096 enforced | MaxDiagnosticsSummarySizeBytes minimum validation |
| `CosmosClientOptions_DiagnosticsVerbosity_EnvVarFallback` | Env var populates options | Environment variable configuration |
| `CosmosClientOptions_DiagnosticsVerbosity_CodeOverridesEnvVar` | Code takes precedence | Code-level value overrides env var |
| `ToString_Overload_UsesSummary_WhenExplicit` | `ToString(Summary)` produces summary | Verbosity precedence |
| `Summary_SingleRegion_SingleRequest` | No deduplication, first only | Single request region |
| `Summary_SingleRegion_TwoRequests` | First + last, no middle | First/last preservation |
| `Summary_SingleRegion_ManyRetries_429` | First + last + 1 aggregated group | Aggregated groups |
| `Summary_MultiRegion_Failover` | Separate region summaries | Region grouping |
| `Summary_MixedStatusCodes` | Multiple aggregated groups per region | Aggregated groups |
| `Summary_DirectAndGateway_Combined` | Both transport types in summary | Mixed Direct and Gateway |
| `Summary_P50_OddCount` | Percentile on odd-sized collection | Aggregated groups |
| `Summary_P50_EvenCount` | Percentile on even-sized collection | Aggregated groups |
| `Summary_P50_SingleItem` | Percentile with 1 item | Aggregated groups |
| `Summary_SizeEnforcement_UnderLimit` | Summary fits within max size | Size under limit |
| `Summary_SizeEnforcement_OverLimit_Truncated` | Falls back to truncated output | Size enforcement |
| `Summary_EmptyTrace` | No requests produces minimal output | Region grouping |
| `Summary_RegionOrdering_Deterministic` | Regions sorted alphabetically | Region ordering |
| `Detailed_Mode_Unchanged` | Existing detailed output is byte-for-byte identical | Parameterless ToString |
| `ToString_Parameterless_AlwaysDetailed` | Parameterless always returns Detailed | Parameterless ToString |

## Task 6: Integration Tests (Emulator)

| Test | Description | Spec Requirement |
|------|-------------|------------------|
| `ReadItem_SummaryMode_ProducesValidJson` | Real read → summary JSON parses correctly | Summary JSON Format |
| `ReadItem_SummaryMode_SizeWithinLimit` | Summary output ≤ configured max bytes | Size under limit |
| `QueryItems_SummaryMode_MultipleRequests` | Query with continuations → summary compacts | Aggregated groups |
| `BulkOperations_SummaryMode_HighRetryCount` | Simulate throttling → verify compaction | Aggregated groups |
| `CrossRegion_SummaryMode_RegionGroups` | Multi-region → separate region summaries | Region grouping |

## Task 7: Baseline / Golden-File Tests

**Scope:** Create baseline JSON files for summary mode output (similar to existing `EndToEndTraceWriterBaselineTests`). Verify serialization stability across code changes.

**Spec requirements:** Summary JSON Format, truncated output format

## Task 8: Changelog & Documentation

**Scope:** Update `changelog.md` with the new feature. Update `.github/copilot-instructions.md` if diagnostics verbosity affects AI assistant behavior.

## Expected Size Reductions

| Scenario | Detailed Size | Expected Summary Size | Reduction |
|----------|--------------|----------------------|-----------|
| 1 request, no retries | ~2 KB | ~1 KB | ~50% |
| 10 retries, same region | ~20 KB | ~2 KB | ~90% |
| 50 retries, 2 regions | ~100 KB | ~3 KB | ~97% |
| 100 retries, 3 regions | ~200 KB | ~4 KB | ~98% |
