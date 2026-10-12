# Engineering Quality Rubric — Cross-SDK PR Review

> **Single source of truth** for the dimensions reviewed by the
> `Engineering Quality Reviewer` agent and (for design-alignment) the
> `cosmos-pr-reviewer` agent. Language-agnostic — translate idioms via
> the per-SDK pack (`sdk-packs/<sdk>.md`).
>
> **Hard rule:** every finding from a reviewer that uses this rubric MUST
> include either a code citation (file + line range) or a
> `verified_by_grep` shell command whose output supports the claim.
> Findings without verification are downgraded to severity `nit` and
> tagged `unverified-pattern-suggestion`.

---

## How this rubric is consumed

- The router loads this file once per Tier 2 / Tier 3 review and injects a
  distilled summary (≤ 1500 tokens of `ACTIVE_REVIEW_CONTEXT`) into the
  Engineering Quality Reviewer's prompt.
- The reviewer reads the full file when needed via `view`.
- Findings are emitted in the standard finding schema (id / severity /
  category / file / line_start / line_end / message / evidence /
  suggested_fix) — with the new categories below.

## New finding categories (additive)

The router/merger now accepts these in addition to the existing set
(`correctness | api | perf | security | style | parity | other`):

- `code_quality` — observability, supportability, debuggability, API hygiene, complexity, allocation patterns
- `testing` — test existence, path coverage, failure-mode coverage, concurrency, determinism, regression scope, benchmark requirement
- `design_alignment` — divergence from the canonical Cosmos design docs (cosmos-pr-reviewer owns this category, but EQ Reviewer can flag obvious cases)
- `agentic` — readiness for LLM-driven / agentic API consumption

`perf` already exists; the rubric below sharpens it but doesn't rename it.

---

## Dimension 1 — Code quality

### 1.1 Observability

| Probe | Look for | Flag if |
|---|---|---|
| Are diagnostics emitted at decision points? (retry attempt, region failover, throttle wait, cache miss/hit on hot caches) | structured logger / tracer / metrics call near every `if/else` that branches user-visible behavior | branch decisions are silent |
| Are diagnostics structured? | key=value pairs, structured fields, typed event objects | new free-text-only `log.info("did X")` strings on hot paths |
| Are correlation IDs propagated? | activity ID / request ID / operation ID flowing through async boundaries | new code path drops the ID |

### 1.2 Supportability & error message hygiene

- Exceptions/errors preserve **inner cause** across rethrow boundaries
- Error messages include enough state to debug **without** asking the customer for repro (operation name, partition / region / endpoint, attempt count, elapsed time)
- No PII / secrets / connection strings in messages
- New status codes / sub-status codes are mapped to a stable enum, not raw integers

### 1.3 Debuggability

- Critical state transitions are logged at trace level (cache invalidation, lease takeover, partition-key range change, endpoint discovery)
- Long-lived background loops emit a heartbeat / progress signal at trace
- Public types implement a useful `ToString()` / `__repr__` / `Display` that surfaces identifying fields

### 1.4 API hygiene (public surface)

| Convention | Source-of-truth |
|---|---|
| Async naming + `CancellationToken`/`Context`/etc. last | per-SDK pack |
| Nullability annotations consistent with the SDK's policy | per-SDK pack |
| Doc comments on every public type / method | per-SDK pack |
| New public methods have at least one consumer-facing example | sample folder grep |
| New public option types default to backward-compatible behavior | review against the previous default |

### 1.5 Complexity (advisory, not blocking)

- Function > 75 lines OR nesting depth > 4 → suggestion to extract
- Cyclomatic complexity is judged subjectively (no tool gate); flag only if a single function holds many independent decision branches that have separate failure modes
- Hot-path code (see `risk_paths` in SDK pack) gets stricter complexity scrutiny than control-plane code

### 1.6 Allocation patterns (cross-cutting with Dimension 3)

Flagged here when allocation looks wrong **for any reason** (debugging, GC,
correctness). Performance-only allocation findings live in Dimension 3.

- Boxing of value types in collections / closures
- LINQ / streams / list comprehensions inside per-document loops
- String concatenation in tight loops without builder
- New `byte[]` / `Memory<byte>` allocations on RNTBD / serialization paths without pool reuse

