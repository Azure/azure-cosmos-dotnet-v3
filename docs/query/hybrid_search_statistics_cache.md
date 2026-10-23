# Hybrid search full-text statistics cache

**Status:** Phases 1-5 expose query-plan metadata, validated configuration,
hierarchical storage of independently aged counts, and constructor-injected client-owned
cache lifetime. Query binding and pipeline integration are not implemented;
queries do not yet reuse cached statistics.

## 1. Recommendation and agreed scope

Add a small, client-owned cache of independently bounded-age counts. Reconstruct
`GlobalFullTextSearchStatistics` for the current plan only when every required
document, path-word, and term-hit count is present, live, and numerically
compatible. Otherwise, run the existing complete statistics query.

Decisions confirmed for this design:

- Freshness is bounded by a TTL and available container/index identity
  checks. There is no new service-side validation request.
- The query plan includes query terms as well as optional full-text paths.
  Use this metadata directly; do not recover terms by parsing SQL.
- Caching is opt-in per client and supports only `FullTextScoreScope.Global`.
  Local queries keep their existing uncached statistics flow. There is no
  per-query cache-age override.

The central invariant is:

> Return a complete, correctly ordered statistics result assembled from counts
> for the same scoring corpus, each within the client's configured cache age.

A path alone is not a sufficient key. The existing result contains
query-dependent term hit counts as well as document and word counts. The plan's
terms let the SDK represent those dependencies explicitly as ordered
path-and-term requirements.

For v1, deliberately avoid background refresh, partial statistics requests, SQL
parsing, and shared in-flight queries. Reuse the existing statistics models,
fan-out pipeline, aggregation, query rewriting, tracing, and retry behavior.

## 2. Current implementation and integration points

Paths below are relative to `Microsoft.Azure.Cosmos\src`.

| Surface | Current behavior | Design consequence |
|---|---|---|
| `Query\Core\QueryPlan\HybridSearchQueryInfo.cs` | Exposes optional `List<FullTextSearchTerms>` using the existing Newtonsoft serializer. | Use the models to identify scalar counts and reconstruct their required order; no separate statistics-requirement model is needed. |
| `Query\Core\Pipeline\CosmosQueryExecutionContextFactory.cs` | Resolves metadata and converts both all ranges and target ranges to EPK ranges. | During cache integration, create bindings only for Global scope; keys do not need routing ranges. |
| `Query\Core\Pipeline\PipelineFactory.cs` | Passes both range lists and `FullTextScoreScope` to the hybrid stage and wraps it with skip/take stages. | Thread the optional Global-only cache binding when needed, without a range-selection refactor. |
| `Query\Core\Pipeline\CrossPartition\HybridSearch\HybridSearchCrossPartitionQueryPipelineStage.cs` | Selects all ranges for Global or target ranges for Local, gathers statistics, rewrites component queries, then executes them. | Use the cache only for Global. Leave Local collection unchanged; Global misses still fan out over all ranges. |
| `Query\Core\Pipeline\CrossPartition\HybridSearch\GlobalFullTextSearchStatistics.cs` | Holds one document count and an ordered list of full-text statistics. | Return this existing representation as a per-lookup snapshot, rather than storing query-shaped bundles. |
| `Query\Core\Pipeline\CrossPartition\HybridSearch\FullTextStatistics.cs` | Holds `TotalWordCount` and ordered `HitCounts`. | Preserve array order and term-specific identity. |
| `Query\v3Query\CosmosQueryClientCore.cs` | Exposes the client context's nullable `HybridSearchStatisticsCache` property; retrieves cached container properties. | Query binding can use the injected cache after checking Global/hybrid eligibility, without reading mutable options. |
| `Resource\ClientContextCore.cs` | Accepts a configured cache or null through its constructor and owns its lifetime. | The factory configures the cache; the context stores only the readonly dependency, not separate age/capacity fields. |

Important existing details:

1. `Global` gathers statistics over `allRanges`; `Local` uses `targetRanges`.
   Selection currently occurs in the hybrid stage, which receives both range
   lists and `FullTextScoreScope`. The statistics query passes `partitionKey: null`,
   preserving the supplied EPK ranges. This does not redefine Local as an
   exact logical tenant filter. Only Global statistics are cacheable in this
   implementation; Local queries must neither consume nor populate the cache.
2. `GatherStatisticsAsync` aggregates counts across all statistics pages and
   emits an empty `QueryPage` containing the actual request charge and response
   metadata.
