## Why

Users of the Azure Cosmos DB .NET SDK who enable cross-region hedging via `AvailabilityStrategy.CrossRegionHedgingStrategy()` have no performant, first-class API to determine whether a given response was produced by a hedged request. Today the only way to detect hedging is to call `Diagnostics.ToString()` and search the resulting JSON for `"Hedge Context"` — a string-parsing approach that is fragile, allocates heavily, and is inappropriate for hot paths (logging, alerting, metrics).

## What Changes

- **New virtual method `IsHedged()` on `CosmosDiagnostics`** — returns `bool` indicating whether the availability strategy activated hedging for this request. Default implementation returns `false` (preserves back-compat).
- **New virtual method `GetHedgedRegions()` on `CosmosDiagnostics`** — returns `IReadOnlyList<string>` containing all regions that received hedge requests (in the order they were dispatched), or an empty list when not hedged. Default implementation returns an empty list.
- **Internal boolean flag on `CosmosTraceDiagnostics`** — set directly by `CrossRegionHedgingAvailabilityStrategy` when a hedge is activated, avoiding any trace-tree walking or dictionary lookup at query time.
- **Internal string field on `CosmosTraceDiagnostics`** — captures the list of all hedged region names, set by the hedging strategy alongside the existing `AddOrUpdateDatum` calls.

## Capabilities

### New Capabilities
- `hedging-detection`: Public APIs on `CosmosDiagnostics` to efficiently detect hedging status and the hedged regions without parsing diagnostics strings.

### Modified Capabilities
<!-- No existing specs to modify -->

## Impact

- **Public API surface**: Two new virtual methods on `CosmosDiagnostics` (non-breaking — default implementations provided).
- **Affected code**:
  - `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnostics.cs` — add virtual methods
  - `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosTraceDiagnostics.cs` — add internal fields + overrides
  - `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/CrossRegionHedgingAvailabilityStrategy.cs` — set flags on response diagnostics
- **Performance**: Zero overhead on non-hedged paths (field defaults to `false`). On hedged paths, a single boolean + list assignment replaces no new work (piggybacks on the existing `AddOrUpdateDatum` call site).
- **Dependencies**: No new external dependencies. Internal-only wiring between hedging strategy and diagnostics.
- **API contract files**: Will require contract update for the new public methods.
