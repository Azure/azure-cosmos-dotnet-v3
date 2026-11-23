# Change Feed Read-Until: Proposed SDK Changes

**Status:** Draft | **Date:** 2026-11-23 | **Author:** Not specified

This proposes pagination/state changes, not document-timestamp inspection. See the [investigation](change-feed-read-until-investigation.md) and [test specification](change-feed-read-until-test-spec.md); no shipped implementation is implied.

## 1. One Opt-In API for Both Feed Modes

Illustrative API; the new property and type do not exist today:

```csharp
var options = new ChangeFeedRequestOptions
{
    ReadUntil = ChangeFeedReadUntil.Current()
};

using FeedIterator iterator = container.GetChangeFeedStreamIterator(
    ChangeFeedStartFrom.ContinuationToken(startToken),
    mode,
    options);
```

`mode` selects LatestVersion or AllVersionsAndDeletes. Typed iterators use the same options and completion mechanism. Omitting `ReadUntil` preserves existing behavior.

`startToken` is the saved anchor; `ReadUntil` enables SDK-managed target GLSN selection.

Preserve each mode's semantics; LatestVersion excludes deletes. Customers supply neither partition IDs nor target GLSNs.

Partition merges are out of scope.

## 2. Shared Bounded-Read State

Retain:

- Container identity, prefix, and feed mode.
- Current continuation for each constituent range.
- Cached target GLSNs for the physical partitions covered.
- Which partitions have reached their target GLSNs.

Resume the anchor. Cache each physical partition's target GLSN from its first successful replay response's session token. No second Now probe. Bounded resumption restores cached target GLSNs.

Advance continuations, including on empty pages, and retire partitions when progress reaches or exceeds their target GLSNs. Completion never inspects payload timestamps.

Missing targets prevent completion. Target acquisition is neither an atomic snapshot nor a historical timestamp bound.

## 3. Integration with the Existing Pipeline

| Component | Proposed change |
|---|---|
| [ChangeFeedRequestOptions](../Microsoft.Azure.Cosmos/src/RequestOptions/ChangeFeedRequestOptions.cs) | Add the boundary option and preserve it when cloning. |
| [ChangeFeedIteratorCore](../Microsoft.Azure.Cosmos/src/ChangeFeed/ChangeFeedIteratorCore.cs) | Initialize or restore bounded state, cache target GLSNs, serialize progress, and expose completion. |
| [ChangeFeedPartitionRangePageAsyncEnumerator](../Microsoft.Azure.Cosmos/src/ChangeFeed/Pagination/ChangeFeedPartitionRangePageAsyncEnumerator.cs) | Evaluate cursor coverage after each response and stop fetching a completed range. |
| [CrossPartitionChangeFeedAsyncEnumerator](../Microsoft.Azure.Cosmos/src/ChangeFeed/Pagination/CrossPartitionChangeFeedAsyncEnumerator.cs) | Coordinate incomplete ranges and support finite completion instead of assuming another page always exists. |

Keep completed ranges in resumable state after removing them from scheduling. Mode affects service requests and payloads, not completion logic.

## 4. Completion, Resume, and Splits

- Return the final page before ending bounded iteration.
- Expose target-reached status distinctly from failure. `HasMoreResults == false` alone is insufficient because existing error paths also set it.
- Persist target GLSNs in a versioned bounded continuation; never replace cached targets on resume.
- Reject scope or mode changes on resume, conflicting boundary options, and malformed state.
- Preserve progress and target GLSNs across splits. Unsupported target remapping must fail explicitly, never omit an uncovered child.
- Cancellation, timeout, buffered errors, and missing targets must not report target completion.
- Consumers checkpoint only after applying returned pages. SDK completion certifies returned data, not destination processing.

## 5. Timestamp Support and Validation Gates

A future service-supported time boundary could feed the same completion engine. Do not introduce an AVAD-only completion branch that scans event `crts`, or substitute LatestVersion `_ts`.

Validate response-session target selection and cursor comparison in both modes. The recorded AVAD first-response rule missed 595 events; scalar parsing is not a supported comparison contract.

False progress can lower GLSN across configurations (105 to 100). Without validated reconciliation, bounded reads must fail explicitly, never report target completion. A version tag alone is insufficient.

Validate both modes across typed/stream APIs, empty pages, fixed target GLSNs under continued writes, resume, splits, errors, and cancellation. Include payload models omitting system timestamps. Separate recorded-page verification from live execution evidence.

The observed same-client post-split 410/1002 behavior remains a separate release concern; successful fresh-client replay is not a permanent routing fix.

## 6. Write-Session Token Limitations

Write-session tokens are causal state, not change-feed continuations or proven target GLSNs. Supporting customer-supplied tokens would divide responsibility as follows:

- The **writers must prepare a compound session token covering every writer for the tenant**.
- The **SDK must merge partition-specific segments**, preserving regional components and configuration versions.
- Concatenation, last-response-wins, or one maximum LSN is insufficient.
- The SDK must retain container identity and split lineage because tokens are not inherently tenant-specific.
- Compounding alone neither proves confirmed-feed visibility nor establishes feed-cursor comparability.
- Writers must collect successful write outcomes because failed responses can also contain session tokens.

Existing [SessionContainer](../Microsoft.Azure.Cosmos/src/SessionContainer.cs) merges per-range tokens, but provides no prefix-scoped read-until contract.
