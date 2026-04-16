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
- **Processor**: `Newtonsoft` (baseline), `Stream` (requires `ENCRYPTION_CUSTOM_PREVIEW` build flag)

## Running the Benchmarks

```powershell
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests

# Run all benchmarks (Newtonsoft only, default build)
dotnet run -c Release --framework net8.0

# Run with Stream processor comparison (requires preview flag)
dotnet run -c Release --framework net8.0 /p:DefineConstants="ENCRYPTION_CUSTOM_PREVIEW"

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

- The `EncryptToProvidedStream` and `DecryptToProvidedStream` benchmarks only work with the `Stream`
  processor. When run with `Newtonsoft`, they will report errors (this is expected).
- The `Stream` processor benchmarks require building with `ENCRYPTION_CUSTOM_PREVIEW` defined.
  Without this flag, only `Newtonsoft` benchmarks are available.
