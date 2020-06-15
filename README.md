[![Build Status](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_apis/build/status/azure-cosmos-dotnet-official-v4?branchName=master)](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build/latest?definitionId=53&branchName=v4)

# Microsoft Azure Cosmos DB .NET SDK Version 4

This client library enables client applications to connect to Azure Cosmos via the SQL API. Azure Cosmos is a globally distributed, multi-model database service. For more information, refer to https://azure.microsoft.com/services/cosmos-db/.

## Getting started

### Install the package

Install the Microsoft Azure Cosmos DB .NET SDK V4 with [NuGet][nuget_package]:

```PowerShell
dotnet add package Azure.Cosmos --version 4.0.0-preview3
```

### Prerequisites

* An [Azure subscription][azure_sub].
* An existing Azure Cosmos account or the [Azure Cosmos Emulator][cosmos_emulator].

### Create an Azure Cosmos account

You can create an Azure Cosmos account using:

* [Azure Portal][cosmos_resource_portal].
* [Azure CLI][cosmos_resource_cli].
* [Azure ARM][cosmos_resource_arm].

Below is an example of how you can create an Azure Cosmos account using the CLI:

```PowerShell
# Create a new resource group to hold the account -
# if using an existing resource group, skip this step
az group create --name <your-resource-name> --location <location>
```

```PowerShell
# Create Azure Cosmos account
az cosmosdb create \
    --resource-group <your-resource-name> \
    --name <account-name> \
    --kind GlobalDocumentDB \
    --locations regionName="<location>" failoverPriority=0 \
    --default-consistency-level "Session"
```

For more information about creating the account see [here][cosmos_resource_cli].

### Authentication

In order to interact with the Azure Cosmos service, you'll need to create an instance of the `CosmosClient` class. You will need an **endpoint** and an **API key**, or the **connection string**.

#### Get the connection string

You can obtain the connection string from the resource information in the [Azure Portal][azure_portal].

Alternatively, you can use the [Azure CLI][azure_cli] snippet below:

```PowerShell
az cosmosdb keys list \
    -n <your-resource-name> \
    -g <your-resource-group-name> \
    --type connection-strings
```

#### Create CosmosClient with the connection string

Once you have the value for the connection string, you can create the `CosmosClient`. In the below example, we are also setting the application region to make sure we are connecting to the closest Azure Cosmos endpoint:

```csharp
CosmosClientOptions clientOptions = new CosmosClientOptions();
clientOptions.ApplicationRegion = "West US";
CosmosClient client = new CosmosClient("<connection-string>", clientOptions);
```

## Key concepts

The following image shows the hierarchy of different entities in an Azure Cosmos account:

