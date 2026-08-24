# Design — Hedging Detection API

## Context

The .NET SDK ships a `CrossRegionHedgingAvailabilityStrategy` that races a
primary request and one or more secondary "hedge arm" requests across
regions. Today, a caller cannot determine whether hedging actually started
for a given operation without serializing the entire diagnostics trace to
JSON and string-matching for a `"Hedge Context"` datum. That is fragile
(format may change), allocation-heavy (deep tree walk + JSON), and not
viable on hot paths.

This change adds a small, focused public API surface to `CosmosDiagnostics`
that exposes:

1. Whether the SDK started hedging (`HedgingStarted`).
2. The full list of region dispatches with reason
   (`GetRequestedRegions`).
3. The list of regions that actually produced a response
   (`GetRespondedRegions`).

## Decisions

### 1. State lives off the trace tree

The new state is stored on a new `HedgingDetectionState` class attached to
`TraceSummary`, **not** as a `TraceDatum` in the trace tree. The
`CosmosDiagnostics.ToString()` JSON shape is therefore unchanged. This
mirrors how `TraceSummary.RegionsContacted` is already populated and read
today.

### 2. `CosmosTraceDiagnostics` delegates to `TraceSummary.HedgingDetectionState`

The internal/concrete `CosmosTraceDiagnostics` is constructed lazily at
response time from a finished root `ITrace`. It is not available during the
request lifecycle. State therefore lives on `TraceSummary`, which is
created at the start of the operation and shared across the whole trace
tree. `CosmosTraceDiagnostics` overrides
`HedgingStarted` / `GetRequestedRegions` / `GetRespondedRegions` and forwards
to `this.Value?.Summary?.HedgingDetectionState`.

### 3. Cross-handler dispatch-reason signaling via `Properties`

`ClientRetryPolicy` (which knows when a retry is fired) does not have
direct access to the routing endpoint URI used by the dispatch site, and
`CrossRegionHedgingAvailabilityStrategy` runs above `RequestInvokerHandler`
whereas the dispatch site is in `TransportHandler`. To carry the
"why is this dispatch happening" signal across handlers without changing
internal contracts, we use `RequestMessage.Properties` (already a
`Dictionary<string,object>` that is deep-cloned per hedge arm by
`RequestMessage.Clone`) with a well-known internal key
`HedgingDetectionState.DispatchReasonPropertyKey`. The value is a
`RequestedRegionReason`. `TransportHandler` reads the key (defaults to
`Initial`), records the dispatch, and removes the key so subsequent
retries on the same `RequestMessage` re-default unless explicitly set.

### 4. Region resolution at dispatch time

`DocumentServiceRequest.RequestContext.RegionName` is not populated until
the dispatch chain (gateway / address resolver) sets it, which is after
the append site. Instead, the dispatch site resolves the region name
from the routing endpoint URI:

```csharp
string regionName = globalEndpointManager.GetLocation(
    serviceRequest.RequestContext.LocationEndpointToRoute);
```

If the URI cannot be resolved to a known region the append is skipped
rather than recording an "unknown" sentinel.

### 5. Hedge primary tagging

Within `CrossRegionHedgingAvailabilityStrategy.CloneAndSendAsync`,
`requestNumber == 0` is the primary arm and is left untagged so the
dispatch site records it as `Initial`. `requestNumber > 0` is a hedge arm
and is explicitly tagged `Hedging`. Because hedge arms 1..N are only
launched after their respective threshold delays elapse without primary
cancellation, **no phantom `Hedging` entry is structurally possible** — if
the primary wins under threshold, `CloneAndSendAsync` is never invoked for
the hedge arms.

### 6. Lock-protected snapshots

All three accessors (`HedgingStarted`, `GetRequestedRegions`,
`GetRespondedRegions`) acquire the same private lock and return snapshots
via `ToArray()` so callers cannot observe a torn list and cannot mutate
internal state. Internal appends acquire the same lock.

### 7. Duplicates in responded regions are allowed

