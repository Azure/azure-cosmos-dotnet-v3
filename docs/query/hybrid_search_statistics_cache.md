# Hybrid search full-text statistics cache

**Status:** Phases 1 and 2 expose query-plan path/term metadata and validated
configuration. Statistics-cache storage and execution are not implemented.

## 1. Recommendation and agreed scope

Add a small, client-owned cache of completed `GlobalFullTextSearchStatistics`
bundles. An opted-in hybrid query can skip its statistics fan-out when its
plan-derived path-and-term requirements, corpus identity, and freshness
requirements match a cached bundle. Otherwise, run the existing statistics
pipeline.

Decisions confirmed for this design:

- Freshness is bounded by a TTL and available container/index/routing identity
  checks. There is no new service-side validation request.
- The query plan includes query terms as well as optional full-text paths.
  Use this metadata directly; do not recover terms by parsing SQL.
- Caching is opt-in per client and supports both `FullTextScoreScope.Global`
  and `Local`. There is no per-query cache-age override.

The central invariant is:

> Reuse only a complete statistics result for the same statistics computation
> over the same scoring corpus, within the client's configured cache age.

A path alone is not a sufficient key. The existing result contains
query-dependent term hit counts as well as document and word counts. The plan's
terms let the SDK represent those dependencies explicitly as ordered
path-and-term requirements.

For v1, deliberately avoid background refresh, partial-path assembly, SQL
parsing, and shared in-flight queries. Reuse the existing statistics models,
fan-out pipeline, aggregation, query rewriting, tracing, and retry behavior.

## 2. Current implementation and integration points

Paths below are relative to `Microsoft.Azure.Cosmos\src`.

| Surface | Current behavior | Design consequence |
|---|---|---|
| `Query\Core\QueryPlan\HybridSearchQueryInfo.cs` | Retains optional `fullTextSearchTerms` as a JSON token, alongside `GlobalStatisticsQuery` and `RequiresGlobalStatistics`. | Normalize the retained metadata into ordered statistics requirements when implementing cache identity. |
| `Query\Core\Pipeline\CosmosQueryExecutionContextFactory.cs` | Resolves container properties, target ranges, and all ranges before constructing the pipeline. | Capture cache identity here, using information already needed by execution. |
| `Query\Core\Pipeline\PipelineFactory.cs` | Dispatches hybrid plans and wraps them with skip/take stages. | Thread an optional cache binding through; do not move ranking or paging logic. |
| `Query\Core\Pipeline\CrossPartition\HybridSearch\HybridSearchCrossPartitionQueryPipelineStage.cs` | Creates a parallel statistics pipeline, gathers its results, rewrites component queries, then executes them. | Perform lookup immediately before statistics gathering and create the statistics pipeline lazily on a miss. |
| `Query\Core\Pipeline\CrossPartition\HybridSearch\GlobalFullTextSearchStatistics.cs` | Holds one document count and an ordered list of full-text statistics. | Cache this existing representation, with defensive copying. |
| `Query\Core\Pipeline\CrossPartition\HybridSearch\FullTextStatistics.cs` | Holds `TotalWordCount` and ordered `HitCounts`. | Preserve array order and term-specific identity. |
| `Query\v3Query\CosmosQueryClientCore.cs` | Has access to the client context and the query-spec serializer; retrieves cached container properties. | Bind plan requirements to serialized parameter values and expose the client-owned cache to execution. |
| `Resource\ClientContextCore.cs` | Owns client-wide services and their lifetime. | Own the cache here, not on each `Container` handle or iterator. |

Important existing details:

1. `Global` gathers statistics over `allRanges`; `Local` uses `targetRanges`.
   The statistics stage currently passes `partitionKey: null`. This design
   preserves that behavior. It does not redefine Local as an exact logical
   tenant filter.
2. `GatherStatisticsAsync` aggregates counts across all statistics pages and
   emits an empty `QueryPage` containing the actual request charge and response
   metadata.
3. Component query placeholders refer to statistics by ordinal. Reordering
   paths, statistics slots, or hit-count arrays can change scoring.
4. Hybrid queries currently do not support externally resumable continuation
   tokens. The cache does not change that contract.

## 3. Why cache bundles rather than individual paths?

For example, statistics for `/text` used by:

```sql
FullTextScore(c.text, "swim", "run")
```

cannot supply the hit-count array for:

```sql
FullTextScore(c.text, "energy")
```

