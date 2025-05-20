// See https://aka.ms/new-console-template for more information
// dotnet publish -c Release -r linux-x64
using Microsoft.Azure.Cosmos;

Console.WriteLine("Hello, World!");

CosmosClient cosmosClient = new CosmosClient("", "");
Console.WriteLine($"Using endpoint: {cosmosClient.Endpoint}");
