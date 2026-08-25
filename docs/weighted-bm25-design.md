# Ranking Using Weighted BM25 Across Multiple Properties and Needles

**Status:** Design proposal  
**Audience:** Query, indexing, runtime, SDK, and relevance teams  
**Last updated:** August 24, 2026

## 1. Requirements

### Functional requirements

Support relevance ranking across:

- Multiple properties.
- Multiple search needles.
- Per-criterion weights.

The initial capability combines BM25 scores produced by `FULLTEXTSCORE`. For
example:

```text
Score(d) =
    5 * wordBM25_displayName(d)
  + 1 * wordBM25_description(d)
  + 0.5 * wordBM25_workspace(d)
```

More generally:

```text
Score(d) = SUM_i(weight_i * score_i(d))
```

Full-text components use BM25, and documents are ordered by the combined
weighted score.

The score-expression and query-plan design must remain extensible to additional
scorers in later releases. Expected future examples include exact equality with
term-frequency/inverse-document-frequency (TF/IDF)-style ranking and n-gram
scoring. Those scorers and their required index/statistics support are not part
of the initial implementation.

### Difference from weighted RRF

Weighted BM25 preserves and combines the original score magnitudes:

```text
Score(d) = SUM_i(weight_i * score_i(d))
```

Weighted reciprocal rank fusion (RRF) first converts each component result into
a rank:

```text
RRF(d) = SUM_i(weight_i / (60 + rank_i(d)))
```

With equal component weights:

| Document | Display-name BM25 | Display rank | Description BM25 | Description rank | Combined BM25 | BM25 result rank | RRF score | RRF result rank |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| A | 100 | 1 | 0.9 | 2 | 100.9 | 1 | `1/61 + 1/62` | 1 (tie) |
| B | 2 | 2 | 1.0 | 1 | 3.0 | 2 | `1/62 + 1/61` | 1 (tie) |

Weighted BM25 strongly prefers document A because it preserves score
magnitude. RRF considers only component ranks and therefore gives both
documents the same score. A decisive BM25 win and a marginal win have the same
RRF contribution when they produce the same rank.

## 2. Limitations and Execution Tradeoffs

### Current stack

The current query stack and runtime do not evaluate multiple property-level
scores as one backend ordering expression. The existing multi-component RRF
path:

1. Creates an ordered query for each component.
2. Retrieves a bounded candidate set from every component.
3. Unions and de-duplicates candidates in the SDK.
4. Converts component scores to ranks.
5. Applies weighted RRF in the SDK.

The statistics portion is reusable: the SDK already gathers global or scoped
BM25 statistics and supplies them to partition queries. The component-query and
fusion portions do not provide exact additive ranking because each component is
truncated independently.

### Option A: Client-side candidate merge

This approach adapts the current multi-component execution shape:

```text
Property A component query --> TOP M --\
Property B component query --> TOP M ----+--> SDK candidate union
Property C component query --> TOP M --/          |
                                                   v
                                      calculate weighted score
                                                   |
                                                   v
                                              global TOP N
```

Each property remains a separate, bounded backend query. The SDK unions
candidates by resource ID and computes:

```text
Score(d) = SUM_i(weight_i * propertyScore_i(d))
```

Each candidate needs a score for every property component. That requires the
component queries to project all needed scores or requires additional score
lookups after building the candidate union.

**Advantages:**

- Reuses more of the current RRF component-query and SDK coordination code.
- Keeps each backend operation narrower and bounded by `TOP M`.
- Reduces the chance that one wide, multi-index backend operation reaches its
  execution timeout.
- Allows component queries to run in parallel and be retried independently.
- Supports a faster initial implementation and straightforward overfetch
  tuning.

**Limitations:**

- No fixed `M` guarantees exact recall.
- RU, network, SDK memory, and buffering increase with properties, partitions,
  and `M`.
- Pagination and continuation state must cover several component queries plus
  the client-side candidate union.
- A positive weight does not change a component's ordering, so applying weights
  before retrieval does not recover omitted candidates.
- Latency may still be high because the client waits for every component across
  every targeted partition.

The recall problem exists even with only two properties. Assume `M = 2` and
equal weights:

| Document | Property A score | A rank | Property B score | B rank | Combined score | Retrieved |
|---|---:|---:|---:|---:|---:|---|
| A1 | 100 | 1 | 0 | 5 | 100 | A only |
| A2 | 90 | 2 | 0 | 4 | 90 | A only |
| **X** | **80** | **3** | **80** | **3** | **160** | **No** |
| B2 | 0 | 4 | 90 | 2 | 90 | B only |
| B1 | 0 | 5 | 100 | 1 | 100 | B only |