even though the path is identical. `DocumentCount` and `TotalWordCount` may be
reusable, but `HitCounts` are not. Because the plan includes the terms, the SDK
can distinguish these requests without parsing `GlobalStatisticsQuery`.

Represent each statistics requirement as the path and its ordered term
descriptors. Preserve the association between a requirement and its statistics
slot; the same path can appear more than once with different term lists.
For example, the normalized requirements for two slots could be:

```text
slot 0: /text     -> ["swim", "run"]
slot 1: /abstract -> ["energy"]

ordered requirements + scoring corpus + statistics semantics
    -> one complete GlobalFullTextSearchStatistics bundle
```

This makes a per-path-and-term cache possible, but v1 deliberately retains
complete bundles. That is a simplicity choice, not a limitation caused by
missing terms: there is one document count, one collection time, one TTL
decision, and no assembly/remapping of values collected by different queries.
The cache is not a `Dictionary<Path, FullTextStatistics>`.

Consequences:

- All required statistics are hit or all are fetched. No partial requests.
- Counts from different collection times are never assembled into one bundle.
- Existing result ordering and query rewriting remain unchanged.
- With a complete producer contract, changing projection, top count, vector
  ranking, SQL formatting, or unrelated parameters can still hit when the
  ordered path-and-term requirements and scoring corpus are identical.
- Different resolved terms, term order, path order, or statistics-affecting
  parameters produce different keys. Do not sort terms and then accidentally
  apply their hit counts to the original order.
- If the producer metadata does not describe all statistics-affecting inputs,
  retain the exact statistics-request guard described in section 5. That
  conservative guard can reduce reuse, but prevents false hits.

Term-subset, term-permutation, and path-subset/superset reuse are explicitly
deferred. Available term metadata can support those extensions later; they are
not required to skip statistics collection for matching requirements.

## 4. Public opt-in and freshness semantics

Phase 2 adds one public `CosmosClientOptions` property in both GA and preview,
rather than an enable flag plus a separate TTL:

```csharp
public TimeSpan? FullTextStatisticsCacheMaxAge { get; set; }
```

Example:

```csharp
CosmosClientOptions clientOptions = new CosmosClientOptions
{
    FullTextStatisticsCacheMaxAge = TimeSpan.FromSeconds(30)
};

using CosmosClient client = new CosmosClient(accountEndpoint, authKey, clientOptions);
QueryRequestOptions queryOptions = new QueryRequestOptions
{
    FullTextScoreScope = FullTextScoreScope.Local
};
```

The example is not a recommended universal TTL. The option currently stores and
validates the allowance; it does not yet change query execution. The lookup and
admission semantics below apply when the cache is integrated.

| Value | Behavior |
|---|---|
| `null` or zero | Disabled: neither consume nor populate the cache. |
| Positive | Allow reuse within this age; permit admission of successful results. |
| Negative, including an infinite-timeout sentinel | Reject immediately in the client-option setter. |

The option is preserved by `CosmosClientOptions.Clone` and applies to eligible
hybrid queries using that client. There is no equivalent on
`QueryRequestOptions`; use a separate client for a different age policy.
During cache-lifetime implementation, capture the age and entry-count policy
at client construction, even if cache allocation is deferred. Later mutation of
options must not change the running cache's policy.

Obtain the policy through the existing client context; do not thread a cache-age
value through `QueryIterator` or `InputParameters`, and do not send it as a
service header. Unsupported execution modes, including server-managed
distributed query execution, bypass the SDK cache.

### What freshness means

Measure age using monotonic elapsed time from the **start of statistics
collection**, not insertion time or last access:

```text
age = now - collectionStartedAt
usable = age >= 0
         AND age < clientMaxAge
```

Use the cache's immutable client-wide age policy to enforce absolute,
non-sliding expiry from collection start. Hits never extend it. All queries
sharing a cache use the same allowance, so no caller-specific age check or
per-entry population allowance is needed. Clients with different policies have
separate caches.

Starting the clock before collection prevents a slow fan-out from appearing
newly fresh when it completes. If collection has already consumed the allowance,
use the fetched result for the current query but do not admit it for reuse.

This is a bound on reuse age, **not** a proof of current index contents or a
bound on service indexing/replication lag. Collection itself is the existing
cross-partition operation, not a new atomic corpus snapshot.

Once selected, a bundle is pinned for that iterator. Do not refresh statistics
between pages or between ranking components. The allowance is checked when
selecting statistics, not guaranteed anew at every page delivery.

### Consistency boundary

