``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|                  Method | DocumentSizeInKb | CompressionAlgorithm |  JsonProcessor |        Mean |     Error |    StdDev |      Median |    Gen0 |    Gen1 |    Gen2 | Allocated |
|------------------------ |----------------- |--------------------- |--------------- |------------:|----------:|----------:|------------:|--------:|--------:|--------:|----------:|
|                 **Encrypt** |                **1** |                 **None** |     **Newtonsoft** |    **22.53 μs** |  **0.511 μs** |  **0.733 μs** |    **22.29 μs** |  **0.1526** |  **0.0305** |       **-** |   **41784 B** |
| EncryptToProvidedStream |                1 |                 None |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |                1 |                 None |     Newtonsoft |    26.31 μs |  0.224 μs |  0.322 μs |    26.23 μs |  0.1526 |  0.0305 |       - |   41440 B |
| DecryptToProvidedStream |                1 |                 None |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |                 **None** |         **Stream** |    **12.85 μs** |  **0.095 μs** |  **0.143 μs** |    **12.84 μs** |  **0.0610** |  **0.0153** |       **-** |   **17528 B** |
| EncryptToProvidedStream |                1 |                 None |         Stream |    13.00 μs |  0.096 μs |  0.141 μs |    12.98 μs |  0.0458 |  0.0153 |       - |   11392 B |
|                 Decrypt |                1 |                 None |         Stream |    13.01 μs |  0.152 μs |  0.228 μs |    13.05 μs |  0.0458 |  0.0153 |       - |   12672 B |
| DecryptToProvidedStream |                1 |                 None |         Stream |    13.48 μs |  0.132 μs |  0.197 μs |    13.45 μs |  0.0458 |  0.0153 |       - |   11504 B |
|                 **Encrypt** |                **1** |               **Brotli** |     **Newtonsoft** |    **27.94 μs** |  **0.226 μs** |  **0.338 μs** |    **27.96 μs** |  **0.1526** |  **0.0305** |       **-** |   **38064 B** |
| EncryptToProvidedStream |                1 |               Brotli |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |                1 |               Brotli |     Newtonsoft |    33.49 μs |  0.910 μs |  1.335 μs |    33.99 μs |  0.1221 |       - |       - |   41064 B |
| DecryptToProvidedStream |                1 |               Brotli |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |               **Brotli** |         **Stream** |    **21.15 μs** |  **1.037 μs** |  **1.521 μs** |    **20.52 μs** |  **0.0610** |  **0.0305** |       **-** |   **16584 B** |
| EncryptToProvidedStream |                1 |               Brotli |         Stream |    20.57 μs |  0.213 μs |  0.292 μs |    20.57 μs |  0.0305 |       - |       - |   11672 B |
|                 Decrypt |                1 |               Brotli |         Stream |    21.14 μs |  2.212 μs |  3.311 μs |    19.46 μs |  0.0305 |       - |       - |   13216 B |
| DecryptToProvidedStream |                1 |               Brotli |         Stream |    19.60 μs |  0.439 μs |  0.600 μs |    19.52 μs |  0.0305 |       - |       - |   12048 B |
|                 **Encrypt** |               **10** |                 **None** |     **Newtonsoft** |    **84.82 μs** |  **3.002 μs** |  **4.208 μs** |    **83.32 μs** |  **0.6104** |  **0.1221** |       **-** |  **170993 B** |
| EncryptToProvidedStream |               10 |                 None |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |               10 |                 None |     Newtonsoft |   112.98 μs | 15.294 μs | 21.934 μs |   100.38 μs |  0.6104 |  0.1221 |       - |  157425 B |
| DecryptToProvidedStream |               10 |                 None |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |                 **None** |         **Stream** |    **39.63 μs** |  **0.658 μs** |  **0.923 μs** |    **39.41 μs** |  **0.3052** |  **0.0610** |       **-** |   **82928 B** |
| EncryptToProvidedStream |               10 |                 None |         Stream |    36.59 μs |  0.272 μs |  0.399 μs |    36.57 μs |  0.1221 |       - |       - |   37048 B |
|                 Decrypt |               10 |                 None |         Stream |    28.64 μs |  0.378 μs |  0.517 μs |    28.59 μs |  0.1221 |  0.0305 |       - |   29520 B |
| DecryptToProvidedStream |               10 |                 None |         Stream |    27.61 μs |  0.237 μs |  0.332 μs |    27.64 μs |  0.0610 |  0.0305 |       - |   18416 B |
|                 **Encrypt** |               **10** |               **Brotli** |     **Newtonsoft** |   **115.28 μs** |  **3.336 μs** |  **4.677 μs** |   **113.71 μs** |  **0.6104** |  **0.1221** |       **-** |  **168065 B** |
| EncryptToProvidedStream |               10 |               Brotli |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |               10 |               Brotli |     Newtonsoft |   118.98 μs |  1.530 μs |  2.195 μs |   118.76 μs |  0.4883 |       - |       - |  144849 B |
| DecryptToProvidedStream |               10 |               Brotli |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |               **Brotli** |         **Stream** |    **90.10 μs** |  **3.136 μs** |  **4.693 μs** |    **88.92 μs** |  **0.2441** |       **-** |       **-** |   **63809 B** |
| EncryptToProvidedStream |               10 |               Brotli |         Stream |    97.27 μs |  1.885 μs |  2.703 μs |    97.35 μs |  0.1221 |       - |       - |   32465 B |
|                 Decrypt |               10 |               Brotli |         Stream |    58.48 μs |  0.956 μs |  1.372 μs |    58.59 μs |  0.1221 |  0.0610 |       - |   30064 B |
| DecryptToProvidedStream |               10 |               Brotli |         Stream |    59.12 μs |  1.160 μs |  1.664 μs |    59.14 μs |  0.0610 |       - |       - |   18960 B |
|                 **Encrypt** |              **100** |                 **None** |     **Newtonsoft** | **1,199.74 μs** | **42.805 μs** | **64.069 μs** | **1,206.48 μs** | **23.4375** | **21.4844** | **21.4844** | **1677978 B** |
| EncryptToProvidedStream |              100 |                 None |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |              100 |                 None |     Newtonsoft | 1,177.48 μs | 25.746 μs | 38.535 μs | 1,172.04 μs | 17.5781 | 15.6250 | 15.6250 | 1260228 B |
| DecryptToProvidedStream |              100 |                 None |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |                 **None** |         **Stream** |   **636.72 μs** | **31.468 μs** | **47.099 μs** |   **630.15 μs** | **16.6016** | **16.6016** | **16.6016** |  **678066 B** |
| EncryptToProvidedStream |              100 |                 None |         Stream |   383.33 μs |  7.441 μs | 10.671 μs |   384.69 μs |  4.3945 |  4.3945 |  4.3945 |  230133 B |
|                 Decrypt |              100 |                 None |         Stream |   384.93 μs | 12.519 μs | 18.738 μs |   383.59 μs |  5.8594 |  5.8594 |  5.8594 |  230753 B |
| DecryptToProvidedStream |              100 |                 None |         Stream |   295.19 μs |  7.094 μs | 10.618 μs |   296.11 μs |  3.4180 |  3.4180 |  3.4180 |  119116 B |
|                 **Encrypt** |              **100** |               **Brotli** |     **Newtonsoft** | **1,178.06 μs** | **63.246 μs** | **94.664 μs** | **1,152.03 μs** | **13.6719** | **11.7188** |  **9.7656** | **1379183 B** |
| EncryptToProvidedStream |              100 |               Brotli |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |              100 |               Brotli |     Newtonsoft | 1,175.01 μs | 41.917 μs | 61.441 μs | 1,156.01 μs | 11.7188 |  9.7656 |  9.7656 | 1124274 B |
| DecryptToProvidedStream |              100 |               Brotli |     Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |               **Brotli** |         **Stream** |   **757.11 μs** | **19.549 μs** | **29.260 μs** |   **754.55 μs** | **10.7422** | **10.7422** | **10.7422** |  **479493 B** |
| EncryptToProvidedStream |              100 |               Brotli |         Stream |   563.46 μs |  9.960 μs | 14.284 μs |   561.60 μs |  2.9297 |  2.9297 |  2.9297 |  180637 B |
|                 Decrypt |              100 |               Brotli |         Stream |   542.34 μs | 14.514 μs | 21.724 μs |   542.04 μs |  6.8359 |  6.8359 |  6.8359 |  231162 B |
| DecryptToProvidedStream |              100 |               Brotli |         Stream |   463.69 μs |  9.130 μs | 12.800 μs |   460.71 μs |  3.4180 |  3.4180 |  3.4180 |  119506 B |
