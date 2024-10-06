``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.400
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |     Gen1 |    Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|---------:|--------:|-----------:|
| **Encrypt** |                **1** |    **37.15 μs** |   **0.683 μs** |   **1.002 μs** |   **3.8452** |   **0.9766** |       **-** |   **47.28 KB** |
| Decrypt |                1 |    45.29 μs |   0.757 μs |   1.062 μs |   4.3945 |   1.0986 |       - |   54.17 KB |
| **Encrypt** |               **10** |   **111.83 μs** |   **1.252 μs** |   **1.874 μs** |  **15.1367** |   **3.0518** |       **-** |  **186.07 KB** |
| Decrypt |               10 |   151.46 μs |   2.259 μs |   3.311 μs |  19.5313 |   2.1973 |       - |  239.94 KB |
| **Encrypt** |              **100** | **1,567.24 μs** | **153.944 μs** | **230.416 μs** | **152.3438** | **109.3750** | **76.1719** | **1773.95 KB** |
| Decrypt |              100 | 2,088.77 μs | 232.084 μs | 347.372 μs | 160.1563 | 113.2813 | 76.1719 | 2042.61 KB |