3. Component query placeholders refer to statistics by ordinal. Reordering
   paths, statistics slots, or hit-count arrays can change scoring.
4. Hybrid queries currently do not support externally resumable continuation
   tokens. The cache does not change that contract.

## 3. Cache scalar dependencies, not query-shaped bundles

The generated statistics query computes independent aggregates:

| Count | Expression | Cache identity |
|---|---|---|
| Document count | `COUNT(1)` | Container RID/ETag |
| Total word count | `SUM(_FullTextWordCount(c.text))` | Document path within the container revision |
| Term hit count | `COUNTIF(FullTextContains(c.text, "swim"))` | Path and complete term descriptor, including optional fuzzy distance |

A hit count is a matching-document count, not the number of term occurrences.
The generated plan emits a separate count for each term argument. Repeated
paths repeat the same word-count aggregate, so storage need not repeat it.

Store document count once per container revision, word count once per path,
and hit count once per `(path, term, distance)` identity. Each scalar carries
its own collection-start timestamp. Different live samples may be combined;
they need not originate from the same statistics query.

`GlobalFullTextSearchStatistics` remains the query-shaped result. At lookup,
walk the plan's metadata in order and reconstruct each `FullTextStatistics`
and its `HitCounts` array. Preserve repeated paths, duplicate terms, and exact
array positions, even though storage deduplicates their counts.
For example, counts collected for `["swim", "run"]` can serve `["run"]`,
`["run", "swim", "run"]`, or separate scoring components on the same path.

All required counts must be present and live before returning a snapshot.
A missing or incompatible count causes a normal complete statistics query,
not a partial request or a fabricated zero. Successful complete responses
populate all their scalar counts after metadata/result validation. No SQL
parsing or partial-statistics SQL generation is introduced.

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
    FullTextScoreScope = FullTextScoreScope.Global
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
Global-scoped hybrid queries using that client. Local scope always bypasses
lookup and admission, even if its current ranges cover the whole container.
There is no equivalent on
`QueryRequestOptions`; use a separate client for a different age policy.
The existing client-context factory constructs a cache from these options when
the age is positive, or passes null when disabled. The cache captures its own
age/capacity policy. Later mutation of options does not reconfigure or replace
the injected dependency.

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
non-sliding expiry from each count's collection start. Hits never extend it.
Refreshing document count, a path's word count, or one term must not renew
other term counts. All queries
sharing a cache use the same allowance, so no caller-specific age check or
per-entry population allowance is needed. Clients with different policies have
separate caches.

Starting the clock before collection prevents a slow fan-out from appearing
newly fresh when it completes. If collection has already consumed the allowance,
use the fetched result for the current query but do not admit it for reuse.

This is a bound on reuse age, **not** a proof of current index contents or a
bound on service indexing/replication lag. Collection itself is the existing
cross-partition operation, not a new atomic corpus snapshot.

The user explicitly accepts independently aged counts from different
collections. Even when every count is live, this does not establish a common
sampling generation. Reject numerically impossible assembled results, such as
a hit count greater than document count or nonzero word count with zero
documents, and fall back to a complete statistics query. Never clamp counts.

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
  producer-defined resolution to stable execution values; a parameter name alone is never a term
  identity.
- Do not infer slot or term associations by parsing SQL. Unknown associations
  make the plan ineligible for caching.
- Missing, null, empty, or incomplete path/term metadata
  bypasses caching; the original statistics flow still works. A termless slot
  is eligible only if the producer explicitly defines its count-array shape.

The optional field is `List<FullTextSearchTerms>`, mirroring the producer's
managed query-plan model. Each entry has `List<string> Path` and
`List<FullTextSearchTerm> Terms`; each term has `string Term` and optional
`int? Distance`. These are ordinary Newtonsoft-annotated properties, just like
the other query-plan fields. Null metadata and null distances are omitted
during serialization. Date-like paths/terms remain strings through their
declared string types.

There is no custom converter, DOM normalization step, or separate
`StatisticsRequirement` class. Missing fields are optional, not deserialization
errors. Unknown fields and value conversions use normal JSON.NET behavior;
incompatible JSON shapes or out-of-range integers follow existing query-plan
error handling rather than being swallowed. QueryPlan does not depend on
Cosmos elements. The cache reads the typed models and constructs independent
path and term identities without a JSON-library bridge or serialize/reparse step.

