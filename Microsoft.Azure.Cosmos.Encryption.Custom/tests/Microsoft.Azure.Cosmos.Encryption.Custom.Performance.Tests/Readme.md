``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=9.0.100-preview.7.24407.12
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |     Gen1 |     Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|---------:|---------:|-----------:|
| **Encrypt** |                **1** |    **47.90 μs** |   **1.284 μs** |   **1.842 μs** |   **4.5776** |   **1.1597** |        **-** |   **56.22 KB** |
| Decrypt |                1 |    59.67 μs |   1.041 μs |   1.558 μs |   5.2490 |   1.3428 |        - |   64.79 KB |
| **Encrypt** |               **10** |   **154.57 μs** |   **2.728 μs** |   **3.998 μs** |  **20.7520** |   **4.1504** |        **-** |  **255.95 KB** |
| Decrypt |               10 |   220.03 μs |   6.124 μs |   8.585 μs |  29.0527 |   5.8594 |        - |  357.41 KB |
| **Encrypt** |              **100** | **2,761.51 μs** | **213.677 μs** | **319.822 μs** | **218.7500** | **173.8281** | **142.5781** | **2459.89 KB** |
| Decrypt |              100 | 2,445.99 μs | 136.839 μs | 200.577 μs | 347.6563 | 300.7813 | 253.9063 | 3406.33 KB |
