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
| **Encrypt** |                **1** |    **36.08 μs** |   **0.508 μs** |   **0.695 μs** |   **3.9063** |   **0.9766** |       **-** |   **48.42 KB** |
| Decrypt |                1 |    44.35 μs |   0.793 μs |   1.187 μs |   4.3945 |   1.0986 |       - |   53.96 KB |
| **Encrypt** |               **10** |   **114.38 μs** |   **1.323 μs** |   **1.940 μs** |  **15.8691** |   **3.1738** |       **-** |  **197.37 KB** |
| Decrypt |               10 |   144.66 μs |   2.856 μs |   4.275 μs |  19.5313 |   3.1738 |       - |  239.73 KB |
| **Encrypt** |              **100** | **1,771.21 μs** | **152.739 μs** | **228.613 μs** | **158.2031** | **119.1406** | **82.0313** | **1887.87 KB** |
| Decrypt |              100 | 2,001.18 μs | 174.355 μs | 260.966 μs | 160.1563 | 111.3281 | 76.1719 | 2042.39 KB |