Opt-in permits stale relevance inputs and therefore potentially different
scores, ordering, and top-K membership. Item requests retain their existing
consistency handling; that does not make cached ranking inputs session-current.
In particular, an implicit session advance or a write by another client does
not automatically invalidate this cache.

All consistency levels may opt in, including Strong, Bounded Staleness,
`GlobalStrong`, and requests carrying explicit session tokens. None of those
settings is a cache-bypass condition. The SDK's normal request-consistency
validation still applies: enabling statistics reuse does not make an invalid
consistency combination valid.

Cache reuse does not prove that statistics reflect a new session token or a
current strong read. The opt-in accepts potentially stale scoring inputs,
including their effect on score-based predicates and projections. Actual
service requests retain their original consistency settings and session tokens.
No additional account read is needed to establish consistency eligibility.

## 5. Query-plan contract and cache identity

### Paths, terms, and statistics-slot association

The producer's field is `hybridSearchQueryInfo.fullTextSearchTerms`. Native
ServiceInterop tests verify the following wire shape using the current Direct
package, with both hybrid query rewrite settings:

```json
{
  "fullTextSearchTerms": [
    {
      "path": ["text"],
      "terms": [{ "term": "swim", "distance": 2 }, { "term": "run" }]
    },
    { "path": ["abstract"], "terms": [{ "term": "energy" }] }
  ]
}
```

`path` contains ordered accessor segments with the root alias removed, not a
slash-delimited path. For example, `c["a/b"]` produces `["a/b"]`, whereas
`c.a.b` produces `["a", "b"]`; `c.tags[0]` produces `["tags", "[0]"]`.
Terms are objects containing text and optional fuzzy `distance`. Parameterized
paths and terms are resolved to their values by the producer.

Preserve list position for the corresponding `FullTextStatistics` slot and
term order for `HitCounts`. Repeated paths are not merged. Native coverage also
checks a FullTextScore predicate on a different path from the ORDER BY RANK
expression: that predicate contributes its own metadata entry. This metadata
must not be limited to rank expressions alone.

Normalization must not become a query analyzer:

- Do not lowercase paths or terms, split paths on `/`, reinterpret escaping,
  sort, deduplicate, stem, tokenize, or split phrases.
- Preserve exact engine-provided term values or structured term descriptors.
  A phrase or multi-token expression is not interchangeable with independent
  words. Include matching mode and per-expression analyzer/language identity
  when those can vary independently of the container policy.
- A flat union of all query terms is insufficient unless the producer contract
  also defines which terms belong to which path/statistics slot.
- Use resolved plan values for parameterized terms. If a future contract
  introduces explicit parameter references, support it only with an explicit
  binding to frozen serialized values; a parameter name alone is never a term
  identity.
- Do not infer slot or term associations by parsing SQL. Unknown associations
  make the plan ineligible for caching.
- Missing, null, empty, unsupported, or malformed required path/term metadata
  bypasses caching; the original statistics flow still works. A termless slot
  is eligible only if the producer explicitly defines its count-array shape.

Phase 1 retains the optional field as a Newtonsoft `JToken` and omits null or
absent metadata during serialization. Field-local deserialization disables
automatic date parsing, so date-like paths and terms remain exact strings
without changing how other plan fields are read. Unknown metadata shapes are
retained without making an otherwise executable legacy plan fail.

The validating accessor and immutable `StatisticsRequirement` normalization
belong to the cache-identity implementation. Until then, metadata retention
alone does not enable caching. Errors in required plan fields still follow
normal query-plan error handling.

### Semantic identity and completeness

Use the normalized path-and-term requirements as the primary query-dependent
identity. `GlobalStatisticsQuery` remains the execution recipe on a miss; its
text need not be the identity when the producer contract completely describes
the statistics computation.

Terms alone do not prove completeness. Document count, word counts, and term
counts may also depend on statistics-side filters, matching semantics, or
analyzer settings. Component-query filters matter here only when they alter the
statistics corpus, not merely the returned documents.

At the producer-contract review, establish which supported plan shapes describe
all these inputs:

- For a known complete contract, use the descriptors plus corpus and semantic
  context. Omit the full SQL text and unrelated parameters from the key.
- For a contract with known path/term mappings but unrepresented statistics
  dependencies, add the exact serialized statistics `SqlQuerySpec` as a
  conservative identity guard. This captures filters and parameter values
  without a new SQL parser.
- If the execution semantics or metadata association are unsupported, bypass
  caching rather than guessing.

This is an internal compatibility decision for recognized plan contracts, not
a customer option or a new cache service. Include the identity-contract kind
in the key so guarded and complete-contract entries cannot match each other.
The presence of paths and terms by itself must not select the complete-contract
case.

