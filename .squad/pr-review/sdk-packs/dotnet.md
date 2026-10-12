# SDK Context Pack — .NET (azure-cosmos-dotnet-v3)

## Repo

- **Sandbox path:** `~/Data/microsoft/sandbox/azure-cosmos-dotnet-v3`
- **GitHub:** `Azure/azure-cosmos-dotnet-v3`
- **Default branch:** `master`
- **SDK area path inside repo:** `Microsoft.Azure.Cosmos/src/`
- **Test command:** `dotnet test Microsoft.Azure.Cosmos.sln`
- **Lint/format command:** `dotnet format`
- **Build command:** `dotnet build Microsoft.Azure.Cosmos.sln`

## Owners & reviewers

- CODEOWNERS file: `.github/CODEOWNERS`
- **Senior reviewers to learn from** (always pull their past comments on touched files via `gh search prs --repo Azure/azure-cosmos-dotnet-v3 --merged --reviewed-by <user>` before commenting):
  - **Kiran Kolli** — github.com/kirankumarkolli
  - **Fabian Meiswinkel** — github.com/FabianMeiswinkel
  - **Kushagra Thapar** — github.com/kushagraThapar — actively engaged on encryption / release-pipeline / CI-vs-shipped-ABI areas
- Frequent reviewers: _fill in from recent merged PRs_
- Domain experts (by area): _populate as you learn_

## Idiomatic patterns

- Async APIs use `Async` suffix and `CancellationToken` last
- Public API additions require API reference doc updates and emulator tests
- Diagnostics flow through `CosmosDiagnostics` — must be appended, not replaced

## Anti-patterns / footguns to call out

- New public surface without `EditorBrowsable` / preview attribute considerations
- Synchronous I/O on async paths
- Allocation-heavy hot paths (use `ArrayPool`/`Memory<T>` where applicable)
- Breaking changes to `CosmosClientOptions` defaults

## Cross-SDK parity map

When a PR touches one of these areas, also look at the listed peer SDK files/folders:

| Area / feature | Java | Python | Go | Rust | Spark | Kafka | Spring-Data |
|---|---|---|---|---|---|---|---|
| Session token handling | `sdk/cosmos/azure-cosmos/src/main/java/com/azure/cosmos/implementation/SessionContainer.java`; `ISessionToken.java`; `VectorSessionToken.java` | `sdk/cosmos/azure-cosmos/azure/cosmos/_session.py` | verify under `sdk/data/azcosmos/` before asserting parity | `sdk/cosmos/azure_data_cosmos_driver/src/models/vector_session_token.rs`; `sdk/cosmos/azure_data_cosmos_driver/src/models/session_token_segment.rs` | — | — | Java SDK inherited |
| Retry policy | `sdk/cosmos/azure-cosmos/src/main/java/com/azure/cosmos/implementation/ResourceThrottleRetryPolicy.java`; `ClientRetryPolicy.java`; `DocumentClientRetryPolicy.java`; `directconnectivity/GoneAndRetryWithRetryPolicy.java` | `sdk/cosmos/azure-cosmos/azure/cosmos/_retry_utility.py`; `_retry_options.py` if present | verify under `sdk/data/azcosmos/` before asserting parity | verify retry policy under `sdk/cosmos/azure_data_cosmos_driver/src/driver/` | connector retry paths | connector retry paths | Java SDK inherited |
| Diagnostics / tracing | `sdk/cosmos/azure-cosmos/src/main/java/com/azure/cosmos/CosmosDiagnostics.java`; `CosmosDiagnosticsRequestInfo.java`; `CosmosDiagnosticsRequestEvent.java`; `FeedResponseDiagnostics.java` | `sdk/cosmos/azure-cosmos/azure/cosmos/_cosmos_diagnostics.py` if present | `sdk/data/azcosmos/diagnostics.go` | `sdk/cosmos/azure_data_cosmos_driver/src/diagnostics/diagnostics_context.rs`; `sdk/cosmos/azure_data_cosmos_driver/src/driver/pipeline/hedging_diagnostics.rs` | connector-specific metrics | connector-specific metrics | Spring logging + Java diagnostics |
| Connection pool |  |  |  |  |  |  |  |
| Throughput control |  |  |  |  |  |  |  |
| Query execution | `sdk/cosmos/azure-cosmos/src/main/java/com/azure/cosmos/implementation/query/**`; `ChangeFeedQueryImpl.java` for feed ordering checks | `sdk/cosmos/azure-cosmos/azure/cosmos/_query_iterable.py`; query execution-context modules | verify under `sdk/data/azcosmos/query*` before asserting parity | verify under `sdk/cosmos/azure_data_cosmos/src/query*` before asserting parity | Spark query pushdown paths | — | Spring query translation |
| Encryption (CMK / always-encrypted) — release pipeline & shipped surface | `sdk/cosmos/azure-cosmos-encryption*` | _no published encryption package — verify before claiming parity_ | _verify_ | _verify_ | — | — | — |

