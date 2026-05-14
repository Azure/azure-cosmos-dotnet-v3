## 1. Setup

- [x] 1.1 Create a feature branch `feature/hedging-detection-api` off `main`.
- [x] 1.2 Preserve previous proposal under `feature/hedging-detection-api-legacy-pr5741` for reference.

## 2. Public API — `CosmosDiagnostics`

- [x] 2.1 Add `virtual bool HedgingStarted()` with default returning `false` and the SE-013 "dispatched, not necessarily wire-issued" `<remarks>` paragraph.
- [x] 2.2 Add `virtual IReadOnlyList<RequestedRegion> GetRequestedRegions()` with default returning `Array.Empty<RequestedRegion>()`.
- [x] 2.3 Add `virtual IReadOnlyList<string> GetRespondedRegions()` with default returning `Array.Empty<string>()`.

## 3. Public types

- [x] 3.1 Create `RequestedRegion` (public readonly struct, `IEquatable<RequestedRegion>`, case-insensitive name equality, `==`/`!=`, custom `GetHashCode` that does not depend on `System.HashCode`, `ToString` = `"{regionName}:{reason}"`).
- [x] 3.2 Create `RequestedRegionReason` (public `enum : byte`) with values `Initial`, `OperationRetry`, `RegionFailover`, `Hedging`, `CircuitBreakerProbe`, `TransportRetry`. Document that `TransportRetry` and `CircuitBreakerProbe` are reserved and not populated by v1 .NET.

## 4. Internal state — `HedgingDetectionState`

- [x] 4.1 Create `Microsoft.Azure.Cosmos.Tracing.HedgingDetectionState` (`sealed`, lock-protected) with `AppendRequested`, `AppendResponded`, `HedgingStarted`, and snapshot getters that return `ToArray()` under the lock.
- [x] 4.2 Add `internal const string DispatchReasonPropertyKey` used as the well-known key on `RequestMessage.Properties` / `DocumentServiceRequest.Properties` to carry the next-dispatch reason across handlers.
- [x] 4.3 Attach a fresh `HedgingDetectionState` to every `TraceSummary` (initialized eagerly).
- [x] 4.4 Add overrides of all three new virtual methods on `CosmosTraceDiagnostics`, delegating to `this.Value?.Summary?.HedgingDetectionState`.

## 5. Dispatch-site wiring

- [x] 5.1 In `CrossRegionHedgingAvailabilityStrategy.CloneAndSendAsync`, set `clonedRequest.Properties[DispatchReasonPropertyKey] = RequestedRegionReason.Hedging` for hedge arms (`requestNumber > 0`). Leave the primary arm untagged so it defaults to `Initial`.
- [x] 5.2 In `ClientRetryPolicy.OnBeforeSendRequest`, after `request.RequestContext.RouteToLocation(locationEndpoint)`, when `retryContext != null`, set `request.Properties[DispatchReasonPropertyKey]` to `RegionFailover` if `retryContext.RetryRequestOnPreferredLocations` else `OperationRetry`. Do not override a value already set by the orchestrator.
- [x] 5.3 In `TransportHandler.ProcessMessageAsync`, after `ToDocumentServiceRequest`, resolve the region for `LocationEndpointToRoute` via `GlobalEndpointManager.GetLocation`, read the reason from `serviceRequest.Properties` (default `Initial`), append a `RequestedRegion` to `request.Trace.Summary.HedgingDetectionState`, then remove the key so subsequent retries re-default.

## 6. Response-site wiring

- [x] 6.1 In `ClientSideRequestStatisticsTraceDatum.RecordResponse`, alongside the existing `AddRegionContacted` call, also call `HedgingDetectionState.AppendResponded(regionName)`.
- [x] 6.2 In `ClientSideRequestStatisticsTraceDatum.RecordHttpResponse`, mirror the same append.
- [x] 6.3 In `ClientSideRequestStatisticsTraceDatum.RecordHttpException`, mirror the same append.

## 7. Tests

- [x] 7.1 `RequestedRegionTests` — equality (case-insensitive), hash, `ToString`, null-name guard, operator coverage.
- [x] 7.2 `HedgingDetectionStateTests` — defaults, null/empty guards, `HedgingStarted` flip on `Hedging`, ordering / duplicates, snapshot independence, thread-safety.
- [x] 7.3 `CosmosDiagnosticsBackwardCompatTests` — legacy `CosmosDiagnostics` subclass that does not override the new virtuals returns safe defaults (AC9).
- [ ] 7.4 Emulator / fault-injection tests covering AC1, AC2, AC3, AC4, AC5, AC8, AC13, AC14, AC15. *(Tracked as follow-up — requires reuse of existing fault-injection infra.)*
- [ ] 7.5 Live multi-region smoke test (AC16). *(Tracked as follow-up — requires a real multi-region account.)*
- [ ] 7.6 Golden-file `ToString` stability test (AC11). *(Tracked as follow-up — existing baseline tests cover most paths.)*

## 8. Contract / changelog

- [x] 8.1 Regenerate `DotNetSDKAPI.net6.json` via `UpdateContracts.ps1`; diff contains only the three new methods, the new struct, and the new enum.
- [x] 8.2 Add a `## Unreleased Preview` entry to `changelog.md` referencing issue #5867.

## 9. Validation

- [x] 9.1 `dotnet build Microsoft.Azure.Cosmos\src\Microsoft.Azure.Cosmos.csproj -c Debug` — clean.
- [x] 9.2 New unit tests pass — 17 / 17 green on `net6.0`.
- [x] 9.3 GA contract enforcement test passes — 107 / 107 green on `net6.0` Release.
