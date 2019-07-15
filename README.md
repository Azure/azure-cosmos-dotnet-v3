# Microsoft Azure Cosmos DB .NET SDK Version 3.0

This project provides a client tools or utilities in .NET that makes it easy to interact with Azure Cosmos DB. Azure cosmos DB is published with nuget name [Microsoft.Azure.Cosmos](https://www.nuget.org/packages/Microsoft.Azure.Cosmos/).

## SDK release notes

<https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/changelog.md>

### SDK API

<https://docs.microsoft.com/dotnet/api/microsoft.azure.cosmos?view=azure-dotnet-preview>

## Samples

Our [Samples folder](https://github.com/Azure/azure-cosmos-dotnet-v3/tree/master/Microsoft.Azure.Cosmos.Samples/CodeSamples) is a good starting point.

```
CosmosClient client = new CosmosClient("https://mycosmosaccount.documents.azure.com:443/", "mysupersecretkey");
Cosmos.Database database = await client.CreateDatabaseIfNotExistsAsync("MyDatabaseName");
Container container = await database.CreateContainerIfNotExistsAsync(
    "MyContainerName",
    "/partitionKeyPath",
    400);

dynamic testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working" };
ItemResponse<dynamic> response = await container.CreateItemAsync(testItem);
```

## Diagnose and troubleshooting issues

<https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk>

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

