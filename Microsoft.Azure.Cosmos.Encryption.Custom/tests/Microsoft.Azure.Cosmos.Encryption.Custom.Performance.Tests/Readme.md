``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|                  Method | DocumentSizeInKb | CompressionAlgorithm |  JsonProcessor |        Mean |     Error |    StdDev |    Gen0 |    Gen1 |    Gen2 | Allocated |
|------------------------ |----------------- |--------------------- |--------------- |------------:|----------:|----------:|--------:|--------:|--------:|----------:|
|                 **Encrypt** |                **1** |                 **None** |     **Newtonsoft** |    **23.89 μs** |  **0.489 μs** |  **0.702 μs** |  **0.1526** |  **0.0305** |       **-** |   **42064 B** |
| EncryptToProvidedStream |                1 |                 None |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |                1 |                 None |     Newtonsoft |    28.65 μs |  0.340 μs |  0.488 μs |  0.1221 |       - |       - |   41440 B |
| DecryptToProvidedStream |                1 |                 None |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |                 **None** | **SystemTextJson** |    **15.67 μs** |  **0.206 μs** |  **0.309 μs** |  **0.0916** |  **0.0305** |       **-** |   **23184 B** |
| EncryptToProvidedStream |                1 |                 None | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |                1 |                 None | SystemTextJson |    15.46 μs |  0.124 μs |  0.186 μs |  0.0610 |  0.0305 |       - |   21448 B |
| DecryptToProvidedStream |                1 |                 None | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |                 **None** |         **Stream** |    **14.29 μs** |  **0.159 μs** |  **0.237 μs** |  **0.0610** |  **0.0153** |       **-** |   **18408 B** |
| EncryptToProvidedStream |                1 |                 None |         Stream |    14.03 μs |  0.225 μs |  0.323 μs |  0.0458 |  0.0153 |       - |   12272 B |
|                 Decrypt |                1 |                 None |         Stream |    13.75 μs |  0.258 μs |  0.370 μs |  0.0305 |       - |       - |   12456 B |
| DecryptToProvidedStream |                1 |                 None |         Stream |    14.10 μs |  0.056 μs |  0.082 μs |  0.0458 |  0.0153 |       - |   11288 B |
|                 **Encrypt** |                **1** |               **Brotli** |     **Newtonsoft** |    **29.79 μs** |  **0.287 μs** |  **0.429 μs** |  **0.1526** |  **0.0305** |       **-** |   **38344 B** |
| EncryptToProvidedStream |                1 |               Brotli |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |                1 |               Brotli |     Newtonsoft |    36.13 μs |  0.684 μs |  1.003 μs |  0.1221 |       - |       - |   41064 B |
| DecryptToProvidedStream |                1 |               Brotli |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |               **Brotli** | **SystemTextJson** |    **22.27 μs** |  **0.206 μs** |  **0.309 μs** |  **0.0610** |       **-** |       **-** |   **22232 B** |
| EncryptToProvidedStream |                1 |               Brotli | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |                1 |               Brotli | SystemTextJson |    21.63 μs |  0.161 μs |  0.240 μs |  0.0610 |  0.0305 |       - |   20488 B |
| DecryptToProvidedStream |                1 |               Brotli | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |               **Brotli** |         **Stream** |    **22.12 μs** |  **0.287 μs** |  **0.420 μs** |  **0.0610** |  **0.0305** |       **-** |   **17464 B** |
| EncryptToProvidedStream |                1 |               Brotli |         Stream |    22.00 μs |  0.212 μs |  0.305 μs |  0.0305 |       - |       - |   12552 B |
|                 Decrypt |                1 |               Brotli |         Stream |    19.76 μs |  0.131 μs |  0.196 μs |  0.0305 |       - |       - |   13000 B |
| DecryptToProvidedStream |                1 |               Brotli |         Stream |    20.27 μs |  0.194 μs |  0.290 μs |  0.0305 |       - |       - |   11832 B |
|                 **Encrypt** |               **10** |                 **None** |     **Newtonsoft** |    **90.20 μs** |  **1.743 μs** |  **2.609 μs** |  **0.6104** |  **0.1221** |       **-** |  **171273 B** |
| EncryptToProvidedStream |               10 |                 None |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |               10 |                 None |     Newtonsoft |   105.28 μs |  1.740 μs |  2.551 μs |  0.6104 |  0.1221 |       - |  157425 B |
| DecryptToProvidedStream |               10 |                 None |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |                 **None** | **SystemTextJson** |    **42.25 μs** |  **0.164 μs** |  **0.245 μs** |  **0.4272** |  **0.0610** |       **-** |  **105625 B** |
| EncryptToProvidedStream |               10 |                 None | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |               10 |                 None | SystemTextJson |    42.52 μs |  0.270 μs |  0.395 μs |  0.3662 |  0.0610 |       - |   96464 B |
| DecryptToProvidedStream |               10 |                 None | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |                 **None** |         **Stream** |    **43.70 μs** |  **0.568 μs** |  **0.850 μs** |  **0.3052** |  **0.0610** |       **-** |   **87488 B** |
| EncryptToProvidedStream |               10 |                 None |         Stream |    40.54 μs |  0.283 μs |  0.424 μs |  0.1221 |       - |       - |   41608 B |
|                 Decrypt |               10 |                 None |         Stream |    28.14 μs |  0.105 μs |  0.150 μs |  0.0916 |  0.0305 |       - |   29304 B |
| DecryptToProvidedStream |               10 |                 None |         Stream |    27.86 μs |  0.111 μs |  0.163 μs |  0.0610 |  0.0305 |       - |   18200 B |
|                 **Encrypt** |               **10** |               **Brotli** |     **Newtonsoft** |   **116.64 μs** |  **0.974 μs** |  **1.397 μs** |  **0.6104** |  **0.1221** |       **-** |  **168345 B** |
| EncryptToProvidedStream |               10 |               Brotli |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |               10 |               Brotli |     Newtonsoft |   124.43 μs |  0.985 μs |  1.475 μs |  0.4883 |       - |       - |  144849 B |
| DecryptToProvidedStream |               10 |               Brotli |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |               **Brotli** | **SystemTextJson** |    **72.28 μs** |  **0.366 μs** |  **0.514 μs** |  **0.2441** |       **-** |       **-** |   **86497 B** |
| EncryptToProvidedStream |               10 |               Brotli | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |               10 |               Brotli | SystemTextJson |    66.36 μs |  0.830 μs |  1.242 μs |  0.2441 |       - |       - |   82201 B |
| DecryptToProvidedStream |               10 |               Brotli | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |               **Brotli** |         **Stream** |    **91.08 μs** |  **3.161 μs** |  **4.731 μs** |  **0.2441** |       **-** |       **-** |   **68369 B** |
| EncryptToProvidedStream |               10 |               Brotli |         Stream |    97.09 μs |  1.725 μs |  2.581 μs |  0.1221 |       - |       - |   37025 B |
|                 Decrypt |               10 |               Brotli |         Stream |    57.83 μs |  0.657 μs |  0.963 μs |  0.1221 |  0.0610 |       - |   29848 B |
| DecryptToProvidedStream |               10 |               Brotli |         Stream |    57.69 μs |  0.554 μs |  0.830 μs |  0.0610 |       - |       - |   18744 B |
|                 **Encrypt** |              **100** |                 **None** |     **Newtonsoft** | **1,167.44 μs** | **45.257 μs** | **67.739 μs** | **25.3906** | **23.4375** | **21.4844** | **1678336 B** |
| EncryptToProvidedStream |              100 |                 None |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |              100 |                 None |     Newtonsoft | 1,182.35 μs | 23.743 μs | 34.803 μs | 17.5781 | 15.6250 | 15.6250 | 1260244 B |
| DecryptToProvidedStream |              100 |                 None |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |                 **None** | **SystemTextJson** |   **830.10 μs** | **27.127 μs** | **40.603 μs** | **25.3906** | **25.3906** | **25.3906** |  **965525 B** |
| EncryptToProvidedStream |              100 |                 None | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |              100 |                 None | SystemTextJson |   749.29 μs | 19.292 μs | 28.279 μs | 18.5547 | 18.5547 | 18.5547 |  950282 B |
| DecryptToProvidedStream |              100 |                 None | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |                 **None** |         **Stream** |   **637.45 μs** | **34.205 μs** | **51.196 μs** | **14.6484** | **14.6484** | **14.6484** |  **719521 B** |
| EncryptToProvidedStream |              100 |                 None |         Stream |   385.08 μs |  5.025 μs |  7.521 μs |  4.8828 |  4.3945 |  4.3945 |  271565 B |
|                 Decrypt |              100 |                 None |         Stream |   380.02 μs | 11.443 μs | 17.128 μs |  6.3477 |  6.3477 |  6.3477 |  230536 B |
| DecryptToProvidedStream |              100 |                 None |         Stream |   304.54 μs |  8.678 μs | 12.989 μs |  2.9297 |  2.9297 |  2.9297 |  118897 B |
|                 **Encrypt** |              **100** |               **Brotli** |     **Newtonsoft** | **1,172.02 μs** | **19.488 μs** | **29.169 μs** | **13.6719** | **11.7188** |  **9.7656** | **1379452 B** |
| EncryptToProvidedStream |              100 |               Brotli |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |              100 |               Brotli |     Newtonsoft | 1,153.94 μs | 12.008 μs | 17.602 μs | 11.7188 |  9.7656 |  9.7656 | 1124251 B |
| DecryptToProvidedStream |              100 |               Brotli |     Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |               **Brotli** | **SystemTextJson** |   **995.73 μs** | **27.736 μs** | **40.655 μs** | **21.4844** | **21.4844** | **21.4844** |  **766965 B** |
| EncryptToProvidedStream |              100 |               Brotli | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |              100 |               Brotli | SystemTextJson |   892.93 μs | 16.896 μs | 25.288 μs | 17.5781 | 17.5781 | 17.5781 |  801458 B |
| DecryptToProvidedStream |              100 |               Brotli | SystemTextJson |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |               **Brotli** |         **Stream** |   **770.06 μs** | **11.317 μs** | **16.939 μs** | **10.7422** | **10.7422** | **10.7422** |  **520929 B** |
| EncryptToProvidedStream |              100 |               Brotli |         Stream |   606.21 μs |  9.107 μs | 13.631 μs |  2.9297 |  2.9297 |  2.9297 |  222081 B |
|                 Decrypt |              100 |               Brotli |         Stream |   537.74 μs |  8.192 μs | 12.007 μs |  6.3477 |  6.3477 |  6.3477 |  230938 B |
| DecryptToProvidedStream |              100 |               Brotli |         Stream |   464.80 μs |  4.408 μs |  6.461 μs |  3.4180 |  3.4180 |  3.4180 |  119300 B |
