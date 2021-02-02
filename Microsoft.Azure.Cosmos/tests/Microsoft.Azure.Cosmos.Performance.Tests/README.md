Sample usage pattern 

> dotnet run -c Release --framework net5.0 -- -j Medium -f *MockedItemBenchmark* -m --allStats --join

Run all benchmarks for gates:
dotnet run -c Release --framework net5.0 --allCategories=GateBenchmark -- -j Medium  -m  --BaselineValidation