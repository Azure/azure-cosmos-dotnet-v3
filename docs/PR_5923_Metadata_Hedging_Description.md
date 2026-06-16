# CrossRegionalHedging: Adds Metadata Hedging Support (PR #5923)

> Corrected and detailed description for PR
> [#5923](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5923).
> Closes issue [#5917](https://github.com/Azure/azure-cosmos-dotnet-v3/issues/5917).
> Full design: [docs/PPAF_Metadata_Hedging_ColdStart_Design.md](./PPAF_Metadata_Hedging_ColdStart_Design.md).

## Summary

Adds **bounded, cold-start cross-region hedging** for metadata cache population so a
slow primary region no longer dominates the first-operation latency tail while the
`Collection` and `PartitionKeyRange` metadata caches are being populated for the
first time.

The feature is **internal in Phase 1** — there is no public configuration API and
no public API/contract surface. The effective opt-in follows the account's PPAF
(Per-Partition Automatic Failover) state and can be force-enabled or suppressed
through an environment variable.

## What changed

| Area | Change |
| --- | --- |
| `MetadataHedgingStrategy` (new) | One instance per `CosmosClient`; orchestrates a single bounded cross-region hedge for an eligible cold-start metadata read. |
| `MetadataRegionalFailureClassifier` (new) | Neutral, shared static (`IsRegionalFailure`) consumed by **both** the strategy and `MetadataRequestThrottleRetryPolicy` — the retry policy no longer depends on the hedging type for its core classification. |
| `ClientCollectionCache` / `PartitionKeyRangeCache` | Wired to consume the strategy for cold-start (first-population) metadata reads. Collection `Read` and `PartitionKeyRange` `ReadFeed` (first page only). |
| `MetadataRequestThrottleRetryPolicy` | Skips preferred-location indices a hedge already burned (cross-region dedup), capping total attempts at the preferred-region count. Receives the hedge context through the narrow `IMetadataHedgeContextReceiver` seam. |
| `ClearingSessionContainerClientRetryPolicy` | Forwards the hedge context to its inner policy via `AttachHedgeContext` so dedup survives policy wrapping. |
| `ConfigurationManager` | `AZURE_COSMOS_METADATA_HEDGING_FOR_COLDSTART_ENABLED` env var + `GetMetadataHedgingForColdStartOptIn()` resolver (env-var only — no public option). |
| Telemetry | New `Azure.Cosmos.Client.MetadataHedging` Meter (`internal MetadataHedgingMetrics`, no public/contract surface), `CosmosDbEventSource` events, and a `Metadata Hedge Context` diagnostics datum. |

## How it works

- A single `MetadataHedgingStrategy` is created per `CosmosClient` and consumed by
  `ClientCollectionCache` (Collection `Read`) and `PartitionKeyRangeCache`
  (`PartitionKeyRange` `ReadFeed`, **first page only**).
- On an eligible cold-start metadata read the strategy issues the **primary**
  request and starts a timer. If the primary has not produced an *acceptable*
  response within the threshold, it dispatches a **single hedged** request to a
  second preferred region and returns the first acceptable winner. The losing
  branch is cancelled and cleaned up in the background (response body + CTS
  disposed, budget released).
- **Threshold** is SDK-derived and **not customer-configurable**:
  `HttpTimeoutPolicyControlPlaneRetriableHotPath.FirstAttemptTimeout` (1 s) +
  `500 ms` = **1.5 s**.
- **Concurrency** is bounded by a per-client budget (`SemaphoreSlim`, default **8**)
  acquired with a non-blocking `Wait(TimeSpan.Zero)`; when exhausted the request
  transparently falls back to primary-only.
- Each branch operates on an **independent `DocumentServiceRequest.Clone()`**, so
  concurrent `RouteToLocation` calls never corrupt one another's target region.
- **Cross-region dedup**: the hedge records the regions it touched in
  `AttemptedEndpoints`; `MetadataRequestThrottleRetryPolicy` then skips preferred-
  location indices a hedge already burned, capping total attempts at the preferred-
  region count. The context is handed to the retry policy through the narrow
  `IMetadataHedgeContextReceiver` seam (forwarded by
  `ClearingSessionContainerClientRetryPolicy`) rather than a fragile concrete-type
  cast.
- **Shared failure classification**: the regional-failure list (503/500,
  `Gone` + `LeaseNotFound`, `Forbidden` + `DatabaseAccountNotFound`,
  `HttpRequestException`, non-user `OperationCanceledException`) lives in the
  neutral `MetadataRegionalFailureClassifier` used by both the strategy and the
  retry policy. A hedge-branch `401` / plain `403` is **never** accepted as a
  winner, and the cross-region auth-reject signal is captured for **both** returned
  responses and thrown `DocumentClientException`s (the `GatewayStoreModel` path).
- **Budget safety**: hedge-endpoint resolution (`GetApplicableEndpoints` +
  `FirstOrDefault`) runs **inside** the `try`/`finally` so that a throw (concurrent
  location-cache refresh/failover) or a null result still releases the permit — a
  permit can no longer leak for the lifetime of the client.

## Enablement (Phase 1 — internal)

There is **no public configuration surface** in Phase 1. The effective opt-in is
tri-state and resolved as **environment variable → PPAF state**:

| Setting | `null` (unset / invalid) | `true` | `false` |
| --- | --- | --- | --- |
| `AZURE_COSMOS_METADATA_HEDGING_FOR_COLDSTART_ENABLED` | follow PPAF | force-enable (even non-PPAF) | suppress (kill switch) |

When the variable is unset, hedging follows the account's PPAF state — **active for
PPAF-enabled multi-region accounts** and **off by default for non-PPAF accounts**.
The Gateway kill-switch seam (`disableCrossRegionalHedging`) is wired into the
eligibility check and is **hard-wired off in Phase 1**.

Resolution precedence:

1. The `AZURE_COSMOS_METADATA_HEDGING_FOR_COLDSTART_ENABLED` env var (when a valid bool) wins.
2. Otherwise `null` → follow the live PPAF state (`customerOptIn ?? isPpafEnabled`).

## Eligibility gate

A live strategy is only constructed when hedging is not explicitly disabled. The
per-request gate in `EvaluateEligibility` skips (falls back to primary-only) for any
of the following reasons:

- Opt-in disabled / Gateway kill-switch on / PPAF disabled (when opt-in is `null`).
- Not a cold-start (first-population) cache read.
- Already hedged this operation.
- Unsupported resource type (only `Collection` and `PartitionKeyRange`).
- `PartitionKeyRange` read that is not the first read-feed page.
- Single-region account (≤ 1 read endpoint, or excluded regions leave no target).
- Concurrency budget exhausted.

## Observability

- New `Azure.Cosmos.Client.MetadataHedging` Meter
  (`hedge fires` / `hedge wins` / `budget exhausted` / `late loser` / `auth reject`).
- `CosmosDbEventSource` events for the same lifecycle points.
- A `Metadata Hedge Context` diagnostics datum (eligibility, regions, threshold,
  fired-elapsed, winner/loser outcome).

## Type of change

New feature (non-breaking change which adds functionality). No public API surface
in Phase 1; observable only through diagnostics/telemetry.

---

## Flow diagram

The eligibility + dispatch decision flow for a single cold-start metadata read:

```mermaid
flowchart TD
    A[Cold-start metadata read<br/>Collection Read / PKRange ReadFeed first page] --> B{Strategy enabled?<br/>opt-in != false}
    B -- No --> P[Primary-only send]
    B -- Yes --> C{EvaluateEligibility}

    C -- "Opt-in disabled" --> P
    C -- "Gateway kill-switch on" --> P
    C -- "PPAF disabled and opt-in null" --> P
    C -- "Not cold start" --> P
    C -- "Already hedged this op" --> P
    C -- "Unsupported resource type" --> P
    C -- "Not first PKRange page" --> P
    C -- "Single region / no target" --> P
    C -- Eligible --> D{Acquire budget permit<br/>Wait TimeSpan.Zero}

    D -- "Budget exhausted" --> P
    D -- "Permit acquired" --> E{Resolve hedge endpoint<br/>inside try/finally}
    E -- "null / throws" --> R[Release permit] --> P
    E -- "Distinct region found" --> F[Send PRIMARY on clone<br/>start 1.5s timer]

    F --> G{Primary done<br/>before threshold?}
    G -- "Yes and acceptable" --> W1[Primary wins<br/>TotalAttempts = 1<br/>cancel timer]
    G -- "Threshold elapsed<br/>or primary faulted" --> K{Re-check<br/>Gateway kill-switch}
    K -- "On" --> W2[Await primary<br/>no hedge fired]
    K -- "Off" --> H[Fire HEDGE on second clone<br/>record AttemptedEndpoints<br/>mark hedged-this-op]

    H --> I{First ACCEPTABLE<br/>winner}
    I -- "Primary acceptable" --> WP[Primary wins<br/>TotalAttempts = 2]
    I -- "Hedge acceptable" --> WH[Hedge wins<br/>TotalAttempts = 2]
    I -- "Both regional failures" --> WF[Prefer primary outcome<br/>retry policy classifies]

    W1 --> Z[Return winner]
    W2 --> Z
    WP --> CL[Cancel + background-clean loser<br/>release budget]
    WH --> CL
    WF --> CL
    CL --> Z
    P --> Z
```

## Sequence diagram

The cold-start path where the primary region is slow and the hedge wins, including
cross-region dedup hand-off to the retry policy:

```mermaid
sequenceDiagram
    autonumber
    participant Cache as ClientCollectionCache /<br/>PartitionKeyRangeCache
    participant Strat as MetadataHedgingStrategy
    participant Budget as Hedge budget<br/>(SemaphoreSlim, 8)
    participant GEM as GlobalEndpointManager
    participant R1 as Primary region
    participant R2 as Secondary region
    participant Retry as MetadataRequestThrottleRetryPolicy

    Cache->>Strat: ExecuteAsync(request, sendToEndpoint, hedgeContext)
    Strat->>Strat: EvaluateEligibility(request, hedgeContext)
    Note over Strat: cold start, supported type,<br/>multi-region, not yet hedged → Eligible

    Strat->>Budget: Wait(TimeSpan.Zero)
    Budget-->>Strat: permit acquired
    Strat->>GEM: GetApplicableEndpoints(read)
    GEM-->>Strat: [R1, R2, ...]
    Note over Strat: hedgeEndpoint = first != primary (R2)

    Strat->>R1: send PRIMARY (request.Clone() → R1)
    Strat->>Strat: start 1.5s timer

    Note over Strat,R1: primary slow — threshold (1.5s) elapses
    Strat->>Strat: re-check Gateway kill-switch (off)
    Strat->>R2: fire HEDGE (request.Clone() → R2)
    Strat->>Strat: record AttemptedEndpoints{R1,R2}<br/>mark hedged-this-operation<br/>Meter: hedge fires

    R2-->>Strat: acceptable response (winner)
    Note over Strat: hedge wins → Meter: hedge wins<br/>RecordWinner(R2)
    Strat->>R1: cancel loser CTS
    Strat--)R1: background cleanup (dispose body+CTS,<br/>release budget, set LoserOutcome)

    Strat-->>Cache: MetadataHedgingResult(response, R2, hedgeFired: true)
    Cache->>Retry: AttachHedgeContext(hedgeContext)
    Note over Retry: on subsequent retry, skip preferred indices<br/>already burned (R1, R2) → caps total attempts
```

### Background cleanup & budget lifecycle

```mermaid
sequenceDiagram
    autonumber
    participant Strat as MetadataHedgingStrategy
    participant Loser as Loser branch task
    participant Budget as Hedge budget

    Note over Strat: winner selected, ownership of<br/>loser CTS transferred to cleanup
    Strat->>Loser: cancel via loser CTS
    Strat--)Strat: _ = BackgroundCleanupAsync(loser, loserCts, diag)
    activate Strat
    Strat->>Loser: await (swallow OCE / faults)
    Loser-->>Strat: settled (late loser)
    Strat->>Strat: dispose response body + CTS<br/>set diag.LoserOutcome<br/>Meter: late loser
    Strat->>Budget: Release()
    deactivate Strat
    Note over Budget: permit always returned —<br/>resolution faults release inside try/finally
```
