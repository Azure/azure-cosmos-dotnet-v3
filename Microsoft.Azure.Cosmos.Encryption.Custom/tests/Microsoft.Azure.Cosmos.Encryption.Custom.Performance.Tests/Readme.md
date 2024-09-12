``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=9.0.100-preview.7.24407.12
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |     Gen1 |     Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|---------:|---------:|-----------:|
| **Encrypt** |                **1** |    **60.05 μs** |   **1.537 μs** |   **2.300 μs** |   **5.0659** |   **1.2817** |        **-** |   **62.65 KB** |
| Decrypt |                1 |    70.76 μs |   0.812 μs |   1.164 μs |   5.7373 |   1.4648 |        - |   71.22 KB |
| **Encrypt** |               **10** |   **165.23 μs** |   **3.741 μs** |   **5.365 μs** |  **21.2402** |   **3.6621** |        **-** |  **262.38 KB** |
| Decrypt |               10 |   231.32 μs |   4.627 μs |   6.635 μs |  29.5410 |   3.4180 |        - |  363.84 KB |
| **Encrypt** |              **100** | **2,572.40 μs** | **242.163 μs** | **362.458 μs** | **201.1719** | **126.9531** | **125.0000** | **2466.27 KB** |
| Decrypt |              100 | 2,952.48 μs | 397.387 μs | 557.081 μs | 255.8594 | 210.9375 | 160.1563 | 3412.88 KB |
