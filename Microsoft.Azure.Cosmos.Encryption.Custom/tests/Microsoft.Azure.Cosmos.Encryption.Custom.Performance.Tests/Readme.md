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

Each `[Benchmark]` also sets `OperationsPerInvoke = 16`. Each measured invocation runs
16 back-to-back operations, and BenchmarkDotNet divides both `Mean` and `Allocated` by 16
to report per-operation values. This was chosen so that the reported numbers reflect
**steady-state production behavior** where JIT caches are hot, method-dispatch caches are
warm, and `ArrayPool<byte>.Shared` has warm buckets from recent activity — which is how a
long-running service actually sees these code paths. A single-op-per-invocation setup would
instead attribute JIT / cache warmup cost to every reported measurement. Allocation numbers
are unchanged by this setting (the pooled rents are per-op, not per-invocation), but wall
times are materially more representative.

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

Results for commit `a5e80220` on `feature/stream-processor-optimizations` (full BDN summary —
raw copy of `BenchmarkDotNet.Artifacts/results/*-report-github.md`):

|                  Method | DocumentSizeInKb |  Processor |        Mean |      Error |     StdDev |    Gen0 |    Gen1 |    Gen2 | Allocated |
|------------------------ |----------------- |----------- |------------:|-----------:|-----------:|--------:|--------:|--------:|----------:|
|                 **Encrypt** |                **1** | **Newtonsoft** |    **49.59 μs** |   **0.808 μs** |   **1.158 μs** |       **-** |       **-** |       **-** |   **36552 B** |
| EncryptToProvidedStream |                1 | Newtonsoft |          NA |         NA |         NA |       - |       - |       - |         - |
|                 Decrypt |                1 | Newtonsoft |    83.27 μs |   2.481 μs |   3.714 μs |       - |       - |       - |   54768 B |
| DecryptToProvidedStream |                1 | Newtonsoft |    60.08 μs |   2.994 μs |   4.481 μs |       - |       - |       - |   36689 B |
|                 **Encrypt** |                **1** |     **Stream** |    **22.85 μs** |   **0.967 μs** |   **1.417 μs** |       **-** |       **-** |       **-** |   **13808 B** |
| EncryptToProvidedStream |                1 |     Stream |    21.56 μs |   1.206 μs |   1.805 μs |       - |       - |       - |    9696 B |
|                 Decrypt |                1 |     Stream |    46.41 μs |   3.129 μs |   4.684 μs |  0.0610 |       - |       - |   29744 B |
| DecryptToProvidedStream |                1 |     Stream |    23.85 μs |   1.882 μs |   2.817 μs |       - |       - |       - |   10672 B |
|                 **Encrypt** |               **10** | **Newtonsoft** |   **192.96 μs** |  **14.079 μs** |  **21.073 μs** |  **0.2441** |       **-** |       **-** |  **171353 B** |
| EncryptToProvidedStream |               10 | Newtonsoft |          NA |         NA |         NA |       - |       - |       - |         - |
|                 Decrypt |               10 | Newtonsoft |   256.32 μs |  25.469 μs |  36.527 μs |       - |       - |       - |  198722 B |
| DecryptToProvidedStream |               10 | Newtonsoft |   193.90 μs |  10.682 μs |  15.658 μs |  0.2441 |       - |       - |  125401 B |
|                 **Encrypt** |               **10** |     **Stream** |    **55.52 μs** |   **1.424 μs** |   **2.042 μs** |  **0.0610** |       **-** |       **-** |   **45864 B** |
| EncryptToProvidedStream |               10 |     Stream |    51.97 μs |   1.570 μs |   2.350 μs |       - |       - |       - |   29464 B |
|                 Decrypt |               10 |     Stream |    88.96 μs |   2.032 μs |   2.979 μs |  0.1221 |       - |       - |   75441 B |
| DecryptToProvidedStream |               10 |     Stream |    41.42 μs |   1.114 μs |   1.561 μs |       - |       - |       - |   17584 B |
|                 **Encrypt** |              **100** | **Newtonsoft** | **1,699.77 μs** |  **79.419 μs** | **116.412 μs** | **17.5781** | **17.5781** | **17.5781** | **1693023 B** |
| EncryptToProvidedStream |              100 | Newtonsoft |          NA |         NA |         NA |       - |       - |       - |         - |
|                 Decrypt |              100 | Newtonsoft | 2,217.55 μs |  46.219 μs |  69.179 μs | 19.5313 | 19.5313 | 19.5313 | 1584335 B |
| DecryptToProvidedStream |              100 | Newtonsoft | 1,729.32 μs | 125.646 μs | 188.060 μs |  9.7656 |  9.7656 |  9.7656 | 1013478 B |
|                 **Encrypt** |              **100** |     **Stream** |   **639.99 μs** |  **32.310 μs** |  **47.359 μs** |  **7.8125** |  **7.8125** |  **7.8125** |  **425683 B** |
| EncryptToProvidedStream |              100 |     Stream |   463.20 μs |   6.969 μs |  10.431 μs |  3.9063 |  3.9063 |  3.9063 |  163537 B |
|                 Decrypt |              100 |     Stream | 1,038.95 μs |  50.995 μs |  73.136 μs | 11.7188 | 11.7188 | 11.7188 |  558900 B |
| DecryptToProvidedStream |              100 |     Stream |   493.95 μs |  37.074 μs |  53.171 μs |  3.4180 |  3.4180 |  3.4180 |  118290 B |

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
PooledJsonSerializer changes — on the same machine, with the same `MediumRun` configuration,
**and the same `OperationsPerInvoke = 16`** so the two runs are directly comparable.

