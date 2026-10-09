# COMBINEDSCORE query execution

`COMBINEDSCORE` ranks the union of retrieved full-text search candidates by a
weighted sum of their component scores, rather than by reciprocal ranks.
Documents are deduplicated by `_rid`; repeated score components remain separate
contributions. This is candidate-based ranking, not exhaustive global top-K.
Equal combined scores are ordered by `_rid` using ordinal comparison.

The query planner owns SQL validation, parameter expansion, component rewrites,
candidate breadth, and final skip/take. The SDK consumes the optional
`scoreCombinationKind` discriminator in the hybrid search plan. `CombinedScore`
selects weighted summation; `Rrf` or an omitted discriminator retains the
existing RRF behavior. Unrecognized kinds are rejected.

Plan weights are finite, non-negative magnitudes. Missing or empty weights
default to one per component. In the order-by rewrite, ascending component
metadata negates the projected score. In the optimized rewrite, that negation
has already been applied to the projection, and the SDK does not apply it again.
The multiplier changes candidate breadth in the rewritten SQL, not the score;
the SDK does not apply another multiplier.

## Numeric and paging limits

The SDK preserves the planner's missing-score sentinels; it does not replace
them with zero. A zero-weight component contributes zero before multiplication,
including when its numeric score is non-finite. Every component must still
have a numeric score. Non-zero weights require finite component scores.

Summation uses `double` in component order. A non-finite weighted product or
partial sum fails the query through the pipeline's error result, even if later
components could cancel an intermediate overflow. Scores are never silently
clamped, and NaN/infinity never determine result ordering. Applications can
reduce weight magnitudes or exclude missing fields to avoid overflow.

The existing client paging stages support counts up to `Int32.MaxValue`.
Larger CombinedScore component TOP/OFFSET/LIMIT counts and hybrid skip/take
counts are rejected before narrowing. Values beyond the plan's `UInt32`
representation, including `UInt64.MaxValue`, fail deserialization. Counts
embedded only in optimized rewritten SQL are left for the service to execute.

## Draft integration status

The public `Microsoft.Azure.Cosmos.Direct` 3.44.1 dependency does not recognize
`COMBINEDSCORE` (SC2005). No dependency version or native binary is changed by
this implementation. Native SQL planning remains unavailable until a compatible
published dependency is adopted. There is no SQL-text detection or automatic
fallback to a different planner.

The SDK advertises `HybridSearch` and `CombinedScore` to gateway planning in
both hybrid optimization modes because execution is implemented. This requires
a service that preserves the discriminator and enforces both capabilities.
`WeightedRankFusion` is neither a substitute nor an additional requirement for
`CombinedScore`. Existing RRF capabilities remain unchanged.
Tests cover synthetic plans, ServiceInterop plan conversion,
gateway serialization, and the thin-client plan deserializer; they do not
establish live gateway or thin-client service support.

`CombinedScoreNativeQueryPlan` and emulator `CombinedScoreTests` are explicitly
ignored pending compatible dependencies. Remove those ignores when validating
integration. Emulator setup uses `templates/emulator-setup.yml`, with
`EMULATORMSIURL` pointing to a compatible build and its existing full-text
override enabled. The integration test enables the existing
`queryEnableFullTextPreviewFeatures` setting for score projections. No new
environment variable or emulator switch is assumed.
