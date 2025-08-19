[![NuGet](https://img.shields.io/nuget/v/Microsoft.Azure.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Azure.Cosmos)
[![NuGet Prerelease](https://img.shields.io/nuget/vpre/Microsoft.Azure.Cosmos.svg)](https://www.nuget.org/packages/Microsoft.Azure.Cosmos)

# Microsoft Azure Cosmos DB .NET SDK AOT

This client library enables client applications to connect to Azure Cosmos DB for NoSQL. Azure Cosmos DB is a globally distributed, multi-model database service. For more information, refer to https://azure.microsoft.com/services/cosmos-db/.


# Feature supported
 The following stream based features are supported

 * Get Account Details
 * Get Databases
 * Get Containers
 * Read Document Collection
 * Query for items.

```csharp
CosmosClient client = new CosmosClient("https://mycosmosaccount.documents.azure.com:443/", "mysupersecretkey");

//Read Account Details.
AccountProperties accountProperties = await client.ReadAccountAsync();
Console.WriteLine($"Account Name: {accountProperties.Id}");

//Read Databases and Containers.

// Read Databases from the Cosmos account as Feed Stream.
FeedIterator db_feed_itr = client.GetDatabaseQueryStreamIterator();
while (db_feed_itr.HasMoreResults)
{
    ResponseMessage db_response = await db_feed_itr.ReadNextAsync();
    if (db_response.IsSuccessStatusCode)
    {
        using JsonDocument dbsQueryResultDoc = JsonDocument.Parse(db_response.Content);

        if (dbsQueryResultDoc.RootElement.TryGetProperty("Databases", out JsonElement documentsElement))
        {
            //Extract database from database array.
            foreach (JsonElement databaseElement in documentsElement.EnumerateArray())
            {
                //Extract database ids.
                string? databaseId = databaseElement.GetProperty("id").GetString();
                Console.WriteLine($"Database: {databaseId}");

                Database database = client.GetDatabase(databaseId);
                // Read Containers from the database as Feed Stream.
                FeedIterator container_feed_itr = database.GetContainerQueryStreamIterator();

                while (container_feed_itr.HasMoreResults)
                {
                    ResponseMessage cont_response = await container_feed_itr.ReadNextAsync();
                    if (cont_response.IsSuccessStatusCode)
                    {
                        using JsonDocument containersQueryResultDoc = JsonDocument.Parse(cont_response.Content);
                        Console.WriteLine($"Raw JSON (cont result): {containersQueryResultDoc.RootElement.GetRawText()}");

                        if (containersQueryResultDoc.RootElement.TryGetProperty("DocumentCollections", out JsonElement containersElement))
                        {
                            foreach (JsonElement containerElement in containersElement.EnumerateArray())
                            {
                                string? containerId = containerElement.GetProperty("id").GetString();
                                string? PartitionKeyPath = containerElement.TryGetProperty("partitionKey", out JsonElement partitionKeyElement)
                                    ? partitionKeyElement.GetProperty("paths").EnumerateArray().FirstOrDefault().GetString()
                                    : null;
                                Console.WriteLine($"Container: {containerId} PartitionKeyPath: {PartitionKeyPath}");

                                // Get Container based on Id.
                                Container container = database.GetContainer(containerId);

                                ContainerProperties containerProperties = await container.ReadContainerAsync();
                                Console.WriteLine($"Container-Read: {containerProperties.Id} PartitionKeyPath: {containerProperties.PartitionKeyPath}");

                                String itemsQuery = "SELECT * FROM c";
                                QueryDefinition itemsQueryDef = new QueryDefinition(itemsQuery);

                                //Querying.
                                FeedIterator queryIterator = container.GetItemQueryStreamIterator(
                                    itemsQueryDef,
                                    requestOptions: new QueryRequestOptions { MaxItemCount = -1 }
                                );

                                while (queryIterator.HasMoreResults)
                                {
                                    ResponseMessage response = await queryIterator.ReadNextAsync();

                                    if (response.Content == null)
                                    {
                                        Console.WriteLine("QueryResponse.Content is null");
                                        continue;
                                    }
                                    if (response.Content.CanSeek && response.Content.Length == 0)
                                    {
                                        Console.WriteLine("QueryResponse.Content stream is empty");
                                        continue;
                                    }

                                    try
                                    {
                                        using JsonDocument itemsQueryResultDoc = JsonDocument.Parse(response.Content);
                                        Console.WriteLine($"Raw JSON (Query result): {itemsQueryResultDoc.RootElement.GetRawText()}");

                                        if (itemsQueryResultDoc.RootElement.TryGetProperty("Documents", out JsonElement docsElement))
                                        {
                                            foreach (JsonElement item in docsElement.EnumerateArray())
                                            {
                                                Console.WriteLine($"Data JSON (Query result): {item.GetRawText()}");
                                            }
                                        }
                                    }
                                    catch (JsonException ex)
                                    {
                                        Console.WriteLine($"JsonException: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
```

## Install via [Nuget.org](https://www.nuget.org/packages/Microsoft.Azure.Cosmos.Aot/)

`Install-Package Microsoft.Azure.Cosmos.Aot`

Only Preview Versions available for use.

## Useful links

- [Get Started APP](https://docs.microsoft.com/azure/cosmos-db/sql-api-get-started)
- [GitHub samples](https://github.com/Azure/azure-cosmos-dotnet-v3/tree/master/Microsoft.Azure.Cosmos.Samples)
- [SDK Best practices](https://docs.microsoft.com/azure/cosmos-db/sql/best-practice-dotnet)
- [MultiMaster samples](https://github.com/markjbrown/azure-cosmosdb-dotnet/tree/master/samples/MultiMaster)
- [Resource Model of Azure Cosmos DB Service](https://docs.microsoft.com/azure/cosmos-db/sql-api-resources)
- [Cosmos DB Resource URI](https://docs.microsoft.com/rest/api/documentdb/documentdb-resource-uri-syntax-for-rest)
- [Partitioning](https://docs.microsoft.com/azure/cosmos-db/partition-data)
- [Introduction to Azure Cosmos DB for NoSQL queries](https://docs.microsoft.com/azure/cosmos-db/sql-api-sql-query)
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
