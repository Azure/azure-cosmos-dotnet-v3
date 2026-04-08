// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

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

// Configure CosmosClient from environment or command-line
string connectionString = builder.Configuration["CosmosDb:ConnectionString"]
    ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Cosmos DB connection string not found. Set COSMOS_CONNECTION_STRING environment variable or CosmosDb:ConnectionString in configuration.");

builder.Services.AddSingleton(new CosmosClient(connectionString));

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
