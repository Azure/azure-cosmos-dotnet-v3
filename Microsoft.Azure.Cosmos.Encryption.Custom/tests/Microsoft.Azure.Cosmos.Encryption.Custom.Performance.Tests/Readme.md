``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb | CompressionAlgorithm |  JsonProcessor |        Mean |     Error |    StdDev |    Gen0 |    Gen1 |    Gen2 |  Allocated |
|-------- |----------------- |--------------------- |--------------- |------------:|----------:|----------:|--------:|--------:|--------:|-----------:|
| **Encrypt** |                **1** |                 **None** |     **Newtonsoft** |    **22.40 μs** |  **0.387 μs** |  **0.580 μs** |  **0.1526** |  **0.0305** |       **-** |   **41.08 KB** |
| Decrypt |                1 |                 None |     Newtonsoft |    26.31 μs |  0.215 μs |  0.322 μs |  0.1526 |  0.0305 |       - |   40.39 KB |
| **Encrypt** |                **1** |                 **None** | **SystemTextJson** |    **14.84 μs** |  **0.245 μs** |  **0.358 μs** |  **0.0916** |  **0.0305** |       **-** |   **22.75 KB** |
| **Encrypt** |                **1** |               **Brotli** |     **Newtonsoft** |    **27.89 μs** |  **0.220 μs** |  **0.329 μs** |  **0.1526** |  **0.0305** |       **-** |   **37.45 KB** |
| Decrypt |                1 |               Brotli |     Newtonsoft |    33.39 μs |  0.612 μs |  0.878 μs |  0.1221 |       - |       - |   40.02 KB |
| **Encrypt** |                **1** |               **Brotli** | **SystemTextJson** |    **21.43 μs** |  **0.168 μs** |  **0.251 μs** |  **0.0916** |  **0.0305** |       **-** |   **21.82 KB** |
| **Encrypt** |               **10** |                 **None** |     **Newtonsoft** |    **82.79 μs** |  **0.439 μs** |  **0.643 μs** |  **0.6104** |  **0.1221** |       **-** |  **167.26 KB** |
| Decrypt |               10 |                 None |     Newtonsoft |    98.98 μs |  0.499 μs |  0.747 μs |  0.6104 |  0.1221 |       - |  153.66 KB |
| **Encrypt** |               **10** |                 **None** | **SystemTextJson** |    **40.74 μs** |  **0.214 μs** |  **0.321 μs** |  **0.4272** |  **0.0610** |       **-** |  **103.26 KB** |
| **Encrypt** |               **10** |               **Brotli** |     **Newtonsoft** |   **112.08 μs** |  **1.172 μs** |  **1.681 μs** |  **0.6104** |  **0.1221** |       **-** |   **164.4 KB** |
| Decrypt |               10 |               Brotli |     Newtonsoft |   117.21 μs |  0.920 μs |  1.349 μs |  0.4883 |       - |       - |  141.38 KB |
| **Encrypt** |               **10** |               **Brotli** | **SystemTextJson** |    **69.51 μs** |  **0.491 μs** |  **0.719 μs** |  **0.2441** |       **-** |       **-** |   **84.58 KB** |
| **Encrypt** |              **100** |                 **None** |     **Newtonsoft** | **1,165.87 μs** | **41.512 μs** | **62.133 μs** | **23.4375** | **21.4844** | **19.5313** | **1638.94 KB** |
| Decrypt |              100 |                 None |     Newtonsoft | 1,166.04 μs | 32.206 μs | 48.204 μs | 17.5781 | 15.6250 | 15.6250 | 1230.62 KB |
| **Encrypt** |              **100** |                 **None** | **SystemTextJson** |   **854.64 μs** | **35.123 μs** | **51.482 μs** | **21.4844** | **21.4844** | **21.4844** |  **942.96 KB** |
| **Encrypt** |              **100** |               **Brotli** |     **Newtonsoft** | **1,121.21 μs** | **24.814 μs** | **37.141 μs** | **13.6719** | **11.7188** |  **9.7656** | **1347.12 KB** |
| Decrypt |              100 |               Brotli |     Newtonsoft | 1,135.33 μs | 11.013 μs | 16.483 μs | 11.7188 |  9.7656 |  9.7656 | 1097.84 KB |
| **Encrypt** |              **100** |               **Brotli** | **SystemTextJson** |   **986.73 μs** | **19.142 μs** | **28.058 μs** | **21.4844** | **21.4844** | **21.4844** |  **749.06 KB** |