The obsolete whole-query `HybridSearchStatisticsCacheKey` is removed.
Cache `TryGet` and `TryAdd` return Boolean outcomes. Missing/null/empty paths
and terms, negative distances, or unneeded statistics bypass caching without
failing the query. A null query-info argument remains a programming error and
throws `ArgumentNullException`.
Individual empty strings remain distinct string values.
Absent/null distance differs from a present zero distance. The cache does not
retain the mutable lists or model instances.
Future cache binding can trace a generic bypass/miss without repeating
validation to recover a more detailed reason.

The helpers are not yet wired into query execution. Errors in required plan
fields still follow normal query-plan error handling.

### Extracted-metadata identity

Use the normalized extracted paths, resolved terms, and fuzzy distances as the
query-dependent identity, together with the container/index identity and
effective scoring corpus. Do not include SQL text, SQL formatting,
compatibility fields, or the serialized parameter collection.

`GlobalStatisticsQuery` is an execution recipe for a cache miss, not an input to
key creation. Different projections, top counts, vector parameters, or unused
parameters must not prevent reuse when extracted full-text requirements and
scoring scope are unchanged. A changed full-text term still changes the key
through the producer's resolved metadata.

Missing or incomplete metadata retains its explicit bypass behavior. Request
options and custom-handler presence are not key inputs or key-creation
eligibility checks. There is no serialized-query fallback or alternate guarded
identity mode.

### Container-revision buckets and search-term keys

Separate container identity from query identity:

```text
Client-owned cache
    (container RID, observed ETag)
        DocumentCount -> (value, collectionStartedAt)
        path key
            TotalWordCount -> (value, collectionStartedAt)
            term key -> (hitCount, collectionStartedAt)
```

| Level | Identity | Purpose |
|---|---|---|
| Container dictionary | Container RID and observed ETag | Isolate containers and revisions; hold document count once. |
| Path dictionary | Ordered path segments | Hold total word count once per path. |
| Hit-count dictionary | Exact term text and optional fuzzy distance | Reuse individual counts across subsets, permutations, and different scoring components. |

Different revisions use different buckets rather than mutating an existing
bucket's ETag or clearing it when another query observes different metadata.
ETags are opaque; do not attempt to order revisions. A late fetch for an older
revision cannot replace the newer revision's entry. Old buckets are reclaimed
through normal expiration and eviction.

Client ownership implicitly partitions by account, client configuration, and
credential context; never share this cache statically across clients.

The scoring corpus is always the entire container, so the key needs no
statistics ranges, partition IDs, EPK boundaries, feed ranges, or partition-key
options. Global queries with the same container identity and extracted
requirements can reuse statistics regardless of their component-query ranges.

The caller must check `FullTextScoreScope.Global` before creating a cache
binding. Do not reintroduce request options or a scope argument into the key.
Keep existing range selection in the hybrid stage: Global misses execute over
`allRanges`, and Local queries continue over `targetRanges` without caching.

The current query properties do not carry a container ETag. Add it to
`ContainerQueryProperties`, populated alongside the already fetched RID in
`GetCachedContainerQueryPropertiesAsync`. Do not hash mutable
`ContainerProperties` instances or retain them in entries.

Use exact metadata values for equality, not `GetHashCode` alone or
a delimiter-joined representation. A digest may accelerate lookup, but matching
digests must still compare the full identity.

Path keys are `CosmosObject` values containing a `path` segment array.
Term keys are `CosmosObject` values containing `term` and nullable `distance`.
Do not join path segments, split phrase strings, or normalize case. No version
or kind discriminator is needed. RID and ETag belong only to the outer map.

Equality and hashing use Cosmos element structural semantics. Object-property
order is irrelevant, while path-segment order remains significant. Hashes are
only dictionary accelerators, not equality substitutes. There is no
serialized identity string or Newtonsoft DOM processing in the cache-key
layer. It constructs the `Microsoft.Azure.Cosmos.Json`-backed identity from
the typed CLR models; later mutations of query-plan models cannot
change existing keys. Never emit raw key objects in diagnostics.

Key creation takes only the hybrid-plan metadata.
It takes no statistics ranges, `SqlQuerySpec`, serializer,
`QueryRequestOptions`, or handler-state flag and does not read
`GlobalStatisticsQuery`.

