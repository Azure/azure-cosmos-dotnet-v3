[![Build Status](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_apis/build/status/azure-cosmos-dotnet-official-v4?branchName=master)](https://cosmos-db-sdk-public.visualstudio.com/cosmos-db-sdk-public/_build/latest?definitionId=53&branchName=v4)

# Microsoft Azure Cosmos DB .NET SDK Version 4

This client library enables client applications to connect to Azure Cosmos via the SQL API. Azure Cosmos is a globally distributed, multi-model database service. For more information, refer to https://azure.microsoft.com/services/cosmos-db/.

## Getting started

### Install the package

Install the Microsoft Azure Cosmos DB .NET SDK V4 with [NuGet][nuget_package]:

```PowerShell
dotnet add package Azure.Cosmos --version 1.0.0-preview3
```

### Prerequisites

* An [Azure subscription][azure_sub].
* An existing Azure Cosmos account or the [Azure Cosmos Emulator][cosmos_emulator].

### Create an Azure Cosmos account

Form Recognizer supports both [multi-service and single-service access][cognitive_resource_portal]. Create a Cognitive Services resource if you plan to access multiple cognitive services under a single endpoint/key. For Form Recognizer access only, create a Form Recognizer resource.

You can create either resource using:

* [Azure Portal][cosmos_resource_portal].
* [Azure CLI][cosmos_resource_cli].
* [Azure ARM][cosmos_resource_arm].

Below is an example of how you can create an Azure Cosmos resource using the CLI:

```PowerShell
# Create a new resource group to hold the resource -
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

For more information about creating the resource see [here][cosmos_resource_cli].

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

#### Create CosmosCLient with the connection string

Once you have the value for the connection string, you can create the `CosmosClient`:

```csharp
CosmosClient client = new CosmosClient("<connection-string>");
```

## Key concepts

### CosmosClient

`CosmosClient` provides operations for:

* Working with Azure Cosmos databases. They include creating and listing through the `CosmosDatabase` type.
* Obtaining the Azure Cosmos account information.

### CosmosDatabase

`CosmosDatabase` provides operations for:

* Working with Azure Cosmos containers. They include creating, modifying, deleting, and listing through the `CosmosContainer` type.
* Working with Azure Cosmos users. Users define access scope and permissions. They include creating, modifying, deleting, and listing through the `CosmosUser` type.

### CosmosContainer

`CosmosContainer` provides operations for:

* Working with items. Items are the conceptually the user's data. They include creating, modifying, deleting, and listing (including query) items.
* Working with scripts. Scripts are defined as Stored Procedures, User Defined Functions, and  

## Useful links

- [Get Started APP](https://docs.microsoft.com/azure/cosmos-db/sql-api-get-started)
- [Github samples](https://github.com/Azure/azure-cosmos-dotnet-v3/tree/master/Microsoft.Azure.Cosmos.Samples/CodeSamples)
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

<!-- LINKS -->
[nuget_package]: https://www.nuget.org/packages/Azure.Cosmos
[cosmos_emulator]: https://docs.microsoft.com/azure/cosmos-db/local-emulator
[cosmos_resource_portal]: https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
[cosmos_resource_cli]: https://docs.microsoft.com/azure/cosmos-db/scripts/cli/sql/create
[cosmos_resource_arm]: https://docs.microsoft.com/azure/cosmos-db/quick-create-template

[azure_cli]: https://docs.microsoft.com/cli/azure
[azure_sub]: https://azure.microsoft.com/free/
[azure_portal]: https://portal.azure.com