The same physical region may respond more than once for a single operation
(e.g., gateway address response then per-replica responses). The responded
list preserves arrival order and duplicates — matching how
`AddRegionContacted` records contacts today.

### 8. Reserved-but-unpopulated enum values

`RequestedRegionReason.TransportRetry` and
`RequestedRegionReason.CircuitBreakerProbe` are part of the public surface
for forward compatibility but are not populated by v1 .NET:

- **TransportRetry** — per-channel retries inside Direct are not surfaced
  at this layer.
- **CircuitBreakerProbe** — PPCB rerouting decisions
  (`GlobalPartitionEndpointManagerCore.TryAddPartitionLevelLocationOverride`)
  occur inside `storeProxy.ProcessMessageAsync`, after the
  pre-dispatch append site. Wiring this requires a flag on
  Direct's `DocumentServiceRequestContext`, which is in a separate
  closed-source package and tracked as a follow-up.

XML docs and CHANGELOG note this clearly so callers won't be surprised by
absence.

## Alternatives Considered

### A. Add a new `TraceDatum` for hedging state

Considered and rejected. It would change the
`Diagnostics.ToString()` JSON shape — a *de facto* contract that customers
parse today, which would risk breaking dashboards and logs even though it
isn't documented. The off-tree state on `TraceSummary` is also faster to
read (no tree walk).

### B. Store directly on `CosmosTraceDiagnostics`

The published spec sketches the state directly on `CosmosTraceDiagnostics`,
but that class is built lazily from the root trace at response time and
isn't available during the request lifecycle. Putting state on
`TraceSummary` (one per operation, shared across the tree) gives us the
same observable semantics with a natural lifecycle.

### C. Add a new internal contract for cross-handler signaling

Considered, but `RequestMessage.Properties` already exists, is already
deep-cloned per hedge arm, and is already shared with
`DocumentServiceRequest.Properties`. Using it avoids a new internal
contract and matches how other features (e.g., partition-key range
override) already piggyback on `Properties`.

## AC Coverage Matrix

| AC   | Mechanism |
| ---- | --------- |
| AC1  | `HedgingDetectionState.HedgingStarted` flag, flipped on first `RequestedRegionReason.Hedging` append. |
| AC2  | Hedge arm reason set only inside `requestNumber > 0` branch; threshold delay gates the dispatch. |
| AC3  | `TransportHandler` appends to `GetRespondedRegions` via `ClientSideRequestStatisticsTraceDatum`; `HedgingStarted` is false. |
| AC4  | `ClientRetryPolicy.OnBeforeSendRequest` tags `OperationRetry` for same-region retries. |
| AC5  | `ClientRetryPolicy.OnBeforeSendRequest` tags `RegionFailover` when `RetryRequestOnPreferredLocations` is true. |
| AC6  | ⏸ Deferred. `RequestedRegionReason.CircuitBreakerProbe` reserved; wiring requires Direct-package change. |
| AC7  | `RequestedRegionReason.TransportRetry` reserved; per-channel retries not surfaced in v1. |
| AC8  | Hedge winner is appended both to `GetRequestedRegions` (with `Hedging`) and to `GetRespondedRegions`. |
| AC9  | Default virtual implementations on `CosmosDiagnostics` return `false` / empty. Covered by `CosmosDiagnosticsBackwardCompatTests`. |
| AC10 | `IReadOnlyList<T>` is returned for both list accessors; snapshots are immutable arrays. |
| AC11 | State held off the trace tree → `ToString()` JSON shape unchanged. |
| AC12 | New struct (`RequestedRegion`) provides `Equals`/`==`/`GetHashCode`/`ToString` with case-insensitive name. |
| AC13 | No phantom `Hedging` entry — see Decision 5. |
| AC14 | Duplicates in `GetRespondedRegions` are allowed and preserved. |
| AC15 | Lock-protected snapshots; concurrent appends are safe — covered by `HedgingDetectionStateTests.ConcurrentAppends_AreThreadSafe`. |
| AC16 | Live multi-region smoke test runs against an existing CosmosClient with `AvailabilityStrategy` configured. |