The component queries return `{A1, A2}` and `{B1, B2}`. Document X is absent
from both candidate sets even though it has the highest additive score.

Recall can be improved by increasing `M`, adapting it to query selectivity, or
fetching additional rounds from promising components. These remain
approximations unless the backend exposes safe upper bounds and continuation
state that let the coordinator prove no unseen document can beat the current
top-*k* threshold. In the worst case, an exact client algorithm can be forced to
retrieve most matching documents.

### Option B: Backend aggregate scoring

This approach sends the complete weighted expression to each targeted
partition:

```text
Global or scoped BM25 statistics
                 |
                 v
  Partition 1 --> complete weighted score --> local TOP N --\
  Partition 2 --> complete weighted score --> local TOP N ----+--> SDK merge --> global TOP N
  Partition 3 --> complete weighted score --> local TOP N --/
```

Each partition traverses the required property indexes, calculates all
property-level BM25 scores for a candidate, applies the weights, and maintains
its top-*k* heap using the final combined score. Once every partition uses the
same score definition and statistics, returning local `TOP N` from each
partition is sufficient to produce exact global `TOP N`.

**Advantages:**

- Preserves exact additive ranking without a fixed per-component candidate
  window.
- Improves recall for documents that score moderately across several
  properties.
- Sends only partition-local winners over the network.
- Reduces SDK buffering and leaves the SDK with a standard k-way score merge.
- Gives the backend visibility needed for WAND, block-max WAND, threshold, and
  other safe score-bound optimizations.

**Required backend work:**

- Extend SQL binding and the query-plan contract with a weighted score-expression
  tree.
- Accept global/scoped BM25 statistics for every property component.
- Open and coordinate multiple property posting sources in one ranked plan.
- Enumerate the union of matching documents and obtain every required component
  score.
- Maintain safe upper bounds for the remaining weighted score.
- Apply the complete score before partition-local truncation.
- Add resource governance, cancellation, diagnostics, and continuation support
  for a potentially long-running ranked operation.

**Limitations and risks:**

- A broad query can keep one backend request active longer and increase timeout
  risk.
- Multiple index traversals and score evaluations can materially increase RU,
  CPU, and memory.
- Exact top-*k* pruning is more complex when terms and properties have different
  posting distributions.
- Filters, partition splits, continuation, and statistics scope expand the
  correctness test matrix.
- Strict execution budgets can conflict with an exact-result contract unless
  the query can resume without changing ordering.

### Tradeoff summary

| Dimension | Client-side candidate merge | Backend aggregate scoring |
|---|---|---|
| Backend timeout exposure | Lower per query because work is split and bounded | Higher for broad queries unless pruning and continuation are effective |
| Ranking recall | Approximate for any fixed `TOP M` | Exact when the complete score is applied before local `TOP N` |
| Backend implementation | Reuses current component queries | Requires a new multi-index ranked-expression plan |
| SDK complexity | Candidate union, buffering, score completion, and multi-query state | K-way merge of already-combined partition scores |
| Network and memory | Grows with `M`, properties, and partitions | Primarily local winners and diagnostics |
| RU predictability | Easier to cap through `M`, but may require expensive overfetch rounds | More query-dependent; needs cost estimation and runtime governance |
| Continuation | Coordinates several component continuations and buffered candidates | Requires resumable state for one combined ranked plan |
| Exactness | Only with exhaustive retrieval or provable multi-round bounds | Natural execution model for the specified score |

If exact additive semantics are part of the public contract, backend aggregate
scoring is the appropriate target. Client-side merging can provide a bounded,
lower-timeout approximation, but the approximation and candidate-window
controls must be explicit to customers rather than presented as equivalent
ranking.

## 3. Competitor Implementations

The closest comparisons are systems that preserve lexical score magnitudes and
combine weighted field or clause scores. BM25F-only systems, custom non-BM25
rankers, and lexical-vector fusion APIs are excluded because they implement
different score semantics.

| Product | Comparable mechanism | Main difference |
|---|---|---|
| Elasticsearch | Query-time field boosts and additive `bool.should` clauses | Text-oriented query variants rather than a general ranked-expression tree. |
| Azure AI Search | BM25 field weights in scoring profiles | Weights are predefined in index configuration. |
| MongoDB Search | Additive boosted clauses in `compound.should` | Operator and analyzer behavior is defined by the Search index. |
| Vespa | Explicit arithmetic over BM25 rank features | Expressions are deployed in schema rank profiles. |