### Key

Use an immutable structured key, with structural equality over:

| Field | Purpose |
|---|---|
| Internal key-format version and identity-contract kind | Explicit contract; distinguish complete semantic identity from guarded identity. |
| Container resource ID | Prevent reuse across container deletion/recreation, even at the same name. |
| Observed container ETag | Conservative container/full-text/index-policy identity. |
| `FullTextScoreScope` | Keep Global and Local scoring separate. |
| Exact statistics range descriptors | Identify the corpus actually sent to the statistics pipeline. |
| Observed physical range IDs and boundaries | Conservatively reject observed split/merge/layout changes. |
| Ordered `StatisticsRequirement` sequence | Preserve each path, its ordered resolved terms/count expressions, and its statistics-slot association. |
| Statistics semantics/corpus descriptor, where present | Include statistics-side filters and matching/analyzer/language choices not already represented by the container ETag and scope. |
| Exact serialized statistics `SqlQuerySpec`, only for guarded identity | Cover statistics-affecting inputs not represented in the recognized metadata contract. |
| Supported semantic request context | Distinguish eligible consistency/read-strategy settings and any supported option that affects statistics execution. |

Client ownership implicitly partitions by account, client configuration, and
credential context; never share this cache statically across clients.

For Global, statistics range identity comes from `allRanges`, not the ranges
used by component queries. For Local, it comes from the actual `targetRanges`.
Capture physical identities before the factory converts `PartitionKeyRange`
objects into `FeedRangeEpk` values. Sort range descriptors deterministically,
preserving boundaries and inclusivity; do not merge adjacent ranges in v1.
Where logical partition/feed-range restrictions affect the effective request,
include their serialized identity as well.

The current query properties do not carry a container ETag. Add it to
`ContainerQueryProperties`, populated alongside the already fetched RID in
`GetCachedContainerQueryPropertiesAsync`. Do not hash mutable
`ContainerProperties` instances or retain them in entries.

Use exact bytes/strings for equality, not `GetHashCode`, a SQL string alone, or
a delimiter-joined representation. A digest may accelerate lookup, but matching
digests must still compare the full identity.

### Serialization and parameter safety

Reuse `CosmosSerializerCore.ToStreamSqlQuerySpec` and the existing
`CosmosSqlQuerySpecJsonConverter` behavior, including custom serialization and
`SerializedParameterValue`. Do not use `ToString()` or an unrelated JSON
serializer to fingerprint parameter objects.

On the eligible opt-in path, freeze serialized parameters once at deferred
pipeline initialization, before plan generation. Use that snapshot for the plan,
statistics request, and component requests. The existing
`SerializedParameterValue` representation can preserve the captured raw JSON
without invoking the user's serializer again.

This is important both for mutable parameter objects and for custom serializers:
the identity must describe the actual statistics request. Do not create a key
from one serialization and populate it using a second, potentially different
serialization. Keep the disabled path unchanged.

For a complete descriptor contract, key only the resolved values referenced by
the plan's statistics descriptors. A change to an unrelated projection/vector
parameter must not invalidate otherwise matching statistics.

For guarded identity, serialize the complete statistics query spec, including
all its parameters, rather than building a dependency analyzer for SQL.
Unknown/custom request overrides capable of changing request semantics should
cause an explicit unsupported-context bypass unless their effect is covered by
the key. Do not silently treat arbitrary `Properties` or injected headers as
irrelevant.

## 6. Liveness checks and invalidation

Perform the following checks when the iterator first needs statistics:

| Check | Failure behavior |
|---|---|
| Option enabled; hybrid plan requires statistics; supported execution context | Bypass cache. |
| Usable path-and-term requirements, known slot associations, and a nonempty statistics query | Bypass cache. |
| Recognized complete statistics identity or conservative request guard | Bypass cache if neither is available. |
| Required RID, ETag, and range identity available | Bypass cache. |
| Exact key present | Otherwise miss. |
| Entry age is within the client-wide allowance | Otherwise remove the expired entry and fetch. |
| Complete, structurally valid immutable result | Otherwise remove that entry and fetch, recording an invariant failure. |

All identity checks use metadata already resolved by normal query setup.
There is no collection HEAD/read, partition probe, statistics-version check,
document count query, or synthetic "liveness query" on a hit.

Consequently:

- An **observed** policy/ETag or routing change produces a different key.
- A refreshed container RID cannot consume the old container's entry.
- An unobserved metadata change or document mutation is not detected by these
  checks. TTL only limits reuse of the sampled statistics.
