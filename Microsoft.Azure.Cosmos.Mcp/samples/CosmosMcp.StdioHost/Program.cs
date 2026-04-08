// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Mcp;
using Microsoft.Azure.Cosmos.Mcp.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Send all logs to stderr so they don't interfere with MCP stdio protocol
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configure CosmosClient — supports both connection string and AAD/Entra ID
string? connectionString = builder.Configuration["CosmosDb:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");

string? accountEndpoint = builder.Configuration["CosmosDb:AccountEndpoint"]
    ?? Environment.GetEnvironmentVariable("COSMOS_ACCOUNT_ENDPOINT");

CosmosClient cosmosClient;

if (!string.IsNullOrEmpty(connectionString))
{
    // Key-based or connection string auth
    cosmosClient = new CosmosClient(connectionString);
}
else if (!string.IsNullOrEmpty(accountEndpoint))
{
    // AAD / Entra ID auth via DefaultAzureCredential
    // Works with: managed identity, az login, Visual Studio credentials, etc.
    cosmosClient = new CosmosClient(accountEndpoint, new DefaultAzureCredential());
}
else
{
    throw new InvalidOperationException(
        "Cosmos DB credentials not found. Set either:\n" +
        "  - COSMOS_CONNECTION_STRING (connection string auth)\n" +
        "  - COSMOS_ACCOUNT_ENDPOINT (AAD/Entra ID auth via DefaultAzureCredential)\n" +
        "Or configure CosmosDb:ConnectionString / CosmosDb:AccountEndpoint in appsettings.");
}

builder.Services.AddSingleton(cosmosClient);

// Add the MCP server with stdio transport
builder.Services.AddCosmosMcpServer(options =>
{
    options.ServerName = "cosmos-db-mcp";
    options.ServerVersion = "0.1.0";

    // Default: read-only. Uncomment to enable writes:
    // options.AllowedOperations = McpOperations.All;
})
.WithStdioTransport();

var host = builder.Build();
await host.RunAsync();
