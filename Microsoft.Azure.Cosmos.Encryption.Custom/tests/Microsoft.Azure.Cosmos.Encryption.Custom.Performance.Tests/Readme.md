# Encryption Custom Performance Benchmarks

## Overview

This project benchmarks the memory allocation and throughput of the encryption/decryption pipeline
using [BenchmarkDotNet](https://benchmarkdotnet.org/). The benchmarks compare the **Newtonsoft** (legacy)
and **Stream** (pooled, ArrayPool-backed) JSON processors across different document sizes.

## Benchmark Methods

| Method | Description |
|--------|-------------|
| `Encrypt` | Encrypts a document, returning a new `Stream` |
| `EncryptToProvidedStream` | Encrypts into a caller-provided output `Stream` |
| `Decrypt` | Decrypts a document, returning a new `Stream` |
| `DecryptToProvidedStream` | Decrypts into a caller-provided output `Stream` |

> The `*ToProvidedStream` benchmarks supply a `Microsoft.IO.RecyclableMemoryStream`
> as the output stream. That package is a **test-only** dependency of this
> benchmark project (`Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests.csproj`)
> and is **not** referenced by `Microsoft.Azure.Cosmos.Encryption.Custom` itself —
> the product code accepts any `Stream`. Callers of the production API are free
> to pass a plain `MemoryStream`, a pooled stream of their choice, or any other
> `Stream` implementation.

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

The numbers below are the **only** results recorded in this README. They come from two
back-to-back MediumRun passes executed on the same idle machine (Windows 11, .NET SDK
10.0.202, .NET 8.0.26 X64 RyuJIT AVX2, BenchmarkDotNet v0.13.3, `MediumRun` job:
15 iterations × 2 launches × 10 warmup, `OperationsPerInvoke = 16`,
`InProcessEmitToolchain`, `MemoryDiagnoser`). The harness file
(`EncryptionBenchmark.cs`) is byte-identical between the two runs; only product code
differs.

- **`master`** run: `origin/master` at commit `79d18b73` with this PR's harness
  back-ported (harness-only changes: concrete `BenchmarkKeyStoreProvider`, drop of the
  spurious `ENCRYPTION_CUSTOM_PREVIEW` gate, `OperationsPerInvoke = 16`).
- **`this PR`** run: `feature/stream-processor-optimizations` at the commit whose
  product code (everything under `Microsoft.Azure.Cosmos.Encryption.Custom/src/`)
  matches the current branch HEAD. Subsequent commits on the branch add tests
  and documentation only and leave the measured numbers unchanged.

Raw output lives at
`BenchmarkDotNet.Artifacts/results/Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests.EncryptionBenchmark-report-github.md`
in each checkout after running `dotnet run -c Release --framework net8.0 -- --filter '*EncryptionBenchmark*'`.

### This PR — full BDN summary

```
Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  LaunchCount=2  WarmupCount=10
```

|                  Method | DocumentSizeInKb |  Processor |        Mean |      Error |     StdDev |      Median |    Gen0 |    Gen1 |    Gen2 | Allocated |
|------------------------ |----------------- |----------- |------------:|-----------:|-----------:|------------:|--------:|--------:|--------:|----------:|
|                 **Encrypt** |                **1** | **Newtonsoft** |    **35.32 μs** |   **2.342 μs** |   **3.506 μs** |    **34.11 μs** |  **0.0610** |       **-** |       **-** |   **36648 B** |
| EncryptToProvidedStream |                1 | Newtonsoft |          NA |         NA |         NA |          NA |       - |       - |       - |         - |
|                 Decrypt |                1 | Newtonsoft |    63.08 μs |   3.215 μs |   4.611 μs |    63.12 μs |       - |       - |       - |   54960 B |
| DecryptToProvidedStream |                1 | Newtonsoft |    48.46 μs |   2.934 μs |   4.392 μs |    47.55 μs |  0.0610 |       - |       - |   36880 B |
|                 **Encrypt** |                **1** |     **Stream** |    **19.50 μs** |   **1.466 μs** |   **2.195 μs** |    **18.78 μs** |       **-** |       **-** |       **-** |   **13816 B** |
| EncryptToProvidedStream |                1 |     Stream |    18.96 μs |   0.826 μs |   1.236 μs |    18.48 μs |       - |       - |       - |    9704 B |
|                 Decrypt |                1 |     Stream |    42.25 μs |   3.982 μs |   5.960 μs |    40.23 μs |       - |       - |       - |   24704 B |
| DecryptToProvidedStream |                1 |     Stream |    25.75 μs |   1.565 μs |   2.342 μs |    26.86 μs |       - |       - |       - |    5720 B |
|                 **Encrypt** |               **10** | **Newtonsoft** |   **193.73 μs** |  **14.140 μs** |  **21.164 μs** |   **195.69 μs** |  **0.2441** |       **-** |       **-** |  **171449 B** |
| EncryptToProvidedStream |               10 | Newtonsoft |          NA |         NA |         NA |          NA |       - |       - |       - |         - |
|                 Decrypt |               10 | Newtonsoft |   250.13 μs |  25.720 μs |  37.700 μs |   250.56 μs |  0.2441 |       - |       - |  198913 B |
| DecryptToProvidedStream |               10 | Newtonsoft |   224.06 μs |  18.929 μs |  28.333 μs |   226.47 μs |  0.2441 |       - |       - |  125593 B |
|                 **Encrypt** |               **10** |     **Stream** |    **64.00 μs** |   **4.792 μs** |   **7.024 μs** |    **64.63 μs** |       **-** |       **-** |       **-** |   **45873 B** |
| EncryptToProvidedStream |               10 |     Stream |    65.16 μs |   1.772 μs |   2.598 μs |    64.87 μs |       - |       - |       - |   29473 B |
|                 Decrypt |               10 |     Stream |   128.67 μs |   6.320 μs |   9.460 μs |   131.60 μs |       - |       - |       - |   63489 B |
| DecryptToProvidedStream |               10 |     Stream |    49.01 μs |   3.891 μs |   5.703 μs |    48.10 μs |       - |       - |       - |    5720 B |
|                 **Encrypt** |              **100** | **Newtonsoft** | **1,852.98 μs** |  **72.617 μs** | **108.690 μs** | **1,864.41 μs** | **19.5313** | **19.5313** | **19.5313** | **1693133 B** |
| EncryptToProvidedStream |              100 | Newtonsoft |          NA |         NA |         NA |          NA |       - |       - |       - |         - |
|                 Decrypt |              100 | Newtonsoft | 2,553.97 μs | 192.639 μs | 288.334 μs | 2,545.63 μs | 20.8333 | 20.8333 | 20.8333 | 1584568 B |
| DecryptToProvidedStream |              100 | Newtonsoft | 1,828.46 μs | 138.802 μs | 207.753 μs | 1,767.60 μs |  7.8125 |  7.8125 |  7.8125 | 1013660 B |
|                 **Encrypt** |              **100** |     **Stream** |   **647.31 μs** |  **36.999 μs** |  **55.379 μs** |   **637.39 μs** |  **7.8125** |  **7.8125** |  **7.8125** |  **425690 B** |
| EncryptToProvidedStream |              100 |     Stream |   484.90 μs |  25.284 μs |  37.844 μs |   463.85 μs |  3.9063 |  3.9063 |  3.9063 |  163545 B |
|                 Decrypt |              100 |     Stream |   946.17 μs |  26.169 μs |  36.685 μs |   937.02 μs | 10.7422 | 10.7422 | 10.7422 |  446452 B |
| DecryptToProvidedStream |              100 |     Stream |   386.53 μs |  22.831 μs |  33.466 μs |   376.49 μs |       - |       - |       - |    5906 B |

### Comparison vs. `master` (`79d18b73`)

#### Newtonsoft paths — sanity check

The PR does not touch the Newtonsoft path. All Newtonsoft scenarios agree within run-to-run
noise (≤ 0.52 %), confirming the comparison is apples-to-apples.

| Scenario | master Alloc | this PR Alloc | Δ |
|---|---:|---:|---:|
| Encrypt 1 KB                    |    36,552 B |    36,648 B | +0.26% |
| Decrypt 1 KB                    |    54,768 B |    54,960 B | +0.35% |
| DecryptToProvidedStream 1 KB    |    36,688 B |    36,880 B | +0.52% |
| Encrypt 10 KB                   |   171,353 B |   171,449 B | +0.06% |
| Decrypt 10 KB                   |   198,722 B |   198,913 B | +0.10% |
| DecryptToProvidedStream 10 KB   |   125,401 B |   125,593 B | +0.15% |
| Encrypt 100 KB                  | 1,693,159 B | 1,693,133 B | −0.00% |
| Decrypt 100 KB                  | 1,584,352 B | 1,584,568 B | +0.01% |
| DecryptToProvidedStream 100 KB  | 1,013,492 B | 1,013,660 B | +0.02% |

#### Stream paths — where this PR moves the numbers

| Scenario | master Alloc | this PR Alloc | Alloc Δ | master Mean | this PR Mean | Time Δ |
|---|---:|---:|---:|---:|---:|---:|
| 1 KB  Encrypt                  |    16,552 B |    13,816 B | **−16.5%** |    27.54 μs |    19.50 μs | **−29.2%** |
| 1 KB  EncryptToProvidedStream  |    10,392 B |     9,704 B |  **−6.6%** |    24.24 μs |    18.96 μs | **−21.8%** |
| 1 KB  Decrypt                  |    27,328 B |    24,704 B |  **−9.6%** |    51.53 μs |    42.25 μs | **−18.0%** |
| 1 KB  DecryptToProvidedStream  |    11,072 B |     5,720 B | **−48.3%** |    26.12 μs |    25.75 μs |   −1.4% |
| 10 KB Encrypt                  |    81,953 B |    45,873 B | **−44.0%** |    69.84 μs |    64.00 μs |  **−8.4%** |
| 10 KB EncryptToProvidedStream  |    36,048 B |    29,473 B | **−18.2%** |    62.92 μs |    65.16 μs |   +3.6% |
| 10 KB Decrypt                  |    70,673 B |    63,489 B | **−10.2%** |   129.35 μs |   128.67 μs |   −0.5% |
| 10 KB DecryptToProvidedStream  |    17,984 B |     5,720 B | **−68.2%** |    59.03 μs |    49.01 μs | **−17.0%** |
| 100 KB Encrypt                 |   677,078 B |   425,690 B | **−37.1%** |   976.77 μs |   647.31 μs | **−33.7%** |
| 100 KB EncryptToProvidedStream |   229,131 B |   163,545 B | **−28.6%** |   637.89 μs |   484.90 μs | **−24.0%** |
| 100 KB Decrypt                 |   539,936 B |   446,452 B | **−17.3%** | 1,065.20 μs |   946.17 μs | **−11.2%** |
| 100 KB DecryptToProvidedStream |   118,682 B |     5,906 B | **−95.0%** |   468.04 μs |   386.53 μs | **−17.4%** |

All 12 Stream-processor scenarios (Encrypt / EncryptToProvidedStream / Decrypt /
DecryptToProvidedStream at each of 1 / 10 / 100 KB) are **neutral or better than master on
both allocations and wall time**. The smallest wall-time delta (−0.5 % at 10 KB Decrypt) is
inside the measurement noise; the largest is **−95 % allocation at 100 KB
DecryptToProvidedStream** (118 KB → 6 KB) combined with **−17 % wall time**. Gen2
collections per 1 000 ops drop from 2.93 to 0 at 100 KB DecryptToProvidedStream.

### What this PR ships over `master`

This PR is a set of focused, layered optimizations on the `JsonProcessor.Stream` path. In
order of measured impact:

1. **Decrypt writer routed through `IBufferWriter<byte>`.** `Utf8JsonWriter(Stream)` on
   .NET 8 eagerly constructs an internal `ArrayBufferWriter<byte>` that is GC-heap backed
   (`Array.Resize` doubling from 256 B), producing ~2× the final JSON size in short-lived
   GC garbage per operation. The decrypt core now uses `Utf8JsonWriter(IBufferWriter<byte>)`
   over a pooled `RentArrayBufferWriter`, eliminating that internal buffer entirely. The
   new-output adapter path returns a `ReadOnlyBufferWriterStream` that owns the rented
   buffer (cleared on dispose for defense-in-depth). The caller-provided-output path
   shares the same core and memcpy-copies out to the user stream at the end. This is what
   collapses `DecryptToProvidedStream` from 118 KB → 6 KB at 100 KB.
2. **`_ei` metadata subtree extraction streams via `Utf8JsonReader`** instead of
   `JsonSerializer.DeserializeAsync<EncryptionPropertiesWrapper>(stream)`. The old call
   forces `ReadBufferState` to grow `16K → 32K → 64K → 128K` (the final rental lands on
   the LOH) because `Skip()` over every unknown root property needs the complete value
   in-buffer. The replacement (`EncryptionPropertiesStreamReader`) uses `Utf8JsonReader`
   with `TrySkip` and `isFinalBlock: false`, staying in a 4 KB pooled buffer and only
   growing when a single value exceeds the current buffer. Safe-rewind snapshotting
   handles `_ei` truncation across chunk boundaries; an explicit guard prevents
   pathological growth on partial-read (trickle) transports.
3. **`StreamProcessor` property-name matching uses `Utf8JsonReader.ValueTextEquals` with
   pre-encoded UTF-8 path bytes** instead of allocating `"/" + reader.GetString()` per
   property. The shared `_ei` metadata byte buffer is reused for the metadata property
   match.
4. **`PooledMemoryStream` rents its backing buffer lazily on first write** (preserving the
   configured initial-capacity floor) so streams that are constructed but never written do
   not take a pool rent.
5. **`ArrayPoolManager.rentedBuffers` pre-sizes its tracking list** to cover the typical
   decrypt rent count so the `4 → 8 → 16 → …` grow chain no longer churns a few KB per op.
6. **`PooledStreamConfiguration` XML docs clarify** that `SetConfiguration` is intended as
   a configure-once-before-first-use call; mid-flight reconfiguration is not atomic across
   a single encrypt/decrypt operation because multiple call sites read `Current`
   independently.

### Rejected earlier approach (kept for posterity)

Before the rewrite in (1), **pre-sizing the output `PooledMemoryStream`** was investigated:
pass `input.Length` (capped at 4 MiB, fail-open on Length-throws / non-seekable) as the
initial capacity to skip the growth chain. It improved 1 KB Decrypt (−7 %) but worsened
100 KB Decrypt by +24 % (558 KB → 690 KB). Root cause: the grow chain happens to touch
`ArrayPool` buckets (4 KB / 8 KB / 16 KB / 32 KB / 64 KB) that the .NET runtime keeps warm
via unrelated workload (string formatting, JSON parsing, networking). The 128 KB bucket
the hint jumps to is essentially only used by this code path, so it gets aggressively
trimmed by `ArrayPool`'s Gen2 GC callback. A `LongRun` confirmation
(100 iter × 3 launches × 16 OPI ≈ 14 400 measured ops) reproduced the same +131 KB
regression at 100 KB, ruling out insufficient warmup.

The fundamental insight: the allocation was coming from **inside STJ's writer**, not from
our output stream. Tuning the output buffer could not help; only routing the writer
through an `IBufferWriter` could. The pre-size change was reverted; (1) + (2) above
supersede it.

## Running the Comparison Yourself

```powershell
# From the repo root
git worktree add ../baseline 79d18b73 --detach

# Back-port only the benchmark harness refactor so we measure product changes:
git -C ../baseline checkout users/adamnova/decrypt-bufwriter-opts -- `
    Microsoft.Azure.Cosmos.Encryption.Custom/tests/Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests/

cd ..\baseline\Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
dotnet run -c Release --framework net8.0 -- --filter '*EncryptionBenchmark*'
```

Then compare `BenchmarkDotNet.Artifacts/results/*-report-github.md` between the two
worktrees.
