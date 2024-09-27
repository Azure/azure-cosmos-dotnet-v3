``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.108
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |     Gen1 |     Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|---------:|---------:|-----------:|
| **Encrypt** |                **1** |    **45.81 μs** |   **0.753 μs** |   **1.080 μs** |   **4.8218** |   **1.2207** |        **-** |   **59.13 KB** |
| Decrypt |                1 |    57.30 μs |   1.049 μs |   1.570 μs |   5.3101 |   1.3428 |        - |   65.08 KB |
| **Encrypt** |               **10** |   **125.15 μs** |   **1.880 μs** |   **2.814 μs** |  **20.9961** |   **3.6621** |        **-** |  **258.85 KB** |
| Decrypt |               10 |   179.29 μs |   2.645 μs |   3.958 μs |  24.4141 |   5.1270 |        - |  301.63 KB |
| **Encrypt** |              **100** | **2,571.39 μs** | **245.773 μs** | **367.862 μs** | **201.1719** | **130.8594** | **125.0000** | **2462.75 KB** |
| Decrypt |              100 | 2,288.56 μs | 179.099 μs | 268.067 μs | 181.6406 | 119.1406 |  97.6563 |  2390.2 KB |