### Elasticsearch

Official documentation: [multi-match query](https://www.elastic.co/docs/reference/query-languages/query-dsl/query-dsl-multi-match-query)
and [Boolean query](https://www.elastic.co/docs/reference/query-languages/query-dsl/query-dsl-bool-query).

Elasticsearch exposes several lexical designs rather than one universal
multi-field score.

`multi_match` with `most_fields` computes a score for each field and combines
the completed field scores:

```json
{
  "query": {
    "multi_match": {
      "query": "distributed consensus",
      "fields": ["displayName^5", "description", "workspace^0.5"],
      "type": "most_fields"
    }
  }
}
```

Boosting can also be applied to individual `bool.should` clauses, allowing
exact, phrase, fuzzy, and full-text clauses to contribute additively.

**Limitations and differences:**

- `multi_match` operates over text-query variants and does not itself define a
  general expression over arbitrary scoring functions.
- `_explain` provides detailed scoring information, but that detail also shows
  the complexity customers must understand when modes are combined.

### Azure AI Search

Official documentation: [Add scoring profiles to a search index](https://learn.microsoft.com/en-us/azure/search/index-add-scoring-profiles).

Azure AI Search uses named scoring profiles stored in the index definition:

```json
{
  "scoringProfiles": [
    {
      "name": "entityRanking",
      "text": {
        "weights": {
          "displayName": 5,
          "description": 1,
          "workspace": 0.5
        }
      }
    }
  ]
}
```

The query selects the profile using `scoringProfile=entityRanking`. Text weights
adjust the contribution of searchable fields to BM25 relevance. Scoring
functions can additionally boost freshness, geographic distance, numeric
magnitude, or tags.

**Limitations and differences:**

- Profiles are index configuration, not arbitrary score-expression syntax.
- A query selects one scoring profile, so applications must predefine useful
  combinations or generate index configuration ahead of time.
- Field weights do not provide independent query-time matching modes and
  tokenization strategies for the same field.

### MongoDB Search

Official documentation: [`compound` operator](https://www.mongodb.com/docs/search/query/operators-collectors/compound/)
and [modify the score](https://www.mongodb.com/docs/search/query/score/modify-score/).

MongoDB Search uses additive scoring clauses inside `compound.should`:

```json
{
  "$search": {
    "compound": {
      "should": [
        {
          "text": {
            "query": "distributed consensus",
            "path": "displayName",
            "score": { "boost": { "value": 5 } }
          }
        },
        {
          "text": {
            "query": "distributed consensus",
            "path": "description",
            "score": { "boost": { "value": 1 } }
          }
        }
      ],
      "minimumShouldMatch": 1
    }
  }
}
```

Matching `should` clauses contribute to the document score, while `filter`
clauses do not. Text, phrase, autocomplete, equals, and other operators can use
boost or constant score modification. This clause model is one of the closest
analogues to the requested heterogeneous additive expression.

**Limitations and differences:**

- Operator score semantics differ; using `equals` does not by itself establish
  the requested complete-value TF/IDF definition.
- Search operators and their analyzers must be represented in the Atlas Search
  index configuration.

### Vespa

Official documentation: [BM25 ranking](https://docs.vespa.ai/en/ranking/bm25.html).

Vespa exposes ranking as a schema-defined expression:

```text
rank-profile entity-ranking {
    first-phase {
        expression:
            10 * attribute(exact_name_match) +
             5 * bm25(displayName) +
             1 * bm25(description) +
           0.5 * bm25(workspace)
    }
}
```

Rank profiles can combine BM25, native rank features, attributes, tensor
operations, vector closeness, and query features. First-phase ranking can be
followed by more expensive second-phase or global-phase reranking. Rank features
can be returned for diagnostics.

**Limitations and differences:**

- Rank profiles are deployed schema configuration rather than unrestricted
  query syntax.
- The application owns transformations and calibration when features use
  different scales.
- Adding a new field, score feature, or expression can require schema and
  deployment changes.
- Flexible expressions expose more control but also more tuning complexity than
  a constrained weighted lexical operator.

### Competitive conclusions

- Field, term, and clause boosting are standard relevance controls.
- Elasticsearch and MongoDB Search are the closest query-time analogues because
  matching boosted clauses contribute additively.
- Azure AI Search and Vespa provide comparable weighted lexical ranking through
  deployed scoring profiles or rank expressions.
- Query-time boosts offer flexibility; deployed profiles offer reuse,
  governance, and more predictable configuration.
- Explainability is expected when users tune interacting weights.

## 4. Proposed Syntax

### Multi-property weighted lexical expression

Use a general weighted score operator over explicit score-producing
expressions:

```sql
SELECT TOP 20 c.id, c.displayName
FROM c
ORDER BY RANK WEIGHTEDSUM(
    FULLTEXTSCORE(
        c.displayName,
        @displayNameTerms,
        @displayNameTermWeights),
    FULLTEXTSCORE(
        c.description,
        @descriptionTerms,
        @descriptionTermWeights),
    FULLTEXTSCORE(
        c.workspace,
        @workspaceTerms,
        @workspaceTermWeights),
    [5, 1, 0.5])
```

Candidate function names include `WEIGHTEDSUM`, `WEIGHTEDSCORE`, and
`ADDITIVESCORE`. `WEIGHTEDSUM` is recommended because it states the score
algebra directly.

BM25 should not appear in the outer function name. The function does not
calculate BM25; it combines scores that have already been produced by child
expressions. The initial children are BM25-based `FULLTEXTSCORE` expressions,
but later scorers may use other models. Naming the outer operator
`WEIGHTEDBM25` would imply that the combination itself calculates BM25, could be
confused with BM25F, and would couple a general algebraic operation to one
scoring implementation. BM25 remains part of `FULLTEXTSCORE` semantics.

Semantics:

```text
WEIGHTEDSUM(d) = SUM_i(weight_i * childScore_i(d))
```

- Higher scores rank first.
- Child scores preserve their original magnitudes.
- The operator does not convert scores to ranks or normalize them.
- Every weight must be finite and greater than zero.
- The number of weights must equal the number of child expressions.
- Ties use a deterministic resource-ID fallback.
- The initial version accepts `FULLTEXTSCORE` children only.

### Per-needle weights within a property component

`FULLTEXTSCORE` can add one weight per search needle:

```sql
SELECT TOP 20 c.id, c.displayName
FROM c
ORDER BY RANK FULLTEXTSCORE(
    c.displayName,
    @terms,
    @termWeights)
```

Semantics:

```text
FULLTEXTSCORE(d) =
    SUM_t(termWeight_t * BM25TermContribution_t(d))
```

This supports term or needle boosting inside each property component. Multiple
property components are then combined additively by `WEIGHTEDSUM`, not by RRF:

```sql
SELECT TOP 20 c.id, c.displayName
FROM c
ORDER BY RANK WEIGHTEDSUM(
    FULLTEXTSCORE(
        c.displayName,
        @displayNameTerms,
        @displayNameTermWeights),
    FULLTEXTSCORE(
        c.description,
        @descriptionTerms,
        @descriptionTermWeights),
    [5, 1])
```

The inner arrays weight BM25 term contributions. The outer array weights the
completed property-level scores. Both levels preserve score magnitude.

### Future score-function extensibility

The expression and plan format should allow additional score-function kinds in
later releases without changing `WEIGHTEDSUM`. Exact-match and n-gram scoring
are expected extensions, not part of the initial implementation:

```sql
-- Future syntax; not part of the initial feature.
ORDER BY RANK WEIGHTEDSUM(
    EXACTMATCHSCORE(c.displayName, @query),
    NGRAMSCORE(c.displayName, @query),
    FULLTEXTSCORE(c.description, @terms),
    [10, 1, 1])
```

Each new scorer requires its own public score semantics, index support,
statistics, validation, and feature negotiation.

### Composition with vector search

Do not allow `VECTORDISTANCE` as a child of `WEIGHTEDSUM` in the initial
feature. Direct addition would combine incompatible raw scales:

```sql
-- Invalid: raw lexical and vector values are not comparable.
ORDER BY RANK WEIGHTEDSUM(
    FULLTEXTSCORE(c.description, @terms),
    VECTORDISTANCE(c.embedding, @vector),
    [1, 1])
```

The desired composition is a complete weighted lexical branch fused with a
vector branch through RRF:

```sql
SELECT TOP 20 c.id, c.displayName
FROM c
ORDER BY RANK RRF(
    WEIGHTEDSUM(
        FULLTEXTSCORE(c.displayName, @terms),
        FULLTEXTSCORE(c.description, @terms),
        [5, 1]),
    VECTORDISTANCE(c.embedding, @vector),
    [2, 1])
```

Supporting `WEIGHTEDSUM` as an RRF child is hybrid-search composition work and
does not change the multi-property weighted lexical scope.

### Validation and limits

- Require at least two child expressions for `WEIGHTEDSUM`.
- Initially allow only `FULLTEXTSCORE`. Add other score-function kinds through
  separately negotiated future capabilities.
- Reject `VECTORDISTANCE` with a targeted score-compatibility error.
- Support literal or parameterized weight arrays.
- Reject zero, negative, and non-finite weights.
- Require a compatible full-text index for every property.
- Resolve analyzer and tokenization from explicit arguments or unambiguous index
  configuration.
- Set explicit service limits on components, properties, and needles.
- Preserve current `ORDER BY RANK` restrictions unless separately expanded.

## 5. Challenges

### Exact top-*k* correctness

Independent component overfetch is only an approximation. The combined winner
can be absent from every individual component window. The runtime must evaluate
the complete expression before local truncation while using only optimizations
whose score bounds preserve exact ordering.

### Lexical score calibration

BM25 scores from different properties do not necessarily have identical score
distributions because document length, average length, term frequency, and
document frequency differ by property. Criterion weights therefore express both
business preference and scale calibration. Component formulas and statistics
must remain stable and explainable or tuned weights will be brittle.

Implicit candidate-set normalization is not proposed because it would change the
required additive semantics and make scores depend on the retrieved candidate
window.

### Lexical-vector compatibility

`FULLTEXTSCORE` and `VECTORDISTANCE` differ in range, distribution, direction,
and stability. Raw addition would make weights difficult to interpret and tune.
RRF should remain the default hybrid combiner.

A future normalized score-fusion feature is possible, but it would require
separate decisions for:

- Min-max, L2, z-score, sigmoid, or learned normalization.
- Per-query versus corpus-level calibration.
- Candidate-window size and missing-candidate behavior.
- Distance-to-similarity conversion.
- Outlier handling and score clipping.
- Explainability and consistency across partitions.

### Distributed statistics

BM25 depends on corpus statistics. Multiple properties and analyzers increase
statistics volume and coordination. Local versus global statistics can change
raw score magnitudes and therefore the effective meaning of customer weights.
The current RRF statistics flow can be reused, but it must gather and map the
statistics for every property component in the combined expression.

### Future scorer extensibility

Exact-match and n-gram score functions are not part of the initial
implementation. The expression and plan contract should accommodate them later
without committing to their scoring semantics now. Exact-match scoring will
need a complete-value statistics definition. N-gram scoring will need index,
analyzer, gram-size, statistics, ingestion-cost, and write-RU decisions.

### RU consumption and latency

The feature may traverse several indexes and score substantially more
candidates before it can establish the combined top-*k*. Performance evaluation
must cover:

- Number of properties and needles.
- Selective and unselective filters.
- Global and local statistics.
- Physical partition count.
- Exact execution with safe score-bound optimizations.
- Memory, network payload, index size, ingestion cost, RU, and end-to-end
  latency.

### API naming and weight semantics

`WEIGHTEDBM25` is recognizable but suggests that the outer operation calculates
BM25 or implements BM25F. `WEIGHTEDSUM`, `WEIGHTEDSCORE`, or `ADDITIVESCORE`
more accurately describe score composition and remain valid when future scorer
types are added.

The product must clearly distinguish three different weight levels:

1. Term weights inside one `FULLTEXTSCORE`.
2. Criterion weights inside the additive lexical expression.
3. Branch weights applied to reciprocal-rank contributions in RRF.

Reusing the same terminology without qualification will create customer
confusion.

### Explainability and relevance validation

Customers cannot tune several interacting weights from an opaque final score.
Component-level score details are necessary for preview quality.

The feature also needs offline relevance evaluation using representative entity,
catalog, directory, and marketplace data. Evaluation should compare the
multi-property additive expression with unweighted BM25 and weighted-RRF
baselines and validate results against a brute-force scoring oracle.

### Open design decisions

- Final public name: `WEIGHTEDSUM`, `WEIGHTEDSCORE`, or `ADDITIVESCORE`.
- Missing, null, empty, and non-string property behavior.
- Global versus local statistics scope.
- Maximum components, properties, and needles.
- Continuation-token and pagination behavior.
- Required explainability for preview.
- Whether normalized lexical-vector score fusion should be a later, separate
  feature.
- Future exact-match score semantics and complete-value statistics.
- Future n-gram sizes, analyzers, languages, and index configuration.
