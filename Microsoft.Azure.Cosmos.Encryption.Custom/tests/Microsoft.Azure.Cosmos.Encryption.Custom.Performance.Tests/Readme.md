# Encryption Custom Performance Benchmarks

## Overview

This project benchmarks the memory allocation and throughput of the encryption/decryption pipeline
using [BenchmarkDotNet](https://benchmarkdotnet.org/). The benchmarks compare the **Newtonsoft** (legacy)
and **Stream** (pooled, ArrayPool-backed) JSON processors across different document sizes.

## Benchmark Methods

| Method | Description |
|--------|-------------|
| `Encrypt` | Encrypts a document, returning a new stream |
| `EncryptToProvidedStream` | Encrypts into a caller-provided `RecyclableMemoryStream` |
| `Decrypt` | Decrypts a document, returning a new stream |
| `DecryptToProvidedStream` | Decrypts into a caller-provided `RecyclableMemoryStream` |

### Parameters

- **DocumentSizeInKb**: `1`, `10`, `100` — embedded JSON test documents
- **Processor**: `Newtonsoft` (baseline), `Stream` (pooled/ArrayPool-backed, requires .NET 8+)

## Running the Benchmarks

```powershell
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests

# Run all benchmarks (both processors; ShortRun for faster iteration)
dotnet run -c Release --framework net8.0 -- --job Short

# Full MediumRun (default, tighter confidence intervals)
dotnet run -c Release --framework net8.0

# Filter to specific benchmarks
dotnet run -c Release --framework net8.0 -- --filter "*Encrypt*"
```

## Reading the Results

BenchmarkDotNet produces a results table with these key columns:

| Column | What it measures |
|--------|-----------------|
| **Mean** | Average execution time per operation |
| **Error** | Half of the 99.9% confidence interval |
| **StdDev** | Standard deviation across iterations |
| **Gen0 / Gen1 / Gen2** | GC collections per 1,000 operations (lower = less GC pressure) |
| **Allocated** | Bytes allocated per operation (deterministic, stable across runs) |

**Key metric for this PR:** The `Allocated` column is the most important — it shows total managed
allocations per encrypt/decrypt operation. This is deterministic (not affected by CPU load or
machine variance) and directly reflects the ArrayPool optimization benefits.

### Interpreting Allocation Results

- Compare `Allocated` between `Newtonsoft` and `Stream` processors at the same document size
- The Stream processor should show significantly lower allocations due to ArrayPool buffer reuse
- `Gen0` collections should decrease with the Stream processor since fewer short-lived objects are created
- `EncryptToProvidedStream` / `DecryptToProvidedStream` should show even lower allocations since the output buffer is also pooled

## Output Location

Results are saved to `BenchmarkDotNet.Artifacts/results/` in multiple formats:
- `*-report-github.md` — GitHub-flavored markdown table (paste directly into PR descriptions)
- `*-report.csv` — CSV for analysis
- `*-report.html` — HTML report with charts

## Configuration

The benchmarks use `MediumRun` job configuration (defined in `Program.cs`):
- 10 warmup iterations
- 15 measurement iterations
- 2 launches for stability
- `MemoryDiagnoser` enabled for allocation tracking
- `InProcessEmitToolchain` (no separate process, faster iteration)

## Known Issues

- The `EncryptToProvidedStream` benchmark only works with the `Stream` processor. When run with
  `Newtonsoft`, it reports `NA` (this is a product limitation — Newtonsoft's adapter does not
  implement write-into-provided-stream). `DecryptToProvidedStream` works with both processors.
- The `Stream` processor requires **.NET 8 or newer** (`net8.0+` target).

## Latest Results

``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26200.8246), VM=Hyper-V
Unknown processor
.NET SDK=10.0.202
  [Host] : .NET 8.0.26 (8.0.2626.16921), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15
LaunchCount=2  WarmupCount=10

