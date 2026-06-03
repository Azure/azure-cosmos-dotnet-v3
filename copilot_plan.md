# Metadata Hedging — Staged Implementation Plan

Source: [docs/PPAF_Metadata_Hedging_ColdStart_Design.md](docs/PPAF_Metadata_Hedging_ColdStart_Design.md)
Branch: `users/kundadebdatta/5917_implement_metadata_hedging`
One commit per stage.

---

## Open dependencies (read before Stage 1)

- **PR [#5829](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5829)** — adds `DocumentClient.IsHedgingDisabledByGateway`. Eligibility rule 1 (§6) reads it. **Not yet on `main`.** Stage 1 will introduce the hook as `Func<bool>`; if #5829 has not merged when Stage 1 lands, the func is wired to a constant `() => false`.
- **PR [#5780](https://github.com/Azure/azure-cosmos-dotnet-v3/pull/5780)** — rewires `MetadataRequestThrottleRetryPolicy` regional-failure classification. Stage 2 must compose with whatever shape merges; if #5780 lands first, Stage 2 just swaps the inline list for `RetryUtility.IsRegionalFailure`. If not, Stage 2 introduces the classification by itself and #5780 rebases onto it.

---

## Stage 0 — Prerequisites & shared helpers (no behavior change)

Adds the seams the rest of the work depends on. Production behavior unchanged.

- Add `Microsoft.Azure.Cosmos/src/Routing/RetryUtility.cs` with `IsRegionalFailure(DocumentServiceResponse response, Exception exception, CancellationToken callerToken)` per design §5.7.2. Single source of truth for `503 / 500 / 410+LeaseNotFound / 403+DatabaseAccountNotFound / HttpRequestException / non-user OperationCanceledException`. Not yet consumed by any caller.
- Add `HttpTimeoutPolicy.FirstAttemptTimeout` (abstract `TimeSpan`) and override on `HttpTimeoutPolicyControlPlaneRetriableHotPath` returning `TimeoutsAndDelays[0].requestTimeout` (§5.9). Required for threshold derivation and the §8 unit invariant.
- Unit tests: a single `RetryUtilityTests` covering each branch of `IsRegionalFailure`, and a one-liner test asserting `HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout == TimeSpan.FromSeconds(1)`.

**Commit:** `Metadata Hedging: Adds RetryUtility.IsRegionalFailure and HttpTimeoutPolicy.FirstAttemptTimeout helpers`

---

## Stage 1 — Core `MetadataHedgingStrategy` (unwired)

Whole helper, gated off everywhere — no caller invokes it yet.

- Public:
  - `CosmosClientOptions.EnableMetadataHedgingForColdStart` (tri-state `bool?`) per §5.1.
  - `CosmosClientOptions.MetadataHedgingOptions` plus the `MetadataHedgingOptions` class (`Threshold`, `ThresholdStep`, `MaxHedgeBranchesPerAttempt`, `PerClientConcurrencyBudget`).
  - Plumb both through `ConnectionPolicy`.
- Internal (under `Microsoft.Azure.Cosmos/src/Routing/`):
  - `MetadataHedgingContext` — `Interlocked.CompareExchange`-published winner, `TryMarkHedgedThisOperation`, `AttemptedEndpoints` keyed on `AbsoluteUri`, `IsFirstReadFeedPage`.
  - `MetadataHedgingResult`, `MetadataHedgeEligibility`, `MetadataHedgeSkipReason` enum, `MetadataHedgeDiagnostics` (volatile `HedgeOutcome`/`LoserOutcome`), `HedgeBranch` enum.
  - `MetadataHedgingStrategy` — full `ExecuteAsync` per §5.3:
    - Per-branch CTSs (`primaryCts`/`hedgeCts`/`timerCts`).
    - `SemaphoreSlim.Wait(TimeSpan.Zero)` budget check.
    - `IsAcceptableWinner(response, branch)` with per-branch 401/403 overlay (§5.13).
    - `CloneForHedge` reuses Authorization/x-ms-date verbatim (§5.13).
    - `SendOneAsync` middle-layer seam (`try { ... } catch { await Task.Yield(); throw; }`) for net472 stack discipline (§5.12).
    - `ObserveWinningTaskAsync` re-raises via `ExceptionDispatchInfo.Capture`.
    - `BackgroundCleanupAsync` owns the loser CTS after ownership-transfer null-out, disposes the loser `DocumentServiceResponse`, never rethrows.
- Tests (in-process send delegate, no network):
  - Eligibility matrix.
  - Loser-cancellation invariant (§5.7.1).
  - Hedge 401/403 per-branch overlay.
  - Late-loser disposal — `DocumentServiceResponse.Dispose` called for both branches.
  - net472 50-concurrent-hedge stack-overflow regression (§5.12).

**Commit:** `Metadata Hedging: Adds MetadataHedgingStrategy core (unwired) with full unit coverage`

---

## Stage 2 — `MetadataRequestThrottleRetryPolicy` coordination

- Switch the policy's inline status-code switch to `RetryUtility.IsRegionalFailure`.
- Add `AttachHedgeContext(MetadataHedgingContext)`.
- Replace `IncrementRetryIndexOnUnavailableEndpointForMetadataRead` with the bounded probe loop (§5.7.4) — preserves `retryContext.RetryLocationIndex = unavailableEndpointRetryCount` side-effect on every `return true`; advances past `hedgeContext.AttemptedEndpoints`; terminates when all preferred regions are exhausted.
- Tests: hedge attempts A+B → retry advances to C (not A/B); cross-policy-type test (wrap policy in decorator or substitute `Mock<IDocumentClientRetryPolicy>`) confirms `as`-cast tolerates the substitution.

**Commit:** `Metadata Hedging: Coordinates MetadataRequestThrottleRetryPolicy with hedge context`

---

## Stage 3 — Wire `ClientCollectionCache`

Source-compat-sensitive because `CollectionCache.GetByNameAsync/GetByRidAsync` are `protected abstract`.

- Add `protected virtual` overloads on `CollectionCache` taking `bool isColdStart`, delegating to the existing `abstract`. Encryption-mirrored caches keep compiling and stay hedge-disabled by default.
- `ClientCollectionCache` overrides the new virtuals → constructs `MetadataHedgingContext { IsColdStart = isColdStart, ResourceType = Collection }` and calls `metadataHedgingStrategy.ExecuteAsync`.
- `ResolveByNameAsync` / `ResolveByRidAsync` pass `isColdStart: !forceRefresh` at the `AsyncCache.GetAsync` factory site (§5.6).
- `DocumentClient.Initialize` constructs the singleton `MetadataHedgingStrategy` and passes it to the cache constructor.
- Tests: cold-start hedges; cache hit doesn't invoke the factory; force-refresh invokes the factory but does NOT hedge (`senderCallCount == 1`).

**Commit:** `Metadata Hedging: Wires ClientCollectionCache to MetadataHedgingStrategy`

---

## Stage 4 — Wire `PartitionKeyRangeCache`

- Build `MetadataHedgingContext { IsColdStart = previousRoutingMap == null, ResourceType = PartitionKeyRange, IsFirstReadFeedPage = true }` in `GetRoutingMapForCollectionAsync`; flip `IsFirstReadFeedPage = false` after page 1.
- Add `hedgeContext` and `CancellationToken` parameters to `ExecutePartitionKeyRangeReadChangeFeedAsync` — replaces the silent `CancellationToken.None` so caller cancellation reaches all branches and the timer.
- Pages 2..N call `request.RequestContext.RouteToLocation(hedgeContext.WinningEndpoint)` and skip hedge by eligibility.
- Tests: page-1 hedges; pages 2..N hit only the winning region; customer-token cancellation cancels both branches + timer.

**Commit:** `Metadata Hedging: Wires PartitionKeyRangeCache to MetadataHedgingStrategy`

---

## Stage 5 — Telemetry & diagnostics surface

- Add `Meter` `Azure.Cosmos.Client.MetadataHedging` with the 6 instruments in §9.1.1 (`fires`, `hedge_wins`, `budget_exhausted`, `late_loser`, `hedge_auth_reject`, `hedge_fired_elapsed`).
- Add `MetadataHedging` keyword + 5 typed events on `CosmosDbEventSource` per §9.1.2.
- Verify the `Metadata Hedge Context` trace datum (already populated by Stage 1) renders in `CosmosDiagnostics` for both fired and skipped paths.
- Diagnostics-shape unit test: every field in §9 + every counter in §9.1 present for a fired-hedge and a skipped-hedge.

**Commit:** `Metadata Hedging: Adds Meter and EventSource telemetry surface`

---

## Stage 6 — Rollout phasing (post-merge ops)

- Single `phaseDefault` constant the strategy resolves from when `EnableMetadataHedgingForColdStart == null`. Phase 1 = `false`. Property is never removed across phases (binary-compat — §12).
- `changelog.md` entry under `### Unreleased` / `Features Added`, customer-facing wording.
- Cross-link from `docs/Cross Region Request Hedging.md` and `docs/PerPartitionAutomaticFailoverDesign.md`.

**Commit:** `Metadata Hedging: Sets Phase 1 default and adds changelog entry`