- Container ETags do not change for every item write and are not corpus versions.
- Splits do not inherently make aggregated counts invalid, but invalidating on
  an observed topology change is a simple, conservative v1 policy.
- Ordinary name-cache-stale retry still refreshes metadata and reconstructs the
  pipeline. The reconstructed pipeline gets a new binding; do not carry the
  original cached statistics across that retry.

Publish a fetched result under the identity captured for that fetch, never
under a newer RID/ETag/routing identity. If a relevant identity change is
explicitly observed during collection, skip admission. Otherwise, a subsequent
query with refreshed metadata naturally cannot hit the old identity.

No write hooks or change-feed subscription are added. Such hooks would miss
external writes and would not establish strict freshness.

## 7. Minimal architecture

```text
ClientContextCore
    captures client-wide age and entry-count policy
    owns one HybridSearchStatisticsCache
                    |
QueryIterator -> InputParameters
                    |
CosmosQueryExecutionContextFactory
    existing metadata / range resolution
    optional immutable cache binding
                    |
PipelineFactory
                    |
HybridSearchCrossPartitionQueryPipelineStage
    first statistics initialization
        |
        +-- hit --> existing component-query rewrite / creation
        |
        +-- miss -> existing parallel statistics pipeline
                    existing parsing / aggregation
                    publish completed statistics
                    existing component-query rewrite / creation
```

### One new service, not a cache framework

Add an internal `HybridSearchStatisticsCache` with a deliberately small surface:

```text
TryGet(bindingKey) -> hit/miss decision and statistics
TryAdd(bindingKey, statistics, collectionStartedAt)
Clear/Dispose
```

Nested immutable `StatisticsRequirement`, `Key`, `Entry`, `Binding`, and decision
types are sufficient; they need not become separate services or public
abstractions.

The cache captures the client-wide maximum age and entry-count limit at
construction. The binding carries only the cache reference and immutable key.
It does not hold a pipeline, response, `ITrace`, cancellation token,
or a reference back to the iterator. The frozen statistics request is an
iterator-owned input, not a cache entry.

Responsibilities:

- **Cache:** key lookup, freshness, bounded retention, atomic publication.
- **Query client/factory:** normalize plan requirements, bind parameters,
  establish identity completeness, scope and eligibility; prepare the binding
  through a narrow internal helper.
- **Hybrid stage:** network execution, error propagation, query rewrite, and
  per-query accounting.

Pass an optional binding through `PipelineFactory` to the hybrid stage. Keep
`IDocumentContainer` and transport interfaces unchanged. Existing pipeline
tests can pass no binding; cache-aware tests can inject a real small cache.

Own the cache lazily in `ClientContextCore` so multiple `GetContainer` handles
share it without creating process-wide state. Clear it and reject new admission
when the client is disposed. Lazy allocation must use the policy captured at
client construction, not mutable options read at first query execution.

### Storage and concurrency

Use a small `Dictionary<Key, Entry>` behind one short lock. Never serialize,
copy large payloads, execute I/O, or await while holding it.

Phase 2 adds one internal `CosmosClientOptions` capacity setting:
`FullTextStatisticsCacheMaxEntries`, defaulting to 256 entries per client.
The setter rejects zero or negative values immediately, leaving the previous
value unchanged. Client construction clones the options without a separate
validation pass. Changing the original options after constructing a client does
not change that client's setting. Internal callers and tests can override it;
there are no public capacity options, environment variables, or global mutable
test settings.
The cache will consume this setting when its storage/lifetime is implemented.

Use only the entry-count limit. Do not add byte-size limits, retained-size
accounting, or size-based admission. This bounds the number of retained entries,
not their combined memory footprint; unusually large entries can still consume
substantial memory.

Remove absolutely expired entries during insertion. If necessary, evict the
oldest collected entry by scanning the bounded dictionary. No LRU list, access
timestamp writes, timer, or background eviction loop is needed.

When concurrent misses complete for the same key, prefer the entry with the
later collection start. A slow older fetch must not replace a newer sample or
reset its freshness. All callers still use the statistics they independently
fetched for their own iteration.

### Why not reuse `AsyncCache`?

The repository's `AsyncCache<TKey, TValue>` is useful for shared asynchronous
initialization, and `AsyncCacheNonBlocking` supports background refresh. Neither
directly supplies this cache's bounded completed-result lookup and expiry
policy.

