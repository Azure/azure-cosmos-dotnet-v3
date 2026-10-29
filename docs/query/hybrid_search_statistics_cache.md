# Hybrid search full-text statistics cache

**Status:** The query-plan metadata, client option, cache, and pipeline wiring
are implemented. Lookup, admission, and skipping the statistics request
(phase 7) are not yet enabled; queries still collect statistics normally.

## 1. Summary

Global-scoped hybrid search queries run a statistics query before their
component queries. It fans out to every partition to compute the document
count, each path's total word count, and each term's hit count. This design
adds an opt-in, client-owned cache of those counts so that a query can skip the
statistics request when every count it needs is present and fresh enough.

Decisions:

- **Opt-in per client** through `CosmosClientOptions.FullTextStatisticsCacheMaxAge`.
  There is no per-query override.
- **Global scope only.** `FullTextScoreScope.Local` neither reads nor populates
  the cache, even when its ranges cover the whole container.
- **Freshness is a TTL.** There are no liveness requests, write hooks, or
  change-feed subscriptions. Container RID and ETag are part of every key.
- **Independent counts.** Each document, word, and hit count is cached and aged
  separately. A result may combine counts from different collections.
- **Use the query plan's metadata** (`fullTextSearchTerms`) to identify counts.
  Never parse SQL or generate partial statistics queries.
- **Reuse existing classes** for statistics, fan-out, aggregation, rewriting,
  tracing, and retries.

## 2. Public option

```csharp
CosmosClientOptions options = new CosmosClientOptions
{
    FullTextStatisticsCacheMaxAge = TimeSpan.FromSeconds(30),
};
```

| Value | Behavior |
|---|---|
| `null` (default) or zero | Disabled. No cache is allocated. |
| Positive | Counts may be reused while younger than this age. |
| Negative | The setter throws `ArgumentOutOfRangeException`. |

Age is measured from the **start** of statistics collection, so a slow fan-out
does not appear fresh. Expiry is absolute: hits never extend it, and refreshing
one count does not renew others. The value is captured when the client is
created; later changes to the options have no effect.

Reused statistics may not reflect recent writes, regardless of consistency
level, read consistency strategy, or session token. This can change
`FullTextScore` values, ranking, and score-based predicates. Actual service
requests keep their normal consistency handling.

The internal `FullTextStatisticsCacheMaxEntries` option (default 256) sets the
target number of cached counts per client. One path with two terms uses four
entries: document count, word count, and two hit counts. It bounds entry count,
not bytes, and concurrent writers can briefly exceed it.

## 3. Query-plan metadata

The plan's `hybridSearchQueryInfo.fullTextSearchTerms` lists, in statistics-slot
order, each scored path and its terms. Parameters are already resolved.

```json
"fullTextSearchTerms": [
  { "path": ["title"], "terms": [{ "term": "energy" }] },
  { "path": ["text"],  "terms": [{ "term": "swim", "distance": 2 }, { "term": "run" }] }
]
```

- `path` holds accessor segments without the root alias: `c.a.b` gives
  `["a", "b"]`, `c["a/b"]` gives `["a/b"]`, and `c.tags[0]` gives `["tags", "[0]"]`.
- `distance` is the optional fuzzy distance; absent differs from zero.
- Repeated paths and duplicate terms are preserved. Their positions map to
  `FullTextStatistics` slots and `HitCounts` entries.

The managed models (`FullTextSearchTerms`, `FullTextSearchTerm`) mirror the
producer and use ordinary Newtonsoft attributes, like other query-plan fields.
Missing or incomplete metadata disables caching for that query; it never fails
the query. Malformed JSON follows normal query-plan error handling.

Parameters and their serialization are assumed stable during iterator
execution. No query-spec snapshots or parameter guards are added.

## 4. Cache design

### Keys

`HybridSearchStatisticsCacheKey` is a sealed class identifying one count.
Every key contains the container RID, ETag, and a private kind:

| Kind | Additional identity | Statistics expression |
|---|---|---|
| Total document count | None | `COUNT(1)` |
| Path word count | Path segments | `SUM(_FullTextWordCount(path))` |
| Term hit count | Path segments, term, distance | `COUNTIF(FullTextContains(path, term))` |

Equality is ordinal and structural, with ordered path segments. The hash is
computed once with `System.HashCode`; the existing `Microsoft.Bcl.HashCode`
package supplies it for `netstandard2.0`. The key references the plan's path
list without copying it, relying on query plans not being mutated after they
are created. Factories throw
`ArgumentNullException` for a null RID, ETag, path, or term; distance is
optional. The cache checks identity and metadata first, so it never passes null.

### Storage and operations

One `ConcurrentDictionary<HybridSearchStatisticsCacheKey, CachedCount>` holds
`(value, collectionStartedAt)` per key. There is no cache-wide lock: every
operation is a single-key dictionary call, so multi-key reads and writes are not
atomic. That is acceptable because counts are already independent.

