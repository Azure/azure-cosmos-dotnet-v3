``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|                  Method | DocumentSizeInKb | CompressionAlgorithm | JsonProcessor |        Mean |     Error |    StdDev |    Gen0 |    Gen1 |    Gen2 |  Allocated |
|------------------------ |----------------- |--------------------- |-------------- |------------:|----------:|----------:|--------:|--------:|--------:|-----------:|
|                 **Encrypt** |                **1** |                 **None** |    **Newtonsoft** |    **22.97 μs** |  **0.545 μs** |  **0.816 μs** |  **0.1526** |  **0.0305** |       **-** |    **41.2 KB** |
| EncryptToProvidedStream |                1 |                 None |    Newtonsoft |    21.46 μs |  0.105 μs |  0.153 μs |  0.1221 |  0.0305 |       - |   34.13 KB |
|                 Decrypt |                1 |                 None |    Newtonsoft |    27.28 μs |  0.149 μs |  0.219 μs |  0.1526 |  0.0305 |       - |   40.84 KB |
| DecryptToProvidedStream |                1 |                 None |    Newtonsoft |    34.38 μs |  1.292 μs |  1.934 μs |  0.1221 |       - |       - |    42.3 KB |
|                 **Encrypt** |                **1** |                 **None** |        **Stream** |    **12.10 μs** |  **0.081 μs** |  **0.116 μs** |  **0.0610** |  **0.0153** |       **-** |   **17.35 KB** |
| EncryptToProvidedStream |                1 |                 None |        Stream |    12.72 μs |  0.829 μs |  1.215 μs |  0.0458 |  0.0153 |       - |   11.36 KB |
|                 Decrypt |                1 |                 None |        Stream |    12.21 μs |  0.180 μs |  0.264 μs |  0.0458 |  0.0153 |       - |   12.38 KB |
| DecryptToProvidedStream |                1 |                 None |        Stream |    12.55 μs |  0.152 μs |  0.213 μs |  0.0458 |  0.0153 |       - |   11.23 KB |
|                 **Encrypt** |                **1** |               **Brotli** |    **Newtonsoft** |    **28.73 μs** |  **0.579 μs** |  **0.830 μs** |  **0.1526** |  **0.0305** |       **-** |   **37.59 KB** |
| EncryptToProvidedStream |                1 |               Brotli |    Newtonsoft |    28.58 μs |  0.293 μs |  0.411 μs |  0.1221 |  0.0305 |       - |   34.54 KB |
|                 Decrypt |                1 |               Brotli |    Newtonsoft |    35.35 μs |  0.894 μs |  1.337 μs |  0.1221 |       - |       - |   40.49 KB |
| DecryptToProvidedStream |                1 |               Brotli |    Newtonsoft |    38.17 μs |  0.409 μs |  0.574 μs |  0.1221 |       - |       - |   42.14 KB |
|                 **Encrypt** |                **1** |               **Brotli** |        **Stream** |    **19.77 μs** |  **0.275 μs** |  **0.395 μs** |  **0.0610** |  **0.0305** |       **-** |   **16.43 KB** |
| EncryptToProvidedStream |                1 |               Brotli |        Stream |    19.40 μs |  0.188 μs |  0.264 μs |  0.0305 |       - |       - |   11.63 KB |
|                 Decrypt |                1 |               Brotli |        Stream |    17.73 μs |  0.138 μs |  0.206 μs |  0.0305 |       - |       - |   12.65 KB |
| DecryptToProvidedStream |                1 |               Brotli |        Stream |    18.05 μs |  0.120 μs |  0.180 μs |  0.0305 |       - |       - |   11.51 KB |
|                 **Encrypt** |               **10** |                 **None** |    **Newtonsoft** |    **84.60 μs** |  **0.488 μs** |  **0.699 μs** |  **0.6104** |  **0.1221** |       **-** |  **168.82 KB** |
| EncryptToProvidedStream |               10 |                 None |    Newtonsoft |    82.21 μs |  0.199 μs |  0.272 μs |  0.4883 |       - |       - |   137.7 KB |
|                 Decrypt |               10 |                 None |    Newtonsoft |   101.88 μs |  0.452 μs |  0.676 μs |  0.6104 |  0.1221 |       - |  155.55 KB |
| DecryptToProvidedStream |               10 |                 None |    Newtonsoft |   107.81 μs |  0.595 μs |  0.890 μs |  0.6104 |  0.1221 |       - |  157.01 KB |
|                 **Encrypt** |               **10** |                 **None** |        **Stream** |    **37.80 μs** |  **0.181 μs** |  **0.266 μs** |  **0.3052** |  **0.0610** |       **-** |   **81.22 KB** |
| EncryptToProvidedStream |               10 |                 None |        Stream |    34.84 μs |  0.326 μs |  0.488 μs |  0.1221 |       - |       - |   36.41 KB |
|                 Decrypt |               10 |                 None |        Stream |    26.40 μs |  0.164 μs |  0.245 μs |  0.1221 |  0.0305 |       - |   28.83 KB |
| DecryptToProvidedStream |               10 |                 None |        Stream |    25.85 μs |  0.175 μs |  0.262 μs |  0.0610 |  0.0305 |       - |   17.98 KB |
|                 **Encrypt** |               **10** |               **Brotli** |    **Newtonsoft** |   **113.23 μs** |  **0.688 μs** |  **0.986 μs** |  **0.6104** |  **0.1221** |       **-** |  **165.98 KB** |
| EncryptToProvidedStream |               10 |               Brotli |    Newtonsoft |   111.05 μs |  0.535 μs |  0.801 μs |  0.4883 |       - |       - |  134.86 KB |
|                 Decrypt |               10 |               Brotli |    Newtonsoft |   122.44 μs |  1.023 μs |  1.499 μs |  0.4883 |       - |       - |  143.28 KB |
| DecryptToProvidedStream |               10 |               Brotli |    Newtonsoft |   127.32 μs |  0.892 μs |  1.308 μs |  0.4883 |       - |       - |  144.93 KB |
|                 **Encrypt** |               **10** |               **Brotli** |        **Stream** |    **84.20 μs** |  **2.861 μs** |  **4.193 μs** |  **0.2441** |       **-** |       **-** |   **62.55 KB** |
| EncryptToProvidedStream |               10 |               Brotli |        Stream |    92.70 μs |  1.253 μs |  1.876 μs |  0.1221 |       - |       - |   31.94 KB |
|                 Decrypt |               10 |               Brotli |        Stream |    54.23 μs |  0.528 μs |  0.775 μs |  0.1221 |       - |       - |    29.1 KB |
| DecryptToProvidedStream |               10 |               Brotli |        Stream |    54.34 μs |  0.505 μs |  0.756 μs |  0.0610 |       - |       - |   18.26 KB |
|                 **Encrypt** |              **100** |                 **None** |    **Newtonsoft** | **1,074.94 μs** | **17.781 μs** | **26.614 μs** | **21.4844** | **19.5313** | **19.5313** | **1654.89 KB** |
| EncryptToProvidedStream |              100 |                 None |    Newtonsoft |   908.83 μs | 44.365 μs | 62.193 μs | 11.7188 |  9.7656 |  9.7656 | 1143.65 KB |
|                 Decrypt |              100 |                 None |    Newtonsoft | 1,126.75 μs | 21.460 μs | 32.120 μs | 17.5781 | 15.6250 | 15.6250 | 1246.93 KB |
| DecryptToProvidedStream |              100 |                 None |    Newtonsoft | 1,183.13 μs | 19.585 μs | 29.314 μs | 15.6250 | 13.6719 | 13.6719 |  1248.4 KB |
|                 **Encrypt** |              **100** |                 **None** |        **Stream** |   **513.68 μs** | **11.309 μs** | **16.927 μs** | **16.6016** | **16.6016** | **16.6016** |  **662.42 KB** |
| EncryptToProvidedStream |              100 |                 None |        Stream |   335.51 μs |  7.015 μs | 10.500 μs |  4.3945 |  4.3945 |  4.3945 |  224.97 KB |
|                 Decrypt |              100 |                 None |        Stream |   310.34 μs |  5.028 μs |  7.525 μs |  6.3477 |  6.3477 |  6.3477 |  225.35 KB |
| DecryptToProvidedStream |              100 |                 None |        Stream |   264.40 μs |  3.169 μs |  4.545 μs |  3.4180 |  3.4180 |  3.4180 |  116.32 KB |
|                 **Encrypt** |              **100** |               **Brotli** |    **Newtonsoft** | **1,098.17 μs** | **10.860 μs** | **16.255 μs** | **13.6719** |  **9.7656** |  **9.7656** | **1363.12 KB** |
| EncryptToProvidedStream |              100 |               Brotli |    Newtonsoft | 1,012.03 μs |  9.265 μs | 13.581 μs |  7.8125 |  5.8594 |  5.8594 | 1107.87 KB |
|                 Decrypt |              100 |               Brotli |    Newtonsoft | 1,137.56 μs |  8.877 μs | 13.012 μs | 11.7188 |  9.7656 |  9.7656 | 1114.15 KB |
| DecryptToProvidedStream |              100 |               Brotli |    Newtonsoft | 1,160.69 μs |  9.399 μs | 13.777 μs | 11.7188 |  9.7656 |  9.7656 | 1115.79 KB |
|                 **Encrypt** |              **100** |               **Brotli** |        **Stream** |   **726.91 μs** | **10.086 μs** | **15.097 μs** | **11.7188** | **11.7188** | **11.7188** |  **468.53 KB** |
| EncryptToProvidedStream |              100 |               Brotli |        Stream |   551.89 μs |  6.359 μs |  9.518 μs |  2.9297 |  2.9297 |  2.9297 |  176.64 KB |
|                 Decrypt |              100 |               Brotli |        Stream |   517.81 μs |  7.945 μs | 11.891 μs |  6.3477 |  6.3477 |  6.3477 |  225.62 KB |
| DecryptToProvidedStream |              100 |               Brotli |        Stream |   440.63 μs |  4.781 μs |  7.007 μs |  3.4180 |  3.4180 |  3.4180 |   116.6 KB |
