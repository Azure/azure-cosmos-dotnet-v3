# Cosmos Bulk support

This sample demonstrates an example of configuring custom serialization

## Description

This example covers several options leveraging CosmosClientOptions.CosmosSerializationOptions:

- IgnoreNullValues
- Disable indented
- CamelCase property naming policy

## Prerequisites

- Azure Cosmos DB NoSQL Account
  - Create a DataBase
- Microsoft.Azure.Cosmos NuGet [package](http://www.nuget.org/packages/Microsoft.Azure.Cosmos/)
- Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute

## Getting Started

### Run

Before running the application you need fill out `EndPointUrl` and `AuthorizationKey` params in the [AppSettings.json](../appSettings.json)

```PowerShell
dotnet run
```

```json
  {
    "id": "CapitalId",
    "pk": "12345",
    "_rid": "+RVXAM52ojQBAAAAAAAAAA==",
    "_etag": "\"00000000-0000-0000-e59b-6f7166ef01d9\"",
    "_attachments": "attachments\/",
  }
```
