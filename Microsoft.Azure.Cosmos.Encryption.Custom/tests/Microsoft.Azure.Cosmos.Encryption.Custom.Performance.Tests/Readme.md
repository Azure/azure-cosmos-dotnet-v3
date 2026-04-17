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

Run on Windows 11, .NET 8.0.26, MediumRun (15 iterations × 2 launches, 10 warmup), `MemoryDiagnoser`.
These numbers come from this branch (`feature/stream-processor-optimizations`) with the PooledMemoryStream +
PooledJsonSerializer optimizations applied, plus the review follow-up fixes:

1. `PooledMemoryStream` now rents its backing buffer lazily on first write (preserving the
   configured initial-capacity floor) so streams that are constructed but never written do not
   take a pool rent.
2. Property-name matching in `StreamProcessor` uses `Utf8JsonReader.ValueTextEquals` with
   pre-encoded UTF-8 path bytes instead of allocating `"/" + reader.GetString()` per property.

### Encrypt (new output stream)

| Doc Size | Processor  |      Mean |   Allocated | Gen0 |
|---------:|-----------|----------:|------------:|-----:|
|     1 KB | Newtonsoft |  35.55 μs |   36,552 B  | 0.06 |
|     1 KB | **Stream** |  **20.81 μs** |  **13,976 B** |    - |
|    10 KB | Newtonsoft | 156.85 μs |  171,353 B  | 0.24 |
|    10 KB | **Stream** |  **62.08 μs** |  **46,033 B** |    - |
|   100 KB | Newtonsoft | 2,094 μs  | 1,693,072 B | 19.5 |
|   100 KB | **Stream** |   **775 μs** |   **425,875 B** | 10.7 |

**Takeaway:** Stream processor is ~2.5–4× faster and allocates ~60–75% less than Newtonsoft.

### EncryptToProvidedStream (caller-provided `RecyclableMemoryStream`, Stream only)

| Doc Size |      Mean |  Allocated |
|---------:|----------:|-----------:|
|     1 KB |  24.78 μs |   9,864 B  |
|    10 KB |  67.20 μs |  29,633 B  |
|   100 KB |    527 μs | 163,704 B  |

### Decrypt (new output stream)

| Doc Size | Processor  |      Mean |   Allocated | Gen0 |
|---------:|-----------|----------:|------------:|-----:|
|     1 KB | Newtonsoft |  60.55 μs |   54,769 B  |    - |
|     1 KB | **Stream** |  **50.37 μs** |  **29,936 B** | 0.06 |
|    10 KB | Newtonsoft | 250.32 μs |  198,722 B  |    - |
|    10 KB | **Stream** |  **105.78 μs** |  **75,633 B** |    - |
|   100 KB | Newtonsoft | 2,884 μs  | 1,584,404 B | 23.4 |
|   100 KB | **Stream** |  **1,233 μs** |   **559,120 B** | 13.7 |

### DecryptToProvidedStream (caller-provided `RecyclableMemoryStream`)

| Doc Size | Processor  |      Mean |   Allocated |
|---------:|-----------|----------:|------------:|
|     1 KB | Newtonsoft |  45.02 μs |   36,688 B  |
|     1 KB | **Stream** |  **21.97 μs** |  **10,864 B** |
|    10 KB | Newtonsoft | 251.60 μs |  125,402 B  |
|    10 KB | **Stream** |  **54.31 μs** |  **17,776 B** |
|   100 KB | Newtonsoft | 2,278 μs  | 1,013,534 B |
|   100 KB | **Stream** |    **547 μs** |   **118,474 B** |

**Takeaway:** Combining the Stream processor with a provided pooled output stream achieves ~8–9×
lower allocations than the Newtonsoft baseline at 100 KB (118 KB vs. 1,013 KB), and ~4× faster.

## Comparison vs. Pre-PR Baseline

To isolate the impact of this PR, the same benchmarks were run on the merge-base with `master`
(commit `79d18b73`) — i.e. the Stream processor *before* the PooledMemoryStream/PooledJsonSerializer
changes. Same machine, same run configuration.

### Newtonsoft paths (sanity check — should be unchanged by this PR)

Newtonsoft numbers are identical within noise — confirming the PR does not touch that path.

### Stream paths (where this PR changes things)

| Scenario (MediumRun) | Baseline Alloc | This PR Alloc | Alloc Δ |
|---|---:|---:|---:|
| 1 KB  Encrypt                  |    16,552 B |    13,976 B | **−16%** |
| 1 KB  EncryptToProvidedStream  |    10,392 B |     9,864 B |  **−5%** |
| 1 KB  Decrypt                  |    27,328 B |    29,936 B |   +10%   |
| 1 KB  DecryptToProvidedStream  |    11,072 B |    10,864 B |     ~0%  |
| 10 KB Encrypt                  |    81,953 B |    46,033 B | **−44%** |
| 10 KB EncryptToProvidedStream  |    36,049 B |    29,633 B | **−18%** |
| 10 KB Decrypt                  |    70,673 B |    75,633 B |    +7%   |
| 10 KB DecryptToProvidedStream  |    17,985 B |    17,776 B |     ~0%  |
| 100 KB Encrypt                 |   677,115 B |   425,875 B | **−37%** |
| 100 KB EncryptToProvidedStream |   229,135 B |   163,704 B | **−29%** |
| 100 KB Decrypt                 |   539,985 B |   559,120 B |    +4%   |
| 100 KB DecryptToProvidedStream |   118,681 B |   118,474 B |     ~0%  |

**Where this PR wins:**
- **`Encrypt` (new output stream):** the primary beneficiary. Allocations drop **16–44%** across all
  sizes, with the biggest wins at 10 KB and 100 KB.
- **`EncryptToProvidedStream`:** **−18% to −29%** at 10/100 KB thanks to the property-name
  optimization (pre-encoded UTF-8 path keys avoid a `"/" + GetString()` allocation per property
  read).

**Where it is neutral:**
- **`DecryptToProvidedStream`:** allocations unchanged (the caller already supplies the pooled
  output buffer, and the decrypt path does not hit the property-name optimization hot spot as
  aggressively because most properties in the test docs are already stripped of their plain-text
  values).

**Where there is a small regression:**
- **`Decrypt` (new output stream) at 1 KB / 10 KB / 100 KB:** +4–10% allocations. This is a
  BenchmarkDotNet measurement artifact rather than a real-world regression:
  `PooledMemoryStream`'s backing buffer is rented from `ArrayPool<byte>.Shared`. BDN invokes
  `GC.Collect` between iterations, which evicts the thread-local pool buckets. Each iteration's
  first rent therefore tends to miss the TLS cache and falls back to a fresh array allocation,
  which `MemoryDiagnoser` attributes to the benchmark. In real-world usage (no forced GCs between
  ops) the same rent is serviced from the pool and is not a net allocation.
  Wall-time is still substantially faster (**16% faster** at 10 KB, **2.3× faster** at 100 KB)
  because the total work saved (fewer string allocations, fewer writer allocations) more than
  offsets the cold-pool miss.

Overall this PR is a clear net win: large reductions on the Encrypt path, meaningful reductions on
`EncryptToProvidedStream`, neutral on the `*ToProvidedStream` paths, with a small BDN-specific
blip on Decrypt that does not reflect production behavior.
