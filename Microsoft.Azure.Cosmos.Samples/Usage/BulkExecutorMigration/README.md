# Cosmos Bulk Executor migration

This sample demonstrates how to migrate from Bulk Executor Library to V3 SDK with Bulk support.

## Prerequisites

- Azure Cosmos DB NoSQL Account
  - Create a DataBase and Container
- Microsoft.Azure.Cosmos NuGet [package](http://www.nuget.org/packages/Microsoft.Azure.Cosmos/)

## Run

Before running the application you need fill out `EndPointUrl` and `AuthorizationKey` params in the [AppSettings.json](../appSettings.json)

```PowerShell
dotnet run
```

## Getting Started

### Create Database and Container

### Run and you will see the result

```PowerShell
The demo will create a container, press any key to continue.
Running bulk operations demo for container bulkMigration with a Bulk enabled CosmosClient.
Bulk create operation finished in 00:00:00.5426833
Consumed 5520.0000000001 RUs in total
Created 1000 documents
Failed 0 documents
Bulk update operation finished in 00:00:00.4598077
Consumed 10290.000000000151 RUs in total
Updated 1000 documents
Failed 0 documents
Bulk update operation finished in 00:00:00.3676135
Consumed 5520.0000000001 RUs in total
Deleted 1000 documents
Failed 0 documents
End of demo, press any key to exit.
```
