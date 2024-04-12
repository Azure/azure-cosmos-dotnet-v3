# Cosmos Performance Testing

The goal of this project is to provide a set of benchmarks to measure the performance of Cosmos SDK. The benchmarks are written using the BenchmarkDotNet. This project contains both end-to-end benchmarks and benchmarks using a mocked version of the Cosmos SDK to removed variablility from the network layer. 

## Running the benchmarks

To run all benchmarks for gates use the command:

```bash
dotnet run -c Release --framework net6.0 --allCategories=GateBenchmark -- -j Medium  -m  --BaselineValidation
```

To run a particular benchmark or set of benchmarks use the command:

```bash
dotnet run -c Release --framework net6.0 --allCategories=GateBenchmark -- -j Medium -f *SpecifyBenchmarkHere* -m --allStats --join
```

When doing this you can specify the name of the benchmark or benchmark class between the `*`s in the command above.