---

## Dimension 2 — Testing quality

> **Pre-flight gate:** before raising any "missing test" finding, the
> reviewer MUST grep the SDK's test directories. The grep command and
> its (empty) result MUST appear in the finding evidence. Use the
> `Test entry points` section of the SDK pack to find the right
> directories.

### 2.1 Existence (the PR #5792 lesson)

For every changed public-facing or hot-path symbol:

```
grep -rn "<symbol>" \
   <SDK pack `Test entry points` paths>
```

If the grep returns hits → do not file a "missing test" finding. Instead:
- check whether the existing tests cover the **changed lines**
- if the existing tests only exercise the success path, file a finding under §2.3 (failure-mode coverage), not §2.1

### 2.2 Path coverage

Open the existing tests for the changed symbol; check that they exercise
the lines actually changed by this PR (not just adjacent lines).
Cite the test file + test name in the evidence when claiming coverage is
adequate or inadequate.

### 2.3 Failure-mode coverage

For every change that adds a new error path, retry path, timeout, or
throttle handling, the PR should add or extend tests for at least one
negative case. Required failure modes per change type:

| Change type | Required negative tests |
|---|---|
| New retry policy / new retryable status | exhausted-retries case + cancellation-during-retry case |
| New timeout configuration | timeout-fires case + timeout-after-partial-response case |
| Throttle / 429 handling change | RU-charge propagation case + retry-after-honored case |
| Concurrency change (lock, semaphore, latch) | concurrent-access-stress test |
| New cache | cache-eviction case + cache-corruption-recovery case |
| New session-token logic | stale-token case + lossy-region case |
| New change-feed lease behavior | takeover-mid-pull case + split-during-checkpoint case |

### 2.4 Concurrency stress tests

If the change touches code holding a lock, semaphore, latch, or shared
mutable state → require a test that exercises the change under
concurrent load (parameterized parallel-degree). Use the SDK pack's
existing concurrency-test utilities (cite path).

### 2.5 Determinism

Flag any added test that:
- uses `Thread.Sleep` / `time.sleep` / `delay()` for assertion synchronization
- depends on wall-clock comparisons without injected clock
- has a flake-prone pattern (e.g., "timing-sensitive — may flake on CI")

Suggest the SDK's existing virtual clock / scheduler / `TaskCompletionSource`-style alternative.

### 2.6 Regression scope

Every fixed bug PR (look for "fix" / "bug" / linked issue) must include
at least one test that **fails before the fix and passes after**. Cite
the test name. If absent → finding.

### 2.7 Benchmark requirement (cross-cutting with Dimension 3)

If the PR touches any path labeled `risk_paths.perf_sensitive` (per the
SDK pack), require either:
- a benchmark in the `Test entry points → benchmark dirs` of the SDK pack, OR
- benchmark numbers in the PR description (before / after / delta)

If neither is present → finding category `testing`, severity `major`.

### 2.8 Cosmos-domain conceptual brainstorm (REQUIRED on every PR)

> This sub-dimension is **mandatory on every PR review at every tier and
> for every SDK** — see `memory/_global.json#ac-4`. The mechanical grep
> checks in §2.1–§2.7 verify what tests *exist*; this section forces a
> first-principles brainstorm of what tests *should* exist given the
> subsystem being touched.

For every PR, after the §2.1–§2.7 grep-driven checks, the reviewer
produces a dedicated review section titled:

> **Test coverage analysis (existing + recommended)**

with two parts:

**Part A — Practical (what exists, what gaps remain for this diff)**

- Inventory existing tests touching the changed lines (file:test_name).
- Classify what each test exercises: happy path / failure mode /
  concurrency / boundary / regression-pin.
- Identify which lines added or modified by *this PR* are still
  uncovered, citing line numbers.

**Part B — Conceptual (what should exist, derived from Cosmos domain
knowledge)**

Pick the chapters in `~/Data/microsoft/sandbox/cosmosdb-design-docs/`
that map to the subsystem being touched, grep them, and use them to
generate test ideas. Minimum chapter map:

| Subsystem touched | Design-doc chapters to consult |
|---|---|
| Routing / partition key resolution | `04-partitioning.md`, `02-gateway-layer.md` |
| Replication / consistency / quorum | `05-replication-consistency.md` |
| Multi-region / failover / PPAF / hub region | `09-global-distribution.md`, `05-replication-consistency.md`, `17-status-codes-and-sdk-retries.md` |
| Session tokens / read consistency | `16-session-tokens.md`, `05-replication-consistency.md` |
| Change feed / lease management | `12-change-feed.md` |
| Query / pagination / cross-partition | `07-query-engine.md`, `04-partitioning.md` |
| Throttling / 429 / retries | `17-status-codes-and-sdk-retries.md` |
| Diagnostics / hedging / availability strategy | `09-global-distribution.md`, `17-status-codes-and-sdk-retries.md`, `02-gateway-layer.md` |
| Serialization / wire format / binary JSON | `03-backend-storage-engine.md`, `06-transport-layer-rntbd.md`, `13-multi-api-surface.md` (+ propose a design-doc extension if the specific wire-format concept is missing) |
| Auth / token cache | `14-key-source-locations.md`, `02-gateway-layer.md` |
| Indexing / RU accounting | `08-indexing-engine.md`, `11-request-units-throttling.md` |

For the subsystem identified, walk the Cosmos-specific failure axes
that apply and emit a concrete recommended test for each one that the
diff plausibly affects:

| Failure axis | Concrete test idea template |
|---|---|
| Partition split mid-operation | `<feature>_handles_split_during_<operation>` — issue the operation, force a split via mock routing/PKRange refresh, assert no duplicate/dropped work |
| Partition merge mid-operation | `<feature>_handles_merge_during_<operation>` — same shape, merge instead of split |
| Replica failover mid-request | `<feature>_handles_replica_unavailable_midflight` — fail the primary mid-stream, assert retry/redirect |
| Region failover (PPAF / hub-region switch) | `<feature>_handles_write_region_change_midflight` |
| Session token staleness across region failover | `<feature>_handles_stale_session_token_after_failover` |
| Throttle (429) with `x-ms-retry-after-ms` | `<feature>_honors_retry_after_on_throttle` |
| Throttle storm (sustained 429) | `<feature>_gives_up_after_max_retries_on_sustained_throttle` |
| Server-side timeout (408) | `<feature>_distinguishes_server_408_from_client_cancellation` |
| Client cancellation during in-flight retry | `<feature>_cancellation_token_short_circuits_retry_loop` |
| Cross-partition vs single-partition divergence | `<feature>_single_partition_path_equals_cross_partition_aggregate` |
| Gateway-mode vs direct-mode parity | `<feature>_gateway_and_direct_modes_produce_equivalent_diagnostics` |
| Cold-start (empty cache) vs warm-cache | `<feature>_cold_start_does_not_skip_<guarantee>` |
| Multi-master conflict (409 / 412 amplification) | `<feature>_multi_master_conflict_returns_actionable_error` |
| Concurrent access on shared mutable state | `<feature>_concurrent_<N>_callers_observe_consistent_state` |
| Hostile / malformed wire input (serialization paths) | `<feature>_rejects_malformed_payload_without_unrecoverable_failure` |
| Recursion / nesting bounds (parsers, hashers) | `<feature>_bounded_at_documented_depth_on_adversarial_input` |
| Long-running stream cancellation | `<feature>_streaming_cancellation_releases_resources` |
| Diagnostics propagation across retries | `<feature>_diagnostics_capture_every_attempt_including_retried` |

The brainstorm output MUST include **at least 3 numbered conceptual
gaps**, each formatted as:

```
<n>. Gap: <one-line description of what is not tested>
    Why it matters (Cosmos domain): <one or two sentences citing the
        relevant chapter, e.g. cosmosdb-design-docs/09-global-distribution.md §3>
    Recommended test: <suggested test name + 1–2 sentences on shape>
```

If the brainstorm produces fewer than 3 gaps, the reviewer must
explicitly say so and justify why the subsystem genuinely has no
additional applicable failure axes — silent omission is not allowed.