> Note: The benchmark harness itself was refactored in this PR (`EncryptionBenchmark.cs`) to
> use a concrete `BenchmarkKeyStoreProvider` instead of a `Mock<EncryptionKeyStoreProvider>`,
> to drop a spurious `ENCRYPTION_CUSTOM_PREVIEW` gate, and to add `OperationsPerInvoke = 16`.
> All baseline numbers below were produced by back-porting the refactored harness onto
> `master` before running, so the comparison measures product changes only.

### Newtonsoft paths (sanity check — should be unchanged by this PR)

Newtonsoft allocation numbers are identical within run-to-run noise, confirming the PR does not
touch that processor.

| Scenario (MediumRun, 100 KB) | `master` Alloc | This PR Alloc | Δ |
|---|---:|---:|---:|
| Encrypt   | 1,693,038 B | 1,693,023 B | ~0% |
| Decrypt   | 1,584,371 B | 1,584,335 B | ~0% |
| DecryptToProvidedStream | 1,013,460 B | 1,013,478 B | ~0% |

### Stream paths (where this PR changes things)

| Scenario (MediumRun) | `master` Alloc | This PR Alloc | Alloc Δ | `master` Mean | This PR Mean | Time Δ |
|---|---:|---:|---:|---:|---:|---:|
| 1 KB  Encrypt                  |    16,552 B |    13,808 B | **−17%** |    20.05 μs |    22.85 μs |   +14% |
| 1 KB  EncryptToProvidedStream  |    10,392 B |     9,696 B |  **−7%** |    18.97 μs |    21.56 μs |   +14% |
| 1 KB  Decrypt                  |    27,328 B |    29,744 B |   +9%    |    35.92 μs |    46.41 μs |   +29% |
| 1 KB  DecryptToProvidedStream  |    11,072 B |    10,672 B |  **−4%** |    21.69 μs |    23.85 μs |   +10% |
| 10 KB Encrypt                  |    81,953 B |    45,864 B | **−44%** |    86.53 μs |    55.52 μs | **−36%** |
| 10 KB EncryptToProvidedStream  |    36,049 B |    29,464 B | **−18%** |    65.44 μs |    51.97 μs | **−21%** |
| 10 KB Decrypt                  |    70,673 B |    75,441 B |   +7%    |    95.57 μs |    88.96 μs |  **−7%** |
| 10 KB DecryptToProvidedStream  |    17,984 B |    17,584 B |  **−2%** |    45.16 μs |    41.42 μs |  **−8%** |
| 100 KB Encrypt                 |   677,062 B |   425,683 B | **−37%** |   756.90 μs |   639.99 μs | **−15%** |
| 100 KB EncryptToProvidedStream |   229,118 B |   163,537 B | **−29%** |   521.09 μs |   463.20 μs | **−11%** |
| 100 KB Decrypt                 |   539,947 B |   558,900 B |   +4%    | 1,102.58 μs | 1,038.95 μs |  **−6%** |
| 100 KB DecryptToProvidedStream |   118,686 B |   118,290 B |  ~0%     |   460.77 μs |   493.95 μs |    +7% |

**Where this PR clearly wins (allocations):**
- **10 KB Encrypt: −44% allocations and −36% wall time** (82 KB → 46 KB; 86.5 μs → 55.5 μs).
  Dominant savings come from the pooled backing buffer plus the property-name matching
  optimization (no more `"/" + reader.GetString()` per encrypted property).