Using them here would also introduce ownership questions for an in-flight
statistics pipeline, cancellation, response metadata, and request charges.
For v1, concurrent cold misses may duplicate the normal statistics query. Each
query owns, cancels, disposes, and accounts for its own work.

That is an explicit simplicity tradeoff, not an accidental lack of
synchronization. Reuse the substantive existing query classes rather than
forcing an asynchronous cache abstraction onto completed values.

## 8. Pipeline behavior

Keep the existing state machine. `Uninitialized` now means "resolve statistics"
rather than unconditionally "gather statistics."

### Hit

1. Check cancellation and perform lookup with the current trace.
2. Select the immutable bundle and record a hit.
3. Reuse `CreateQueryPipelineStages`, `RewriteOrderByQueryInfo`, and
   `FormatComponentQueryText`.
4. Transition to `Initialized` and execute the normal component-query move in
   the same call.

Do not create or execute the parallel statistics pipeline on this path. Do not
manufacture an empty statistics page: there was no statistics request, charge,
service activity ID, or response header set.

### Miss or bypass

1. Lazily create the existing `ParallelCrossPartitionQueryPipelineStage` with
   the same SQL, selected statistics ranges, null partition key, prefetch policy,
   and concurrency behavior used today.
2. Capture the monotonic collection start before the first network operation.
3. Run the existing `GatherStatisticsAsync`.
4. On complete success, make an immutable cache snapshot and attempt admission
   if this query is eligible. Do not publish partial aggregates.
5. Create component stages using the same gathered statistics.
6. Return the existing statistics `QueryPage`, including its actual request
   charge, then execute components on subsequent moves as today.

Component creation failure does not turn into a cache miss/retry. A completely
valid statistics result may remain cached even if later component execution
fails; those are distinct operations.

`RequiresGlobalStatistics == false` retains its current path and never looks
up or populates statistics.

### Errors and cancellation

Eligibility misses, expiry, and capacity-driven eviction are expected optimization
decisions and must have explicit trace reasons. They are not exceptions.

Failures from actual statistics execution, required response parsing, or
component creation retain the existing `TryCatch<QueryPage>` behavior. Do not
serve an expired entry on failure, catch arbitrary exceptions and return a hit,
cache exceptions, or restart the whole query as a hidden cache recovery.

Validate aggregate dimensions and supported numeric shape before publication;
do not rely solely on existing `Debug.Assert` checks for a reusable result.
Validate the statistics-slot and term/count-slot mapping against the normalized
requirements. Use the producer's documented output cardinality: do not assume
the number of words in a raw phrase is the number of hit-count elements.
A mismatch involving only optional metadata skips admission with a
`DescriptorMismatch` reason and retains normal uncached execution. Malformed
required service results remain errors, not cache bypasses.

Cancellation before lookup does not return a hit. Cancellation during a fetch
publishes nothing. A narrow race after successful publication need not remove
an already valid entry, but cancellation of the current operation must still
propagate normally. No caller can cancel another caller's fetch.

Dispose only pipelines actually created by the iterator. Cache eviction or
client disposal must not mutate the bundle already selected by a query.

## 9. Cached value ownership and RU accounting

Reuse `GlobalFullTextSearchStatistics` and `FullTextStatistics`, but defensively
copy the outer statistics collection and every hit-count array before
publication. Their current constructors accept externally owned collections or
arrays; read-only exposure alone does not ensure immutable backing storage.

Store only:

```text
Entry
    immutable statistics
    collectionStartedAt
    diagnosticEntryId
```

Do not store `QueryPage`, `QueryPageParameters`, headers, session tokens, query
metrics, service activity IDs, request charges, or diagnostics trees.

For a miss, the current query reports its real statistics RU cost once. For a
hit, statistics RU cost is zero and component-query charges remain unchanged.
No prior query's charge or activity ID is replayed. Query-plan and normal
metadata work are not skipped by this cache.

Hit and miss paths can have different numbers of empty pages. That is
intentional: consumers must drain iterators rather than rely on an initial
empty statistics page. Skip/take, result projection, RRF/weighted ranking, and
continuation restrictions otherwise remain owned by existing stages.

## 10. Debuggability

Use the existing `ITrace.StartChild` and `AddDatum` facilities. Add a
`Hybrid Search Statistics Cache` child at statistics selection with structured,
low-cardinality fields:

