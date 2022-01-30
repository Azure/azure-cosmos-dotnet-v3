Sample usage pattern 

> dotnet run -c Release --framework net6.0 -- -j medium -f *MockedItemBenchmark* -m --allStats --join

Run all benchmarks for gates:
dotnet run -c Release --framework net6.0 --allCategories=GateBenchmark -- -j medium  -m  --BaselineValidation