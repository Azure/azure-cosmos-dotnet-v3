# This branch contains a copy of the direct package files
This is very useful for troubleshooting issues in the HA/Transport stacks and being able to debug through the entire SDK. Most of the files are in the 'Microsoft.Azure.Cosmos\src\direct' folder. This branch does not include or use the service interop. This do to the difficulty of compiling native code while providing little benefit.

## How to update the branch and direct files

### 1. Merge the latest in github repository
If there are no breaking changes since the last direct sync everything should build and work correctly. If there are breaking changes the new direct files will need to be updated. Proceed to the next step.

### 2. Update the direct files
You need to have access to the Cosmos DB msdata repository to do this. It's recommended to sync to the branch the SDK is currently using instead of main branch. The main branch might have breaking or different behavior that is not yet in the direct nuget. Syncing to the direct branch will give the exact code used in the public SDK.

1. Direct version is listed here: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Directory.Build.props#L7
2. Open msdata repo and navigate to all branches: https://msdata.visualstudio.com/CosmosDB/_git/CosmosDB/branches?_a=all
3. Find the direct branch under sdkReleases/direct/{actual branch like EN20220301_3.28.1} and favorite it
4. On your local machine checkout that branch
5. Run the DirectUpdateFiles.ps1 script that is inside this branch. This assumes the github repo is at 'C:\azure-cosmos-dotnet-v3\Microsoft.Azure.Cosmos\src\direct" and the msdata repo is at 'C:\CosmosDB\'. If your repositories are not at that location you will need to update the script: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/direct/main/DirectUpdateFiles.ps1
6. Try to compile the project. If the build fails because of missing files then the script will need to be updated to include those new files. All the files should be listed in the msdata repo with the project 'Microsoft.Azure.Cosmos.Direct.csproj'


[![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Azure.Cosmos)
[![NuGet Prerelease](https://img.shields.io/nuget/vpre/Microsoft.Azure.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Azure.Cosmos)

# Microsoft Azure Cosmos DB .NET SDK Version 3

This client library enables client applications to connect to Azure Cosmos via the SQL API. Azure Cosmos is a globally distributed, multi-model database service. For more information, refer to https://azure.microsoft.com/services/cosmos-db/.

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
using (FeedIterator<dynamic> feedIterator = await container.GetItemQueryIterator<dynamic>(
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

## Useful links

- [Get Started APP](https://docs.microsoft.com/azure/cosmos-db/sql-api-get-started)
- [GitHub samples](https://github.com/Azure/azure-cosmos-dotnet-v3/tree/master/Microsoft.Azure.Cosmos.Samples)
- [SDK Best practices](https://docs.microsoft.com/azure/cosmos-db/sql/best-practice-dotnet)
- [MultiMaster samples](https://github.com/markjbrown/azure-cosmosdb-dotnet/tree/master/samples/MultiMaster)
- [Resource Model of Azure Cosmos DB Service](https://docs.microsoft.com/azure/cosmos-db/sql-api-resources)
- [Cosmos DB Resource URI](https://docs.microsoft.com/rest/api/documentdb/documentdb-resource-uri-syntax-for-rest)
- [Partitioning](https://docs.microsoft.com/azure/cosmos-db/partition-data)
- [Introduction to SQL API of Azure Cosmos DB Service](https://docs.microsoft.com/azure/cosmos-db/sql-api-sql-query)
- [SDK API](https://docs.microsoft.com/dotnet/api/microsoft.azure.cosmos?view=azure-dotnet)
- [Using emulator](https://github.com/Azure/azure-documentdb-dotnet/blob/master/docs/documentdb-nosql-local-emulator.md)
- [Capture traces](https://github.com/Azure/azure-documentdb-dotnet/blob/master/docs/documentdb-sdk_capture_etl.md)
- [Release notes](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/changelog.md)
- [Diagnose and troubleshooting](https://docs.microsoft.com/azure/cosmos-db/troubleshoot-dot-net-sdk)

## Microsoft Open Source Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).

Resources:

- [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/)
- [Microsoft Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/)
- Contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with questions or concerns


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
