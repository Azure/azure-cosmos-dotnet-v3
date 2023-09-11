# Running benchmarks on Azure Virtual Machines

[Azure Arm Template](https://azure.microsoft.com/products/arm-templates/) makes executing the Azure Cosmos DB SDK Benchmark extremely easy. And reaching following abilities:

- Ability to execute benchmarking on multiple machines with one ARM Template
- Capturing diagnostics data for requests that exceed the latency threshold
- AppInsights: Live metrics (10s)
  - Success rate
  - Error
  - P90, P99, P999, P9999

| Parameter name                                          | Description                                            |
| ------------------------------------------------------ | ------------------------------------------------------ |
| projectName | Specifies a name for generating resource names. |
| location | Specifies the location for all resources. |
| adminUsername | Specifies a username for the Virtual Machine. |
| adminPassword | Specifies a password for the Virtual Machine. |
| vmSize | Specifies a Virtual Machine size |
| vNetName | Specifies a Virtual Network name |
| vNetAddressPrefixes | Specifies a Virtual Network Address Prefix |
| vNetSubnetName | Specifies a Virtual Network Subnet name |
| vNetSubnetAddressPrefix | Specifies a Virtual Network Subnet Address Prefix  |
| cosmosURI | Specifies the URI of the Cosmos DB account |
| cosmosKey | Specifies the key for the Cosmos DB account |
| throughput | Specifies Collection throughput use |
| operationsCount | Specifies the number of operations to execute |
| parallelism | Specifies the degree of parallelism |
| resultsContainer | Specifies the name of the container to which the results to be saved |
| vmCount | Specifies the number of Virtual Machines that will part of the test bench |
| workloadType | Specifies the workload |
| benchmarkingToolsBranchName | Specifies the GitHub branch for the benchmark tool source code repository |
| diagnosticsLatencyThresholdInMS | Specifies request latency threshold for capturing diagnostic data |
| storageAccountName | Specifies Storage Account Nmae |
| metricsReportingIntervalInSec | Specifies metrics reporting interval in seconds |
| applicationInsightsName | Specifies Application Insights Account Name |
| startDate | Specifies Diagnostic Blob storage container folder prefix  |

   [![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure%2Fazure-cosmos-dotnet-v3%2Fmaster%2FMicrosoft.Azure.Cosmos.Samples%2FTools%2FBenchmark%2FAzureVmBenchmark%2Fazuredeploy.json)

After benchmark was launched use Shared Dashboard for viewing charts with

## Workload types

| Workload Type                                          | Description                                            |
| ------------------------------------------------------ | ------------------------------------------------------ |
| InsertV3BenchmarkOperation                             | Inserts single document             |
| QueryStreamCrossPkDistinctFullDrainV3BenchmarkOperation | Execute a `DISTINCT` query cross partitions and access data using a steram |
| QueryStreamCrossPkDistinctWithPaginationV3BenchmarkOperation | Execute a `DISTINCT` query cross partitions and access data using a steram and pagination |
| QueryStreamCrossPkGroupByFullDrainV3BenchmarkOperation | Execute a `GROUP BY` query cross partitions and access data using a steram |
| QueryStreamCrossPkGroupByWithPaginationV3BenchmarkOperation | Execute a `GROUP BY` query cross partitions and access data using a steram and pagination |
| QueryStreamCrossPkOrderByFullDrainV3BenchmarkOperation | Execute a `ORDER BY` query cross partitions and access data using a steram |
| QueryStreamCrossPkOrderByWithPaginationV3BenchmarkOperation | Execute a `ORDER BY` query cross partitions and access data using a steram and pagination |
| QueryStreamCrossPkV3BenchmarkOperation | Execute `select * from T where T.id = @id` query cross partitions and access data using a steram |
| QueryStreamCrossPkWithPaginationV3BenchmarkOperation | Execute `select * from T where T.id = @id` query cross partitions and access data using a steram and pagination |
| QueryStreamSinglePkDistinctFullDrainV3BenchmarkOperation | Execute a `DISTINCT` query on a single partition and access data using a steram |
| QueryStreamSinglePkDistinctWithPaginationV3BenchmarkOperation | Execute a `DISTINCT` query on a single partition and access data using a steram and pagination |
| QueryStreamSinglePkGroupByFullDrainV3BenchmarkOperation | Execute a `GROUP BY` query on a single partition and access data using a steram |
| QueryStreamSinglePkGroupByWithPaginationV3BenchmarkOperation | Execute a `GROUP BY` query on a single partition and access data using a steram and pagination |
| QueryStreamSinglePkOrderByFullDrainV3BenchmarkOperation | Execute a `ORDER BY` query on a single partition and access data using a stream |
| QueryStreamSinglePkOrderByWithPaginationV3BenchmarkOperation | Execute a `ORDER BY` query on a single partition and access data using a steram and pagination |
| QueryStreamSinglePkV3BenchmarkOperation | Execute `select * from T where T.id = @id` query on a single partition and access data using a steram |
| QueryStreamSinglePkWithPaginationV3BenchmarkOperation | Execute `select * from T where T.id = @id` query on a single partition and access data using a steram and pagination |
| QueryTCrossPkDistinctFullDrainV3BenchmarkOperation | Execute a `DISTINCT` query cross partitions and access data |
| QueryTCrossPkDistinctWithPaginationV3BenchmarkOperation | Execute a `DISTINCT` query cross partitions and access data using a pagination |
| QueryTCrossPkGroupByFullDrainV3BenchmarkOperation | Execute a `GROUP BY` query cross partitions and access data |
| QueryTCrossPkGroupByWithPaginationV3BenchmarkOperation | Execute a `GROUP BY` query cross partitions and access data using a pagination |
| QueryTCrossPkOrderByFullDrainV3BenchmarkOperation | Execute a `ORDER BY` query cross partitions and access data |
| QueryTCrossPkOrderByWithPaginationV3BenchmarkOperation | Execute a `ORDER BY` query cross partitions and access data using a pagination |
| QueryTCrossPkV3BenchmarkOperation | Execute `select * from T where T.id = @id` query cross partitions partition and access data |
| QueryTCrossPkWithPaginationV3BenchmarkOperation | Execute `select * from T where T.id = @id` query cross partitions partition and access data using a pagination |
| QueryTSinglePkDistinctFullDrainV3BenchmarkOperation | Execute a `DISTINCT` query on a single partition and access data |
| QueryTSinglePkDistinctWithPaginationV3BenchmarkOperation | Execute a `DISTINCT` query on a single partition and access data using a pagination |
| QueryTSinglePkGroupByFullDrainV3BenchmarkOperation | Execute a `GROUP BY` query on a single partition and access data |
| QueryTSinglePkGroupByWithPaginationV3BenchmarkOperation | Execute a `GROUP BY` query on a single partition and access data using a pagination |
| QueryTSinglePkOrderByFullDrainV3BenchmarkOperation | Execute a `ORDER BY` query on a single partition and access data |
| QueryTSinglePkOrderByWithPaginationV3BenchmarkOperation | Execute a `ORDER BY` query on a single partition and access data using a pagination |
| QueryTSinglePkV3BenchmarkOperation | Execute `select * from T where T.id = @id` query on a single partition and access data |
| QueryTSinglePkWithPaginationV3BenchmarkOperation | Execute  `select * from T where T.id = @id` query on a single partition and access data using a pagination |
| ReadFeedStreamV3BenchmarkOperation | Execute Read Feed query |
| ReadNotExistsV3BenchmarkOperation | Execute query for not existing item |
| ReadStreamExistsV3BenchmarkOperation | Read item stream |
| ReadStreamExistsWithDiagnosticsV3BenchmarkOperation | Read item stream with diagnostics data |
| ReadTExistsV3BenchmarkOperation | Read single item |


## Diagnose and troubleshooting
The Benchmark tool output logs may be found on the each Virtual Machine in user home directory.

- Connect to VM using serial console
- Enter login and password

```bash
benchmarking@Benchmarking-dotnet-SDK-vm1:~$ ls

agent.err  agent.out
```

```bash
benchmarking@Benchmarking-dotnet-SDK-vm1:~$ cat agent.out 

CSC : warning CS8002: Referenced assembly 'MathNet.Numerics, Version=4.15.0.0, Culture=neutral, PublicKeyToken=null' does not have a strong name. [/var/lib/waagent/custom-script/download/2/azure-cosmos-dotnet-v3/Microsoft.Azure.Cosmos.Samples/Tools/Benchmark/CosmosBenchmark.csproj]
Azure VM Location:eastus
BenchmarkConfig arguments
IsServerGC: True
--------------------------------------------------------------------- 
{
  "WorkloadType": "ReadTExistsV3BenchmarkOperation",
  "EndPoint": "https://cosmosdb-benchmark-dotnet-test-thr.documents.azure.com:443/",
  "Database": "db",
  "Container": "data",
```
