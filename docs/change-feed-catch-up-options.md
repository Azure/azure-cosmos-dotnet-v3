# Change Feed Catch-up Options

Goal: Bounded catch-up for a fenced partial PK, assuming source readiness.

1. **Timestamp:** Target `maxWriteTs + buffer` using `crts`. Requires verified per-range time progress, including empty pages; buffer is heuristic.
2. **Read-start snapshot:** Reuse [Java's bounded-LSN approach](https://github.com/Azure/azure-sdk-for-java/pull/42160). Late starts may scan beyond cutoff.
3. **Cutoff snapshot:** Capture and persist per-range end checkpoints at cutoff; drain later. Requires a capture step.
4. **Write bookmarks:** Merge write-response session tokens. Validate conversion to change-feed progress.
