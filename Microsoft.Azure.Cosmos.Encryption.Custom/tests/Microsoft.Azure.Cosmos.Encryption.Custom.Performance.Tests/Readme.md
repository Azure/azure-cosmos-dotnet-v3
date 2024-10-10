``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.400
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |  JsonProcessor |        Mean |     Error |    StdDev |    Gen0 |    Gen1 |    Gen2 |  Allocated |
|-------- |----------------- |--------------- |------------:|----------:|----------:|--------:|--------:|--------:|-----------:|
| **Encrypt** |                **1** |     **Newtonsoft** |    **22.40 μs** |  **0.342 μs** |  **0.501 μs** |  **0.1526** |  **0.0305** |       **-** |   **36.44 KB** |
| Decrypt |                1 |     Newtonsoft |    25.81 μs |  0.305 μs |  0.427 μs |  0.1526 |  0.0305 |       - |   39.19 KB |
| **Encrypt** |                **1** | **SystemTextJson** |    **14.72 μs** |  **0.275 μs** |  **0.411 μs** |  **0.0916** |  **0.0153** |       **-** |   **22.55 KB** |
| Decrypt |                1 | SystemTextJson |    25.69 μs |  0.290 μs |  0.396 μs |  0.1526 |  0.0305 |       - |   39.19 KB |
| **Encrypt** |               **10** |     **Newtonsoft** |    **83.98 μs** |  **0.437 μs** |  **0.613 μs** |  **0.6104** |  **0.1221** |       **-** |  **166.64 KB** |
| Decrypt |               10 |     Newtonsoft |    99.39 μs |  0.553 μs |  0.827 μs |  0.6104 |  0.1221 |       - |  152.45 KB |
| **Encrypt** |               **10** | **SystemTextJson** |    **41.92 μs** |  **0.212 μs** |  **0.304 μs** |  **0.4272** |  **0.0610** |       **-** |  **103.06 KB** |
| Decrypt |               10 | SystemTextJson |    99.43 μs |  0.558 μs |  0.835 μs |  0.6104 |  0.1221 |       - |  152.45 KB |
| **Encrypt** |              **100** |     **Newtonsoft** | **1,074.93 μs** | **11.946 μs** | **17.510 μs** | **25.3906** | **23.4375** | **21.4844** | **1638.32 KB** |
| Decrypt |              100 |     Newtonsoft | 1,133.11 μs | 20.544 μs | 29.463 μs | 17.5781 | 15.6250 | 15.6250 | 1229.43 KB |
| **Encrypt** |              **100** | **SystemTextJson** |   **797.64 μs** | **15.574 μs** | **22.828 μs** | **26.3672** | **26.3672** | **26.3672** |  **942.81 KB** |
| Decrypt |              100 | SystemTextJson | 1,120.97 μs | 14.956 μs | 22.386 μs | 19.5313 | 17.5781 | 17.5781 | 1229.45 KB |
