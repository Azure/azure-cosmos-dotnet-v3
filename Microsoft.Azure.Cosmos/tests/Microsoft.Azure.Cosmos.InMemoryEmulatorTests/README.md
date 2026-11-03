# In-memory emulator tests

These tests run the **SDK built from this checkout** against the Rust Cosmos SDK's
`azure_data_cosmos_emulator` executable over real loopback HTTP. This is the same
memory-backed host used by [Go v2's native-driver tests](https://github.com/Azure/azure-sdk-for-go/tree/feature/azcosmos/v2/sdk/data/azcosmos/internal/testdata).
It is not a mocked HTTP handler, the Windows emulator, or the Linux emulator container.
No Azure account, Docker daemon, TLS certificate installation, or service credentials
are required.

## Build the emulator once

Prerequisites: Git, rustup, a C compiler/linker, and network access to GitHub,
Rust toolchain downloads, and Cargo dependencies. Use macOS/arm64 or Linux/amd64,
matching Go v2's tested platforms. The .NET project uses the repository's .NET 6
target and MSTest versions; install a current .NET SDK and the .NET 6 runtime.

Build in a separate directory, not inside this SDK checkout:

```sh
rustup toolchain install 1.95.0 --profile minimal
git init cosmos-emulator-source
git -C cosmos-emulator-source fetch --depth=1 \
  https://github.com/Azure/azure-sdk-for-rust.git \
  13b9707cf9a73d274c6e2309321d4b0e92b52375
git -C cosmos-emulator-source checkout --detach FETCH_HEAD
cd cosmos-emulator-source
cargo +1.95.0 build --locked --release -p azure_data_cosmos_emulator \
  --target-dir ../emulator-target
```

The source revision, Rust toolchain, and locked dependencies match the Go v2
reference. The crate is not published to crates.io, and no executable is vendored
here. Update the pins here and in the CI pipeline together, then rerun the .NET
suite; a Rust source build alone is not a compatibility check.

## Run from the .NET repository root

```sh
export COSMOS_IN_MEMORY_EMULATOR_PATH=/absolute/path/emulator-target/release/azure_data_cosmos_emulator
dotnet test Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests \
  --configuration Release --logger "console;verbosity=normal"
```

PowerShell:

```powershell
$env:COSMOS_IN_MEMORY_EMULATOR_PATH = "/absolute/path/emulator-target/release/azure_data_cosmos_emulator"
dotnet test Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests --configuration Release
```

The SDK can also be used with a later installed runtime for local development by
prefixing the command with `DOTNET_ROLL_FORWARD=Major` (PowerShell:
`$env:DOTNET_ROLL_FORWARD = "Major"`). CI installs the .NET 6 runtime explicitly.

Only the process-backed tests have `TestCategory=InMemoryEmulator`; use
`--filter TestCategory=InMemoryEmulator` to select them. Lifecycle/parser unit tests
run without an emulator. When the executable variable is **unset**, process-backed
tests are reported as skipped, including in an ordinary solution test run. When it
is **set but invalid**, or the host fails startup/health/capability checks, the suite
fails instead of silently skipping or falling back to another service.

## Lifecycle and isolation

The class fixture starts its own process with the copied `emulator-config.json`.
Every listener uses port `0`; the assigned account and management endpoints come
from the host's JSON `ready` record, never a fixed port. Waiting for readiness is
bounded to 30 seconds, each health/capability request to 10 seconds, and cleanup kills and waits for
the owned process (including when startup fails). Startup errors include stderr.
Tests create unique item/partition IDs, delete their items, and dispose clients.
All memory-backed state disappears when the process exits.

The fixture requires capabilities API v1 and Gateway V1. Only loopback HTTP root
endpoints are accepted. Proxy use is disabled for these test clients; no TLS
validation bypass is installed. A dummy signing key satisfies the SDK's local key
validation; the emulator does **not** enforce authentication. Never use real keys
or expose the host beyond loopback.

The configuration pre-provisions `itemdb/items` with `/pk`, Hash V2, four physical
partitions, one region, session consistency, and no throttling. The tests cover
typed create/read/replace/upsert/delete, 404/409/412 error mapping, ETag and
request-charge response metadata, same-ID partition isolation, patch, paginated
partition-scoped read feed, process isolation, cleanup, and invalid startup config.
The read-feed test deliberately sends no SQL query.

## Boundaries

This is an SDK engineering tool, **not a supported Cosmos DB service emulator**.
Its volatile store and simplified query/RU/replication models do not establish
production compatibility, durability, security, or performance.

This initial .NET suite exercises **Gateway V1 only**. It does not cover Direct
RNTBD/TCP, Gateway V2, TLS, authentication/RBAC, LINQ or SQL query-plan acquisition,
transactional-batch service parity, bulk, encryption, change-feed processors,
regional outages/write-region failover, or server-side scripting. The additional
controlled scenarios below verify narrowly scoped split/merge and replica-lag
behavior, not those broader features. The Rust host advertises more
operations than this suite verifies; those capabilities are not .NET compatibility
claims. In particular, Go documents emulator/live differences for conditional
batch reads and content-response suppression.

Continue using `Microsoft.Azure.Cosmos.EmulatorTests` and live-account suites for
those features and service-parity validation. Their default backend and existing
test selections are unchanged. This separation follows the canonical
[high-level architecture's Gateway/Direct distinction](https://github.com/tvaron3/cosmosdb-design-docs/blob/main/01-high-level-architecture.md#1-high-level-architecture).

## Reusing existing emulator scenarios

The shared CI template also runs **42 existing, unchanged test methods** from
`CosmosItemTests`, `CosmosDatabaseTests`, and `CosmosContainerTests` in
`Microsoft.Azure.Cosmos.EmulatorTests`. Assertions are not copied into another
suite or relaxed for this backend. `ci-scenarios.json` is the explicit,
runtime-validated allowlist, not the broader source inventory.

| CI job | Existing shared methods / cases | Original host-contract methods / cases | Controlled methods / cases | New numeric-wire methods / cases | New resource-input methods / cases | Total cases |
|---|---:|---:|---:|---:|---:|---:|
| Items | 35 / 35 | 11 / 14 | 1 / 3 | 2 / 15 | 2 / 2 | 69 |
| Reads | 7 / 7 | 0 / 0 | 2 / 4 | 0 / 0 | 0 / 0 | 11 |

Items covers item patch/serialization, missing-item delete/patch errors, IDs,
multiple Gateway clients and container recreation, database/container CRUD,
metadata contracts, and create-if-not-exists races. Reads covers missing-item
reads, database read/feed/query, and container stream queries. The original 14
host-contract cases, including partition-scoped item read feed and lifecycle and
startup failures, run exactly once in Items. These groups are compatibility
boundaries, not a claim of balanced test time or increased hosted-agent capacity.

## Deterministic topology and replica controls

These are **three new test methods / seven leaf cases**, not seven distinct service
capabilities. The existing 42 shared methods and original 14 host-contract cases
are retained.

| New method | Variants | Meaningful assertions |
|---|---|---|
| `PartitionTopologyTests.ManualSplitMergePreservesPartitionScopedContinuation` | Midpoint, explicit EPK, storage split | Real manual Preparing/Swapping/Succeeded transitions; topology 1 -> 2 -> 1; parent/children retirement; exact, duplicate-free logical-PK feed continuations on original/fresh clients across split/merge; all 38 item values/ETags preserved and active child/merged ranges represented in response session tokens. |
| `ReplicationRoutingTests.PausedReplicaHonorsEventualReadRouting` | Preferred East, preferred West, application region West | Pause actual West replication, write in East, observe actual SDK item endpoints: East reads the item, stale West returns 404 under Eventual reads. |
| `ReplicationRoutingTests.ExplicitSessionTokenRecoversFromReplicaLagAndResume` | One supplied-token workflow | West rejects the write token with 404/1002; the SDK retries West -> East and reads the correct value; resume drains the backlog; subsequent session reads succeed in West without another regional fallback. |

Every case starts a fresh owned host using `emulator-complex-config.json`: two
virtual regions, one initial Hash V2 partition, Session consistency, no throttling,
and zero configured replication delay. Management split/merge phase advancement
and replication pause/resume create deterministic state, not synthetic HTTP
errors. Observing handlers forward requests and responses unchanged. Only
condition-based bounded polling waits for operation completion or replica catch-up;
no fixed sleep is used to guess that a fault happened.

The topology observation uses the host's supported name-addressed partition-range
feed. **It is not `.NET GetFeedRangesAsync` or proof of SDK physical-range cache
recovery.** The latter currently fails because the SDK sends RID-addressed
`/dbs/<RID>/colls/<RID>/pkranges` requests and this host resolves only names.
Logical-PK operations can still succeed while those metadata requests fail.
Automatic session-cache recovery without a supplied token and managed item SQL
are also blocked; no request/response rewrite, fake topology, weaker assertion, or
skip is used to certify them.

The reference is the freshly inspected **current**
[Go v2 branch at 1812c74](https://github.com/Azure/azure-sdk-for-go/tree/1812c74daac993850521063937b764c621a0a52a/sdk/data/azcosmos).
Its split/merge/manual-phase specifications are **catalog-only**, not executable
Go tests. Its real
[read-routing test](https://github.com/Azure/azure-sdk-for-go/blob/1812c74daac993850521063937b764c621a0a52a/sdk/data/azcosmos/emulator_test.go#L1005-L1050)
uses replication pause but only resumes in cleanup; its
[session-retry test](https://github.com/Azure/azure-sdk-for-go/blob/1812c74daac993850521063937b764c621a0a52a/sdk/data/azcosmos/retry_native_test.go#L118-L140)
uses native injected faults. The new .NET cases use real controlled replica lag
and assert post-resume convergence, while deliberately leaving full range/query
topology parity unclaimed.

The full source-level Go/.NET mapping separates existing .NET coverage, new
controlled coverage, Go/FFI-specific cases, and blocked contracts. The Go source
inventory's 393 test declarations, 755 table-input occurrences, and 530 source
labels are non-additive and are not runtime pass counts. These seven cases alone
do **not** establish complete Go v2 parity.

## Exact numeric wire fidelity

One additional new .NET method,
`BinaryNumericWireTests.PointRoundTripPreservesNumberAndRequestedWireFormat`,
has **14 cases**: seven values (`9007199254740993`, signed Int64 limits, `0.1`,
`0.125`, `-1234.5678`, `1e-6`) crossed with text/binary controls. It uses the
unmodified managed SDK for create/read and asserts the actual response marker
(`0x80` for Cosmos binary, `{` for text). The decoded number is compared as an
exact rational using integer arithmetic, avoiding an intermediate `double`
conversion that could hide lost integer precision.

All 14 cases executed successfully against the pinned real host before selection.
This fills the exact Go point-wire numeric matrix rather than relying on existing
codec unit tests or ordinary binary CRUD. The original 42 shared assertions remain
unchanged. A separate server-patch case verifies the exact persisted fractional
increment `1.5 -> 2.5`, without copying Go's client-side increment strategies.
Two further shared methods assert the existing empty-ID database/container
creation validation, including the managed `ArgumentNullException` parameter name.
These close portable Go input-validation scenarios with regression tests rather
than treating ad hoc API probes as coverage.
CI now executes **80 cases** in two disjoint jobs: original 14, reused
42, controlled 7, numeric-wire 14, fractional increment 1, and resource-input 2. This point-item binary support does not
imply .NET's separate binary transactional-batch framing is supported.

## Shared fixture selection and CI gates

`COSMOS_EMULATOR_BACKEND=InMemory` explicitly selects the shared backend.
`EmulatorTestEnvironment` starts a fresh owned host before test fixtures
create any default clients. The common client factories route setup, teardown,
the static default client, option/builder overloads, and extra Gateway clients to
that endpoint with a dummy key. Explicit Direct, AAD, multi-region, resource-token,
and endpoint-override requests are rejected rather than sent to a different
service. The backend disables the process's default HTTP proxy and restores it
after disposing tracked clients and the host; metadata-validation handlers also
disable proxy use.

Each job has its own process and volatile state on dynamically assigned loopback
ports. The existing delete-all-databases fixtures therefore cannot affect another
job. Run these fixtures serially within a job; do not enable assembly/class
parallelism while they perform destructive setup. Leaving the backend variable
unset (or setting it to `Emulator`) preserves the ordinary full-emulator setup.
Setting only the executable variable does not switch existing emulator tests.
Unknown backends and missing/invalid executables fail closed.

Use the same gated runner locally on Linux or macOS:

```sh
export COSMOS_IN_MEMORY_EMULATOR_PATH=/absolute/path/emulator-target/release/azure_data_cosmos_emulator
python3 -B Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests/run_ci.py \
  --group Items --results-directory /tmp/cosmos-inmemory-items
python3 -B Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests/run_ci.py \
  --group Reads --results-directory /tmp/cosmos-inmemory-reads
```

The runner sets the shared backend only for its child processes. Each job discovers
the entire approved shared-method set and the complete in-memory project through
VSTest before executing its disjoint assigned subsets. The original 14-case host
contract is separately pinned and cannot be removed or moved out of Items.
Gates require the manifest groups to be nonempty and disjoint, exact discovery
membership, exact executed-case counts/identities, successful run completion, and
zero skips or failures. VSTest's fully qualified discovery enumerates methods;
the checked-in counts and leaf TRX results additionally account for the endpoint,
split-mode, and regional-routing data rows. All 42 shared methods currently have one case.
Discovery, assignments, leaf results, host executable hash/source pin, and timing
evidence are published with each job's diagnostics.

The runner owns a separate POSIX process group for each command and terminates
remaining children on exit, failure, or cancellation. This also cleans up hosts
when a crashing testhost cannot execute assembly cleanup. Directly invoking
`dotnet test` does not provide that outer crash-cleanup boundary.

### Candidates not enabled

These are observed incompatibilities with the pinned host, not ignored tests in
the approved jobs. All remain covered by their existing full-emulator selections.

| Candidate | Observed boundary |
|---|---|
| `ItemSinglePartitionQueryGateway` | The host's account response supplies `queryEngineConfiguration` as `"{}"`. The .NET item-query pipeline rejects the empty configuration before executing SQL; its partially initialized provider's finalizer also crashes the testhost. Other item-SQL/ORDER BY/query-metrics inventory candidates are not certified by the aborted broad trial. |
| `ItemIterator`, `PerfItemIterator`, both `ItemStreamIterator` rows | Unscoped item read-feed trials fail with stale RID/range-cache errors or a missing response stream. This does not invalidate the passing, explicitly partition-scoped host-contract feed case. |
| `PatchConditionTest`, `BatchPatchConditionTest` | Conditional patch returns 200 instead of 412; conditional batch patch returns 400 instead of 412. |
| `ItemPatchFailureTest` | The exact existing patch error-message assertion fails. |
| Both `GivenReplaceItem*WhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync` methods | Existing 404 assertions receive 400 for these replacement payloads. |
| `CosmosContainerTests.IteratorTest` | Its exact SQL-filtered metadata query returns zero results instead of one; the separate database and container stream-query methods in Reads pass unchanged. |
| `NegativePartitionedCreateDelete` | The host accepts a partition-key definition the existing test expects to reject with 400. |
| Full FeedRange/split-routing recovery and automatic session capture | RID-addressed partition metadata is not resolved by this host. `GetFeedRangesAsync` fails before splitting; a no-explicit-token replica-lag probe sends no usable cached session token and returns 404/0 rather than recovering. |
| .NET transactional batch, including complete hierarchical keys | The host's Gateway V1 batch handler expects a JSON operation array. The unmodified .NET binary batch request fails with 400, `Invalid batch JSON body`; no framing rewrite is installed. |

The remaining applicable Go HPK transaction variant is implemented in
[the normal-emulator HPK isolation test](../Microsoft.Azure.Cosmos.EmulatorTests/HierarchicalBatchIsolationTests.cs),
not added to the in-memory allowlist. Its two depth rows cover two-/three-level
null/undefined/numeric keys (Boolean third component), atomic create/read,
and same-ID isolation. Go's client-side incomplete-prefix rejection is not a
portable .NET requirement: actual Windows execution accepted those prefix
transactions. The .NET behavior is preserved and the difference is recorded,
rather than adding a new SDK guard or disguising it as equivalent coverage.
The test uses the existing Windows Batch lane and requires no new pipeline
definition or fixture infrastructure. This .NET test-coverage addition is separate
from the host's unresolved binary-batch portability.

Binary response variants, quota/exact metrics, broader SQL/LINQ, HPK/range
semantics, encryption fixtures, change-feed history/processors, auth/TLS/Direct,
and live multi-region scenarios remain unverified or full-emulator/live-only.
Advertising a host operation or Gateway V1 does not certify these assertions.
In particular, preserve the canonical
[partition-key/EPK/range distinction](https://github.com/tvaron3/cosmosdb-design-docs/blob/main/04-partitioning.md#sdk-side-terminology)
when investigating the unscoped-feed failures. Fix host semantics and rerun the
exact existing scenario before adding it to the manifest; do not add skips,
response rewriting, or weaker assertions to make a candidate green.

## Azure Pipelines

`templates/build-inmemory-emulator.yml` supplies the two Linux matrix jobs used by
`azure-pipelines.yml` (PR CI), an independent stage in `azure-pipelines-nightly.yml`,
and the focused `azure-pipelines-inmemory-emulator.yml` entry point. Each job uses
the project's available Microsoft-hosted Linux queue (`ubuntu-24.04`), not the
Windows OneES agents used by the existing emulator jobs. Existing full-emulator
and live-account selections continue to use their unchanged default backend.

Each job builds the pinned host with locked dependencies, authenticates to the same
Azure SDK Rust Cargo feed used by Go v2, installs the .NET 8 SDK and .NET 6 runtime,
and runs its gated manifest group. Rust compilation dominated the initial CI
baseline (273 seconds versus roughly 4.5 seconds of host-contract test execution).
For this first small cohort, independent builds preserve independent job execution
and avoid adding an artifact-producer dependency/queue boundary. They cost an
additional Rust build; no speedup or available parallel slots are assumed.
Build-once fan-out needs a separate measured comparison before adding that
complexity.

Each job publishes uniquely named test runs and `inmemory-emulator-Items-results`
or `inmemory-emulator-Reads-results` artifacts. Diagnostics use the existing
`PublishBuildArtifacts@1` / `Container` backend, not Pipeline Artifacts. Jobs do not
relax network isolation, retry away failures, or fall back to crates.io when feed
authentication fails.

The focused pipeline is registered as **dotnet-v3-inmemory-emulator** (definition
87) in **cosmos-db-sdk-public/cosmos-db-sdk-public**, alongside `dotnet-v3-ci` and
`dotnet-v3-nightly`. Queue a branch that contains this integration with:

```sh
az pipelines run --id 87 --branch <feature-branch> \
  --organization https://dev.azure.com/cosmos-db-sdk-public \
  --project cosmos-db-sdk-public
```

The existing GitHub service connection is reused; no additional account credential
is needed by the tests. The build identity needs read/upstream access to the Azure
SDK Rust feed. A local build uses the developer's Cargo registry configuration.
Registration alone does not establish a successful run; inspect the run's build,
test, and published result steps when enabling this on another build identity.
PR/nightly wiring becomes active when the change is merged into the target branch.

### Automatic PR coverage

The emulator job runs inside the existing **dotnet-v3-ci** pipeline (definition
63). Definition 87 deliberately keeps `pr: none`: enabling a second PR trigger
there would execute the same tests twice. The nightly pipeline also reuses the
shared job.

Coverage follows the existing SDK PR trigger: target branches `main` and
`releases/*`, with path exclusions `*.md`, `docs/**/*`, and
`Microsoft.Azure.Cosmos/contracts/**/*`. Thus it covers PRs selected by ordinary
SDK CI, not literally every PR: excluded-path-only changes and PRs targeting other
branches do not trigger this pipeline. A mixed code/documentation PR still runs.
The existing contributor-comment and fork-PR authorization protections remain in
effect; this change does not bypass approval gates or grant fork builds secrets.

No separate definition registration is required for automatic coverage because
definition 63 already consumes `azure-pipelines.yml`. The integration must be
present in the PR validation YAML and merged for ongoing coverage of the target
branch. A successful manually queued definition-87 run validates the shared job,
but is not by itself evidence that a PR event has triggered definition 63.
An upper stacked PR targeting its lower feature branch does not match these normal
PR branch filters. Validate its exact revision manually and observe any native
stack final-base CI separately; do not broaden the trigger to make a feature
branch eligible.

## References inspected

- [Go feature/azcosmos/v2](https://github.com/Azure/azure-sdk-for-go/tree/1812c74daac993850521063937b764c621a0a52a/sdk/data/azcosmos):
  `README.md` ("Running the end-to-end tests"), `emulator_test.go`,
  `internal/testdata/README.md`, `emulator-config.json`, and `ci.yml`.
- [Pinned Rust host](https://github.com/Azure/azure-sdk-for-rust/tree/13b9707cf9a73d274c6e2309321d4b0e92b52375/sdk/cosmos/azure_data_cosmos_emulator):
  `README.md`, `src/main.rs`, `src/data_plane.rs`, and `src/management.rs`;
  [Rust SDK tests](https://github.com/Azure/azure-sdk-for-rust/tree/13b9707cf9a73d274c6e2309321d4b0e92b52375/sdk/cosmos/azure_data_cosmos/tests/in_memory_emulator_tests).
