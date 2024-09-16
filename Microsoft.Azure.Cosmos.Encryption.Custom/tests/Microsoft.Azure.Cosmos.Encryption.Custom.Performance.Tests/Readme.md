``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=9.0.100-rc.1.24452.12
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |     Gen1 |     Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|---------:|---------:|-----------:|
| **Encrypt** |                **1** |    **60.61 μs** |   **0.554 μs** |   **0.758 μs** |   **4.6997** |   **1.5869** |        **-** |   **57.72 KB** |
| Decrypt |                1 |    61.02 μs |   0.984 μs |   1.473 μs |   5.0049 |   1.2817 |        - |   61.57 KB |
| **Encrypt** |               **10** |   **158.70 μs** |   **3.114 μs** |   **4.565 μs** |  **15.8691** |   **3.9063** |        **-** |  **196.51 KB** |
| Decrypt |               10 |   189.60 μs |   1.248 μs |   1.829 μs |  24.6582 |   4.8828 |        - |  303.41 KB |
| **Encrypt** |              **100** | **1,773.67 μs** | **135.590 μs** | **202.944 μs** | **117.1875** |  **50.7813** |  **41.0156** | **1784.35 KB** |
| Decrypt |              100 | 2,787.37 μs | 264.329 μs | 395.636 μs | 230.4688 | 158.2031 | 136.7188 | 3067.03 KB |
