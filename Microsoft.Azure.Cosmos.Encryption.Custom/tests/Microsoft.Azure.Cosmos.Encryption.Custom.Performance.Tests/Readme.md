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
| **Encrypt** |                **1** |     **Newtonsoft** |    **23.24 μs** |  **0.606 μs** |  **0.888 μs** |  **0.1526** |  **0.0305** |       **-** |   **36.44 KB** |
| Decrypt |                1 |     Newtonsoft |    24.95 μs |  0.230 μs |  0.344 μs |  0.1526 |  0.0305 |       - |   39.27 KB |
| **Encrypt** |                **1** | **SystemTextJson** |    **14.46 μs** |  **0.165 μs** |  **0.242 μs** |  **0.0916** |  **0.0153** |       **-** |   **22.48 KB** |
| Decrypt |                1 | SystemTextJson |    16.05 μs |  0.284 μs |  0.416 μs |  0.0916 |  0.0305 |       - |    22.1 KB |
| **Encrypt** |               **10** |     **Newtonsoft** |    **84.60 μs** |  **0.908 μs** |  **1.359 μs** |  **0.6104** |  **0.1221** |       **-** |  **166.64 KB** |
| Decrypt |               10 |     Newtonsoft |    98.39 μs |  0.856 μs |  1.255 μs |  0.6104 |  0.1221 |       - |  152.53 KB |
| **Encrypt** |               **10** | **SystemTextJson** |    **41.72 μs** |  **0.341 μs** |  **0.501 μs** |  **0.4272** |  **0.0610** |       **-** |  **102.99 KB** |
| Decrypt |               10 | SystemTextJson |    46.91 μs |  0.430 μs |  0.630 μs |  0.4272 |  0.0610 |       - |  105.06 KB |
| **Encrypt** |              **100** |     **Newtonsoft** | **1,072.91 μs** | **13.802 μs** | **20.231 μs** | **25.3906** | **21.4844** | **21.4844** | **1638.34 KB** |
| Decrypt |              100 |     Newtonsoft | 1,107.93 μs | 12.955 μs | 18.990 μs | 17.5781 | 15.6250 | 15.6250 | 1229.52 KB |
| **Encrypt** |              **100** | **SystemTextJson** |   **794.00 μs** | **25.274 μs** | **37.047 μs** | **24.4141** | **24.4141** | **24.4141** |  **942.73 KB** |
| Decrypt |              100 | SystemTextJson |   819.31 μs | 14.159 μs | 20.754 μs | 22.4609 | 22.4609 | 22.4609 | 1037.04 KB |