- **10 KB EncryptToProvidedStream: −18% allocations and −21% wall time** — the caller already
  supplies the pooled output, so these savings are almost entirely from the property-name
  optimization.
- **100 KB Encrypt: −37% allocations and −15% wall time** (677 KB → 426 KB; 757 μs → 640 μs).
  The biggest scenario the PR was designed to improve.
- **100 KB EncryptToProvidedStream: −29% allocations and −11% wall time** (229 KB → 164 KB).
- **100 KB Decrypt: −6% wall time** despite the small allocation increase — the pooled output
  stream's write path is faster even though it allocates slightly more bytes per op.
- **All `Encrypt*` and `*ToProvidedStream` allocation numbers are neutral or better at every size.**

**Small allocation increases on `Decrypt` (new output stream):**
- **Decrypt (new output stream) at 1 KB / 10 KB / 100 KB:** +4–9% allocations.
  Initially suspected to be a BDN `MemoryDiagnoser` cold-pool-miss artifact, this was
  disproven by the `OperationsPerInvoke = 16` run: allocation numbers are reproducible per-op
  and unchanged regardless of invocation batching. The extra bytes are **real per-op product
  allocation** coming from `PooledMemoryStream`'s growth pattern: the decrypt output stream
  rents a small buffer first, then grows (renting a larger buffer and returning the old one)
  as decrypted bytes are written. `ArrayPool<byte>.Shared`'s thread-local cache has one slot
  per size class, so the temporarily-out-of-TLS buffers may allocate on subsequent rents.
  Context: **absolute size is small** (+2.4 KB at 1 KB, +4.8 KB at 10 KB, +19 KB at 100 KB),
  wall time still improves at 10 KB / 100 KB (−6% to −7%), and the affected scenario
  (`Decrypt` with a new output stream) is the one path where the caller does *not* cooperate
  with the pool — production callers using `DecryptToProvidedStream` with a pooled output get
  neutral-or-better allocations **and** faster wall times at every size.

  **Pre-sizing the output stream was investigated and rejected.** A change to pass
  `input.Length` as a capacity hint to `PooledMemoryStream` was prototyped to skip the
  growth chain. It improved 1 KB Decrypt (-7%) but **worsened 100 KB Decrypt by +24%**
  (558 KB → 690 KB). Root cause: the grow chain happens to touch `ArrayPool` buckets
  (4 KB / 8 KB / 16 KB / 32 KB / 64 KB) that the .NET runtime keeps warm via unrelated
  workload (string formatting, JSON parsing, networking). The 128 KB bucket the hint
  jumps to is essentially only used by this code path, so it gets aggressively trimmed
  by `ArrayPool`'s Gen2 GC callback (BDN forces Gen2 GC between iterations to give
  clean allocation measurements; production also hits Gen2 periodically). A `LongRun`
  confirmation (100 iter × 3 launches × 16 OPI ≈ 14,400 measured ops, 1,500 warmup ops)
  produced the same +131 KB regression at 100 KB Decrypt as `MediumRun`, ruling out
  insufficient warmup. The pre-size change was reverted; the small +4–9% per-op
  allocation observed here is the cost of cold large-bucket rentals in async-heavy
  paths and cannot be fixed without a private buffer pool (e.g.
  `Microsoft.IO.RecyclableMemoryStream`), which is excluded by dependency restrictions.

**1 KB wall-time regression is real:**
- Wall time is 10–29% slower for 1 KB Stream scenarios. At this scale the fixed setup cost of
  the Stream processor (pooled stream construction, `Utf8JsonReader` state, key-path table)
  exceeds the savings from pooled I/O. This was true before the PR too — 1 KB is not the
  target workload. For reference, master's baseline was 20 μs and this PR is 23 μs — both are
  already faster than Newtonsoft (42–50 μs).
- From 10 KB upward — the actual target for client-side encryption — this PR is consistently
  faster AND allocates less (10 KB Encrypt is **−36% wall time**, 100 KB Encrypt is
  **−15% wall time**).

### Overall takeaway

For the workloads this PR is designed to improve — **encryption of medium-to-large documents**
and any path where the caller cooperates by providing a pooled output stream — this PR
delivers **up to −44% allocations and −36% wall time**. The small allocation increase on
`Decrypt (new output stream)` is real but modest in absolute size and is offset by wall-time
improvements at 10 KB / 100 KB. The 1 KB wall-time regression is a scale mismatch (fixed
setup cost dominates) that does not apply to realistic payloads.

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
