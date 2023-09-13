# Cosmos Bulk support

This sample demonstrates the basic usage of the CosmosClient bulk mode by performing a high volume of operations.

## Prerequisites

- Azure Cosmos DB NoSQL Account
  - Create a DataBase and Container
- Microsoft.Azure.Cosmos NuGet [package](http://www.nuget.org/packages/Microsoft.Azure.Cosmos/)
- Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute

## Run

Before running the application you need fill out `EndPointUrl` and `AuthorizationKey` params in the [AppSettings.json](../appSettings.json)

```PowerShell
dotnet run
```

## Getting Started

### Create Database and Container
