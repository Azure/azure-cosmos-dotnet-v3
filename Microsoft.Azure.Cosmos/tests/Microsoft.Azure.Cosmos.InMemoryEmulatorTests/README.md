# Running the in-memory emulator tests

These tests build this checkout's .NET SDK and run it against an owned Rust
`azure_data_cosmos_emulator` process. No Azure account or Windows emulator is needed.

## Prerequisites and host build

Use macOS/arm64 or Linux/amd64 with Git, rustup, a C compiler/linker, the .NET 8 SDK
and .NET 6 runtime. Keep host source and build output outside this SDK checkout.

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
```

## Run from the .NET repository root

```sh
dotnet test Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.InMemoryEmulatorTests \
  --configuration Release --logger "console;verbosity=normal"
```

Use `--filter TestCategory=InMemoryEmulator` for process-backed cases only.
Without `COSMOS_IN_MEMORY_EMULATOR_PATH`, those cases skip; parser/lifecycle tests
still run. An explicitly configured missing/invalid executable or failed startup
fails the tests. CI requires process-backed execution and no skips; inspect TRX
and host build logs rather than treating compilation as a passing test run.

## Safety and result interpretation

Fixtures start and dispose their own processes, discover dynamic loopback HTTP
ports and discard all stored data on exit. The host does not enforce
authentication: use only the test dummy key, never real keys or remote endpoints.
Do not expose its listeners or change these tests to use a shared/live account.

This is a Gateway-only engineering test backend, not production-service parity.
Keep the existing Windows/full-emulator and live suites for unsupported features.

The shared job is in `templates/build-inmemory-emulator.yml`; PR/nightly entry
points and the focused pipeline use it. To request a focused run for a branch
containing the tests:

```sh
az pipelines run --id 87 --branch <feature-branch> \
  --organization https://dev.azure.com/cosmos-db-sdk-public \
  --project cosmos-db-sdk-public
```

Normal PR CI targets `main` and `releases/*` with existing path/fork approval
filters; a docs-only or feature-target PR may not trigger it. A manual run is not
proof of automatic PR execution. Do not change triggers or security to force a run.
