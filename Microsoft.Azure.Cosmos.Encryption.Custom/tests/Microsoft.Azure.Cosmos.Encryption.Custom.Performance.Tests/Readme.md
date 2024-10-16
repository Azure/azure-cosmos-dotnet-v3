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
| **Encrypt** |                **1** |                 **None** |     **Newtonsoft** |    **22.84 μs** |  **0.676 μs** |  **1.013 μs** |  **0.1526** |  **0.0305** |       **-** |   **41.08 KB** |
| Decrypt |                1 |                 None |     Newtonsoft |    26.85 μs |  0.273 μs |  0.409 μs |  0.1526 |  0.0305 |       - |   40.47 KB |
| **Encrypt** |                **1** |                 **None** | **SystemTextJson** |    **15.41 μs** |  **0.224 μs** |  **0.335 μs** |  **0.0916** |  **0.0305** |       **-** |   **22.64 KB** |
| Decrypt |                1 |                 None | SystemTextJson |    14.41 μs |  0.121 μs |  0.174 μs |  0.0763 |  0.0153 |       - |   20.95 KB |
| **Encrypt** |                **1** |               **Brotli** |     **Newtonsoft** |    **28.44 μs** |  **0.246 μs** |  **0.369 μs** |  **0.1526** |  **0.0305** |       **-** |   **37.45 KB** |
| Decrypt |                1 |               Brotli |     Newtonsoft |    34.21 μs |  0.795 μs |  1.189 μs |  0.1221 |       - |       - |    40.1 KB |
| **Encrypt** |                **1** |               **Brotli** | **SystemTextJson** |    **21.68 μs** |  **0.264 μs** |  **0.378 μs** |  **0.0610** |       **-** |       **-** |   **21.71 KB** |
| Decrypt |                1 |               Brotli | SystemTextJson |    20.41 μs |  0.167 μs |  0.249 μs |  0.0610 |  0.0305 |       - |   20.01 KB |
| **Encrypt** |               **10** |                 **None** |     **Newtonsoft** |    **82.84 μs** |  **0.495 μs** |  **0.725 μs** |  **0.6104** |  **0.1221** |       **-** |  **167.26 KB** |
| Decrypt |               10 |                 None |     Newtonsoft |   100.04 μs |  0.733 μs |  1.096 μs |  0.6104 |  0.1221 |       - |  153.74 KB |
| **Encrypt** |               **10** |                 **None** | **SystemTextJson** |    **41.34 μs** |  **0.245 μs** |  **0.351 μs** |  **0.4272** |  **0.0610** |       **-** |  **103.15 KB** |
| Decrypt |               10 |                 None | SystemTextJson |    41.09 μs |  0.264 μs |  0.395 μs |  0.3662 |  0.0610 |       - |    94.2 KB |
| **Encrypt** |               **10** |               **Brotli** |     **Newtonsoft** |   **112.09 μs** |  **0.821 μs** |  **1.203 μs** |  **0.6104** |  **0.1221** |       **-** |   **164.4 KB** |
| Decrypt |               10 |               Brotli |     Newtonsoft |   119.50 μs |  1.371 μs |  1.966 μs |  0.4883 |       - |       - |  141.45 KB |
| **Encrypt** |               **10** |               **Brotli** | **SystemTextJson** |    **70.75 μs** |  **0.423 μs** |  **0.620 μs** |  **0.2441** |       **-** |       **-** |   **84.47 KB** |
| Decrypt |               10 |               Brotli | SystemTextJson |    64.51 μs |  1.042 μs |  1.560 μs |  0.2441 |       - |       - |   80.27 KB |
| **Encrypt** |              **100** |                 **None** |     **Newtonsoft** | **1,142.95 μs** | **36.247 μs** | **54.253 μs** | **23.4375** | **21.4844** | **19.5313** | **1638.94 KB** |
| Decrypt |              100 |                 None |     Newtonsoft | 1,160.91 μs | 26.561 μs | 39.755 μs | 17.5781 | 15.6250 | 15.6250 | 1230.71 KB |
| **Encrypt** |              **100** |                 **None** | **SystemTextJson** |   **835.31 μs** | **25.982 μs** | **38.084 μs** | **26.3672** | **26.3672** | **26.3672** |   **942.9 KB** |
| Decrypt |              100 |                 None | SystemTextJson |   731.05 μs | 23.379 μs | 33.530 μs | 18.5547 | 18.5547 | 18.5547 |     928 KB |
| **Encrypt** |              **100** |               **Brotli** |     **Newtonsoft** | **1,138.53 μs** | **21.347 μs** | **31.952 μs** | **13.6719** | **11.7188** |  **9.7656** |  **1347.1 KB** |
| Decrypt |              100 |               Brotli |     Newtonsoft | 1,150.43 μs | 15.475 μs | 22.684 μs | 11.7188 |  9.7656 |  9.7656 | 1097.91 KB |
| **Encrypt** |              **100** |               **Brotli** | **SystemTextJson** |   **994.72 μs** | **26.940 μs** | **39.489 μs** | **19.5313** | **19.5313** | **19.5313** |  **748.94 KB** |
| Decrypt |              100 |               Brotli | SystemTextJson |   886.36 μs | 14.437 μs | 21.162 μs | 17.5781 | 17.5781 | 17.5781 |  782.67 KB |
