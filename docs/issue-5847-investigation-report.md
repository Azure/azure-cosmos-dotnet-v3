# Issue #5847 Investigation Report

## Scope
- Issue: `ChangeFeedEstimator` reports very large `EstimatedLag` when the associated `ChangeFeedProcessor` starts from `Now`/`WithStartTime` and leases have not checkpointed yet.
- Goal for this change: create a reproduction and provide a pre-fix plan for human review.

## Findings
- `ChangeFeedEstimatorIterator` creates per-lease iterators using:
  - `startFromBeginning: string.IsNullOrEmpty(continuationToken)`
  - Source: `Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/ChangeFeedEstimatorIterator.cs`
- The estimator path has no access to `ChangeFeedProcessor` start configuration (`WithStartTime`, `WithStartFromBeginning`, or default `Now`) when a lease has no continuation token.
- As a result, an uncheckpointed lease is measured from beginning, which can produce large initial lag values that do not represent actual backlog for `Now`/`WithStartTime` processors.

## Reproduction Added
- Test: `Repro5847_ShouldEstimateLargeLagWhenLeaseHasNoContinuationToken`
- File: `Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/ChangeFeed/ChangeFeedEstimatorIteratorTests.cs`
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

## Implemented Fix (Current PR)
- Chosen behavior for uncheckpointed leases (empty `ContinuationToken`): do **not** start estimator reads from `Beginning`.
- In `ChangeFeedEstimatorIterator`, `startFromBeginning` is now `false` for this case, which causes the underlying iterator to use `Now` semantics instead of beginning-based lag.
- This removes the inflated lag spikes reproduced in issue #5847 for processors started with `Now` / `WithStartTime`.

## Compatibility Note
- This fix prioritizes avoiding false high lag for `Now`/`WithStartTime` deployments.
- For `WithStartFromBeginning` deployments, before the first checkpoint per lease the estimator can temporarily under-report lag (typically near zero / now-based) because lease metadata does not persist the original start-position intent yet.
