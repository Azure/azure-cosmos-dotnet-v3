# Cosmos Bulk support

This sample demonstrates the example of implementig custom diagnostic and event listener

## Description

This listener can cover following aspects:

1. Write its own monitoring library with the custom implementation of aggregation or whatever you want to do with this data.
2. Support an APM tool which is not open telemetry compliant.

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
