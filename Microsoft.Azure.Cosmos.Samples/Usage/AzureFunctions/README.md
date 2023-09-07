# Cosmos client with Azure Function

THis sample demonstrates how to maintain a Cosmos client and reuse the instance among Azure Function executions.

## Prerequisites

- Azure Cosmos DB NoSQL Account
  - Create a DataBase and Container
- Microsoft.Azure.Cosmos NuGet [package](http://www.nuget.org/packages/Microsoft.Azure.Cosmos/)

## Run

Before running the application you need fill out `EndPointUrl` and `AuthorizationKey` params in the [AppSettings.json](AppSettings.json)

```PowerShell
dotnet run
```

## Getting Started

### Create Database and Container

### Send post request

```bash
curl --location 'http://localhost:7071/api/CosmosClient' \
--header 'Content-Type: application/json' \
--data '{
    "id": "id",
    "name" : "name",
    "description" : "description",
    "isComplete": false
}'
```