| Field | Examples |
|---|---|
| `Decision` | `Hit`, `Miss`, `Bypass` |
| `Reason` | `NotFound`, `Expired`, `Disabled`, `MissingPaths`, `MissingTerms`, `InvalidRequirements`, `UnknownSlotMapping`, `MissingIdentity`, `UnsupportedExecutionContext`, `InvalidCachedShape` |
| `Scope` | `Global`, `Local` |
| `IdentityContract` | `CompleteDescriptors`, `GuardedRequest` |
| `RequirementCount`, `TermDescriptorCount`, `StatisticsRangeCount` | Counts, not paths, terms, or boundaries. |
| `MaxAgeMs`, `EntryAgeMs` | Client-wide allowance and entry age; entry age only if an entry was found. |
| `EntryId` | Opaque cache-local identifier for matching a hit to a population event. |
| `PopulationDecision` | `Stored`, `TooOld`, `Superseded`, `IdentityChanged`, `DescriptorMismatch`, `ClientDisposed` |

If the full key changes, the result may simply be `NotFound`. Do not claim to
know whether terms, a RID, an ETag, or ranges changed without evidence, and do not
maintain a second history index just to classify misses.

Nest real statistics execution beneath an existing query trace or a clearly
named `Gather Hybrid Search Statistics` child. A hit should visibly lack that
network subtree.

Do not log raw SQL, parameters, full-text paths, terms, hit counts, full keys,
or hashes derived from low-entropy search terms. Existing query-text opt-ins
remain separate. An opaque entry ID gives useful correlation without making
cache diagnostics a new customer-data channel.

No new public metrics or custom trace-datum hierarchy is required for v1.

## 11. Verification plan

### Pure cache tests

Use an injected monotonic elapsed-time delegate, following the existing
testability pattern in `WebExceptionRetryPolicy`, rather than sleeping.

Cover exact expiry boundaries, collection duration exceeding the allowance,
non-sliding hits, isolated clients with different age policies, defensive
copies, the entry-count limit, eviction, disposal, and out-of-order concurrent
publication.

### Key and plan compatibility tests

Cover absent/null/empty/malformed paths and terms, path-to-term association,
path and term order/case, repeated paths with different term groups, duplicate
terms, phrases versus separate words, matching/analyzer choices, and output-slot
mapping. Include parameter-bound terms, custom serializers,
`SerializedParameterValue`, mutable parameter snapshots, and exact equality
even when lookup hashes collide.

For complete descriptors, verify that equivalent literal and parameter-bound
requirements share identity when their resolved values and semantics match,
and that unrelated parameter changes do not cause misses. For guarded identity,
verify exact statistics query/parameter matching and separation from
complete-contract entries. The same terms over different statistics filters
must never collide.

Exercise both gateway-deserialized and locally generated plans. Confirm that
older producers without metadata execute normally and that unfamiliar optional
metadata does not corrupt required plan fields.

### Pipeline tests

Extend the existing mock and statistics-range/request-charge assertions in
`tests\Microsoft.Azure.Cosmos.Tests\Query\Pipeline\NonStreamingOrderByQueryTests.cs`.

| Scenario | Required observation |
|---|---|
| Disabled or missing metadata | Existing statistics requests and result behavior. |
| First eligible query | Full statistics fan-out; one complete admitted bundle. |
| Same key while live | Zero statistics backend requests; unchanged frozen-corpus results. |
| Same path, different terms | Miss, with no reuse of the previous hit-count array. |
| Same terms assigned to different paths | Miss; no cross-path count reuse. |
| Same path, reordered or repeated terms | Preserve slot order; no sorting/deduplication-based false hit. |
| Same descriptors, different projection/vector-only parameters | Hit under the complete contract; guarded identity may conservatively miss. |
| Same descriptors, different statistics-side filters or matching semantics | Different identity or explicit bypass, never a false hit. |
| Missing terms, unresolved bindings, or unknown slot mapping | Explicit bypass with normal statistics execution. |
| Optional descriptor/result shape mismatch | Skip admission; no incompatible bundle can later be reused. |
| Expired entry | Normal fresh statistics request. |
| Clients with different age allowances | Independent cache policy and storage; no per-query override. |
| Global with different component target ranges | Reuse only if global statistics identity is identical. |
| Local with different effective statistics ranges | No cross-corpus reuse. |
| Global versus Local over otherwise equal ranges | Separate cache identities. |
| Observed RID, ETag, range ID, or boundary change | Miss; no reuse under the refreshed identity. |
| Name-cache-stale pipeline reconstruction | New binding; preserved existing retry behavior. |
| Partial statistics failure or cancellation | No partial admission and no stale fallback. |
| Concurrent cold misses | Independent cancellation, responses, and RU accounting. |
| Successful statistics followed by component failure | Original component error, no hidden replay. |
| Valid empty-corpus statistics | Normal zero-count handling; no invented values. |
| Skip/take, singleton, weighted RRF, and multi-page reads | Existing ranking and paging behavior on a frozen corpus. |
| Long-lived iterator crossing TTL | Same selected statistics throughout that iterator. |
| Strong, Bounded Staleness, GlobalStrong, or explicit session tokens | Eligible for opt-in caching; retain original service request semantics. |
| Unsupported execution context or distributed execution | Explicit bypass with normal execution. |
| Diagnostic inspection | Correct decisions and charges; no raw search content or old response metadata. |

