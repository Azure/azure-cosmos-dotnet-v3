# Running the in-memory emulator tests

These tests build this checkout's .NET SDK and run it against owned Rust
`azure_data_cosmos_emulator` processes. No Azure account or Windows emulator is needed.

## Prerequisites and host build

Use macOS/arm64 or Linux/amd64 with Git, rustup, a C compiler/linker, Python 3, the
.NET 8 SDK and .NET 6 runtime. Keep host source and build output outside this checkout.

```sh
source_dir=/absolute/path/cosmos-emulator-source
target_dir=/absolute/path/cosmos-emulator-target
git init "$source_dir"
git -C "$source_dir" fetch --depth=1 https://github.com/Azure/azure-sdk-for-rust.git \
  13b9707cf9a73d274c6e2309321d4b0e92b52375
git -C "$source_dir" checkout --detach FETCH_HEAD
rustup toolchain install 1.95.0 --profile minimal
```

For the Azure SDK Rust Cargo feed, copy the pinned source's
`eng/templates/config.toml.template` to an isolated `$CARGO_HOME/config.toml`.
Configure your approved Cargo credentials for `azure-sdk-for-rust-public` with
read/upstream access before building. Do not commit credentials, bypass an
authentication failure or replace the feed with crates.io. The existing CI job
performs feed authentication with `NuGetAuthenticate@1` and `CargoAuthenticate@0`.

```sh
export CARGO_HOME=/absolute/path/cosmos-emulator-cargo
mkdir -p "$CARGO_HOME"
cp "$source_dir/eng/templates/config.toml.template" "$CARGO_HOME/config.toml"
# Configure the feed credentials for this CARGO_HOME before the next command.
cargo +1.95.0 build --locked --release -p azure_data_cosmos_emulator \
  --manifest-path "$source_dir/Cargo.toml" --target-dir "$target_dir"
export COSMOS_IN_MEMORY_EMULATOR_PATH="$target_dir/release/azure_data_cosmos_emulator"
export COSMOS_IN_MEMORY_EMULATOR_SOURCE_REVISION=13b9707cf9a73d274c6e2309321d4b0e92b52375
```

## Run from the .NET repository root

Use the gated runner for the same disjoint groups as CI:

```sh
python3 -B Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests/run_ci.py \
  --group Items --results-directory /tmp/cosmos-inmemory-items
python3 -B Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests/run_ci.py \
  --group Reads --results-directory /tmp/cosmos-inmemory-reads
python3 -B -m unittest discover \
  -s Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests \
  -p test_run_ci.py
```

The runner selects the shared backend for its child processes and checks
`ci-scenarios.json` against actual discovery and passing leaf results. Missing,
extra, duplicate, skipped or failed cases fail the run. Keep the executable
manifest and assertions authoritative; do not weaken them to accommodate the host.
Results directories contain the assignments, discovery, TRX and verified summary.

For host-project-only debugging:

```sh
dotnet test Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests \
  --configuration Release --logger "console;verbosity=normal"
```

Without the executable variable, process-backed cases skip; configured invalid
paths/startup fail. Direct `dotnet test` lacks the gated runner's outer crash
cleanup and is not equivalent to CI. Do not set `COSMOS_EMULATOR_BACKEND=InMemory`
for the entire normal emulator suite: only the selected methods are host-eligible.
Leave that variable unset (or `Emulator`) for existing Windows/full-emulator tests.

## Safety and limitations

Fixtures own volatile stores and processes on dynamic loopback HTTP ports; cleanup
stops owned hosts, and the runner also cleans up after testhost crash/cancellation.
Never use real keys, remote/live endpoints or exposed listeners: the host does not
enforce authentication. Shared fixtures delete databases; keep them serial within
a job and do not point them at a shared account.

Only the selected Gateway scenarios are supported here. RID-addressed partition
metadata, managed item SQL and managed binary transactional batch remain
incompatible with this pin; do not run unselected candidates as if they were
certified. Logical-partition topology tests do not establish full routing-cache
recovery. Retain normal Windows/live coverage for unsupported behavior; a passing
host run is not complete service or cross-SDK parity.

The shared CI template is `templates/build-inmemory-emulator.yml`. Inspect the
group's test results and `inmemory-emulator-Items-results` or
`inmemory-emulator-Reads-results` diagnostics. To request a focused branch run:

```sh
az pipelines run --id 87 --branch <feature-branch> \
  --organization https://dev.azure.com/cosmos-db-sdk-public \
  --project cosmos-db-sdk-public
```

Normal PR CI targets `main` and `releases/*` with existing path/fork approval
filters; docs-only and dependent feature-target PRs may not trigger it. Observe
manual and automatic/native-stack runs separately; do not broaden triggers or
change security to force a run.