```text
GetElapsedTime() -> TimeSpan
TryGet(rid, etag, hybridSearchQueryInfo, out GlobalFullTextSearchStatistics) -> bool
Add(rid, etag, hybridSearchQueryInfo, statistics, collectionStartedAt)
IsQuerySupported(hybridSearchQueryInfo) -> bool   (internal static)
```

**`TryGet`** walks the plan in order and reads each required count, using a
single `now`. It returns new arrays in plan order, so callers cannot mutate
cached data. It misses if any count is absent or expired, or if the
combination is impossible: a hit count above the document count, or a nonzero
word count with zero documents. Values are never clamped.

**`Add`** checks identity, supported metadata, freshness, and that the response
dimensions match the plan before writing anything. Each count is written with
`AddOrUpdate`, keeping the newer collection time; equal times keep the
existing value. Responses that need more entries than the target are skipped.

**Eviction** runs after `Add` only when the dictionary exceeds the target; that
check is the only `Count` call. Expired counts are lookup misses and stay until
then. Eviction sorts one `ToArray()` snapshot by collection time and removes
entries oldest first: every expired entry, then fresh entries until the target
is met. Expired entries always sort first, so one pass handles both. Oldest
entries are closest to expiry anyway, and unlike LRU, lookups need no writes.
Never use LINQ directly over the live dictionary; it can throw during
concurrent writes. Eviction is best effort: concurrent passes can evict a
just-refreshed count, which only causes a later miss.

The cache is not disposable. Client disposal makes the context property throw;
an in-flight query that already holds the cache can still use it until the
query finishes.

## 5. Wiring

| Component | Role |
|---|---|
| `ClientContextCore.Create` | Creates the cache when the age is positive; otherwise passes null. The context stores only the readonly reference. |
| `CosmosClientContext`, `CosmosQueryClient` | Expose the nullable cache. |
| `CosmosQueryClientCore` | Adds the container ETag to `ContainerQueryProperties` from the same cached container read as the RID. |
| `CosmosQueryExecutionContextFactory`, `PipelineFactory` | Forward the cache without any hybrid-specific decisions. |
| `HybridSearchCrossPartitionQueryPipelineStage` | Keeps the cache only for Global scope and supported metadata. Stores the cache, RID, and ETag in private readonly fields. |

Name-cache-stale retries rebuild the stage with refreshed RID and ETag, so a
retried query never uses the previous container's counts.

## 6. Pipeline behavior (phase 7)

The existing `Uninitialized` state resolves statistics:

- **Hit:** build component pipelines from the cached statistics and run them
  immediately. Do not create the statistics pipeline or emit a statistics
  page; the hit costs zero statistics RU.
- **Miss:** capture `GetElapsedTime()` before any statistics request, run the
  existing statistics pipeline, pass the result to `Add`, and continue as today.
  The query always uses its own fetched result, whether or not it was cached.

Concurrent misses run independent statistics queries; there is no in-flight
sharing. Failures and cancellation follow the existing `TryCatch` behavior, and
nothing is cached from a failed or cancelled collection. Selected statistics
are pinned for the iterator.

Diagnostics should add a child trace with a hit, miss, or bypass decision, a
reason, and slot counts. Do not log paths, terms, keys, or counts.

## 7. Testing

- `HybridSearchStatisticsCacheTests` and `HybridSearchStatisticsCacheKeyTests`
  cover identity, freshness, reuse across queries, numeric checks, eviction,
  and concurrency, using an injected clock.
- `QueryPlanBaselineTests.FullTextSearchTerms` captures native plan metadata;
  `QueryPartitionProviderTests` covers deserialization and malformed metadata.
- `ClientContextCoreTests`, `FactoryTests`, and `CosmosClientOptionsUnitTests`
  cover ownership, wiring, ETag propagation, and option validation.
- Phase 7 adds behavior tests showing a warm cache skips the statistics request,
  plus emulator tests.

Feature tests use lightweight case types and explicit loops, not
`DataTestMethod`/`DataRow`. Tests read private state through reflection rather
than test-only members.

## 8. Alternatives considered

| Alternative | Reason not chosen |
|---|---|
| Whole-query statistics bundles | Counts are independent; scalar entries allow reuse across subsets, reordering, and other queries. |
| Hierarchical storage by container and path | More bookkeeping than a flat dictionary with kind-specific keys. |
| Requiring all counts from one collection | Rejected in favor of independent counts with numeric checks. |
| `AsyncCache` / `AsyncLazy` sharing | Adds in-flight ownership, cancellation, and RU attribution for completed values. |
| `Dictionary` with a lock | Unnecessary once multi-key atomicity was dropped. |
| LRU eviction | Writes on every hit; oldest-first suits TTL entries. |
| `CosmosObject` key identity | Allocates; typed fields with `System.HashCode` are simpler. |
| Liveness or version requests | Adds latency and RU; TTL is the agreed freshness bound. |
| Per-query option or Local caching | Out of scope for v1. |
| SQL parsing or partial statistics queries | Duplicates engine logic; every miss runs the complete query. |
| Byte-size limits or cache disposal | Not needed for a small, client-owned cache. |
