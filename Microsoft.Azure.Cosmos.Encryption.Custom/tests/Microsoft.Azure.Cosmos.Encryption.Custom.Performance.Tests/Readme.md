``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |  JsonProcessor |        Mean |     Error |    StdDev |    Gen0 |    Gen1 |    Gen2 |  Allocated |
|-------- |----------------- |--------------- |------------:|----------:|----------:|--------:|--------:|--------:|-----------:|
| **Encrypt** |                **1** |     **Newtonsoft** |    **22.23 μs** |  **0.392 μs** |  **0.562 μs** |  **0.1526** |  **0.0305** |       **-** |   **36.44 KB** |
| Decrypt |                1 |     Newtonsoft |    25.65 μs |  0.482 μs |  0.691 μs |  0.1221 |       - |       - |   39.27 KB |
| **Encrypt** |                **1** | **SystemTextJson** |    **15.61 μs** |  **0.886 μs** |  **1.242 μs** |  **0.0916** |  **0.0305** |       **-** |   **22.48 KB** |
| Decrypt |                1 | SystemTextJson |    14.68 μs |  0.334 μs |  0.479 μs |  0.0763 |  0.0153 |       - |   20.83 KB |
| **Encrypt** |               **10** |     **Newtonsoft** |    **83.23 μs** |  **1.608 μs** |  **2.147 μs** |  **0.6104** |  **0.1221** |       **-** |  **166.64 KB** |
| Decrypt |               10 |     Newtonsoft |   101.62 μs |  1.638 μs |  2.349 μs |  0.6104 |  0.1221 |       - |  152.53 KB |
| **Encrypt** |               **10** | **SystemTextJson** |    **41.49 μs** |  **0.317 μs** |  **0.464 μs** |  **0.4272** |  **0.0610** |       **-** |  **102.99 KB** |
| Decrypt |               10 | SystemTextJson |    41.53 μs |  0.505 μs |  0.725 μs |  0.3662 |  0.0610 |       - |   94.09 KB |
| **Encrypt** |              **100** |     **Newtonsoft** | **1,081.23 μs** | **13.538 μs** | **18.978 μs** | **25.3906** | **23.4375** | **21.4844** | **1638.32 KB** |
| Decrypt |              100 |     Newtonsoft | 1,135.00 μs | 32.719 μs | 45.867 μs | 17.5781 | 15.6250 | 15.6250 | 1229.52 KB |
| **Encrypt** |              **100** | **SystemTextJson** |   **819.27 μs** | **22.564 μs** | **33.074 μs** | **25.3906** | **25.3906** | **25.3906** |  **942.76 KB** |
| Decrypt |              100 | SystemTextJson |   698.30 μs | 20.402 μs | 29.905 μs | 21.4844 | 21.4844 | 21.4844 |  927.92 KB |