### Emulator and acceptance criteria

Extend existing hybrid-search emulator coverage, including both scoring scopes
and multiple physical partitions. Use injected time for expiry tests.

On a frozen corpus, a warm eligible query must:

1. Send no global-statistics query while still sending component queries.
2. Return the same documents, ordering, and scores as the uncached counterpart.
3. Omit the statistics request charge, without dropping any other actual charge.

After a write, results may use the old ranking inputs within the opted-in
interval. After expiry, a new statistics request must be observed. Tests must
not claim index freshness before the emulator/service has actually exposed the
write.

Measure warm first-result latency, actual RU totals, hit rate, and retained
memory under high-cardinality terms and many Local scopes. Do not assert a
universal percentage improvement or simulate an engine field as evidence that
the real producer already emits it.

## 12. Implementation sequence and review gates

1. **Confirm the producer contract.** Obtain actual path-and-term payloads from
   gateway and ServiceInterop plans, including repeated paths, phrases,
   parameterized terms, and statistics-slot mappings. Establish whether the
   descriptors fully identify statistics filters and matching semantics; use
   the request guard until completeness is established. No new tokenization
   logic belongs here.
2. **Review the public semantics.** Approve the client option, consistency
   bypass policy, and bounded-staleness documentation. Keep the default off.
3. **Add the passive cache and tests.** Keep key/entry types nested and retain
   the existing statistics result classes.
4. **Wire identity and options.** Capture the `CosmosClientOptions` policy in
   client-context ownership; update `ContainerQueryProperties`,
   `CosmosQueryClient`/`CosmosQueryClientCore`, and query-factory bindings.
   Do not add cache-age fields to request options or `InputParameters`.
5. **Integrate the hybrid stage.** Add the optional binding to factories, lazy
   statistics pipeline creation, hit selection, completed-result admission, and
   trace decisions. Leave ranking algorithms and transport retries alone.
6. **Finish coverage and documentation.** Update applicable API baselines,
   serialization/diagnostic tests, emulator tests, and customer guidance. The
   implementation changes shipped behavior and needs a customer-facing
   changelog entry; this design-only document does not.

Phase 1 verifies the wire representation and emitted ordering through the
current package, including repeated paths, fuzzy terms, parameter resolution,
and SELECT/WHERE usage. Complete statistics identity, including any additional
corpus/filter dependencies, still requires the cache-identity review. Missing or
unsupported metadata must reduce cache coverage, not block query execution.

## 13. Deliberately deferred

| Alternative | Why not v1 |
|---|---|
| Per-path or per-term cache assembly | Terms are available, but assembly adds partial coverage, slot remapping, and multiple collection times/TTLs; complete bundles keep v1 smaller. |
| Query-text parsing to extract dependencies | Duplicates engine knowledge about expressions, tokenization, language, filters, and parameter use. |
| In-flight query coalescing | Adds cancellation, pipeline lifetime, trace ownership, and RU-attribution decisions. |
| Background refresh or stale-on-error | More lifecycle work and a less obvious freshness/failure contract. |
| New backend liveness requests | Adds latency/RU and requires a service version contract; TTL alone cannot provide strict freshness. |
| Process-wide or persistent cache | Cross-client isolation, lifecycle, and invalidation complexity without a demonstrated requirement. |
| Granular write/index invalidation | SDK writes are only one source of change and do not establish index visibility. |

If workloads show poor whole-bundle hit rates, use the existing path-and-term
metadata to evaluate subset/permutation reuse or entries keyed by an individual
`StatisticsRequirement`. First define result-slot remapping, shared document
count ownership, and the treatment of independently collected values. Confirm
any analyzer/corpus dependencies not already covered by the producer contract.
Term availability enables that extension; simplicity and snapshot ownership are
the reasons to defer it, not an assumption that terms are missing.
