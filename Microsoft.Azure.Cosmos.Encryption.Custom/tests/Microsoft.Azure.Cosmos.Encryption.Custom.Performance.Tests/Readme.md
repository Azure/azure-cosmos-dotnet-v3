``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|                  Method | DocumentSizeInKb | CompressionAlgorithm | JsonProcessor |      Mean |     Error |    StdDev |   Gen0 |   Gen1 |   Gen2 | Allocated |
|------------------------ |----------------- |--------------------- |-------------- |----------:|----------:|----------:|-------:|-------:|-------:|----------:|
|                 **Decrypt** |                **1** |                 **None** |        **Stream** |  **12.80 μs** |  **0.161 μs** |  **0.237 μs** | **0.0458** | **0.0153** |      **-** |  **12.31 KB** |
| DecryptToProvidedStream |                1 |                 None |        Stream |  12.82 μs |  0.075 μs |  0.108 μs | 0.0458 | 0.0153 |      - |   10.9 KB |
|                 **Decrypt** |                **1** |               **Brotli** |        **Stream** |  **19.41 μs** |  **0.613 μs** |  **0.899 μs** | **0.0305** |      **-** |      **-** |  **12.99 KB** |
| DecryptToProvidedStream |                1 |               Brotli |        Stream |  18.52 μs |  0.206 μs |  0.288 μs | 0.0305 |      - |      - |  11.58 KB |
|                 **Decrypt** |               **10** |                 **None** |        **Stream** |  **26.96 μs** |  **0.103 μs** |  **0.148 μs** | **0.1221** | **0.0305** |      **-** |  **28.77 KB** |
| DecryptToProvidedStream |               10 |                 None |        Stream |  25.94 μs |  0.104 μs |  0.149 μs | 0.0610 | 0.0305 |      - |  17.65 KB |
|                 **Decrypt** |               **10** |               **Brotli** |        **Stream** |  **53.24 μs** |  **0.602 μs** |  **0.882 μs** | **0.1221** | **0.0610** |      **-** |  **29.45 KB** |
| DecryptToProvidedStream |               10 |               Brotli |        Stream |  54.38 μs |  0.471 μs |  0.691 μs | 0.0610 |      - |      - |  18.33 KB |
|                 **Decrypt** |              **100** |                 **None** |        **Stream** | **336.31 μs** |  **7.637 μs** | **11.194 μs** | **5.8594** | **5.8594** | **5.8594** | **225.27 KB** |
| DecryptToProvidedStream |              100 |                 None |        Stream | 283.21 μs |  2.668 μs |  3.993 μs | 2.9297 | 2.9297 | 2.9297 | 115.98 KB |
|                 **Decrypt** |              **100** |               **Brotli** |        **Stream** | **487.48 μs** |  **7.638 μs** | **11.433 μs** | **6.8359** | **6.8359** | **6.8359** | **225.84 KB** |
| DecryptToProvidedStream |              100 |               Brotli |        Stream | 457.04 μs | 10.030 μs | 14.384 μs | 3.4180 | 3.4180 | 3.4180 | 116.52 KB |
