# Microsoft.Azure.Cosmos.NativeDriverBenchmark

Apples-to-apples single-item read benchmark comparing:

| Path | API used | Transport |
|---|---|---|
| **V3 SDK** (`Microsoft.Azure.Cosmos` NuGet) | `Container.ReadItemStreamAsync` | Gateway (HTTPS) |
| **Native driver** (PR #4515 cdylib) | `NativeCosmosClient.ReadItemAsync` | Gateway (HTTPS) |

Both paths read the same `(id, partitionKey)` and return raw bytes — no
typed deserialization on either side. SDK is pinned to
`ConnectionMode.Gateway` so the comparison stays apples-to-apples with the
native driver's HTTPS-via-`reqwest` transport (Phase 6 of PR #4515 is
gateway-only today).

The style mirrors the V3 perf-tests `SerializerBenchmark.cs`: one
`[MemoryDiagnoser]` class, two `[Benchmark]` methods, baseline-vs-other
ratio in the output. No 1000-concurrent-reads — single-call latency only,
matching the simple "criterion happy-path" style of the Rust SDK
benchmarks.

## Configuration — env vars only (no secrets in source)

| Env var | Example |
|---|---|
| `COSMOS_ENDPOINT` | `https://my-account.documents.azure.com:443/` |
| `COSMOS_KEY` | account master key (read-only is sufficient) |
| `COSMOS_DATABASE` | `db1` |
| `COSMOS_CONTAINER` | `items` |
| `COSMOS_ITEM_ID` | `d00a39eb-5401-45f7-ae3e-211c5383a327` |
| `COSMOS_PARTITION_KEY` | `TBDfaa02479-865f-4116-9a84-8617a10704c4` |

Your sample doc (`taskNum`, `cost`, `description`, `children[…]`, etc.) is
perfect — no need to re-hydrate. Just pick any one of those entries and
plug its `id` + `pk` into the env vars above.

## Prereqs

1. `azurecosmosdriver.dll` must be on the .NET probing path. Build it
   with the helper from the sibling project:
   ```powershell
   pwsh ..\Microsoft.Azure.Cosmos.NativeDriverPoc\scripts\build-native-dll.ps1
   ```
2. Real Cosmos account reachable from this machine (the emulator works
   too, but defeats the "real comparison" intent).

## Usage

```powershell
# 1. Sanity-check connectivity + that both paths see the same doc:
$env:COSMOS_ENDPOINT       = "https://<account>.documents.azure.com:443/"
$env:COSMOS_KEY            = "<key>"
$env:COSMOS_DATABASE       = "db1"
$env:COSMOS_CONTAINER      = "items"
$env:COSMOS_ITEM_ID        = "d00a39eb-5401-45f7-ae3e-211c5383a327"
$env:COSMOS_PARTITION_KEY  = "TBDfaa02479-865f-4116-9a84-8617a10704c4"

dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- validate
# Expected: "PASS — both paths returned HTTP 200 with N bytes."

# 2. Run the benchmark:
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark
```

BenchmarkDotNet writes a markdown summary into
`BenchmarkDotNet.Artifacts/results/` next to a CSV + JSON. The summary
table shows `Mean / Error / StdDev / Ratio / Allocated`.

## Reading the output

- **Mean** — per-call latency averaged over iterations.
- **Ratio** — V3 SDK is the baseline (1.00). Native ratio < 1.00 = faster.
- **Allocated** — managed bytes allocated per call. Native path should
  show fewer allocations once the spec-defined zero-copy body accessor
  paths are exercised; today the native binding still copies bytes out
  of the unmanaged buffer in `MaterializeResponse`.

## Architecture notes — why a separate project

- **Isolation**: BenchmarkDotNet insists on Release builds and spawns
  its own worker process per benchmark; co-mingling with the POC's
  F-checks would muddle both.
- **Reuse via `[InternalsVisibleTo]`**: the V2 POC already implements a
  production-shaped `NativeCosmosClient`. The benchmark project just
  references the V2 csproj and accesses the internal types via an
  `InternalsVisibleTo` entry added to the POC csproj.
- **Solution layout**: project is part of `AsyncFfiPoc.sln`, so opening
  the worktree in Visual Studio shows all three POC projects.

## Future iterations (not needed for tomorrow's comparison)

- Add an AAD-token-based config (`DefaultAzureCredential`) once the
  native driver lands its credential surface.
- Add a `WriteItem` benchmark using `cosmos_operation_create_item` once
  the SDK side is configured to skip `EnableContentResponseOnWrite`.
- Add a query benchmark once `cosmos_query_items` is bound.
