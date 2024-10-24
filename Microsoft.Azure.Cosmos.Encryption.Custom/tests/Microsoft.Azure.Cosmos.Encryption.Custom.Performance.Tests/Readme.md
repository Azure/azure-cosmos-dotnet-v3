``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|                  Method | DocumentSizeInKb | CompressionAlgorithm | JsonProcessor |        Mean |     Error |    StdDev |      Median |    Gen0 |    Gen1 |    Gen2 | Allocated |
|------------------------ |----------------- |--------------------- |-------------- |------------:|----------:|----------:|------------:|--------:|--------:|--------:|----------:|
|                 **Encrypt** |                **1** |                 **None** |    **Newtonsoft** |    **22.51 μs** |  **0.393 μs** |  **0.576 μs** |    **22.63 μs** |  **0.1526** |  **0.0305** |       **-** |   **41784 B** |
| EncryptToProvidedStream |                1 |                 None |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |                1 |                 None |    Newtonsoft |    27.10 μs |  0.124 μs |  0.174 μs |    27.07 μs |  0.1526 |  0.0305 |       - |   41440 B |
| DecryptToProvidedStream |                1 |                 None |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |                 **None** |        **Stream** |    **12.82 μs** |  **0.063 μs** |  **0.091 μs** |    **12.78 μs** |  **0.0610** |  **0.0153** |       **-** |   **17768 B** |
| EncryptToProvidedStream |                1 |                 None |        Stream |    12.86 μs |  0.127 μs |  0.190 μs |    12.86 μs |  0.0458 |  0.0153 |       - |   11632 B |
|                 Decrypt |                1 |                 None |        Stream |    12.90 μs |  0.169 μs |  0.253 μs |    12.89 μs |  0.0458 |  0.0153 |       - |   12672 B |
| DecryptToProvidedStream |                1 |                 None |        Stream |    13.60 μs |  0.189 μs |  0.271 μs |    13.58 μs |  0.0458 |  0.0153 |       - |   11504 B |
|                 **Encrypt** |                **1** |               **Brotli** |    **Newtonsoft** |    **28.87 μs** |  **0.346 μs** |  **0.474 μs** |    **28.74 μs** |  **0.1526** |  **0.0305** |       **-** |   **38064 B** |
| EncryptToProvidedStream |                1 |               Brotli |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |                1 |               Brotli |    Newtonsoft |    35.28 μs |  0.905 μs |  1.269 μs |    35.40 μs |  0.1221 |       - |       - |   41064 B |
| DecryptToProvidedStream |                1 |               Brotli |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |               **Brotli** |        **Stream** |    **21.52 μs** |  **0.750 μs** |  **1.026 μs** |    **21.21 μs** |  **0.0610** |  **0.0305** |       **-** |   **16824 B** |
| EncryptToProvidedStream |                1 |               Brotli |        Stream |    20.80 μs |  0.228 μs |  0.312 μs |    20.76 μs |  0.0305 |       - |       - |   11912 B |
|                 Decrypt |                1 |               Brotli |        Stream |    19.55 μs |  0.443 μs |  0.636 μs |    19.33 μs |  0.0305 |       - |       - |   13216 B |
| DecryptToProvidedStream |                1 |               Brotli |        Stream |    19.86 μs |  0.192 μs |  0.270 μs |    19.82 μs |  0.0305 |       - |       - |   12048 B |
|                 **Encrypt** |               **10** |                 **None** |    **Newtonsoft** |    **96.62 μs** | **10.278 μs** | **15.384 μs** |    **86.34 μs** |  **0.6104** |  **0.1221** |       **-** |  **170993 B** |
| EncryptToProvidedStream |               10 |                 None |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |               10 |                 None |    Newtonsoft |   106.98 μs |  3.407 μs |  5.100 μs |   104.40 μs |  0.6104 |  0.1221 |       - |  157425 B |
| DecryptToProvidedStream |               10 |                 None |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |                 **None** |        **Stream** |    **39.15 μs** |  **0.200 μs** |  **0.281 μs** |    **39.16 μs** |  **0.3052** |  **0.0610** |       **-** |   **83168 B** |
| EncryptToProvidedStream |               10 |                 None |        Stream |    39.27 μs |  2.127 μs |  2.982 μs |    39.00 μs |  0.1221 |       - |       - |   37288 B |
|                 Decrypt |               10 |                 None |        Stream |    28.94 μs |  0.369 μs |  0.518 μs |    28.91 μs |  0.0916 |  0.0305 |       - |   29520 B |
| DecryptToProvidedStream |               10 |                 None |        Stream |    27.56 μs |  0.167 μs |  0.235 μs |    27.54 μs |  0.0610 |  0.0305 |       - |   18416 B |
|                 **Encrypt** |               **10** |               **Brotli** |    **Newtonsoft** |   **116.87 μs** |  **0.707 μs** |  **0.991 μs** |   **116.89 μs** |  **0.6104** |  **0.1221** |       **-** |  **168065 B** |
| EncryptToProvidedStream |               10 |               Brotli |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |               10 |               Brotli |    Newtonsoft |   144.59 μs | 14.519 μs | 21.282 μs |   139.95 μs |  0.4883 |       - |       - |  144849 B |
| DecryptToProvidedStream |               10 |               Brotli |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |               **Brotli** |        **Stream** |    **91.28 μs** |  **3.359 μs** |  **5.027 μs** |    **89.89 μs** |  **0.2441** |       **-** |       **-** |   **64049 B** |
| EncryptToProvidedStream |               10 |               Brotli |        Stream |    98.61 μs |  1.831 μs |  2.741 μs |    99.15 μs |  0.1221 |       - |       - |   32705 B |
|                 Decrypt |               10 |               Brotli |        Stream |    60.11 μs |  1.366 μs |  2.044 μs |    59.71 μs |  0.1221 |  0.0610 |       - |   30064 B |
| DecryptToProvidedStream |               10 |               Brotli |        Stream |    58.15 μs |  1.689 μs |  2.422 μs |    58.25 μs |       - |       - |       - |   18960 B |
|                 **Encrypt** |              **100** |                 **None** |    **Newtonsoft** | **1,087.44 μs** | **15.865 μs** | **23.254 μs** | **1,085.47 μs** | **21.4844** | **19.5313** | **19.5313** | **1677999 B** |
| EncryptToProvidedStream |              100 |                 None |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |              100 |                 None |    Newtonsoft | 1,124.12 μs | 15.278 μs | 22.395 μs | 1,123.48 μs | 17.5781 | 15.6250 | 15.6250 | 1260236 B |
| DecryptToProvidedStream |              100 |                 None |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |                 **None** |        **Stream** |   **517.26 μs** |  **7.106 μs** | **10.636 μs** |   **520.35 μs** | **14.6484** | **14.6484** | **14.6484** |  **678303 B** |
| EncryptToProvidedStream |              100 |                 None |        Stream |   339.83 μs |  5.149 μs |  7.706 μs |   337.59 μs |  4.3945 |  4.3945 |  4.3945 |  230367 B |
|                 Decrypt |              100 |                 None |        Stream |   346.38 μs | 10.316 μs | 15.440 μs |   343.34 μs |  6.3477 |  6.3477 |  6.3477 |  230757 B |
| DecryptToProvidedStream |              100 |                 None |        Stream |   280.22 μs |  4.289 μs |  6.420 μs |   278.61 μs |  3.4180 |  3.4180 |  3.4180 |  119111 B |
|                 **Encrypt** |              **100** |               **Brotli** |    **Newtonsoft** | **1,113.95 μs** | **15.209 μs** | **22.764 μs** | **1,103.81 μs** | **13.6719** |  **9.7656** |  **9.7656** | **1379180 B** |
| EncryptToProvidedStream |              100 |               Brotli |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 Decrypt |              100 |               Brotli |    Newtonsoft | 1,138.03 μs |  8.340 μs | 12.224 μs | 1,137.53 μs | 11.7188 |  9.7656 |  9.7656 | 1124260 B |
| DecryptToProvidedStream |              100 |               Brotli |    Newtonsoft |          NA |        NA |        NA |          NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |               **Brotli** |        **Stream** |   **723.60 μs** | **10.132 μs** | **15.165 μs** |   **719.90 μs** | **11.7188** | **11.7188** | **11.7188** |  **479748 B** |
| EncryptToProvidedStream |              100 |               Brotli |        Stream |   551.93 μs |  7.420 μs | 10.641 μs |   550.24 μs |  2.9297 |  2.9297 |  2.9297 |  180882 B |
|                 Decrypt |              100 |               Brotli |        Stream |   540.31 μs | 12.842 μs | 19.222 μs |   542.34 μs |  6.8359 |  6.8359 |  6.8359 |  231164 B |
| DecryptToProvidedStream |              100 |               Brotli |        Stream |   452.60 μs |  3.476 μs |  5.203 μs |   452.38 μs |  3.4180 |  3.4180 |  3.4180 |  119509 B |
