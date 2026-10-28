[![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Azure.Cosmos)
[![NuGet Prerelease](https://img.shields.io/nuget/vpre/Microsoft.Azure.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Azure.Cosmos)

# Microsoft Azure Cosmos DB .NET SDK Version 3

This client library enables client applications to connect to Azure Cosmos DB for NoSQL. Azure Cosmos DB is a globally distributed, multi-model database service. For more information, refer to https://azure.microsoft.com/services/cosmos-db/.

```csharp
CosmosClient client = new CosmosClient("https://mycosmosaccount.documents.azure.com:443/", "mysupersecretkey");
Database database = await client.CreateDatabaseIfNotExistsAsync("MyDatabaseName");
Container container = await database.CreateContainerIfNotExistsAsync(
    "MyContainerName",
    "/partitionKeyPath",
    400);

// Create an item
dynamic testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };
ItemResponse<dynamic> createResponse = await container.CreateItemAsync(testItem);

// Query for an item
using (FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(
    "select * from T where T.status = 'done'"))
{
    while (feedIterator.HasMoreResults)
    {
        FeedResponse<dynamic> response = await feedIterator.ReadNextAsync();
        foreach (var item in response)
        {
            Console.WriteLine(item);
        }
    }
}
```

## Install via [Nuget.org](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/)

`Install-Package Microsoft.Azure.Cosmos`

For available versions, see [SDK versioning](./docs/versioning.md).

### Opt-in local query planning on Linux

On Linux x64 and ARM64, set `AZURE_COSMOS_USE_QUERY_INTEROP=true` before starting
the application to generate query plans locally using the bundled
`Microsoft.Azure.Cosmos.QueryPlanInterop.Linux` native library. Publish with
`linux-x64` or `linux-arm64` to include the matching native asset; RID-independent
NuGet builds retain both assets under `runtimes`.

The default remains gateway query planning on Linux. Windows keeps its existing
ServiceInterop behavior. `AZURE_COSMOS_BYPASS_QUERY_PARSING=true` overrides the
opt-in and forces gateway planning. If the native library or its dependencies
cannot load, the SDK logs the failure and uses gateway planning. Availability
is checked once per process; restart after fixing native dependencies.
This option changes query-plan generation only, not the client's Direct/Gateway
connection mode. Queries that can already skip query planning continue to do so.
Linux musl distributions are not supported by these glibc native assets.
The native libraries require glibc 2.17 or later and `libatomic.so.1` (for example,
the `libatomic1` package on Ubuntu).

PR CI runs `templates/build-linux-queryplaninterop.yml` with native planning enabled:

- `LinuxQueryPlanInterop` runs the account-free query-plan, routing, and baseline
  tests on Ubuntu 22.04 x64 and on Ubuntu 22.04 ARM64 (an ARM64 container under
  QEMU on the hosted x64 agent). Each leg checks the native library's
  dependencies with `ldd`, confirms the test process inherits
  `AZURE_COSMOS_USE_QUERY_INTEROP=true`, and requires two sentinel tests to pass:
  `OptedInLinuxProcessUsesNativePlanning` (the inherited flag routes queries to
  QueryPlanInterop) and `AbsFilterTargetsBothSigns` (the library plans a query). The
  ARM64 leg is non-blocking while the published ARM64 library fails to load with
  "cannot allocate memory in static TLS block".
- `LinuxQueryPlanInteropRuntimeImage` loads the library inside the stock
  `aspnet:8.0-jammy` and `aspnet:8.0-azurelinux3.0-distroless-extra` images for
  x64 and ARM64 and prints the exact `dlopen` error when it cannot. This lane is
  non-blocking while the package still needs `libatomic.so.1`, which these images
  do not include.
- `LinuxQueryPlanInteropLive` reuses the existing secret `COSMOSDB_MULTI_REGION`
  and runs the seven account-backed regressions tagged `LinuxQueryPlanInteropLive`
  on Ubuntu x64. The account must be reachable from hosted Ubuntu agents and allow
  key authentication, full-text/vector queries, and containers with 25,000 RU/s.
  Tests use temporary databases and delete only their own resources; the Unicode
  case reuses the legacy assertions without its account-wide cleanup.

Fork PRs do not run the secret-backed job. Missing credentials, native-load
failures, skipped tests, and existing query failures fail the job rather than
silently falling back or passing.

## Useful links

- [Get Started APP](https://docs.microsoft.com/azure/cosmos-db/sql-api-get-started)
- [GitHub samples](https://github.com/Azure/azure-cosmos-dotnet-v3/tree/main/Microsoft.Azure.Cosmos.Samples)
- [SDK Best practices](https://learn.microsoft.com/azure/cosmos-db/best-practice-dotnet)
- [MultiMaster samples](https://github.com/markjbrown/azure-cosmosdb-dotnet/tree/main/samples/MultiMaster)
- [Resource Model of Azure Cosmos DB Service](https://docs.microsoft.com/azure/cosmos-db/sql-api-resources)
- [Cosmos DB Resource URI](https://docs.microsoft.com/rest/api/documentdb/documentdb-resource-uri-syntax-for-rest)
- [Partitioning](https://docs.microsoft.com/azure/cosmos-db/partition-data)
- [Introduction to Azure Cosmos DB for NoSQL queries](https://docs.microsoft.com/azure/cosmos-db/sql-api-sql-query)
- [SDK API](https://docs.microsoft.com/dotnet/api/microsoft.azure.cosmos?view=azure-dotnet)
- [Using emulator](https://learn.microsoft.com/azure/cosmos-db/emulator)
- [Capture traces](https://github.com/Azure/azure-documentdb-dotnet/blob/main/docs/documentdb-sdk_capture_etl.md)
- [Release notes](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/main/changelog.md)
- [Diagnose and troubleshooting](https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk)

## Microsoft Open Source Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).

Resources:

- [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/)
- [Microsoft Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/)
- Contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with questions or concerns


## Contributing

For details on contributing to this repository, see the [contributing guide](CONTRIBUTING.md).

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
