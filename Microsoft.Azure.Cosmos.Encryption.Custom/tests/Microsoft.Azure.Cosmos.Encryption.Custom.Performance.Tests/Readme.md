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
| **Encrypt** |                **1** |    **81.01 μs** |   **2.595 μs** |   **3.804 μs** |   **4.7607** |   **1.5869** |        **-** |   **58.87 KB** |
| Decrypt |                1 |    68.96 μs |   1.627 μs |   2.333 μs |   5.0049 |   1.2207 |        - |   61.57 KB |
| **Encrypt** |               **10** |   **190.05 μs** |   **5.716 μs** |   **8.556 μs** |  **16.1133** |   **3.9063** |        **-** |  **197.66 KB** |
| Decrypt |               10 |   215.60 μs |   4.424 μs |   6.484 μs |  24.6582 |   4.8828 |        - |  303.41 KB |
| **Encrypt** |              **100** | **2,261.05 μs** | **212.952 μs** | **298.529 μs** | **148.4375** |  **78.1250** |  **74.2188** | **1785.47 KB** |
| Decrypt |              100 | 3,139.31 μs | 316.105 μs | 473.131 μs | 224.6094 | 175.7813 | 130.8594 | 3066.66 KB |
