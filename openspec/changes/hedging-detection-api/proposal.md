## Why

Users of the Azure Cosmos DB .NET SDK who enable cross-region hedging (or any
multi-region availability behavior) today have no first-class API to ask:

1. *"Did the SDK actually start hedging this operation?"*
2. *"Which regions did the SDK dispatch this operation to, and why?"*
3. *"Which regions responded?"*

The only currently supported observability path is to parse the JSON returned
by `Diagnostics.ToString()`, which is fragile, allocation-heavy, and not safe
for hot paths (logging, alerting, metrics). The closest existing API,
`Diagnostics.GetContactedRegions()`, only enumerates regions for which a
*response* was recorded and does not distinguish a dispatch reason (initial
attempt vs. retry vs. region failover vs. hedge arm). It also does not surface
"hedging began" as a single boolean.

This proposal adds a small, focused public API to `CosmosDiagnostics`
covering all three questions, while preserving the existing JSON shape of
`ToString()` so callers continue to render diagnostics the same way.

Tracking issue: [Azure/azure-cosmos-dotnet-v3#5867](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5867).

## What Changes

- **New `CosmosDiagnostics.HedgingStarted()`** — `virtual bool`, returns
  `true` if at least one dispatch on this operation was tagged as a hedge
  arm. Default implementation returns `false`.
- **New `CosmosDiagnostics.GetRequestedRegions()`** — `virtual IReadOnlyList<RequestedRegion>`,
  returns every region the SDK dispatched to in observed dispatch order,
  each tagged with a `RequestedRegionReason`. Default implementation returns
  an empty list.
- **New `CosmosDiagnostics.GetRespondedRegions()`** — `virtual IReadOnlyList<string>`,
  returns the names of all regions that produced a response, in arrival
  order, with duplicates allowed. Default implementation returns an empty
  list.
- **New `RequestedRegion`** — public readonly struct with `RegionName`,
  `Reason`, `IEquatable<RequestedRegion>` (case-insensitive on name),
  `==`/`!=` operators, custom `GetHashCode`, and a `ToString` of
  `"{regionName}:{reason}"`.
- **New `RequestedRegionReason`** — public `enum : byte` with values
  `Initial`, `OperationRetry`, `RegionFailover`, `Hedging`,
  `CircuitBreakerProbe`, `TransportRetry`. `TransportRetry` and
  `CircuitBreakerProbe` are reserved for future use and are not populated
  by the v1 .NET implementation.
- **Internal `HedgingDetectionState`** — per-trace lock-protected state
  attached to `TraceSummary` (the same pattern used today for
  `RegionsContacted`). Hosts the three lists/flag, exposes
  `AppendRequested(string, RequestedRegionReason)`,
  `AppendResponded(string)`, and snapshot getters. Defines an internal
  `DispatchReasonPropertyKey` used to signal the dispatch reason from
  upstream sites (`ClientRetryPolicy`,
  `CrossRegionHedgingAvailabilityStrategy`) to the downstream dispatch
  site (`TransportHandler`).
- **Wiring at four sites**:
  1. `CrossRegionHedgingAvailabilityStrategy.CloneAndSendAsync` — set
     `Properties[DispatchReasonPropertyKey] = Hedging` for hedge arms
     (`requestNumber > 0`).
  2. `ClientRetryPolicy.OnBeforeSendRequest` — after route resolution,
     when a retry is in flight, set the reason to `OperationRetry` or
     `RegionFailover` based on `retryContext.RetryRequestOnPreferredLocations`.
  3. `TransportHandler.ProcessMessageAsync` — after `ToDocumentServiceRequest`,
     resolve the region for `LocationEndpointToRoute` via
     `GlobalEndpointManager.GetLocation`, consume the property (default to
     `Initial`) and append a `RequestedRegion`. Removes the property so
     subsequent retries default unless a new reason is set.
  4. `ClientSideRequestStatisticsTraceDatum` — at the three response-record
     sites (Direct response, HTTP response, HTTP exception) append the
     responding region name to `HedgingDetectionState`, alongside the
     existing `AddRegionContacted` call.

## Capabilities

### New Capabilities

- `hedging-detection`: Public APIs on `CosmosDiagnostics` to efficiently
  observe whether the SDK started hedging, the regions it dispatched to and
  why, and the regions that responded — without parsing diagnostics strings.

### Modified Capabilities

<!-- None — this is a purely additive public surface. -->

## Impact

- **Public API surface**: three new virtual methods on `CosmosDiagnostics`,
  one new readonly struct (`RequestedRegion`), one new enum
  (`RequestedRegionReason`). All additions are non-breaking — every new
  member is virtual with a safe default, struct equality is well-defined,
  enum has reserved values for future expansion.
- **Affected files**:
  - `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnostics.cs`
  - `Microsoft.Azure.Cosmos/src/Diagnostics/CosmosTraceDiagnostics.cs`
  - `Microsoft.Azure.Cosmos/src/Diagnostics/RequestedRegion.cs` *(new)*
  - `Microsoft.Azure.Cosmos/src/Diagnostics/RequestedRegionReason.cs` *(new)*
  - `Microsoft.Azure.Cosmos/src/Tracing/HedgingDetectionState.cs` *(new)*
  - `Microsoft.Azure.Cosmos/src/Tracing/TraceSummary.cs`
  - `Microsoft.Azure.Cosmos/src/Routing/AvailabilityStrategy/CrossRegionHedgingAvailabilityStrategy.cs`
  - `Microsoft.Azure.Cosmos/src/ClientRetryPolicy.cs`
  - `Microsoft.Azure.Cosmos/src/Handler/TransportHandler.cs`
  - `Microsoft.Azure.Cosmos/src/Tracing/TraceData/ClientSideRequestStatisticsTraceDatum.cs`
- **Performance**: O(1) per dispatch and per response append (single lock
  acquire + list add). State is held off the trace tree, so the JSON shape
  produced by `Diagnostics.ToString()` is unchanged.
- **API contract files**: `DotNetSDKAPI.net6.json` updated to include the
  three new methods, the new struct, and the new enum.
- **Reserved enum values**: `RequestedRegionReason.TransportRetry` and
  `RequestedRegionReason.CircuitBreakerProbe` are reserved for the future
  and not populated in v1 — PPCB rerouting decisions happen inside the
  closed-source `Microsoft.Azure.Cosmos.Direct` package after the dispatch
  append site, and per-channel transport retries inside Direct are not
  surfaced at this layer yet.