## Risk paths (consumed by Router Stage 2)

```yaml
risk_paths:
  public_api:
    - Microsoft.Azure.Cosmos/src/Resource/**/*.cs
    - Microsoft.Azure.Cosmos/src/CosmosClient*.cs
    - Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs
  retry:
    - Microsoft.Azure.Cosmos/src/**/Retry*
  auth:
    - Microsoft.Azure.Cosmos/src/Authorization/**
    - Microsoft.Azure.Cosmos.Encryption*/**
  serialization:
    - Microsoft.Azure.Cosmos/src/Serializer/**
  query:
    - Microsoft.Azure.Cosmos/src/Query/**
  diagnostics:
    - Microsoft.Azure.Cosmos/src/Diagnostics/**
  release_pipeline:
    - Microsoft.Azure.Cosmos.Encryption*/**.csproj
    - Microsoft.Azure.Cosmos.Encryption*/**.yml
    - templates/build-test*.yml
    - templates/build-test-encryption*.yml
    - templates/build-preview*.yml
    - templates/static-tools-encryption*.yml
    - templates/encryption-*.yml
  change_feed_processor:
    # 5-year-latent regression chain: PR #105 → #158 → #2491 → #5807. Lease
    # management bugs in CFP have repeatedly slipped past review because the
    # pre-PR #2491 codepath used non-deterministic lease IDs. Java SDK's
    # LeaseStoreManagerImpl (lines 164-180) is the canonical lease-id-as-PK
    # reference implementation — compare against Java when reviewing changes here.
    - Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/LeaseManagement/**
    - Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/Bootstrapping/**
    - Microsoft.Azure.Cosmos/src/ChangeFeed/**
```

## Recurring review themes

