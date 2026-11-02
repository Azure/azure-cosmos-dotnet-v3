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
regional failover, or server-side scripting. The Rust host advertises more
operations than this suite verifies; those capabilities are not .NET compatibility
claims. In particular, Go documents emulator/live differences for conditional
batch reads and content-response suppression.

Continue using `Microsoft.Azure.Cosmos.EmulatorTests` and live-account suites for
those features and service-parity validation. Their configuration, fixtures, and
test selection are unchanged. This separation follows the canonical
[high-level architecture's Gateway/Direct distinction](https://github.com/tvaron3/cosmosdb-design-docs/blob/main/01-high-level-architecture.md#1-high-level-architecture).

## Azure Pipelines

`azure-pipelines-inmemory-emulator.yml` is a standalone, manually queued Linux job.
It builds the pinned host with locked dependencies, authenticates to the same
Azure SDK Rust Cargo feed used by Go v2, installs the .NET toolchain/runtime, and
sets the executable variable before running this project. The job publishes TRX
results and treats a skipped process test as failure. It does not change the
existing Windows-emulator or live-account pipelines.

Register/queue this YAML in Azure DevOps to enable the job; the build identity needs
read/upstream access to the Azure SDK Rust feed. A local build uses the developer's
Cargo registry configuration. The job does not relax network isolation or bypass
feed authentication on failure.

## References inspected

- [Go feature/azcosmos/v2](https://github.com/Azure/azure-sdk-for-go/tree/1812c74daac993850521063937b764c621a0a52a/sdk/data/azcosmos):
  `README.md` ("Running the end-to-end tests"), `emulator_test.go`,
  `internal/testdata/README.md`, `emulator-config.json`, and `ci.yml`.
- [Pinned Rust host](https://github.com/Azure/azure-sdk-for-rust/tree/13b9707cf9a73d274c6e2309321d4b0e92b52375/sdk/cosmos/azure_data_cosmos_emulator):
  `README.md`, `src/main.rs`, `src/data_plane.rs`, and `src/management.rs`;
  [Rust SDK tests](https://github.com/Azure/azure-sdk-for-rust/tree/13b9707cf9a73d274c6e2309321d4b0e92b52375/sdk/cosmos/azure_data_cosmos/tests/in_memory_emulator_tests).