Cache lookup and admission take captured RID/ETag and the hybrid plan.
Missing RID or ETag returns false without creating a bucket. Future query
bindings must retain that pair and an owned snapshot of the ordered path/term
metadata used by the fetch; never reinterpret old results using a newer plan
or container revision at publication.

Consistency/read strategy, session tokens, scan/precision flags, excluded
regions, and custom handlers do not partition the cache or reject key creation.
The existing request pipeline remains responsible for request validation and
execution. This does not change the opt-in acceptance of potentially stale
statistics or the consistency settings used by actual service requests.

### Serialization and parameter stability

For plan and data requests, reuse `CosmosSerializerCore.ToStreamSqlQuerySpec` and the existing
`CosmosSqlQuerySpecJsonConverter` behavior, including custom serialization and
`SerializedParameterValue`. Do not use `ToString()` or an unrelated JSON
serializer to reinterpret parameter objects.

The cache relies on parameter values and serialization output remaining stable
during iterator execution. Use the existing query specification and parameter
serialization path for planning, statistics collection, component queries, and
retries. Do not add query-spec snapshots, parameter freezing, extra serialization
passes, or mutation guards for caching.

This assumption keeps resolved plan metadata aligned with parameterized
statistics requests. Mutating parameter values or changing serializer output
mid-iterator is outside this cache's guarantees. Cache identities still use
only resolved paths and terms; cached keys/counts retain neither parameter
objects nor serialized query payloads. Existing pre-serialized parameter
handling remains unchanged.

The key does not inspect request options or execute request-header callbacks.
Query execution continues to use its existing options and handler pipeline.

## 6. Liveness checks and invalidation

Perform the following checks when the iterator first needs statistics:

| Check | Failure behavior |
|---|---|
| Option enabled and an SDK-managed hybrid plan requires statistics | Otherwise the SDK cache is not used. |
| `FullTextScoreScope.Global` | Local scope bypasses both lookup and admission. |
| Usable extracted path-and-term requirements and known slot associations | Bypass cache. |
| Required RID and ETag available | Bypass cache. |
| Exact key present | Otherwise miss. |
| Every selected count is within the client-wide allowance | Otherwise remove expired counts and fetch. |
| Assembled counts satisfy numeric invariants | Otherwise fetch; do not clamp or return partial results. |
| Complete, structurally valid immutable result | Otherwise remove that entry and fetch, recording an invariant failure. |

All identity checks use metadata already resolved by normal query setup.
There is no collection HEAD/read, partition probe, statistics-version check,
document count query, or synthetic "liveness query" on a hit.

Consequently:

- An **observed** container policy/ETag change selects a different bucket.
- A refreshed container RID cannot consume the old container's entry.
- An unobserved metadata change or document mutation is not detected by these
  checks. TTL only limits reuse of the sampled statistics.
- Container ETags do not change for every item write and are not corpus versions.
- Partition splits/merges alone do not change the container-wide identity.
  A miss still uses the existing fan-out and partition-retry behavior.
- Ordinary name-cache-stale retry still refreshes metadata and reconstructs the
  pipeline. The reconstructed pipeline gets a new binding; do not carry the
  original cached statistics across that retry.

Publish a fetched result under the identity captured for that fetch, never
under a newer RID/ETag identity. If a relevant identity change is
explicitly observed during collection, skip admission. Otherwise, a subsequent
query with refreshed metadata naturally cannot hit the old identity.

No write hooks or change-feed subscription are added. Such hooks would miss
external writes and would not establish strict freshness.

## 7. Minimal architecture

