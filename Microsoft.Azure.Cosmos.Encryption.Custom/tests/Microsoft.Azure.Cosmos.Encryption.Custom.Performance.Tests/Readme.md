``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.26100.2033)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.403
  [Host] : .NET 8.0.10 (8.0.1024.46610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|                  Method | DocumentSizeInKb | CompressionAlgorithm | JsonProcessor |      Mean |    Error |   StdDev |   Gen0 |   Gen1 |   Gen2 | Allocated |
|------------------------ |----------------- |--------------------- |-------------- |----------:|---------:|---------:|-------:|-------:|-------:|----------:|
|                 **Decrypt** |                **1** |                 **None** |        **Stream** |  **12.63 μs** | **0.146 μs** | **0.213 μs** | **0.0458** | **0.0153** |      **-** |  **12.31 KB** |
| DecryptToProvidedStream |                1 |                 None |        Stream |  12.58 μs | 0.082 μs | 0.115 μs | 0.0458 | 0.0153 |      - |   10.9 KB |
|                 **Decrypt** |                **1** |               **Brotli** |        **Stream** |  **19.30 μs** | **0.744 μs** | **1.066 μs** | **0.0305** |      **-** |      **-** |  **12.99 KB** |
| DecryptToProvidedStream |                1 |               Brotli |        Stream |  18.39 μs | 0.320 μs | 0.449 μs | 0.0305 |      - |      - |  11.58 KB |
|                 **Decrypt** |               **10** |                 **None** |        **Stream** |  **26.64 μs** | **0.150 μs** | **0.220 μs** | **0.1221** | **0.0305** |      **-** |  **28.77 KB** |
| DecryptToProvidedStream |               10 |                 None |        Stream |  25.71 μs | 0.138 μs | 0.203 μs | 0.0610 | 0.0305 |      - |  17.65 KB |
|                 **Decrypt** |               **10** |               **Brotli** |        **Stream** |  **53.89 μs** | **0.631 μs** | **0.945 μs** | **0.1221** | **0.0610** |      **-** |  **29.45 KB** |
| DecryptToProvidedStream |               10 |               Brotli |        Stream |  54.60 μs | 0.605 μs | 0.887 μs | 0.0610 |      - |      - |  18.33 KB |
|                 **Decrypt** |              **100** |                 **None** |        **Stream** | **450.58 μs** | **6.572 μs** | **9.837 μs** | **8.3008** | **8.3008** | **8.3008** | **320.02 KB** |
| DecryptToProvidedStream |              100 |                 None |        Stream | 379.11 μs | 4.473 μs | 6.415 μs | 4.3945 | 4.3945 | 4.3945 |    163 KB |
|                 **Decrypt** |              **100** |               **Brotli** |        **Stream** | **303.48 μs** | **5.305 μs** | **7.940 μs** | **5.8594** | **5.8594** | **5.8594** | **215.95 KB** |
| DecryptToProvidedStream |              100 |               Brotli |        Stream | 253.71 μs | 3.448 μs | 5.054 μs | 2.9297 | 2.9297 | 2.9297 | 111.63 KB |
