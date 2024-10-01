``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.400
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |    Gen1 |    Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|--------:|--------:|-----------:|
| **Encrypt** |                **1** |    **37.28 μs** |   **0.676 μs** |   **0.991 μs** |   **3.7231** |  **0.9766** |       **-** |   **46.29 KB** |
| Decrypt |                1 |    43.12 μs |   1.753 μs |   2.623 μs |   3.6011 |  1.1597 |       - |   44.63 KB |
| **Encrypt** |               **10** |   **115.55 μs** |   **1.717 μs** |   **2.570 μs** |  **14.1602** |  **3.5400** |       **-** |  **174.92 KB** |
| Decrypt |               10 |   121.81 μs |   2.127 μs |   3.050 μs |  12.9395 |  3.1738 |       - |  159.56 KB |
| **Encrypt** |              **100** | **1,571.06 μs** | **128.737 μs** | **192.687 μs** | **142.5781** | **95.7031** | **66.4063** | **1660.08 KB** |
| Decrypt |              100 | 1,687.55 μs | 143.998 μs | 215.529 μs | 101.5625 | 62.5000 | 44.9219 | 1253.19 KB |