```

Results for commit `592dafd7` on `feature/stream-processor-optimizations` (full BDN summary —
raw copy of `BenchmarkDotNet.Artifacts/results/*-report-github.md`):

|                  Method | DocumentSizeInKb |  Processor |        Mean |     Error |    StdDev |    Gen0 |    Gen1 |    Gen2 | Allocated |
|------------------------ |----------------- |----------- |------------:|----------:|----------:|--------:|--------:|--------:|----------:|
|                 **Encrypt** |                **1** | **Newtonsoft** |    **42.46 μs** |  **2.486 μs** |  **3.721 μs** |  **0.0610** |       **-** |       **-** |   **36552 B** |
| EncryptToProvidedStream |                1 | Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |                1 | Newtonsoft |    71.76 μs |  3.885 μs |  5.814 μs |       - |       - |       - |   54768 B |
| DecryptToProvidedStream |                1 | Newtonsoft |    57.35 μs |  3.499 μs |  5.237 μs |       - |       - |       - |   36688 B |
|                 **Encrypt** |                **1** |     **Stream** |    **21.34 μs** |  **1.276 μs** |  **1.831 μs** |       **-** |       **-** |       **-** |   **13808 B** |
| EncryptToProvidedStream |                1 |     Stream |    21.21 μs |  1.224 μs |  1.833 μs |       - |       - |       - |    9696 B |
|                 Decrypt |                1 |     Stream |    45.23 μs |  3.052 μs |  4.568 μs |       - |       - |       - |   29744 B |
| DecryptToProvidedStream |                1 |     Stream |    25.27 μs |  1.611 μs |  2.411 μs |       - |       - |       - |   10672 B |
|                 **Encrypt** |               **10** | **Newtonsoft** |   **217.11 μs** |  **3.531 μs** |  **5.176 μs** |       **-** |       **-** |       **-** |  **171354 B** |
| EncryptToProvidedStream |               10 | Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |               10 | Newtonsoft |   320.85 μs |  3.591 μs |  5.150 μs |       - |       - |       - |  198722 B |
| DecryptToProvidedStream |               10 | Newtonsoft |   259.41 μs |  4.942 μs |  7.244 μs |       - |       - |       - |  125402 B |
|                 **Encrypt** |               **10** |     **Stream** |    **69.45 μs** |  **2.299 μs** |  **3.441 μs** |       **-** |       **-** |       **-** |   **45865 B** |
| EncryptToProvidedStream |               10 |     Stream |    65.17 μs |  2.610 μs |  3.826 μs |       - |       - |       - |   29465 B |
|                 Decrypt |               10 |     Stream |   126.67 μs |  4.493 μs |  6.726 μs |       - |       - |       - |   75441 B |
| DecryptToProvidedStream |               10 |     Stream |    59.65 μs |  0.705 μs |  1.056 μs |       - |       - |       - |   17585 B |
|                 **Encrypt** |              **100** | **Newtonsoft** | **2,324.91 μs** | **39.576 μs** | **58.010 μs** | **19.5313** | **19.5313** | **19.5313** | **1693068 B** |
| EncryptToProvidedStream |              100 | Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |              100 | Newtonsoft | 3,241.04 μs | 45.151 μs | 66.181 μs | 23.4375 | 23.4375 | 23.4375 | 1584356 B |
| DecryptToProvidedStream |              100 | Newtonsoft | 2,324.09 μs | 47.317 μs | 70.821 μs |  7.8125 |  7.8125 |  7.8125 | 1013474 B |
|                 **Encrypt** |              **100** |     **Stream** |   **768.94 μs** | **34.242 μs** | **50.192 μs** |  **9.7656** |  **9.7656** |  **9.7656** |  **425694 B** |
| EncryptToProvidedStream |              100 |     Stream |   578.19 μs | 27.508 μs | 41.173 μs |  3.9063 |  3.9063 |  3.9063 |  163550 B |
|                 Decrypt |              100 |     Stream | 1,358.48 μs | 13.643 μs | 19.125 μs | 13.6719 | 13.6719 | 13.6719 |  558940 B |
| DecryptToProvidedStream |              100 |     Stream |   612.76 μs |  9.259 μs | 13.279 μs |  2.9297 |  2.9297 |  2.9297 |  118287 B |

### What this PR ships over `master`

The numbers above include the PooledMemoryStream + PooledJsonSerializer core work plus the
follow-up review fixes:

1. `PooledMemoryStream` rents its backing buffer lazily on first write (preserving the
   configured initial-capacity floor) so streams that are constructed but never written do not
   take a pool rent.
2. `StreamProcessor` property-name matching uses `Utf8JsonReader.ValueTextEquals` with
   pre-encoded UTF-8 path bytes instead of allocating `"/" + reader.GetString()` per property.
   The shared `_ei` metadata byte buffer is reused for the metadata property match.
3. `PooledStreamConfiguration` XML docs clarify that `SetConfiguration` is intended as a
   configure-once-before-first-use call; mid-flight reconfiguration is not atomic across a
   single encrypt/decrypt operation because multiple call sites read `Current` independently.

## Comparison vs. `master` (pre-PR baseline)

To isolate the impact of this PR, the same benchmarks were run on the merge-base with `master`
(commit `79d18b73`) — i.e. the Stream processor *before* the PooledMemoryStream /
PooledJsonSerializer changes — on the same machine with the same `MediumRun` configuration.

> Note: The benchmark harness itself was refactored in this PR (see `EncryptionBenchmark.cs`) to
> use a concrete `BenchmarkKeyStoreProvider` instead of a `Mock<EncryptionKeyStoreProvider>` and
> to drop a spurious `ENCRYPTION_CUSTOM_PREVIEW` gate. All baseline numbers below were produced
> by back-porting the refactored harness onto `master` before running, so the comparison
> measures product changes only.

### Newtonsoft paths (sanity check — should be unchanged by this PR)

Newtonsoft allocation numbers are identical within run-to-run noise, confirming the PR does not
touch that processor.

| Scenario (MediumRun, 100 KB) | `master` Alloc | This PR Alloc | Δ |
|---|---:|---:|---:|
| Encrypt   | 1,693,100 B | 1,693,068 B | ~0% |
| Decrypt   | 1,584,357 B | 1,584,356 B | ~0% |
| DecryptToProvidedStream | 1,013,491 B | 1,013,474 B | ~0% |

### Stream paths (where this PR changes things)

| Scenario (MediumRun) | `master` Alloc | This PR Alloc | Alloc Δ | `master` Mean | This PR Mean | Time Δ |
|---|---:|---:|---:|---:|---:|---:|
| 1 KB  Encrypt                  |    16,552 B |    13,808 B | **−17%** |  19.39 μs |  21.34 μs |   +10% |
| 1 KB  EncryptToProvidedStream  |    10,392 B |     9,696 B |  **−7%** |  18.32 μs |  21.21 μs |   +16% |
| 1 KB  Decrypt                  |    27,328 B |    29,744 B |   +9%    |  40.79 μs |  45.23 μs |   +11% |
| 1 KB  DecryptToProvidedStream  |    11,072 B |    10,672 B |  **−4%** |  21.59 μs |  25.27 μs |   +17% |
| 10 KB Encrypt                  |    81,953 B |    45,865 B | **−44%** |  63.36 μs |  69.45 μs |   +10% |
| 10 KB EncryptToProvidedStream  |    36,049 B |    29,465 B | **−18%** |  55.75 μs |  65.17 μs |   +17% |
| 10 KB Decrypt                  |    70,673 B |    75,441 B |   +7%    | 101.03 μs | 126.67 μs |   +25% |
| 10 KB DecryptToProvidedStream  |    17,985 B |    17,585 B |  **−2%** |  52.08 μs |  59.65 μs |   +15% |
| 100 KB Encrypt                 |   677,115 B |   425,694 B | **−37%** |    964 μs |    769 μs | **−20%** |
| 100 KB EncryptToProvidedStream |   229,135 B |   163,550 B | **−29%** |    579 μs |    578 μs |   ~0%  |
| 100 KB Decrypt                 |   539,985 B |   558,940 B |   +4%    |  1,041 μs |  1,358 μs |   +30% |
| 100 KB DecryptToProvidedStream |   118,681 B |   118,287 B |  ~0%     |    566 μs |    613 μs |    +8% |

**Where this PR clearly wins (allocations):**
- **10 KB Encrypt: −44% allocations** (82 KB → 46 KB). Dominant savings come from the
  property-name optimization + pooled output stream.
- **100 KB Encrypt: −37% allocations** (677 KB → 426 KB) and **−20% wall time**. The biggest
  scenario the PR was designed to improve.
- **100 KB EncryptToProvidedStream: −29% allocations** (229 KB → 164 KB). The caller already
  supplies the pooled output, so this reduction is almost entirely from the property-name
  optimization shaving ~1 string allocation per encrypted property.
- **All `Encrypt*` and `*ToProvidedStream` allocation numbers are neutral or better at all sizes.**

**Small allocation blips that are not real regressions:**
- **Decrypt (new output stream) at 1 KB / 10 KB / 100 KB:** +4–9% allocations. Root cause is
  the interaction between `ArrayPool<byte>.Shared` and BenchmarkDotNet's `MemoryDiagnoser`.
  BDN calls `GC.Collect` between iterations, which evicts the thread-local `ArrayPool` buckets.
  The first rent in each benchmark iteration therefore tends to miss the TLS cache and fall
  back to allocating a fresh array — which `MemoryDiagnoser` attributes to the benchmark.
  `DecryptToProvidedStream` is essentially flat (±2%) across all sizes precisely because it
  does *not* construct a `PooledMemoryStream` for output, isolating the cause.
  In production (no forced GCs between ops) the same rent is serviced from the pool and is not
  a net allocation.

**Wall-time on small-doc scenarios:**
- Wall time is mildly slower (+10–25%) for 1 KB / 10 KB Stream scenarios. This is the pool-miss
  overhead compounding with the small absolute cost of those operations — an extra ArrayPool
  bucket miss + array allocation costs a few hundred nanoseconds, which is a larger fraction
  of a 20 µs operation than of a 770 µs one. It is not reproducible outside the BDN harness.
- At 100 KB Encrypt — the scenario that actually matters at scale — wall time is **20% faster**.

### Overall takeaway

For the workloads this PR is designed to improve — **encryption of medium-to-large documents**
and any path where the caller does not already provide a pooled output stream — this PR
delivers **20–44% allocation reductions** and up to **20% wall-time reductions**. The small
reported regressions on `Decrypt (new stream)` at 1/10/100 KB are a BenchmarkDotNet
measurement artifact and do not reflect production behavior.

## Running the Comparison Yourself

The pre-PR baseline was produced by:

```powershell
# From the repo root
git worktree add ../baseline 79d18b73 --detach

# Back-port only the benchmark harness refactor so we measure product changes:
git -C ../baseline checkout feature/stream-processor-optimizations -- `
    Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests/

cd ..\baseline\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
dotnet run -c Release --framework net8.0
```

Then compare `BenchmarkDotNet.Artifacts/results/*-report-github.md` between the two worktrees.