```text
ClientContextCore
    captures client-wide age and entry-count policy
    owns one HybridSearchStatisticsCache
        (RID, ETag) -> document count + path word counts + per-term hit counts
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

Phase 4 adds an internal `HybridSearchStatisticsCache` with a small surface:

```text
GetElapsedTime() -> monotonic time used to capture collection start
TryGet(containerRID, containerETag, hybridPlan, out statistics) -> bool
TryAdd(containerRID, containerETag, hybridPlan, statistics, collectionStartedAt) -> bool
Clear/Dispose
```

The service consumes existing typed query-plan models and returns the existing
statistics classes. Private container/path dictionaries and an immutable
`CachedCount` value hold only the scalar and its timestamp. No whole-query key,
statistics-requirement class, or general cache framework is needed.

The cache captures maximum age and the client-wide entry-count limit at
construction. Lookup builds a new independent statistics snapshot in plan
order, without renewing any count's age. `Count` reports retained hit-count
entries; `ContainerRevisionCount` reports nonempty container buckets.
The future binding carries the cache reference, captured RID/ETag, and an
owned snapshot of the hybrid plan's ordered path/term metadata.
It does not hold a pipeline, response, `ITrace`, cancellation token,
or a reference back to the iterator. The statistics request is an
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

Phase 5 uses constructor injection. The existing `ClientContextCore.Create`
factory configures a `HybridSearchStatisticsCache` from the cloned client
options for a positive age; otherwise it passes null. The context constructor
accepts either and stores only a readonly cache reference. It has no separate
cache age/capacity fields and performs no lazy creation.

The nullable `HybridSearchStatisticsCache` properties on `CosmosClientContext`
and `CosmosQueryClient` expose that same dependency. Enabled clients allocate
one empty cache during construction, not on first query. Disabled clients
allocate none. Creating containers or reading the property never creates a
replacement. Changes to supplied or exposed options cannot change cache policy.

The context owns disposal through its existing cleanup flow, with a null-safe
cache disposal call before the other resources. There is no additional context
disposal lock or change to when its disposed flag is set. CosmosClient already
guards the normal disposal entry point, and the cache's own lock synchronizes
lookup/admission with cache disposal. After disposal, property access throws the
normal ObjectDisposedException and held cache references reject new admission.
Selected statistics remain independent of disposal.

During phase-6 integration, use the cache property after query-level eligibility
checks; null means disabled. Do not introduce another factory/get-or-create
accessor or re-read mutable ClientOptions to decide enablement. Global/Local
binding and statistics request-skipping remain pending.

### Storage and concurrency

Use one outer container-revision dictionary, nested path dictionaries, and
per-path hit-count dictionaries behind one short lock. The limit applies to
the total number of distinct hit-count entries across all paths and containers.
Remove empty path and container parents; do not retain document/word counts
without any descendant terms. Thus parent counts are bounded by term capacity.
Misses do not create storage.

Prepare immutable keys and validate/copy incoming scalar values outside the
lock. Allocate lookup result buffers outside the lock, read selected counts
under one lock, then construct the returned statistics objects outside it.
Never execute I/O or await while holding the lock. Use a monotonic clock with
an injected elapsed-time delegate for deterministic tests.

Phase 2 adds one internal `CosmosClientOptions` capacity setting:
`FullTextStatisticsCacheMaxEntries`, defaulting to 256 term hit counts per client.
The setter rejects zero or negative values immediately, leaving the previous
value unchanged. Client construction clones the options without a separate
validation pass. Changing the original options after constructing a client does
not change that client's setting. Internal callers and tests can override it;
there are no public capacity options, environment variables, or global mutable
test settings.
The client owner passes the captured entry limit into the storage constructor.
There are no per-container limits.

Use only the entry-count limit. Do not add byte-size limits, retained-size
accounting, or size-based admission. This bounds the number of retained entries,
not their combined memory footprint; unusually large entries can still consume
substantial memory.

Remove expired counts during insertion. If necessary, evict the oldest
collected hit counts by bounded scan, preserving already-cached terms needed
by the incoming publication. Deduplicate repeated `(path, term, distance)`
slots for capacity accounting. If a complete response exceeds total term
capacity, skip its admission without evicting existing data. No LRU list,
access timestamp writes, timers, or background eviction is needed.

For each individual counter, prefer the later collection start. A slow older
fetch may fill missing terms but must not replace newer document, word, or hit
counts. Equal timestamps retain the existing counter. Only counters actually
present in a successful response are updated; unrelated terms keep their ages.
All callers still use their independently fetched result on a miss.

Age is checked again under the lock after copying, so a slow copy cannot admit
an expired entry. The cache also rejects future collection-start times.
Disposal clears all dictionary levels and prohibits lookup/admission,
including publication already copying a snapshot when disposal occurs.
Clearing alone removes retained entries but allows subsequent admission.
Neither operation mutates a snapshot already selected by an iterator.

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
4. On complete success, pass the valid aggregate to `TryAdd` under the captured
   RID/ETag and ordered plan metadata. The cache validates and distributes its
   scalar values; do not publish partial aggregates or fetch only missing terms.
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

Reuse `GlobalFullTextSearchStatistics` and `FullTextStatistics` as input/output
models. Admission copies scalar values, never retaining input arrays, lists,
or mutable plan objects. Lookup creates an independent read-only outer list
and new hit-count arrays; eviction, refresh, and disposal cannot alter a
snapshot already selected by an iterator.

Store only:

```text
CachedCount
    value
    collectionStartedAt
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
| `Reason` | `NotFound`, `Expired`, `Disabled`, `IneligibleMetadata`, `UnsupportedExecutionContext`, `InvalidCachedShape` |
| `Scope` | `Global` for cache operations; `Local` may be traced as a scope bypass. |
| `PathSlotCount`, `TermSlotCount`, `StatisticsRangeCount` | Counts, not paths, terms, or boundaries. |
| `MaxAgeMs` | Client-wide allowance applied independently to every selected count. |
| `PopulationDecision` | `Stored`, `TooOld`, `Superseded`, `IdentityChanged`, `DescriptorMismatch`, `ClientDisposed` |

