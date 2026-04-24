``` ini

BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.26200.8246), VM=Hyper-V
Unknown processor
.NET SDK=10.0.102
  [Host]     : .NET 6.0.36 (6.0.3624.51421), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.36 (6.0.3624.51421), X64 RyuJIT AVX2


```
|         Method |     Mean |    Error |   StdDev |   Gen0 | Allocated |
|--------------- |---------:|---------:|---------:|-------:|----------:|
| ReadItemStream | 57.74 μs | 0.724 μs | 0.642 μs | 0.9766 |  26.41 KB |