**Severity policy for §2.8 findings**

- Gap in a hot path with a known historical incident class (e.g.
  region-failover correctness, session-token staleness, partition
  split mid-query) → severity `major`.
- Gap in a feature-flagged or preview path → severity `minor` unless
  the gap would mask a security/correctness defect on opt-in.
- Gap that is purely additive coverage hygiene → severity `nit`.

**Output integration**

The §2.8 brainstorm section goes into the final consolidated review
under its own heading. It is NOT optional and is NOT collapsed into
the per-file findings — it stands as its own block so the author can
copy-paste recommended tests into a follow-up commit.

---

## Dimension 3 — Performance

> Reviewer judgement applies — this dimension produces findings only
> when the reviewer can name a concrete cost (not "this might be slow").

### 3.1 Hot-path identification

Use the SDK pack's `risk_paths` block. A change is on a "hot path" if:
- it is on a per-request, per-document, per-bulk-item, per-batch-item, per-change-feed-event path, OR
- the SDK pack labels the path explicitly as `perf_sensitive`

If the change is **not** on a hot path → most §3.x findings should be
suggestions, not majors.

### 3.2 Allocation hotspots

Flag with concrete evidence (line citation):
- per-call allocations on the hot path that did not exist before this PR
- new boxing of value types
- new intermediate collections (e.g., `.ToList()` mid-pipeline) where streaming would do
- new string interpolation where a pooled builder is the SDK convention

Suggest the SDK's existing pool / span / rope abstraction (cite path from the SDK pack).

### 3.3 Lock contention

For any new lock / semaphore / mutex on a hot path:
- check granularity — does it cover more than necessary?
- check ordering — does it nest with existing locks? if yes, document
- check duration — is any I/O / await held under the lock?

### 3.4 Async patterns

- No `.Wait()` / `.Result` / `Task.Run` / `block_on` / `await asyncio.run` on async hot paths
- `ConfigureAwait(false)` (or per-SDK equivalent) in library code
- Cancellation tokens / contexts propagated through every new async call
- Backpressure for any new producer/consumer or reactive flow (`Flux`, `IAsyncEnumerable`, channels, etc.)

### 3.5 Payload efficiency

- No new full-document copies on the hot path
- No new redundant JSON serialization round-trips
- No new request-or-response payload bloat (e.g., always-on diagnostics
  fields that double the wire size)

### 3.6 Benchmark gating

(See §2.7.)

---

## Dimension 4 — Cosmos design alignment

**Owned by `cosmos-pr-reviewer`.** EQ Reviewer flags only obvious
contradictions with chapters injected by the router into
`ACTIVE_REVIEW_CONTEXT.design_doc_chapters`.

Every domain-level finding (correctness or design_alignment) MUST cite
`cosmosdb-design-docs/<chapter>.md#<anchor>`.

Behaviors:
- PR contradicts a documented chapter → blocker finding with citation
- PR introduces a concept absent from the docs → suggestion finding:
  "consider extending `<chapter>.md` to document <concept>"
  (per `copilot-instructions.md` rule)
- PR is consistent with the docs → cite the chapter in the approval
  comment, no finding

(Curated path → chapter map: `pr-review/design-docs-map.md`.)

---

## Dimension 5 — Agentic / AI readiness

> Public APIs are increasingly consumed by LLM-driven code (agents,
> tool-callers, MCP servers, copilots). The 5 dimensions below are the
> ones the user explicitly chose for review. Other agentic dimensions
> exist (cancellation-budget, deterministic-behavior, token-efficient
> errors, MCP-compatibility) — explicitly **not** in scope today.

### 5.1 streaming_async_for_llm

- Bulk-result APIs (query, change-feed, list, bulk import/export) MUST expose a streaming variant: `IAsyncEnumerable<T>`, async iterator, `Flux<T>`, `Stream<T>`, channel, etc.
- A new bulk-result API that only returns a buffered collection → finding category `agentic`, severity `major`
- A new bulk-result API that exposes streaming but also returns a buffered collection → fine, no finding
- Probe: grep for the new method's return type; if it's `List<T>` / `IList<T>` / `[]T` / `Vec<T>` and the underlying source is paged → flag

