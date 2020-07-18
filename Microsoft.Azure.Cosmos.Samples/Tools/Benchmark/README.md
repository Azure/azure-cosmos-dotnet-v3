# Microsoft Azure Cosmos DB .NET Benchmark tool

## Sample tool usage
```
dotnet run CosmosBenchmark.csproj -e {ACCOUNT_ENDPOINT} -k {ACCOUNT_KEY}
```

Dry tun targeting emulator will look like below
```
dotnet run CosmosBenchmark.csproj -e "https://localhost:8081" -k "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="
```

To target Microsoft.Azure.Cosmos\src\Microsoft.Azure.Cosmos.csproj
```
dotnet run CosmosBenchmark.csproj -p:OSSProjectRef=True -e {ACCOUNT_ENDPOINT} -k {ACCOUNT_KEY}
```


![image](https://user-images.githubusercontent.com/6880899/61565403-8e41bd00-aa96-11e9-9996-b7fc77c3aed3.png)

## Usage
```
>dotnet run CosmosBenchmark.csproj
CosmosBenchmark 1.0.0
Copyright (C) 2019 CosmosBenchmark

  -e                     Required. Cosmos account end point

  -k                     Required. Cosmos account master key

  --database             Database to use

  --container            Collection to use

  -t                     Collection throughput use

  -n                     Number of documents to insert

  --cleanuponstart       Start with new collection

  --cleanuponfinish      Clean-up after run

  --partitionkeypath     Container partition key path

  --pl                   Degree of parallism

  --itemtemplatefile     Item template

  --minthreadpoolsize    Min thread pool size

  --help                 Display this help screen.

  --version              Display version information.
```

## Running on Azure

If you quickly get results, you can use our [guide to leverage Azure Container Instances](./AzureContainerInstances/README.md) to execute the benchmarks in any number of Azure regions with very little setup required.