There is no single cached-entry ID or collection time for an assembled result.
Do not assign one counter's age to the entire snapshot. If a required path or
term is absent, the result may simply be `NotFound`. Do not claim to
know whether terms, a RID, or an ETag changed without evidence, and do not
maintain a second history index just to classify misses.

Nest real statistics execution beneath an existing query trace or a clearly
named `Gather Hybrid Search Statistics` child. A hit should visibly lack that
network subtree.

Do not log raw SQL, parameters, full-text paths, terms, hit counts, full keys,
or hashes derived from low-entropy search terms. Existing query-text opt-ins
remain separate. Use the existing iterator trace relationships for correlation
without making cache diagnostics a new customer-data channel.

No new public metrics or custom trace-datum hierarchy is required for v1.

## 11. Verification plan

### Pure cache tests

Use an injected monotonic elapsed-time delegate, following the existing
testability pattern in `WebExceptionRetryPolicy`, rather than sleeping.

Cover exact expiry boundaries, collection duration exceeding the allowance,
non-sliding hits, isolated clients with different age policies, defensive
copies, the global term-count limit, eviction, disposal, and out-of-order
concurrent publication. Verify independent per-counter timestamps, no renewal
of old terms when shared counts refresh, numeric-compatibility misses, exact
metadata/result dimensions, conflicting duplicates, and oversized batch bypass.

### Key and plan compatibility tests

Cover absent/null/empty/incomplete paths and terms, normal Newtonsoft parsing
and error behavior for incompatible JSON, path-to-term association,
path and term order/case, repeated paths with different term groups, duplicate
terms, phrases versus separate words, matching/analyzer choices, and output-slot
mapping. Include parameter-bound terms, custom serializers,
`SerializedParameterValue`, stable execution parameters, and exact equality
even when lookup hashes collide.

Verify that equivalent literal and parameter-bound
requirements share identity when their resolved values and semantics match,
and that unrelated parameter changes do not cause misses. Use real native plans
with different projections, vector values, parameter ordering, and unused
parameters to verify cross-query reuse. Different containers/revisions must
select different buckets; Local scoring remains uncached.

Exercise both gateway-deserialized and locally generated plans. Confirm that
older producers without metadata execute normally and that unfamiliar optional
metadata does not corrupt required plan fields.

### Pipeline tests

Extend the existing mock and statistics-range/request-charge assertions in
`tests\Microsoft.Azure.Cosmos.Tests\Query\Pipeline\NonStreamingOrderByQueryTests.cs`.

