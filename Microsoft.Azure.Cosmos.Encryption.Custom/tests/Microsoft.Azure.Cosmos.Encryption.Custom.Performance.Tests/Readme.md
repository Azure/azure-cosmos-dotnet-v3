``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=9.0.100-rc.1.24452.12
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |       Mean |     Error |    StdDev |     Gen0 |     Gen1 |     Gen2 |  Allocated |
|-------- |----------------- |-----------:|----------:|----------:|---------:|---------:|---------:|-----------:|
| **Encrypt** |                **1** |   **134.2 μs** |  **24.65 μs** |  **36.89 μs** |   **4.5166** |   **1.4648** |        **-** |   **56.71 KB** |
| Decrypt |                1 |   122.4 μs |   2.57 μs |   3.77 μs |   5.1270 |   1.5869 |        - |   64.01 KB |
| **Encrypt** |               **10** |   **309.7 μs** |  **40.78 μs** |  **61.04 μs** |  **14.6484** |   **3.9063** |        **-** |  **185.33 KB** |
| Decrypt |               10 |   435.5 μs |  28.40 μs |  40.73 μs |  21.4844 |   5.3711 |        - |  265.22 KB |
| **Encrypt** |              **100** | **3,666.4 μs** | **285.40 μs** | **418.34 μs** | **136.7188** |  **70.3125** |  **62.5000** | **1670.51 KB** |
| Decrypt |              100 | 4,928.2 μs | 441.88 μs | 661.39 μs | 195.3125 | 121.0938 | 101.5625 | 2617.38 KB |
