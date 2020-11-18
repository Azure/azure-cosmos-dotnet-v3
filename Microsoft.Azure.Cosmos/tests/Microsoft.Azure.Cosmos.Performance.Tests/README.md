Sample usage pattern 

> dotnet run -c Release --framework netcoreapp3.1 -- -j Medium -f *MockedItemBenchmark* -m --allStats --join

Run all benchmarks for gates:
dotnet run -c Release --framework netcoreapp3.1 --allCategories=GateBenchmark -- -j Medium  -m  --BaselineValidation