### 5.2 structured_io_contracts

- Errors carry typed payloads — sub-status codes, fault domains, retry hints — not free text
- Results carry typed metadata — request charge, region, retry count — not embedded in log strings
- New error types that wrap a single `string message` field → finding suggesting structured fields
- New result types that emit human-readable diagnostics through `ToString()` only → suggest a structured `Diagnostics` accessor that returns parseable values

### 5.3 idempotency_safe_retry

- Every new write API declares its idempotency contract in doc comments: `idempotent` | `idempotent-with-token` | `non-idempotent`
- If the contract is `idempotent-with-token`, the API accepts a client-supplied idempotency key (or returns one and accepts it on retry)
- If the contract is `non-idempotent`, the doc comment includes a warning to agentic consumers
- Grep: any new public write/POST/PUT/DELETE method without an idempotency note → finding suggesting the doc-comment addition

### 5.4 observable_for_llm_diagnostics

- New diagnostics fields are key=value or typed accessor, not free-text-only
- Existing structured diagnostics are not removed in favor of free text
- New telemetry tags for OpenTelemetry / `ActivitySource` / `Tracer` use stable, snake_case attribute names (per per-SDK convention)
- Avoid log lines like `"Retried 3 times due to throttle on partition pk=foo at region East US"` — instead emit `(retry_count=3, throttle_status=429, partition_key="foo", region="eastus")` as structured fields
- Probe: diff for new logger/`info`/`warn`/`error` calls; if message contains `${var}` interpolation that re-stringifies a structured field → finding

### 5.5 tool_callable_public_api

- New public methods are **single-purpose** — no mode flags that change return shape
- Parameter shape is JSON-schema-friendly: prefer typed records / option objects over positional args with optional / nullable params
- Side effects are documented in a structured tag block (`@side-effect: writes`, `@idempotent: yes/no`, `@cost-units: high|low`)
- New types exposed on the public surface should be JSON-serializable without custom converters where reasonable
- Goal: anyone wrapping the SDK as an MCP server / function-call tool can do so with a thin adapter — flag designs that would force a thick adapter

---

## Dimension 6 — Pre-flight verification gates (cross-cutting)

> The reviewer MUST execute the corresponding probe and include the
> command output in the finding's `evidence` field for any of these
> finding classes:

| Finding class | Required probe | Where to find paths |
|---|---|---|
| Missing test | `grep -rn "<symbol>" <test-dirs>` | SDK pack → `Test entry points` |
| Missing benchmark | `grep -rn "<symbol>" <benchmark-dirs>` | SDK pack → `Test entry points → benchmark dirs` |
| Missing changelog entry | `grep -i "<feature-keyword>" CHANGELOG.md changelog.md` | repo root |
| Missing public docs | `grep -rn "<concept>" docs/` + `grep -rn "<concept>" ~/Data/microsoft/sandbox/cosmosdb-design-docs/` | repo + design-docs |
| Diverges from peer SDK | actual `view` of peer SDK file at the cited line range | SDK pack → `Cross-SDK parity map` |
| Breaks SDK convention | `grep` of public surface for the asserted convention | SDK pack → `Idiomatic patterns` |
| Missing observability | `grep -rn "<existing logger/tracer call>" <changed-file-dir>` to confirm convention | SDK pack → `Observability surfaces` |

Findings without a verification command → downgraded to severity `nit`,
category unchanged, message prefixed with
`[unverified-pattern-suggestion]`.

---

## Severity guidance

| Severity | When to use |
|---|---|
| `blocker` | correctness bug, data loss, security, broken public contract, divergence from documented design behavior |
| `major` | missing test for new failure mode, missing benchmark on perf-sensitive path, allocation regression on hot path, new free-text-only error contract, new bulk-result API without streaming |
| `minor` | API hygiene (naming, doc comment), suggested clarifying refactor, suggestion to add structured field |
| `nit` | style, alternative naming, downgraded unverified-pattern-suggestion |

When in doubt, downgrade. The router's Stage 8 merge will deduplicate
overlapping findings from PR Deep Reviewer + EQ Reviewer.
