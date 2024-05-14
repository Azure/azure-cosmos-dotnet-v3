# Azure Cosmos DB Fault Injection Library for .NET

The Azure Cosmos DB Fault Injection Library for .NET is a library that allows you to inject faults into the Azure Cosmos DB .NET SDK. This library is designed to help you test the resiliency of your application when using Azure Cosmos DB. The library is built on top of the [Azure Cosmos DB SDK for .NET](https://github.com/Azure/azure-cosmos-dotnet-v3)

## Getting Started

The Azure Cosmos DB Fault Injection Library for .NET is available as a NuGet package. You can install it


### Prerequisites

- [.NET 6.0](https://dotnet.microsoft.com/download/dotnet/5.0)
- An active [Azure Cosmos DB Account](https://docs.microsoft.com/en-us/azure/cosmos-db/create-cosmosdb-resources-portal). Alternativly you can use the [Azure Cosmos DB Emulator](https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator) for local development.

## Key Concepts

The Azure Cosmos DB Fault Injection Library for .NET is a library that allows you to inject faults into the Azure Cosmos DB .NET SDK. The faults come in the form of Fault Injection Rules. There are two types of Fault Injection Rules: Server Error Rules and Connection Error Rules. Server Error Rules simulate server-side errors such as 429 Too Many Requests, 503 Service Unavailable, etc. Connection Error Rules simulate connection errors such as network timeouts, connection resets, etc.

## Use Cases

## Examples

## Troubleshooting

## Next Steps

## Contributing
