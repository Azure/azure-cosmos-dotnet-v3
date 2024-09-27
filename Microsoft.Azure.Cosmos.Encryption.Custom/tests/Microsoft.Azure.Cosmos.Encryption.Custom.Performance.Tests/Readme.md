``` ini

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22631.4169)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=9.0.100-rc.1.24452.12
  [Host] : .NET 6.0.33 (6.0.3324.36610), X64 RyuJIT AVX2

Job=MediumRun  Toolchain=InProcessEmitToolchain  IterationCount=15  
LaunchCount=2  WarmupCount=10  

```
|  Method | DocumentSizeInKb |        Mean |      Error |     StdDev |     Gen0 |     Gen1 |     Gen2 |  Allocated |
|-------- |----------------- |------------:|-----------:|-----------:|---------:|---------:|---------:|-----------:|
| **Encrypt** |                **1** |    **61.45 μs** |   **1.676 μs** |   **2.457 μs** |   **4.9438** |   **1.2207** |        **-** |   **61.25 KB** |
| Decrypt |                1 |    77.89 μs |   1.959 μs |   2.933 μs |   5.7373 |   1.4648 |        - |   71.22 KB |
| **Encrypt** |               **10** |   **171.64 μs** |   **3.341 μs** |   **4.791 μs** |  **21.2402** |   **3.6621** |        **-** |  **260.97 KB** |
| Decrypt |               10 |   255.57 μs |   7.833 μs |  11.724 μs |  29.2969 |   4.3945 |        - |  363.84 KB |
| **Encrypt** |              **100** | **2,601.33 μs** | **215.481 μs** | **322.522 μs** | **199.2188** | **125.0000** | **123.0469** | **2464.88 KB** |
| Decrypt |              100 | 3,156.06 μs | 321.419 μs | 481.084 μs | 355.4688 | 300.7813 | 261.7188 | 3413.05 KB |
