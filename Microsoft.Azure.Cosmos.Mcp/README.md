# Microsoft.Azure.Cosmos.Mcp

An MCP (Model Context Protocol) server extension for the Azure Cosmos DB .NET SDK. Enables AI agents like Claude Code, GitHub Copilot, and Cursor to query, explore, and operate on your Cosmos DB data — zero infrastructure required.

## Quick Start

### 1. Install the package

```bash
dotnet add package Microsoft.Azure.Cosmos.Mcp
```

### 2. Add to your app (stdio transport)

**Connection string auth:**
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(new CosmosClient(connectionString));
builder.Services.AddCosmosMcpServer()
    .WithStdioTransport();

await builder.Build().RunAsync();
```

**AAD / Entra ID auth (DefaultAzureCredential):**
```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(new CosmosClient(accountEndpoint, new DefaultAzureCredential()));
builder.Services.AddCosmosMcpServer()
    .WithStdioTransport();

await builder.Build().RunAsync();
```

### 3. Configure your MCP client

**VS Code / Claude Code (`mcp.json`) — connection string:**
```json
{
  "servers": {
    "cosmos-db": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "./path/to/your/host/"],
      "env": {
        "COSMOS_CONNECTION_STRING": "${env:COSMOS_CONNECTION_STRING}"
      }
    }
  }
}
```

**VS Code / Claude Code (`mcp.json`) — AAD / Entra ID:**
```json
{
  "servers": {
    "cosmos-db": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "./path/to/your/host/"],
      "env": {
        "COSMOS_ACCOUNT_ENDPOINT": "https://your-account.documents.azure.com:443/"
      }
    }
  }
}
```
Uses `DefaultAzureCredential` which automatically picks up `az login`, managed identity, Visual Studio credentials, etc.

## Features

### MCP Tools (10 tools)

| Tool | Description | Requires |
|------|-------------|----------|
| `cosmos_list_databases` | List all databases | — |
| `cosmos_list_containers` | List containers with metadata | — |
| `cosmos_query` | Execute SQL queries with parameterization | `Query` |
| `cosmos_read_item` | Point read by id + partition key | `Read` |
| `cosmos_get_schema` | Infer schema by sampling documents | `SchemaDiscovery` |
| `cosmos_upsert_item` | Create or replace a document | `Write` |
| `cosmos_delete_item` | Delete a document | `Write` |
| `cosmos_patch_item` | Partial update with patch operations | `Write` |
| `cosmos_vector_search` | Vector similarity search | `VectorSearch` |
| `cosmos_analyze_diagnostics` | Parse CosmosDiagnostics JSON | `Diagnostics` |

### MCP Resources (5 resources)

| Resource URI | Description |
|-------------|-------------|
| `cosmos://databases` | All accessible databases |
| `cosmos://{db}/containers` | Containers with partition key info |
| `cosmos://{db}/{container}/schema` | Inferred JSON Schema |
| `cosmos://{db}/{container}/indexing-policy` | Current indexing configuration |
| `cosmos://{db}/{container}/throughput` | RU/s and autoscale settings |

### MCP Prompts (3 prompts)

| Prompt | Description |
|--------|-------------|
| `explore_data` | Guided data exploration workflow |
| `optimize_query` | Query optimization with RU comparison |
| `model_data` | Data modeling assistant |

## Configuration

### Default (read-only)

```csharp
builder.Services.AddCosmosMcpServer();
```

### Enable writes

```csharp
builder.Services.AddCosmosMcpServer(options =>
{
    options.AllowedOperations = McpOperations.All;
});
```

### Restrict access

```csharp
builder.Services.AddCosmosMcpServer(options =>
{
    // Only expose specific databases/containers
    options.DatabaseFilter = db => db.Id == "my-app-db";
    options.ContainerFilter = (db, c) => !c.Id.StartsWith("_");

    // Disable specific tools
    options.DisabledTools = new() { "cosmos_delete_item" };

    // Query safety
    options.Query.MaxItemCount = 50;
    options.Query.MaxQueryLengthChars = 1000;
});
```

### HTTP transport (ASP.NET Core)

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new CosmosClient(connectionString));
builder.Services.AddCosmosMcpServer(options =>
{
    options.ServerName = "my-cosmos-mcp";
});

var app = builder.Build();
app.MapCosmosMcpServer("/mcp");
app.Run();
```

## Security

- **Read-only by default** — writes must be explicitly enabled
- **Query sanitization** — rejects DDL/DML, enforces SELECT-only, caps length
- **Container scoping** — filtered containers never leak names
- **Credential passthrough** — uses your existing `CosmosClient` credentials
- **Operation gating** — tools only appear when their operation flag is set

## Architecture

```
AI Agent (Claude/Copilot/Cursor)
    │ MCP Protocol (JSON-RPC 2.0)
    │ Transport: stdio or Streamable HTTP
    ▼
Microsoft.Azure.Cosmos.Mcp
    ├── Tools (query, read, write, search, diagnostics)
    ├── Resources (databases, containers, schema, indexing, throughput)
    ├── Prompts (explore, optimize, model)
    ├── Schema (inference + TTL cache)
    └── Security (operation filter, query sanitizer, container scoping)
         │
         ▼
    CosmosClient (your existing credentials/config)
```

## Requirements

- .NET 8.0+
- `Microsoft.Azure.Cosmos` 3.58.0+
- `ModelContextProtocol` 1.2.0+