- **AAD auth changes — cross-credential compatibility matrix:** any PR touching `Microsoft.Azure.Cosmos/src/Authorization/` (especially `TokenCredentialCache.cs` and `AuthorizationTokenProviderTokenCredential.cs`) must be tested against the full `DefaultAzureCredential` chain, not just a mock stub.
  - **The `claims` vs `IsCaeEnabled` distinction is a footgun.** `AzureCliCredential` maps any non-empty `TokenRequestContext.Claims` directly to `az account get-access-token --claims-challenge '...'` — an interactive MFA prompt that throws `CredentialUnavailableException`. `IsCaeEnabled = true` is the correct, credential-agnostic way to advertise CAE/cp1 capability.
  - **Rule:** `claims` must only be set on `TokenRequestContext` when there is an *active* CAE revocation challenge (i.e., `cachedClaimsChallenge` is non-null). Normal-path token requests MUST have `claims == null` and `isCaeEnabled: true`.
  - **Required tests:** (a) normal (no-revocation) path sends `null` claims — assert in a `CredentialUnavailableException`-on-claims mock to catch regressions deterministically; (b) revocation path sends correct merged `acrs`+`cp1` claims; (c) post-revocation 3rd acquisition returns to null-claims normal path (validates `cachedClaimsChallenge = null` clear at line 337 is preserved); (d) `DefaultAzureCredential` / `AzureCliCredential` e2e: if emulator tests skip these, add a unit test with a credential stub that throws on non-empty claims.
  - **Volatile double-read pattern:** `cachedClaimsChallenge` is `volatile string?`. Any code that reads it twice — even within a few lines (e.g., once to compute a merged value, once for a branch condition) — is a TOCTOU race against `ResetCachedToken` firing from a request-processing thread. Always snapshot: `string? challenge = this.cachedClaimsChallenge;` then use `challenge` exclusively. See PR #6008 commit `f0027255b` for the introduced regression and PR #5549 for the original miss.
  - **Root cause of the original miss (PR #5549):** test mock accepted any `claims` value silently; emulator CI uses cert/connection-string credentials that tolerate non-empty claims; `AzureCliCredential` is a dev-machine-only path never exercised in CI; reviewer focus was on "does CAE revocation work?" not "does the normal path still work for all credential types?".

- **CI vs shipped artifact parity (encryption / security packages):** the dev CI may build a project-ref form of a security package while the published NuGet links against a different preprocessor surface. Always ask: "does the CI build configuration match the shipped artifact's preprocessor symbols and feature set?"
- **Env-var-gated behavior changes must update `changelog.md`** with (a) the env-var
  name, (b) default value, (c) what changes when enabled/disabled, and (d) rollback
  instructions. Established pattern from `AZURE_COSMOS_AGGRESSIVE_TIMEOUT_DETECTION_ENABLED`,
  `AZURE_COSMOS_OPTIMISTIC_DIRECT_EXECUTION_ENABLED`,
  `AZURE_COSMOS_CHANGE_FEED_LEASE_ID_AS_PARTITION_KEY_ENABLED` (PR #5807). Missing
  changelog for env-var changes is a recurring miss — flag it.
- **`[DoNotParallelize]` test attribute scope:** prefer method-level over class-level.
  Class-level forces *all* tests in the file to run serially, hurting CI throughput
  even when only one test has the parallelism dependency. Flag class-level usage and
  ask author to narrow scope to the specific affected tests.
- **Customer remediation guidance for behavior-change PRs:** when a fix changes
  observable behavior (e.g., new partition-key strategy in CFP), the PR description
  / changelog must include guidance for customers on pre-existing affected state
  (e.g., "if you have duplicate leases from the old codepath, run X to clean up").
  Don't ship a fix that leaves customers silently broken.
- **Static-field state in classes used by long-lived clients:** static fields are a
  recurring footgun in test cleanup and memory profiles. Ask: "can this be instance
  state instead?" before approving new statics in `ChangeFeedProcessor/`,
  `ConsistencyLevels/`, or anything held by `CosmosClient` for the client's lifetime.
- _populate via feedback loop_

## Perf-sensitive paths

> Consumed by **Engineering Quality Reviewer** Dimension 3 + Dimension 2.7. Touching any of these → bench numbers required.

```yaml
perf_sensitive_paths:
  request_path:
    - Microsoft.Azure.Cosmos/src/Handler/**
    - Microsoft.Azure.Cosmos/src/ClientContextCore.cs
    - Microsoft.Azure.Cosmos/src/RequestInvokerHandler.cs
    - Microsoft.Azure.Cosmos/src/Routing/**
  bulk_path:
    - Microsoft.Azure.Cosmos/src/Batch/**
    - Microsoft.Azure.Cosmos/src/Bulk*.cs
  change_feed_path:
    # Reuse risk_paths.change_feed_processor — same hot loop, different review lens
    - Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/LeaseManagement/**
    - Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/Bootstrapping/**
    - Microsoft.Azure.Cosmos/src/ChangeFeed/**
  serialization_path:
    - Microsoft.Azure.Cosmos/src/Serializer/**
    - Microsoft.Azure.Cosmos/src/CosmosElements/**
    - Microsoft.Azure.Cosmos/src/Json/**
  routing_cache_path:
    - Microsoft.Azure.Cosmos/src/Routing/PartitionKeyRangeCache.cs
    - Microsoft.Azure.Cosmos/src/Routing/CollectionCache.cs
    - Microsoft.Azure.Cosmos/src/Routing/AddressResolver.cs
    - Microsoft.Azure.Cosmos/src/Routing/LocationCache.cs
    - Microsoft.Azure.Cosmos/src/Routing/GlobalEndpointManager.cs
```

## Test entry points

> Consumed by **Engineering Quality Reviewer** Dimension 2 and the pre-flight verification gates.

```yaml
test_entry_points:
  unit:
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/**
  integration:
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/**
  emulator:
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/**
  e2e:
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/Query/**
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EncryptionTests/**
  benchmark:
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/**
    - Microsoft.Azure.Cosmos.Samples/Tools/Benchmark/**
  concurrency_stress:
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/**Stress*
    - Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/**Concurrent*
test_naming_convention: '*Tests.cs (xUnit/MSTest), classes [TestClass]; method-scoped [DoNotParallelize] preferred over class-scoped'
test_run_subset_command: 'dotnet test Microsoft.Azure.Cosmos.sln --filter "FullyQualifiedName~<symbol>"'
```

## Observability surfaces

```yaml
observability:
  diagnostics_class:
    - Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnostics.cs
    - Microsoft.Azure.Cosmos/src/Diagnostics/CosmosDiagnosticsContext.cs
  tracer_or_logger:
    - Microsoft.Azure.Cosmos/src/Diagnostics/OpenTelemetry/**
    - System.Diagnostics.Activity / DiagnosticSource (no first-party logger; consumers wire through Activity)
  structured_field_convention:
    - OTel-semconv attribute names (snake_case dot-segmented), e.g. db.cosmosdb.request_charge, db.cosmosdb.activity_id
    - Append-only on CosmosDiagnostics — never replace existing fields
  retry_diagnostics_pattern:
    - ClientSideRequestStatistics records retry attempts in CosmosDiagnostics; preserve append-on-each-retry semantics
    - Never replace the diagnostics object across a retry boundary
  request_charge_diagnostics:
    - ResponseMessage.Headers.RequestCharge surfaces RU on every response
    - CosmosDiagnostics aggregates total RU across retries and sub-requests
```

## Agentic / AI consumer surfaces

```yaml
agentic_consumer_surfaces:
  client_entry_points:
    - Microsoft.Azure.Cosmos/src/CosmosClient.cs
    - Microsoft.Azure.Cosmos/src/CosmosClientBuilder.cs
    - Microsoft.Azure.Cosmos/src/Fluent/**
  bulk_or_batch_apis:
    - Microsoft.Azure.Cosmos/src/Batch/TransactionalBatch.cs
    - Microsoft.Azure.Cosmos/src/Batch/TransactionalBatchResponse.cs
    - Microsoft.Azure.Cosmos/src/CosmosClientOptions.cs (AllowBulkExecution)
  streaming_apis:
    - Microsoft.Azure.Cosmos/src/Resource/Container/Container.GetItemQueryStreamIterator.cs
    - Microsoft.Azure.Cosmos/src/ChangeFeedProcessor/ChangeFeedProcessorBuilder.cs
    - FeedIterator<T> / FeedIterator (stream + typed)
  result_types_with_diagnostics:
    - Microsoft.Azure.Cosmos/src/Resource/ItemResponse.cs
    - Microsoft.Azure.Cosmos/src/Resource/FeedResponse.cs
    - Microsoft.Azure.Cosmos/src/ResponseMessage.cs
  error_types:
    - Microsoft.Azure.Cosmos/src/CosmosException.cs
    # MUST expose StatusCode, SubStatusCode, ActivityId, RequestCharge, RetryAfter as typed properties — not buried in Message
  idempotency_relevant_writes:
    - CreateItemAsync / CreateItemStreamAsync — non-idempotent (409 on retry); flag any new variant
    - UpsertItemAsync — idempotent
    - ReplaceItemAsync — idempotent (with ETag)
    - PatchItemAsync — idempotent (with ETag)
    - DeleteItemAsync — idempotent (404 on retry is benign)
    - any new write API MUST declare its idempotency contract in xmldoc
```

## Related repos to also search

- `azure-cosmos-client-engine` (shared Rust core)
- `azure-cosmos-rust-nosql-api-samples`
