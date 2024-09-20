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
| **Encrypt** |                **1** |    **44.46 μs** |   **0.661 μs** |   **0.969 μs** |   **4.2114** |   **1.0376** |        **-** |   **51.96 KB** |
| Decrypt |                1 |    56.00 μs |   1.062 μs |   1.589 μs |   5.0049 |   1.2817 |        - |   61.57 KB |
| **Encrypt** |               **10** |   **131.08 μs** |   **0.893 μs** |   **1.309 μs** |  **16.3574** |   **3.1738** |        **-** |   **200.9 KB** |
| Decrypt |               10 |   174.88 μs |   3.443 μs |   4.938 μs |  24.6582 |   4.8828 |        - |  303.41 KB |
| **Encrypt** |              **100** | **2,052.43 μs** | **230.487 μs** | **344.982 μs** | **160.1563** | **107.4219** |  **83.9844** | **1891.44 KB** |
| Decrypt |              100 | 2,791.54 μs | 284.376 μs | 425.641 μs | 234.3750 | 166.0156 | 140.6250 | 3066.91 KB |
