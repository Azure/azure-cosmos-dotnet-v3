# SDK Context Pack — <SDK NAME>

> Loaded automatically by the PR Review Router for any PR in this SDK.
> Keep entries short. Promote items here from `memory/<sdk>.json` once they
> are stable enough to be permanent guidance.

## Repo

- **Sandbox path:** `~/Data/microsoft/sandbox/<repo>`
- **GitHub:** `<owner>/<repo>`
- **Default branch:** `main` | `master` | `<other>`
- **SDK area path inside repo:** `<path/to/sdk/source>`
- **Test command:** `<how to run unit tests>`
- **Lint/format command:** `<how to lint>`
- **Build command:** `<how to build locally>`

## Owners & reviewers

- CODEOWNERS file: `<path or n/a>`
- Frequent reviewers: `<aliases>`
- Domain experts (by area):
  - `<area>` → `<alias>`

## Idiomatic patterns

- `<bullet list of how this SDK does things — naming, error model, async style, logging, etc.>`

## Anti-patterns / footguns to call out

- `<recurring mistakes specific to this SDK>`

## Risk paths (used by router tier scoring)

The PR Review Router scores PR risk partly from which paths are touched. Fill in
glob/regex fragments matched against changed-file paths in this SDK.

```yaml
risk_paths:
  public_api:    # public surface — score +4 when touched
    - <pattern>
  retry:         # retry / resilience policy — score +3
    - <pattern>
  auth:          # auth / token / credentials — score +3
    - <pattern>
  serialization: # JSON/binary encoders, model attributes — score +3
    - <pattern>
  query:         # query engine, query plan, partition routing — score +3
    - <pattern>
  diagnostics:   # tracing, metrics, telemetry, logging — score +3
    - <pattern>
```

If this section is missing, the router falls back to filename heuristics and notes the fallback in the score rationale.

## Cross-SDK parity map

When a PR touches one of these areas, also look at the listed peer SDK files/folders:

| Area / feature | Java | .NET | Python | Go | Rust | Spark | Kafka | Spring-Data |
|---|---|---|---|---|---|---|---|---|
| Session token handling |  |  |  |  |  |  |  |  |
| Retry policy |  |  |  |  |  |  |  |  |
| Diagnostics / tracing |  |  |  |  |  |  |  |  |
| Connection pool |  |  |  |  |  |  |  |  |
| Throughput control |  |  |  |  |  |  |  |  |
| Query execution |  |  |  |  |  |  |  |  |

Fill cells with relative paths inside the corresponding sandbox repo.

## Recurring review themes

- `<things you find yourself saying repeatedly on this SDK's PRs>`

## Perf-sensitive paths

> Consumed by **Engineering Quality Reviewer** Dimension 3 (perf) and
> Dimension 2.7 (benchmark requirement). When a PR touches any of
> these paths, the reviewer raises the bar on allocation patterns and
> requires either an in-repo benchmark or before/after numbers in the PR.

```yaml
perf_sensitive_paths:
  request_path:           # per-request hot path — every CRUD op flows through here
    - <pattern>
  bulk_path:              # bulk import / bulk executor — per-document amplification
    - <pattern>
  change_feed_path:       # per-event delivery loop
    - <pattern>
  serialization_path:     # JSON / RNTBD encoding on every payload
    - <pattern>
  routing_cache_path:     # PK range cache, address cache, location cache hot lookups
    - <pattern>
```

## Test entry points

> Consumed by **Engineering Quality Reviewer** Dimension 2 (testing) and
> the pre-flight verification gates. When the reviewer wants to check
> "does a test exist for X?", grep these directories first — only file
> a "missing test" finding if grep returns no hits.

```yaml
test_entry_points:
  unit:
    - <path/to/unit/test/dir>
  integration:
    - <path/to/integration/test/dir>
  emulator:
    - <path/to/emulator-bound/tests>
  e2e:
    - <path/to/end-to-end/tests>
  benchmark:
    - <path/to/benchmarks>
  concurrency_stress:
    - <path/to/concurrency/stress/tests>
test_naming_convention: <e.g., "Test*.cs", "test_*.py", "*Test.java", "*_test.go">
test_run_subset_command: <e.g., `dotnet test --filter "FullyQualifiedName~<symbol>"`>
```

## Observability surfaces

> Consumed by **Engineering Quality Reviewer** Dimension 1 (code
> quality, observability sub-rubric) and Dimension 5.4
> (observable_for_llm_diagnostics). When the reviewer wants to check
> "is the new code path emitting diagnostics consistent with the rest
> of the SDK?", look here for the canonical patterns.

```yaml
observability:
  diagnostics_class:
    - <e.g., Microsoft.Azure.Cosmos.CosmosDiagnostics — central diagnostics aggregator>
  tracer_or_logger:
    - <e.g., Microsoft.Azure.Cosmos.Diagnostics.OpenTelemetry — the Activity/Tracer surface>
  structured_field_convention:
    - <e.g., snake_case attribute names, OTel-semconv-compatible>
  retry_diagnostics_pattern:
    - <where retries are recorded, naming convention>
  request_charge_diagnostics:
    - <where request charge / RU diagnostics are emitted>
```

## Agentic / AI consumer surfaces

> Consumed by **Engineering Quality Reviewer** Dimension 5
> (agentic / AI readiness). The classes / methods listed here are the
> ones most likely to be wrapped by an MCP server, function-call
> adapter, or LLM-driven harness — apply Dimension 5 rubric strictly
> when the PR adds or modifies these.

```yaml
agentic_consumer_surfaces:
  client_entry_points:
    - <e.g., CosmosClient, CosmosClientBuilder>
  bulk_or_batch_apis:
    - <e.g., TransactionalBatch, BulkExecutor>
  streaming_apis:
    - <e.g., GetItemQueryStreamIterator, ChangeFeedProcessor.Build>
  result_types_with_diagnostics:
    - <e.g., ItemResponse<T>, FeedResponse<T> — should expose typed diagnostics>
  error_types:
    - <e.g., CosmosException — should expose sub-status code, retry-after, request-charge as typed properties>
  idempotency_relevant_writes:
    - <e.g., CreateItemAsync, ReplaceItemAsync, UpsertItemAsync, PatchItemAsync — flag any new write API without an idempotency note>
```

## Related repos to also search

- `<other repos that depend on or extend this SDK — e.g. shared client engine, samples, tests>`
