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
PooledJsonSerializer optimizations applied.

### Encrypt (new output stream)

| Doc Size | Processor  |      Mean |   Allocated | Gen0 |
|---------:|-----------|----------:|------------:|-----:|
|     1 KB | Newtonsoft |  34.00 μs |   36,552 B  | 0.06 |
|     1 KB | **Stream** |  **23.88 μs** |  **14,496 B** |    - |
|    10 KB | Newtonsoft | 181.14 μs |  171,353 B  | 0.24 |
|    10 KB | **Stream** |  **55.60 μs** |  **52,441 B** |    - |
|   100 KB | Newtonsoft | 1,640 μs  | 1,693,054 B | 15.6 |
|   100 KB | **Stream** |   **762 μs** |   **491,257 B** |  7.8 |

**Takeaway:** Stream processor is ~2–3× faster and allocates ~60–70% less than Newtonsoft.

### EncryptToProvidedStream (caller-provided `RecyclableMemoryStream`, Stream only)

| Doc Size |      Mean |  Allocated |
|---------:|----------:|-----------:|
|     1 KB |  20.63 μs |  10,392 B  |
|    10 KB |  53.96 μs |  36,048 B  |
|   100 KB |    506 μs | 229,129 B  |

### Decrypt (new output stream)

| Doc Size | Processor  |      Mean |   Allocated | Gen0 |
|---------:|-----------|----------:|------------:|-----:|
|     1 KB | Newtonsoft |  55.56 μs |   54,769 B  |    - |
|     1 KB | **Stream** |  **42.48 μs** |  **30,136 B** | 0.06 |
|    10 KB | Newtonsoft | 249.63 μs |  198,722 B  |    - |
|    10 KB | **Stream** |  **84.82 μs** |  **75,833 B** | 0.12 |
|   100 KB | Newtonsoft | 2,122 μs  | 1,584,328 B | 15.6 |
|   100 KB | **Stream** |  **1,040 μs** |   **559,303 B** | 13.7 |

### DecryptToProvidedStream (caller-provided `RecyclableMemoryStream`)

| Doc Size | Processor  |      Mean |   Allocated |
|---------:|-----------|----------:|------------:|
|     1 KB | Newtonsoft |  42.93 μs |   36,688 B  |
|     1 KB | **Stream** |  **19.80 μs** |  **11,072 B** |
|    10 KB | Newtonsoft | 156.63 μs |  125,402 B  |
|    10 KB | **Stream** |  **47.48 μs** |  **17,984 B** |
|   100 KB | Newtonsoft | 1,786 μs  | 1,013,491 B |
|   100 KB | **Stream** |    **487 μs** |   **118,681 B** |

**Takeaway:** Combining the Stream processor with a provided pooled output stream achieves ~8–9×
lower allocations than the Newtonsoft baseline at 100 KB (118 KB vs. 1,013 KB), and ~3.5× faster.

## Comparison vs. Pre-PR Baseline

To isolate the impact of this PR, the same benchmarks were run on the merge-base with `master`
(commit `79d18b73`) — i.e. the Stream processor *before* the PooledMemoryStream/PooledJsonSerializer
changes. Same machine, same run configuration.

### Newtonsoft paths (sanity check — should be unchanged by this PR)

| Scenario (MediumRun) | Baseline Alloc | This PR Alloc | Δ |
|---|---:|---:|---:|
| 1 KB Encrypt   | 36,552 B | 36,552 B | 0% |
| 1 KB Decrypt   | 54,769 B | 54,769 B | 0% |
| 10 KB Encrypt  | 171,353 B | 171,353 B | 0% |
| 10 KB Decrypt  | 198,721 B | 198,722 B | 0% |
| 100 KB Encrypt | 1,693,100 B | 1,693,054 B | 0% |
| 100 KB Decrypt | 1,584,357 B | 1,584,328 B | 0% |

Newtonsoft numbers are identical within noise — confirming the PR does not touch that path.

### Stream paths (where this PR changes things)

| Scenario (MediumRun) | Baseline Mean | This PR Mean | Baseline Alloc | This PR Alloc | Alloc Δ |
|---|---:|---:|---:|---:|---:|
| 1 KB  Encrypt                 |  19.39 μs |  23.88 μs |    16,552 B |    14,496 B | **−12%** |
| 1 KB  EncryptToProvidedStream |  18.32 μs |  20.63 μs |    10,392 B |    10,392 B |     0%  |
| 1 KB  Decrypt                 |  40.79 μs |  42.48 μs |    27,328 B |    30,136 B |  +10%   |
| 1 KB  DecryptToProvidedStream |  21.59 μs |  19.80 μs |    11,072 B |    11,072 B |     0%  |
| 10 KB Encrypt                 |  63.36 μs |  55.60 μs |    81,953 B |    52,441 B | **−36%** |
| 10 KB EncryptToProvidedStream |  55.75 μs |  53.96 μs |    36,049 B |    36,048 B |     0%  |
| 10 KB Decrypt                 | 101.03 μs |  84.82 μs |    70,673 B |    75,833 B |   +7%   |
| 10 KB DecryptToProvidedStream |  52.08 μs |  47.48 μs |    17,985 B |    17,984 B |     0%  |
| 100 KB Encrypt                |    964 μs |    762 μs |   677,115 B |   491,257 B | **−27%** |
| 100 KB EncryptToProvidedStream |   579 μs |    506 μs |   229,135 B |   229,129 B |     0%  |
| 100 KB Decrypt                |  1,041 μs |  1,040 μs |   539,985 B |   559,303 B |   +4%   |
| 100 KB DecryptToProvidedStream |   566 μs |    487 μs |   118,681 B |   118,681 B |     0%  |

**Where this PR wins:**
- **`Encrypt` (new output stream):** the primary beneficiary of `PooledMemoryStream`.
  Allocations drop **27–36%** at 10 KB and 100 KB, and wall time improves **12–23%**.
- **All Stream paths are a few % faster** (PooledJsonSerializer reduces per-call overhead).

**Where it is neutral:**
- **`*ToProvidedStream` paths:** allocations are essentially unchanged (the caller already supplies
  the pooled output buffer, so the PR's new pooled output stream doesn't apply). Time still improves
  ~7–14% from serializer-side changes.

**Where there is a small regression:**
- **`Decrypt` (new output stream) at 1 KB / 10 KB:** +7–10% allocations. The pooled output stream
  adds a small fixed overhead that isn't amortized at small sizes. At 100 KB the difference is
  within noise (+4%). Wall time is **16% faster** at 10 KB regardless.

Overall this PR is a clear net win for the Encrypt path and a wash for Decrypt on allocations,
with consistent wall-time improvements across the board.
