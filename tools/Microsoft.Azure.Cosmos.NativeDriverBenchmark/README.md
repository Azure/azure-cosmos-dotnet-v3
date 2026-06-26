# Microsoft.Azure.Cosmos.NativeDriverBenchmark

Apples-to-apples single-item CRUD + single-partition query benchmarks
comparing three drivers:

| Path | API used | Transport | Role |
|---|---|---|---|
| **V3 SDK Gateway** (`Microsoft.Azure.Cosmos` NuGet) | `Container.*StreamAsync` / `GetItemQueryStreamIterator` | Gateway (HTTPS) | **Baseline** — apples-to-apples with native |
| **V3 SDK Direct** (`Microsoft.Azure.Cosmos` NuGet) | `Container.*StreamAsync` / `GetItemQueryStreamIterator` | Direct (TCP) | Production-typical reference |
| **Native driver** (PR #4515 cdylib) | `NativeCosmosClient.*Async` | Gateway (HTTPS) | The new thing |

Five operations are covered — Read, Create, Replace, Delete, **Query** — one
BDN class per op (15 benchmark methods + the 3 read methods = **18 total**:
4 CRUD classes × 3 methods + 1 Query class × 6 methods). The Query class
exercises two SHAPES — *SinglePage* (whole result fits in one round-trip)
and *Paginated* (`MaxItemCount=2` forces multi-page walk) — across all
three drivers. V3 SDK Gateway is the BDN baseline
(`Ratio = 1.00`) for each class because it shares a transport with the
native driver, so the native ratio is meaningful. V3 SDK Direct shows the
TCP-direct headroom — that's the gap the native driver eventually needs
to close once it grows a direct-mode transport (Phase 6 of PR #4515 is
gateway-only today).

## Configuration — env vars only (no secrets in source)

| Env var | Example |
|---|---|
| `COSMOS_ENDPOINT` | `https://my-account.documents.azure.com:443/` |
| `COSMOS_KEY` | account master key (read-only is sufficient) |
| `COSMOS_DATABASE` | `db1` |
| `COSMOS_CONTAINER` | `items` |
| `COSMOS_ITEM_ID` | `ITEM-ID` |
| `COSMOS_PARTITION_KEY` | `PK` |

Your sample doc (`taskNum`, `cost`, `description`, `children[…]`, etc.) is
perfect — no need to re-hydrate. Just pick any one of those entries and
plug its `id` + `pk` into the env vars above.

### Recommended: a `.env` file + loader script

Typing six `$env:` lines every time you open a new shell gets old. A
template + loader is checked in:

```powershell
# one-time
cd tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark
copy .env.example .env
notepad .env          # fill in real endpoint + key

# every new shell
. .\scripts\Load-Env.ps1
# Loaded 6 variable(s) from ...\.env
```

`.env` is gitignored (rule: `*.env` with a `!*.env.example` exception);
`.env.example` is the committed template. The loader sets
process-scope variables visible to `dotnet run` started from the same
shell — no `setx`, nothing persists machine-wide.

Loader behaviour: skips blank lines and `#` comments, strips surrounding
`"` / `'` quotes, tolerates an `export ` prefix, no variable
interpolation. Override the path with `. .\scripts\Load-Env.ps1 -Path C:\secrets\cosmos.env`
if you keep your secrets elsewhere.

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
. .\scripts\Load-Env.ps1   # reads .env into the current shell

# Sanity check 1: reads-only (cheap, no writes).
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- validate
# Expected: "PASS — all three paths returned HTTP 200 with N bytes."

# Sanity check 2: full CRUD per mode (writes 3 throwaway docs, deletes them).
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- validate --crud
# Expected: "PASS — all three modes completed CREATE/READ/REPLACE/READ/DELETE."

# Sanity check 3: query shapes per mode (seeds 5 docs in a fresh PK, runs
# both shapes via all three drivers, asserts doc counts, cleans up).
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- validate --query
# Expected: "PASS — all three modes returned the expected doc counts in both shapes."

# Full benchmark matrix (5 classes; CRUD ×3 each + Query ×6 = 18 benchmarks).
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark

# Filter to a subset (BDN's --filter is glob-style on FullName.Method):
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- --filter "*ReadItem*"
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- --filter "*Create*"
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- --filter "*QueryItems*"
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- --filter "*Native*"
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- --filter "*SinglePage*"
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- --filter "*Paginated*"

# Discover without running.
dotnet run -c Release --project .\tools\Microsoft.Azure.Cosmos.NativeDriverBenchmark -- --list flat
```

BenchmarkDotNet writes a markdown summary into
`BenchmarkDotNet.Artifacts/results/` next to a CSV + JSON. Each benchmark
class produces its own table with `Mean / Min / Max / Error / StdDev / Ratio / Allocated`.
`Min` and `Max` come from `[MinColumn, MaxColumn]` on each benchmark class —
useful for spotting whether the long tail is wider on one driver than another
within a single short run. (No p95/p99 — these are short, sub-second
benchmarks; for sustained-load tail-latency numbers use the soak harness.)

## RU and time budget

Reads cost ~1 RU; writes cost ~7 RU (Create / Delete) and ~11 RU (Replace).
Per benchmark method, ShortRun runs `(3 warmup + 10 measured) × InvocationCount`
calls. Write benchmarks use `InvocationCount=4` (vs `16` for reads) to keep
RU costs reasonable.

| Class | Pre-seed RU | Per-method ops | Full-class RU | Notes |
|---|---|---|---|---|
| Read | 0 | 208 measured + 80 warmup = 288 | ~870 (~290 reads × 3 modes × 1 RU) | uses the pre-existing doc |
| Create | 0 (pool is in-memory) | 52 measured + 5 warmup = 57 | ~1,200 (~170 creates × 7 RU + cleanup deletes) | pool of 256 ids per mode |
| Replace | ~20 (3 pre-seed creates) | 52 measured + 5 warmup = 57 | ~1,900 (~170 replaces × 11 RU) | one target doc per mode |
| Delete | ~5,400 (256 × 3 × ~7 RU) | 52 measured + 5 warmup = 57 | ~6,500 (pre-seed dominates) | pre-creates 768 docs |
| **Query** | ~70 (10 pre-seed creates × ~7 RU) | 104 measured + 15 warmup = 119 per shape; 2 shapes × 3 modes = 6 methods | ~3,500 (~700 SinglePage @ ~5 RU + ~700 Paginated × 5 pages @ ~3 RU) | seeds 10 docs in a fresh PK, deletes them in cleanup |

A **full-matrix run** on a 400 RU/s container takes about **3 minutes** wall
time and consumes about **10,000 RU** (well under throttle limit if the
container has burst capacity, otherwise expect ~25 seconds of 429-induced
backoff during the Delete pre-seed).

To stay below ~3,000 RU, run `--filter "*ReadItem*"` (full read matrix) or
`--filter "*Replace*"` (lightest write op) instead of the full suite.

## Reading the output

- **Mean** — per-call latency averaged over iterations.
- **Min** — fastest single-call observation across all measured iterations.
  Under stable network conditions, this is "what the driver can do when nothing
  is in the way." A native Min meaningfully below the SDK Min on the same
  transport = real per-call wins (not just averaging artifacts).
- **Max** — slowest single-call observation. A Max far above Mean = at least
  one outlier (GC pause, network blip, threadpool hiccup). Watch for: native
  Max no worse than SDK Max = no new tail-latency surprises from FFI.
- **Ratio** — V3 SDK Gateway is the baseline (1.00) per class.
  - V3 SDK Direct ratio < 1.00 = TCP-direct is faster than gateway. Typical: ~0.4–0.7 for reads, ~0.85–0.95 for writes (writes are server-bound — less gateway-vs-direct delta).
  - Native driver ratio close to 1.00 = native is competitive with the SDK on the same transport.
  - Native ratio < 1.00 vs Gateway baseline = native is faster than the SDK on gateway.
- **Allocated** — managed bytes per call. The interesting column. Native
  path typically shows **~15× fewer managed allocations** because the response
  body lands as a `byte[]` instead of a `Stream` wrapper. The Allocated Ratio
  column on the native row is the headline number for the FFI win.

### Example output (Replace, real account, 2026-06-03)

```
| Method                                                          | Mean     | Allocated | Ratio | Alloc Ratio |
|---------------------------------------------------------------- |---------:|----------:|------:|------------:|
| 'V3 SDK — Gateway (ReplaceItemStreamAsync)'                     | 45.78 ms |     31 KB |  1.00 |        1.00 |
| 'V3 SDK — Direct (ReplaceItemStreamAsync)'                      | 42.36 ms |  33.21 KB |  0.93 |        1.07 |
| 'Native driver — Gateway (NativeCosmosClient.ReplaceItemAsync)' | 43.73 ms |   2.11 KB |  0.96 |        0.07 |
```

Native is within noise of Gateway on wall time (same transport, lower
managed overhead) and is **15× lower** on managed allocations per op.

## State-isolation contract for the write benchmarks

Each write benchmark class manages its own state so the timed window is
just the wire op — no id generation, no pre-create overhead.

| Class | GlobalSetup | Per-iteration | GlobalCleanup |
|---|---|---|---|
| `ReadItem` | Build 3 clients, warm each 5× against the pre-existing `COSMOS_ITEM_ID` | Single read of the same doc | Dispose clients |
| `CreateItem` | Build 3 clients, generate 256 unique ids per mode (in-memory), warm each 5× | Pop next id from pool, CREATE | Best-effort DELETE all consumed ids |
| `ReplaceItem` | Build 3 clients, pre-create 1 doc per mode via gateway, warm each 5× | REPLACE same doc with bumped version | DELETE the 3 target docs |
| `DeleteItem` | Build 3 clients, pre-create 256 docs per mode via gateway (the pool), warm each 5× | Pop next doc from pool, DELETE | Best-effort DELETE any unused pool docs |

All write benchmarks use a **class-local partition key** of the form
`bench-{op}-pk-{runGuid}` so they don't touch the read-benchmark's
pre-seeded doc and don't collide with each other across runs. The native
client takes its PK at ctor time and uses it for every op on that client
— this is the constraint that drives the "all writes in a class share
one PK" design.

If a run is killed mid-benchmark, leaked docs share the `bench-{op}-*` id
prefix and can be scrubbed with a one-off cleanup query against the
container if necessary.

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

## Future iterations

- Add an AAD-token-based config (`DefaultAzureCredential`) once the
  native driver lands its credential surface.
- Add a query benchmark once `cosmos_query_items` is bound.
- Add a transactional-batch benchmark once the FFI exposes it.