| Scenario | Required observation |
|---|---|
| Disabled or missing metadata | Existing statistics requests and result behavior. |
| First eligible query | Full statistics fan-out; admit document, path-word, and per-term hit counts. |
| Same key while live | Zero statistics backend requests; unchanged frozen-corpus results. |
| Same path, an uncached term | Full miss; never reuse another term's hit count. |
| Same terms assigned to different paths | Miss; no cross-path count reuse. |
| Same path, reordered/subset/repeated terms | Reuse individual counts and reconstruct the exact requested order and duplicates. |
| Requested counts collected by different queries | Hit if every selected scalar is live and the assembled counts are numerically compatible. |
| Refreshed document/word count with an expired old term | Miss; parent refresh does not renew the term. |
| Live old hit count exceeds a newer document count | Miss and fetch the complete statistics query; do not clamp values. |
| Same descriptors, different projection/vector-only or unused parameters | Global cache hit when container identity matches. |
| Different supported path/term/distance values or scoring corpus | Different identity or explicit bypass, never a false hit. |
| Missing terms, unresolved bindings, or unknown slot mapping | Explicit bypass with normal statistics execution. |
| Optional descriptor/result shape mismatch | Skip admission; no incompatible bundle can later be reused. |
| Expired entry | Normal fresh statistics request. |
| Clients with different age allowances | Independent cache policy and storage; no per-query override. |
| Global with different component target ranges or original routing inputs | Reuse the same container-wide statistics. |
| Local scope, including ranges covering the whole container | No cache lookup or admission; preserve existing statistics requests. |
| Local after an equivalent Global query warms the cache | Still collect Local statistics; do not consume the Global entry. |
| Observed RID or ETag change | Miss; no reuse under the refreshed identity. |
| Partition topology change with otherwise unchanged identity | No topology discriminator in the Global key. |
| Name-cache-stale pipeline reconstruction | New binding; preserved existing retry behavior. |
| Partial statistics failure or cancellation | No partial admission and no stale fallback. |
| Concurrent cold misses | Independent cancellation, responses, and RU accounting. |
| Successful statistics followed by component failure | Original component error, no hidden replay. |
| Valid empty-corpus statistics | Normal zero-count handling; no invented values. |
| Skip/take, singleton, weighted RRF, and multi-page reads | Existing ranking and paging behavior on a frozen corpus. |
| Long-lived iterator crossing TTL | Same selected statistics throughout that iterator. |
| Strong, Bounded Staleness, GlobalStrong, or explicit session tokens | Eligible for opt-in caching; retain original service request semantics. |
| Server-managed distributed execution | Remains outside this SDK statistics pipeline. |
| Diagnostic inspection | Correct decisions and charges; no raw search content or old response metadata. |

### Emulator and acceptance criteria

Extend existing hybrid-search emulator coverage with Global cache scenarios
across multiple physical partitions and Local uncached regressions. Use injected
time for expiry tests.

On a frozen corpus, a warm eligible query must:

1. Send no global-statistics query while still sending component queries.
2. Return the same documents, ordering, and scores as the uncached counterpart.
3. Omit the statistics request charge, without dropping any other actual charge.

After a write, results may use the old ranking inputs within the opted-in
interval. After expiry, a new statistics request must be observed. Tests must
not claim index freshness before the emulator/service has actually exposed the
write.

Measure warm first-result latency, actual RU totals, hit rate, and retained
memory under high-cardinality terms and many containers. Do not assert a
universal percentage improvement or simulate an engine field as evidence that
the real producer already emits it.

## 12. Implementation sequence and review gates

1. **Confirm the producer contract.** Obtain actual path-and-term payloads from
   gateway and ServiceInterop plans, including repeated paths, phrases,
   parameterized terms, and statistics-slot mappings. Use extracted metadata
   as the query-dependent key; do not reintroduce serialized SQL or parameter
   guards. No new tokenization logic belongs here.
2. **Review the public semantics.** Approve the client option, consistency
   bypass policy, and bounded-staleness documentation. Keep the default off.
3. **Add the passive cache and tests.** Cache scalar counts under path and term
   identities, reconstruct the existing statistics result classes in plan order,
   and rely on stable parameters and serialization during iterator execution.
4. **Wire identity and options.** Capture the `CosmosClientOptions` policy in
   client-context ownership; update `ContainerQueryProperties`,
   `CosmosQueryClient`/`CosmosQueryClientCore`, and query-factory bindings.
   Gate cache bindings on Global scope; leave the existing statistics range
   selection and Local execution in the hybrid stage.
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
| Common-generation-only statistics | Not selected: the user accepts independently bounded-age counts. Numeric compatibility is still checked before returning a result. |
| Query-text parsing to extract dependencies | Duplicates engine knowledge about expressions, tokenization, language, filters, and parameter use. |
| In-flight query coalescing | Adds cancellation, pipeline lifetime, trace ownership, and RU-attribution decisions. |
| Background refresh or stale-on-error | More lifecycle work and a less obvious freshness/failure contract. |
| New backend liveness requests | Adds latency/RU and requires a service version contract; TTL alone cannot provide strict freshness. |
| Process-wide or persistent cache | Cross-client isolation, lifecycle, and invalidation complexity without a demonstrated requirement. |
| Granular write/index invalidation | SDK writes are only one source of change and do not establish index visibility. |

Subset, permutation, repeated-slot, and cross-query count reuse are implemented.
Generating narrower statistics queries for only missing terms remains deferred:
every miss continues using the existing complete statistics query.
