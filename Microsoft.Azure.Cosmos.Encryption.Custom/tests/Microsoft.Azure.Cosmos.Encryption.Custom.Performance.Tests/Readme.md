``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4317)
Intel Core i7-8700 CPU 3.20GHz (Coffee Lake), 1 CPU, 12 logical and 6 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|                  Method | DocumentSizeInKb | CompressionAlgorithm | JsonProcessor |        Mean |     Error |    StdDev |    Gen0 |    Gen1 |    Gen2 | Allocated |
|------------------------ |----------------- |--------------------- |-------------- |------------:|----------:|----------:|--------:|--------:|--------:|----------:|
|                 **Encrypt** |                **1** |                 **None** |    **Newtonsoft** |    **37.85 μs** |  **1.110 μs** |  **1.627 μs** |  **0.4272** |  **0.0610** |       **-** |   **42264 B** |
| EncryptToProvidedStream |                1 |                 None |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |                1 |                 None |    Newtonsoft |    44.66 μs |  0.441 μs |  0.619 μs |  0.4272 |  0.0610 |       - |   41824 B |
| DecryptToProvidedStream |                1 |                 None |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |                 **None** |        **Stream** |    **22.58 μs** |  **0.125 μs** |  **0.175 μs** |  **0.1831** |  **0.0305** |       **-** |   **16960 B** |
| EncryptToProvidedStream |                1 |                 None |        Stream |    22.38 μs |  0.184 μs |  0.263 μs |  0.0916 |  0.0305 |       - |   10824 B |
|                 Decrypt |                1 |                 None |        Stream |    22.91 μs |  0.262 μs |  0.384 μs |  0.1221 |  0.0305 |       - |   12672 B |
| DecryptToProvidedStream |                1 |                 None |        Stream |    23.72 μs |  0.164 μs |  0.230 μs |  0.1221 |  0.0305 |       - |   11504 B |
|                 **Encrypt** |                **1** |               **Brotli** |    **Newtonsoft** |    **43.64 μs** |  **0.205 μs** |  **0.294 μs** |  **0.3662** |  **0.0610** |       **-** |   **38560 B** |
| EncryptToProvidedStream |                1 |               Brotli |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |                1 |               Brotli |    Newtonsoft |    53.46 μs |  0.820 μs |  1.177 μs |  0.4272 |  0.0610 |       - |   41465 B |
| DecryptToProvidedStream |                1 |               Brotli |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |                **1** |               **Brotli** |        **Stream** |    **32.38 μs** |  **1.145 μs** |  **1.713 μs** |  **0.1221** |       **-** |       **-** |   **16016 B** |
| EncryptToProvidedStream |                1 |               Brotli |        Stream |    31.38 μs |  0.300 μs |  0.440 μs |  0.0610 |       - |       - |   11104 B |
|                 Decrypt |                1 |               Brotli |        Stream |    29.75 μs |  0.623 μs |  0.933 μs |  0.1221 |  0.0610 |       - |   13104 B |
| DecryptToProvidedStream |                1 |               Brotli |        Stream |    31.54 μs |  0.848 μs |  1.243 μs |  0.1221 |  0.0610 |       - |   11936 B |
|                 **Encrypt** |               **10** |                 **None** |    **Newtonsoft** |   **150.68 μs** |  **1.072 μs** |  **1.572 μs** |  **1.7090** |  **0.2441** |       **-** |  **172946 B** |
| EncryptToProvidedStream |               10 |                 None |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |               10 |                 None |    Newtonsoft |   175.71 μs |  0.510 μs |  0.715 μs |  1.7090 |  0.2441 |       - |  159282 B |
| DecryptToProvidedStream |               10 |                 None |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |                 **None** |        **Stream** |    **84.55 μs** |  **0.921 μs** |  **1.290 μs** |  **0.7324** |  **0.1221** |       **-** |   **76473 B** |
| EncryptToProvidedStream |               10 |                 None |        Stream |    80.35 μs |  0.445 μs |  0.624 μs |  0.2441 |       - |       - |   30592 B |
|                 Decrypt |               10 |                 None |        Stream |    73.96 μs |  0.895 μs |  1.340 μs |  0.2441 |       - |       - |   29521 B |
| DecryptToProvidedStream |               10 |                 None |        Stream |    73.28 μs |  0.410 μs |  0.587 μs |  0.1221 |       - |       - |   18417 B |
|                 **Encrypt** |               **10** |               **Brotli** |    **Newtonsoft** |   **176.59 μs** |  **0.989 μs** |  **1.387 μs** |  **1.7090** |  **0.2441** |       **-** |  **170034 B** |
| EncryptToProvidedStream |               10 |               Brotli |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |               10 |               Brotli |    Newtonsoft |   188.99 μs |  1.414 μs |  2.028 μs |  1.4648 |  0.2441 |       - |  146722 B |
| DecryptToProvidedStream |               10 |               Brotli |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |               **10** |               **Brotli** |        **Stream** |   **119.56 μs** |  **2.676 μs** |  **4.005 μs** |  **0.6104** |  **0.1221** |       **-** |   **57353 B** |
| EncryptToProvidedStream |               10 |               Brotli |        Stream |   135.75 μs |  2.564 μs |  3.758 μs |  0.2441 |       - |       - |   26009 B |
|                 Decrypt |               10 |               Brotli |        Stream |    94.95 μs |  0.886 μs |  1.299 μs |  0.2441 |       - |       - |   29953 B |
| DecryptToProvidedStream |               10 |               Brotli |        Stream |    96.03 μs |  0.990 μs |  1.388 μs |  0.1221 |       - |       - |   18849 B |
|                 **Encrypt** |              **100** |                 **None** |    **Newtonsoft** | **1,840.46 μs** | **32.654 μs** | **48.875 μs** | **37.1094** | **35.1563** | **29.2969** | **1694760 B** |
| EncryptToProvidedStream |              100 |                 None |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |              100 |                 None |    Newtonsoft | 1,890.28 μs | 42.505 μs | 62.303 μs | 29.2969 | 21.4844 | 21.4844 | 1276883 B |
| DecryptToProvidedStream |              100 |                 None |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |                 **None** |        **Stream** | **1,087.73 μs** | **13.689 μs** | **19.632 μs** | **23.4375** | **23.4375** | **23.4375** |  **612646 B** |
| EncryptToProvidedStream |              100 |                 None |        Stream |   740.73 μs |  5.711 μs |  8.190 μs |  5.8594 |  5.8594 |  5.8594 |  164676 B |
|                 Decrypt |              100 |                 None |        Stream |   788.32 μs |  9.755 μs | 13.990 μs |  9.7656 |  9.7656 |  9.7656 |  230802 B |
| DecryptToProvidedStream |              100 |                 None |        Stream |   708.46 μs |  8.961 μs | 13.135 μs |  4.8828 |  4.8828 |  4.8828 |  119133 B |
|                 **Encrypt** |              **100** |               **Brotli** |    **Newtonsoft** | **1,651.60 μs** | **24.139 μs** | **34.620 μs** | **23.4375** | **19.5313** | **13.6719** | **1395951 B** |
| EncryptToProvidedStream |              100 |               Brotli |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 Decrypt |              100 |               Brotli |    Newtonsoft | 1,726.48 μs | 44.020 μs | 63.132 μs | 21.4844 | 19.5313 | 13.6719 | 1140913 B |
| DecryptToProvidedStream |              100 |               Brotli |    Newtonsoft |          NA |        NA |        NA |       - |       - |       - |         - |
|                 **Encrypt** |              **100** |               **Brotli** |        **Stream** | **1,058.70 μs** | **11.463 μs** | **16.803 μs** | **15.6250** | **15.6250** | **15.6250** |  **414066 B** |
| EncryptToProvidedStream |              100 |               Brotli |        Stream |   915.29 μs | 25.105 μs | 37.576 μs |  3.9063 |  3.9063 |  3.9063 |  115194 B |
|                 Decrypt |              100 |               Brotli |        Stream |   913.38 μs | 19.519 μs | 27.993 μs |  8.7891 |  8.7891 |  8.7891 |  231061 B |
| DecryptToProvidedStream |              100 |               Brotli |        Stream |   808.23 μs | 12.405 μs | 18.568 μs |  4.8828 |  4.8828 |  4.8828 |  119414 B |