![Azure Cosmos DB resource model](https://docs.microsoft.com/azure/cosmos-db/media/databases-containers-items/cosmos-entities.png)

### CosmosClient

`CosmosClient` is the client:

* Working with Azure Cosmos databases. They include creating and listing through the `CosmosDatabase` type.
* Obtaining the Azure Cosmos account information.

### Database

A database is the unit of management for a set of Azure Cosmos containers. It maps to the `CosmosDatabase` class and supports:

* Working with Azure Cosmos containers. They include creating, modifying, deleting, and listing through the `CosmosContainer` type.
* Working with Azure Cosmos users. Users define access scope and permissions. They include creating, modifying, deleting, and listing through the `CosmosUser` type.

### Containers

An Azure Cosmos container is the unit of scalability both for provisioned throughput and storage. A container is horizontally partitioned and then replicated across multiple regions. It maps to the `CosmosContainer` class and supports:

* Working with items. Items are the conceptually the user's data. They include creating, modifying, deleting, and listing (including query) items.
* Working with scripts. Scripts are defined as Stored Procedures, User Defined Functions, and Triggers.

For more details visit [here][cosmos_resourcemodel].

## Examples

The following section provides several code snippets illustrating common patterns used in the Azure Cosmos DB .NET SDK.

* [Creating a database and container](#creating-a-database-and-container)
* [Creating an item](#creating-an-item)
* [Reading an item](#reading-an-item)
* [Optimistic concurrency](#optimistic-concurrency)
* [Query items](#query-items)
* [Executing a stored procedure](#executing-a-stored-procedure)

### Creating a database and container

Azure Cosmos DB uses partitioning to scale individual containers in a database to meet the performance needs of your application. In partitioning, the items in a container are divided into distinct subsets called *logical partitions*. When creating an Azure Cosmos container, it is required that the [partition key][cosmos_partition] is defined, and optionally, [provisioned throughput][cosmos_throughput] can be specified.

```csharp
CosmosDatabase database = await client.CreateDatabaseIfNotExistsAsync("my-database");
CosmosContainer container = await database.CreateContainerIfNotExistsAsync("my-container", "/partitionKey", throughput: 1000);
```

### Creating an item

Azure Cosmos DB .NET SDK lets users use POCO types to store items in containers and uses System.Text.Json to serialize the content. Any failure scenarios are communicated through exceptions:

```csharp
public class MyItem
{
    public string id { get; set; }
    public int value { get; set; }
    public string partitionKey { get; set; }
}

MyItem item = new MyItem() { id = "myItem", partitionKey = "myPartitionKey", value = 10};

try
{
    ItemResponse<MyItem> response = await container.CreateItemAsync<MyItem>(item, new PartitionKey("myPartitionKey"));
}
catch (CosmosException exception)
{
    Console.Write(exception.ToString());
}
```

Users can also opt-out of the serialization cost by providing a `Stream` that represents the payload of the item to store. Any failure scenarios can be detected by the `Response` Status.

```csharp
using (Response response = await container.CreateItemStreamAsync(streamPayload: stream, partitionKey: new PartitionKey("myPartitionKey")))
{
    Console.WriteLine($"Status {response.Status}");
    if (response.ContentStream != null)
    {
        using (Stream responseStream = await response.ContentStream)
        {
            using (StreamReader streamReader = new StreamReader(responseStream))
            {
                // Read response content
            }
        }
    }
}
```

### Reading an item

Items in an Azure Cosmos container are uniquely identified by their `id` and partition key value. Azure Cosmos DB .NET SDK lets users read items and serialize them into a POCO type using System.Text.Json. Any failure scenarios are communicated through exceptions:

```csharp
try
{
    ItemResponse<MyItem> response = await container.ReadItemAsync<MyItem>("myItem", new PartitionKey("myPartitionKey"));
    MyItem item = response.Value;
}
catch (CosmosException exception)
{
    Console.Write(exception.ToString());
}
```

Users can also opt-out of the serialization cost and obtain the Stream directly:

```csharp
using (Response response = await container.ReadItemAsync("myItem", new PartitionKey("myPartitionKey")))
{
    Console.WriteLine($"Status {response.Status}");
    if (response.ContentStream != null)
    {
        using (Stream responseStream = await response.ContentStream)
        {
            using (StreamReader streamReader = new StreamReader(responseStream))
            {
                // Read response content
            }
        }
    }
}
```

### Optimistic concurrency

Azure Cosmos DB supports [optimistic concurrency control][cosmos_optimistic] to prevent lost updates or deletes and detection of conflicting operations. Users are expected to use the `Etag` and detect PreconditionFailed scenarios like so:

```csharp
ItemResponse<MyItem> readResponse = await container.ReadItemAsync<MyItem>("myItem", new PartitionKey("myPartitionKey"));
MyItem item = readResponse.Value;
item.value += 10;
try
{
    ItemRequestOptions requestOptions = new ItemRequestOptions()
    {
        IfMatch = readResponse.Etag
    };
    ItemResponse<MyItem> replaceItem = await container.ReplaceItemAsync<MyItem>(item, "myItem", new PartitionKey("myPartitionKey"), requestOptions);
}
catch (CosmosException cosmosException)
{
    if (cosmosException.Status == (int)System.Net.HttpStatusCode.PreconditionFailed))
    {
        // Optimistic concurrency failed, do another read with the latest Etag and re-apply change.
    }
}
```

### Query items

Azure Cosmos containers can be queried for items using POCO types that leverage System.Text.Json serialization:

```csharp
QueryDefinition queryDefinition = new QueryDefinition("select * from c where c.value > @expensive")
    .WithParameter("@expensive", 10);
await foreach(MyItem item in container.GetItemQueryResultsAsync<MyItem>(queryDefinition))
{
        Console.WriteLine(item.id);
}
```

Users can also opt-out of the serialization and access the underlying Stream directly:

```csharp
QueryDefinition queryDefinition = new QueryDefinition("select * from c where c.value > @expensive")
    .WithParameter("@expensive", 10);
await foreach(Response response in container.GetItemQueryStreamResultsAsync(queryDefinition))
{
    // Directly work with response.ContentStream
}
```

### Executing a stored procedure

Azure Cosmos DB has [server side programmability][cosmos_scripts] support. Users can author and execute Stored procedures directly from the SDK:

```csharp
CosmosScripts scripts = container.Scripts;
string sprocBody = @"function simple(prefix)
   {
       var collection = getContext().getCollection();
       // Query documents and take 1st item.
       var isAccepted = collection.queryDocuments(
       collection.getSelfLink(),
       'SELECT * FROM root r',
       function(err, feed, options) {
           if (err)throw err;
           // Check the feed and if it's empty, set the body to 'no docs found',
           // Otherwise just take 1st element from the feed.
           console.log('Executed stored procedure');
           if (!feed || !feed.length) getContext().getResponse().setBody(""no docs found"");
           else getContext().getResponse().setBody(prefix + JSON.stringify(feed[0]));
       });
       if (!isAccepted) throw new Error(""The query wasn't accepted by the server. Try again/use continuation token between API and script."");
   }";
StoredProcedureProperties storedProcedure = new StoredProcedureProperties("myStoredProcedure", sprocBody);
Response<StoredProcedureProperties> storedProcedureResponse = await scripts.CreateStoredProcedureAsync(storedProcedure);
StoredProcedureExecuteResponse<string> sprocResponse = await scripts.ExecuteStoredProcedureAsync<string>(
                        "myStoredProcedure",
                        new PartitionKey("myPartitionKey"),
                        new dynamic[] {"myPrefixString"},
                        new StoredProcedureRequestOptions()
                        {
                            EnableScriptLogging = true
                        });
string result = sprocResponse;
Console.WriteLine(result);
Console.WriteLine(sprocResponse.ScriptLog);
```

## Next steps

- [Get Started APP](https://docs.microsoft.com/azure/cosmos-db/create-sql-api-dotnet-v4)
- [Github samples](https://github.com/Azure/azure-cosmos-dotnet-v3/tree/master/Microsoft.Azure.Cosmos.Samples/CodeSamples)
- [Resource Model of Azure Cosmos DB Service](https://docs.microsoft.com/azure/cosmos-db/sql-api-resources)
- [Cosmos DB Resource URI](https://docs.microsoft.com/rest/api/documentdb/documentdb-resource-uri-syntax-for-rest)
- [Partitioning](https://docs.microsoft.com/azure/cosmos-db/partition-data)
- [Introduction to SQL API of Azure Cosmos DB Service](https://docs.microsoft.com/azure/cosmos-db/sql-api-sql-query)
- [SDK API](https://docs.microsoft.com/dotnet/api/azure.cosmos?view=azure-dotnet)
- [Using emulator](https://github.com/Azure/azure-documentdb-dotnet/blob/master/docs/documentdb-nosql-local-emulator.md)
- [Capture traces](https://github.com/Azure/azure-documentdb-dotnet/blob/master/docs/documentdb-sdk_capture_etl.md)
- [Release notes](https://github.com/Azure/azure-cosmos-dotnet-v3/blob/v4/changelog.md)
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

<!-- LINKS -->
[nuget_package]: https://www.nuget.org/packages/Azure.Cosmos
[cosmos_emulator]: https://docs.microsoft.com/azure/cosmos-db/local-emulator
[cosmos_resource_portal]: https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
[cosmos_resource_cli]: https://docs.microsoft.com/azure/cosmos-db/scripts/cli/sql/create
[cosmos_resource_arm]: https://docs.microsoft.com/azure/cosmos-db/quick-create-template
[cosmos_throughput]: https://docs.microsoft.com/azure/cosmos-db/set-throughput
[cosmos_partition]: https://docs.microsoft.com/azure/cosmos-db/partitioning-overview#choose-partitionkey
[cosmos_optimistic]: https://docs.microsoft.com/azure/cosmos-db/database-transactions-optimistic-concurrency#optimistic-concurrency-control
[cosmos_scripts]: https://docs.microsoft.com/azure/cosmos-db/how-to-write-stored-procedures-triggers-udfs
[cosmos_resourcemodel]: https://docs.microsoft.com/azure/cosmos-db/databases-containers-items

[azure_cli]: https://docs.microsoft.com/cli/azure
[azure_sub]: https://azure.microsoft.com/free/
[azure_portal]: https://portal.azure.com
