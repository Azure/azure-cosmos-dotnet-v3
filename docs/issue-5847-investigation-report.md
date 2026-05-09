# Issue #5847 Investigation Report

## Scope
- Issue: `ChangeFeedEstimator` reports very large `EstimatedLag` when the associated `ChangeFeedProcessor` starts from `Now`/`WithStartTime` and leases have not checkpointed yet.
- Goal for this change: create a reproduction and provide a pre-fix plan for human review.

## Findings
- `ChangeFeedEstimatorIterator` creates per-lease iterators using:
  - `startFromBeginning: string.IsNullOrEmpty(continuationToken)`
  - Source: `/home/runner/work/azure-cosmos-dotnet-v3/azure-cosmos-dotnet-v3/Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/ChangeFeedEstimatorIterator.cs`
- The estimator path has no access to `ChangeFeedProcessor` start configuration (`WithStartTime`, `WithStartFromBeginning`, or default `Now`) when a lease has no continuation token.
- As a result, an uncheckpointed lease is measured from beginning, which can produce large initial lag values that do not represent actual backlog for `Now`/`WithStartTime` processors.

## Reproduction Added
- Test: `Repro5847_ShouldEstimateLargeLagWhenLeaseHasNoContinuationToken`
- File: `/home/runner/work/azure-cosmos-dotnet-v3/azure-cosmos-dotnet-v3/Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/ChangeFeed/ChangeFeedEstimatorIteratorTests.cs`
- Repro behavior in test:
  - Lease `0`: no continuation token → estimator passes `startFromBeginning = true` and computes large lag.
  - Lease `1`: has continuation token → estimator passes `startFromBeginning = false` and computes near-zero lag.

## Pre-Fix Plan (for Human Review)
1. Decide intended behavior for leases without continuation tokens when processor start point is not `Beginning`:
   - Option A: estimator honors processor start position.
   - Option B: estimator returns `0` or explicit “unknown” for uninitialized leases.
2. Choose metadata strategy to align estimator with processor configuration:
   - Persist processor start strategy/time in lease metadata, or
   - Store an estimator-specific marker for “uninitialized lease” and avoid beginning-based lag until first checkpoint.
3. Define compatibility behavior for existing lease documents that do not contain new metadata.
4. Add/adjust tests:
   - Unit tests for estimator behavior with empty continuation and each start strategy.
   - Emulator/integration test covering fresh deploy with `WithStartTime(DateTime.UtcNow)`.
5. Confirm public behavior/documentation updates:
   - Clarify semantics of `EstimatedLag` before first checkpoint.
   - Document migration/rollout notes if lease schema changes are introduced.

## Notes
- This change intentionally does not implement a fix; it only captures reproducible behavior and a proposed plan for review.
