## 1. Setup

- [ ] 1.1 Create a git worktree for the feature branch (e.g., `feature/hedging-detection-api`) off `master` to isolate the work from the main working directory

## 2. Public API — CosmosDiagnostics

- [ ] 2.1 Add virtual method `IsHedged()` to `CosmosDiagnostics` returning `false` by default, with XML doc comments explaining the method's purpose and behavior
- [ ] 2.2 Add virtual method `GetRespondingRegion()` to `CosmosDiagnostics` returning `null` by default, with XML doc comments

## 3. Internal Implementation — CosmosTraceDiagnostics

- [ ] 3.1 Add `internal bool isHedged` field and `internal string respondingRegion` field to `CosmosTraceDiagnostics`
- [ ] 3.2 Override `IsHedged()` in `CosmosTraceDiagnostics` to return the `isHedged` field
- [ ] 3.3 Override `GetRespondingRegion()` in `CosmosTraceDiagnostics` to return the `respondingRegion` field

## 4. Hedging Strategy Integration

- [ ] 4.1 In `CrossRegionHedgingAvailabilityStrategy.ExecuteAvailabilityStrategyAsync`, set `isHedged = true` and `respondingRegion` on the response diagnostics at every code path that returns a hedged response (both the fast-path when a final result arrives during the hedging loop and the fallback path that drains remaining tasks)
- [ ] 4.2 Ensure the flag is set for the primary-region-wins case (requestNumber == 0) as well as hedge-region-wins cases

## 5. Tests

- [ ] 5.1 Add unit tests verifying `CosmosDiagnostics.IsHedged()` default returns `false` and `GetRespondingRegion()` default returns `null`
- [ ] 5.2 Add unit tests for `CosmosTraceDiagnostics` overrides — verify fields are readable after being set
- [ ] 5.3 Add or update hedging integration/emulator tests to assert `IsHedged()` returns `true` and `GetRespondingRegion()` returns the expected region when hedging is activated
- [ ] 5.4 Add tests verifying non-hedged requests (no availability strategy, single region, non-document resource type) return `IsHedged() == false`

## 6. API Contract Update

- [ ] 6.1 Run the contract generation tool (`UpdateContracts.ps1`) to update the public API contract files reflecting the new methods on `CosmosDiagnostics`

## 7. Validation

- [ ] 7.1 Build the solution (`dotnet build Microsoft.Azure.Cosmos.sln`) and verify no compilation errors
- [ ] 7.2 Run existing unit tests to confirm no regressions
