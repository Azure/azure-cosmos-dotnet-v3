``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=9.0.100-rc.1.24452.12
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |     Gen1 |    Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|---------:|--------:|-----------:|
| **Encrypt** |                **1** |    **37.87 μs** |   **0.699 μs** |   **0.979 μs** |   **3.7842** |   **0.9766** |       **-** |   **47.03 KB** |
| Decrypt |                1 |    47.27 μs |   1.148 μs |   1.683 μs |   4.3945 |   1.0986 |       - |   54.17 KB |
| **Encrypt** |               **10** |   **113.49 μs** |   **1.380 μs** |   **1.934 μs** |  **15.1367** |   **3.0518** |       **-** |  **185.81 KB** |
| Decrypt |               10 |   146.93 μs |   1.724 μs |   2.581 μs |  19.5313 |   2.1973 |       - |  239.94 KB |
| **Encrypt** |              **100** | **1,610.57 μs** | **161.738 μs** | **242.081 μs** | **150.3906** | **107.4219** | **74.2188** | **1773.64 KB** |
| Decrypt |              100 | 2,058.97 μs | 202.464 μs | 303.039 μs | 160.1563 | 107.4219 | 76.1719 | 2042.61 KB |
