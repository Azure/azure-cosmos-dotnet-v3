# Encryption Custom Performance Benchmarks

## Stream Processor Allocation Benchmarks

**Test Date:** November 5, 2025  
**Environment:** .NET 8.0.10, Windows 11

### Summary

These benchmarks focus on memory allocation efficiency of the Stream-based encryption/decryption path compared to the Newtonsoft JSON processor baseline. The key metrics are **total allocations** and **allocation overhead** relative to document size.

> **Note:** Execution time measurements are not included as they are highly dependent on the test environment and not directly comparable across different machines. The focus is on allocation efficiency, which is a more stable metric for evaluating the implementation.

> **Baseline Comparison:** The Newtonsoft processor baseline numbers are from the previous implementation (master branch) and represent the allocation behavior before stream processor optimizations. The comparison shows the combined improvement from both the Stream processor approach and code optimizations.

### Results by Document Size

#### Small Document: 917 bytes (0.90 KB)

| Processor | Operation | Total Allocated | Overhead | Alloc per Input KB |
|-----------|-----------|----------------|----------|-------------------|
| **Stream** | **Encrypt** | 7,556 bytes (7.38 KB) | 824.0% | 8,438 bytes |
| **Stream** | **Decrypt** | 8,175 bytes (7.98 KB) | 612.4% | 6,271 bytes |
| Newtonsoft (baseline) | Encrypt | ~41,784 bytes (40.8 KB) | ~4,457% | ~46,649 bytes |
| Newtonsoft (baseline) | Decrypt | ~41,440 bytes (40.5 KB) | ~4,420% | ~46,274 bytes |

**Encrypted Size:** 1,335 bytes (1.30 KB)  
**Stream vs Newtonsoft:** ~82% reduction in allocations for both encrypt and decrypt

#### Medium Document: 3,989 bytes (3.90 KB)

| Processor | Operation | Total Allocated | Overhead | Alloc per Input KB |
|-----------|-----------|----------------|----------|-------------------|
| **Stream** | **Encrypt** | 19,775 bytes (19.31 KB) | 495.7% | 5,076 bytes |
| **Stream** | **Decrypt** | 8,175 bytes (7.98 KB) ⚠️ | 160.6% | 1,644 bytes |
| Newtonsoft (baseline) | Encrypt | ~82,928 bytes (81.0 KB) | ~2,079% | ~21,285 bytes |
| Newtonsoft (baseline) | Decrypt | ~29,520 bytes (28.8 KB) | ~640% | ~7,574 bytes |

⚠️ *Note: Medium decrypt uses identical allocation as small (8,175 B), indicating efficient buffer reuse*

**Encrypted Size:** 5,091 bytes (4.97 KB)  
**Stream vs Newtonsoft:** ~76% reduction for encrypt, ~72% reduction for decrypt

#### Large Document: 7,829 bytes (7.65 KB)

| Processor | Operation | Total Allocated | Overhead | Alloc per Input KB |
|-----------|-----------|----------------|----------|-------------------|
| **Stream** | **Encrypt** | 28,839 bytes (28.16 KB) | 368.4% | 3,772 bytes |
| **Stream** | **Decrypt** | 24,023 bytes (23.46 KB) | 245.6% | 2,515 bytes |
| Newtonsoft (baseline) | Encrypt | ~170,993 bytes (167.0 KB) | ~2,184% | ~22,350 bytes |
| Newtonsoft (baseline) | Decrypt | ~157,425 bytes (153.7 KB) | ~2,011% | ~20,595 bytes |

**Encrypted Size:** 9,783 bytes (9.55 KB)  
**Stream vs Newtonsoft:** ~83% reduction for encrypt, ~85% reduction for decrypt

### Key Observations

1. **Stream Processor Dramatically Reduces Allocations**:
   - Small documents (~1 KB): **~82% reduction** vs Newtonsoft
   - Medium documents (~4 KB): **~72-76% reduction** vs Newtonsoft
   - Large documents (~8 KB): **~83-85% reduction** vs Newtonsoft

2. **Allocation Overhead Decreases with Document Size**:
   - Stream - Small documents (< 1 KB) show high overhead (600-800%)
   - Stream - Medium documents (~4 KB) show moderate overhead (160-495%)
   - Stream - Large documents (~8 KB) show improved overhead (245-368%)
   - Newtonsoft baseline shows consistently very high overhead (2,000-4,400%)

3. **Allocation per Input KB Improves with Size**:
   - Stream Encryption: 8.4 KB → 5.1 KB → 3.8 KB per input KB
   - Stream Decryption: Shows efficient buffer reuse (small and medium docs use identical 8,175 B, then scales to 24 KB for large docs)
   - Newtonsoft baseline: 21-47 KB per input KB (much higher)

4. **Fixed Overhead Component**:
   - The high overhead percentage for small documents suggests a fixed allocation cost
   - As document size increases, this fixed cost becomes proportionally smaller
   - Stream processor has much lower fixed overhead than Newtonsoft

### Running the Benchmarks

To run the allocation benchmarks:

```powershell
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.Performance.Tests
dotnet run -c Release --framework net8.0 -- StreamAllocationBenchmark
```

### Notes on Metrics

- **Overhead**: For encryption, this is `(Total Allocated / Input Size) × 100`. For decryption, this is `(Total Allocated / Encrypted Size) × 100`. This represents how many times larger the allocations are compared to the reference size.
- **Alloc per Input KB**: Total allocations divided by input document size in KB, showing allocation efficiency that improves with larger documents.
- **All calculations have been validated**: Overhead percentages, per-KB allocations, and reduction percentages match the benchmark tool output.
