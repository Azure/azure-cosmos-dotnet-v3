# Cosmos DB SDK-Embedded MCP Server — Prototype Design & Build Prompt

## What This Document Is

This is a self-contained specification and build prompt for creating a prototype MCP server that runs as an extension package on top of the Azure Cosmos DB .NET SDK (`Microsoft.Azure.Cosmos`). The goal is a zero-infrastructure experience: one NuGet package, one line of configuration, and any MCP-compatible AI agent can query, search, and operate on your Cosmos DB data.

-----

## 1. Problem Statement

Today, connecting an AI agent to Cosmos DB data requires deploying the standalone [AzureCosmosDB/MCPToolKit](https://github.com/AzureCosmosDB/MCPToolKit) — a full .NET 9 ASP.NET Core application with Dockerfiles, Bicep templates, Entra ID app registrations, and Azure Container App deployment. That’s powerful for production, but the barrier to entry for a developer who just wants Claude Code or GitHub Copilot to talk to their Cosmos DB container is enormous.

The SDK-embedded MCP server eliminates that barrier entirely. It reuses the `CosmosClient` the developer already has, exposes MCP tools/resources/prompts through the official C# MCP SDK (`ModelContextProtocol` NuGet), and runs as either a stdio process or a Streamable HTTP endpoint — no separate deployment required.

### Key Differentiation from the Existing MCPToolKit

|Aspect       |MCPToolKit (Existing)                                    |SDK-Embedded MCP (This Project)                         |
|-------------|---------------------------------------------------------|--------------------------------------------------------|
|Deployment   |Standalone ASP.NET Core app, Docker, Azure Container Apps|NuGet package, runs in-process or as dotnet tool        |
|Auth         |Entra ID app registration required                       |Reuses existing CosmosClient credentials                |
|Setup        |Bicep + PowerShell scripts                               |`builder.AddCosmosMcpServer(cosmosClient)`              |
|Target user  |Platform teams deploying for Foundry agents              |Individual developers using Claude Code, Copilot, Cursor|
|Vector search|Requires Azure OpenAI for embeddings                     |Brings-your-own-embeddings, or optional integration     |
|Transport    |SSE (legacy)                                             |Streamable HTTP + stdio                                 |

-----

## 2. Architecture

```
┌──────────────────────────────────────────────────────────┐
│  AI Agent (Claude Code / GitHub Copilot / Cursor / etc.) │
│  ┌──────────────┐                                        │
│  │  MCP Client   │                                       │
│  └──────┬───────┘                                        │
└─────────┼────────────────────────────────────────────────┘
          │ MCP Protocol (JSON-RPC 2.0)
          │ Transport: stdio or Streamable HTTP
          │
┌─────────▼────────────────────────────────────────────────┐
│  Microsoft.Azure.Cosmos.Mcp  (this package)              │
│                                                          │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐  │
│  │  MCP Tools   │  │ MCP Resources │  │  MCP Prompts   │  │
│  │              │  │              │  │                │  │
│  │ • query      │  │ • databases  │  │ • explore_data │  │
│  │ • read_item  │  │ • containers │  │ • optimize_    │  │
│  │ • upsert     │  │ • schema     │  │   query        │  │
│  │ • delete     │  │ • indexing   │  │ • model_data   │  │
│  │ • patch      │  │   policy     │  │                │  │
│  │ • vector_    │  │ • throughput │  │                │  │
│  │   search     │  │              │  │                │  │
│  │ • bulk_ops   │  │              │  │                │  │
│  └──────┬──────┘  └──────┬───────┘  └────────────────┘  │
│         │                │                               │
│  ┌──────▼────────────────▼───────┐                       │
│  │    CosmosClient (existing)    │                       │
│  │    User's credentials/config  │                       │
│  └───────────────────────────────┘                       │
└──────────────────────────────────────────────────────────┘
```

### Package Structure

```
Microsoft.Azure.Cosmos.Mcp/
├── CosmosMcpServerBuilder.cs        # Fluent configuration API
├── CosmosMcpServerExtensions.cs     # DI extension methods
├── Tools/
│   ├── QueryTool.cs                 # SQL query execution
│   ├── PointReadTool.cs             # Read by id + partition key
│   ├── UpsertTool.cs                # Create or update documents
│   ├── DeleteTool.cs                # Delete by id + partition key
│   ├── PatchTool.cs                 # Partial update operations
│   ├── VectorSearchTool.cs          # Vector similarity search
│   ├── BulkOperationsTool.cs        # Batch/bulk operations
│   ├── ContainerManagementTool.cs   # List/create containers
│   └── DiagnosticsTool.cs           # Analyze CosmosDiagnostics
├── Resources/
│   ├── DatabaseListResource.cs      # List all databases
│   ├── ContainerListResource.cs     # List containers in a database
│   ├── SchemaDiscoveryResource.cs   # Sample-based schema inference
│   ├── IndexingPolicyResource.cs    # Current indexing policy
│   └── ThroughputResource.cs        # Current RU/s configuration
├── Prompts/
│   ├── ExploreDataPrompt.cs         # Guided data exploration
│   ├── OptimizeQueryPrompt.cs       # Query optimization workflow
│   └── ModelDataPrompt.cs           # Data modeling assistant
├── Schema/
│   ├── SchemaInferrer.cs            # Sample docs → JSON Schema
│   └── SchemaCache.cs               # TTL-based schema cache
├── Security/
│   ├── OperationFilter.cs           # Allow/deny tool filtering
│   └── QuerySanitizer.cs            # SQL injection protection
└── Hosting/
    ├── StdioHostExtensions.cs       # stdio transport setup
    └── HttpHostExtensions.cs        # Streamable HTTP transport
```

-----

## 3. Configuration API Design

### Minimal Setup (Hosted App / ASP.NET Core)

```csharp
var builder = WebApplication.CreateBuilder(args);

// Existing CosmosClient setup
builder.Services.AddSingleton(new CosmosClient(
    connectionString: builder.Configuration["CosmosDb:ConnectionString"]
));

// One line to add the MCP server
builder.Services.AddCosmosMcpServer(options =>
{
    options.ServerName = "my-cosmos-mcp";
    options.ServerVersion = "1.0.0";
});

var app = builder.Build();
app.MapCosmosMcpServer("/mcp"); // Streamable HTTP endpoint
app.Run();
```

### Minimal Setup (stdio / dotnet tool)

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(new CosmosClient(connectionString));
builder.Services.AddCosmosMcpServer()
    .WithStdioTransport();

var host = builder.Build();
await host.RunAsync();
```

### Advanced Configuration

```csharp
builder.Services.AddCosmosMcpServer(options =>
{
    options.ServerName = "my-cosmos-mcp";
    options.ServerVersion = "1.0.0";

    // Security: control which operations are allowed
    options.AllowedOperations = McpOperations.Read | McpOperations.Query;
    // options.AllowedOperations = McpOperations.All; // includes writes

    // Scope: limit which databases/containers are exposed
    options.DatabaseFilter = db => db.Id != "system-db";
    options.ContainerFilter = (db, container) => !container.Id.StartsWith("_");

    // Schema discovery settings
    options.SchemaDiscovery.SampleSize = 20;       // docs to sample per container
    options.SchemaDiscovery.CacheDuration = TimeSpan.FromMinutes(30);

    // Query safety
    options.Query.MaxItemCount = 100;               // max results per query
    options.Query.MaxQueryLengthChars = 2000;        // prevent absurdly long queries
    options.Query.AllowCrossPartitionQueries = true;
    options.Query.DefaultConsistencyLevel = ConsistencyLevel.Session;

    // Vector search (optional)
    options.VectorSearch.Enabled = true;
    options.VectorSearch.EmbeddingDimensions = 1536;
    options.VectorSearch.VectorPath = "/embedding";
    options.VectorSearch.DistanceFunction = DistanceFunction.Cosine;

    // Diagnostics
    options.IncludeDiagnosticsTools = true;
    options.DiagnosticsLatencyThreshold = TimeSpan.FromMilliseconds(500);
});
```

-----

## 4. MCP Tools Specification

Each tool should be implemented as a class decorated with `[McpServerToolType]` and individual methods with `[McpServerTool]` from the official C# MCP SDK.

### 4.1 `cosmos_query`

Execute a SQL query against a specific container.

```
Parameters:
  - database (string, required): Database name
  - container (string, required): Container name
  - query (string, required): SQL query string
  - parameters (object, optional): Query parameters as key-value pairs
  - partition_key (string, optional): Partition key value for scoped query
  - max_items (integer, optional, default 25): Max documents to return

Returns:
  - documents (array): Query results
  - request_charge (number): RU cost
  - continuation_token (string|null): For pagination
  - diagnostics_summary (string): Latency and region info
```

**Implementation notes:**

- ALWAYS use parameterized queries internally when parameter values are provided
- Apply the configured `MaxItemCount` ceiling
- Include `QueryRequestOptions` with the user’s consistency level
- Return `RequestCharge` and a human-readable diagnostics summary (not the full JSON blob)
- Sanitize the query string: reject DDL-like patterns, reject queries starting with anything other than SELECT

### 4.2 `cosmos_read_item`

Point read a single document by id and partition key.

```
Parameters:
  - database (string, required): Database name
  - container (string, required): Container name
  - id (string, required): Document id
  - partition_key (string, required): Partition key value

Returns:
  - document (object): The document
  - request_charge (number): RU cost
  - etag (string): Document ETag
```

### 4.3 `cosmos_upsert_item`

Create or replace a document (only available when writes are enabled).

```
Parameters:
  - database (string, required): Database name
  - container (string, required): Container name
  - document (object, required): The document to upsert (must include id and partition key)

Returns:
  - document (object): The upserted document
  - request_charge (number): RU cost
  - status (string): "created" or "replaced"
```

**Implementation notes:**

- Gate behind `McpOperations.Write` flag
- Validate document contains `id` field
- Return clear error if partition key is missing from the document

### 4.4 `cosmos_delete_item`

Delete a document by id and partition key (only when writes enabled).

```
Parameters:
  - database (string, required): Database name
  - container (string, required): Container name
  - id (string, required): Document id
  - partition_key (string, required): Partition key value

Returns:
  - request_charge (number): RU cost
  - deleted (boolean): true
```

### 4.5 `cosmos_patch_item`

Partial update using Cosmos DB patch operations (writes enabled).

```
Parameters:
  - database (string, required): Database name
  - container (string, required): Container name
  - id (string, required): Document id
  - partition_key (string, required): Partition key value
  - operations (array, required): Array of patch operations
    Each operation: { op: "set"|"add"|"remove"|"replace"|"incr", path: string, value: any }

Returns:
  - document (object): Updated document
  - request_charge (number): RU cost
```

### 4.6 `cosmos_vector_search`

Perform vector similarity search (only when vector search is enabled and configured).

```
Parameters:
  - database (string, required): Database name
  - container (string, required): Container name
  - query_vector (array<number>, required): The query embedding vector
  - top_k (integer, optional, default 10): Number of nearest neighbors
  - filter (string, optional): SQL WHERE clause to pre-filter candidates
  - projection (array<string>, optional): Fields to return (exclude embedding by default)

Returns:
  - results (array): Documents with similarity scores
  - request_charge (number): RU cost
```

**Implementation notes:**

- Build the VectorDistance SQL function call internally
- Always exclude the embedding vector field from results by default (it’s huge and wastes tokens)
- Support optional hybrid search by combining vector distance with a text filter

### 4.7 `cosmos_list_databases`

List all databases in the account (convenience tool alternative to using resources).

```
Parameters: none

Returns:
  - databases (array<string>): Database IDs
```

### 4.8 `cosmos_list_containers`

List containers in a database with key metadata.

```
Parameters:
  - database (string, required): Database name

Returns:
  - containers (array):
    Each: { id, partitionKeyPath, indexingPolicy_summary, defaultTtl, vectorIndexes }
```

### 4.9 `cosmos_get_schema`

Infer schema by sampling documents from a container.

```
Parameters:
  - database (string, required): Database name
  - container (string, required): Container name
  - sample_size (integer, optional, default 20): Documents to sample

Returns:
  - schema (object): JSON Schema representation
  - sample_count (number): Documents actually sampled
  - partition_key_path (string): The container's partition key
```

**Implementation notes:**

- Query N random documents (using `ORDER BY c._ts DESC` for recency or a random offset approach)
- Merge property sets across samples to produce a union schema
- Detect types, required vs optional fields, nested objects, arrays
- Cache the result per the configured TTL

### 4.10 `cosmos_analyze_diagnostics`

Parse and explain a CosmosDiagnostics JSON blob (the tool for support scenarios).

```
Parameters:
  - diagnostics_json (string, required): Raw CosmosDiagnostics JSON string

Returns:
  - summary (string): Human-readable explanation
  - issues (array<string>): Detected problems (high latency hops, retries, 429s, region failovers)
  - recommendations (array<string>): Actionable suggestions
  - metrics (object): { totalLatencyMs, requestCharge, retryCount, regionsContacted }
```

-----

## 5. MCP Resources Specification

Resources are read-only data the agent can inspect for context. Implement with `[McpServerResourceType]`.

### 5.1 `cosmos://databases`

Lists all database IDs. URI: `cosmos://databases`

### 5.2 `cosmos://{database}/containers`

Lists containers in a database with partition key paths. URI template: `cosmos://{database}/containers`

### 5.3 `cosmos://{database}/{container}/schema`

Inferred JSON Schema from document sampling. URI template: `cosmos://{database}/{container}/schema`

### 5.4 `cosmos://{database}/{container}/indexing-policy`

Current indexing policy as JSON. URI template: `cosmos://{database}/{container}/indexing-policy`

### 5.5 `cosmos://{database}/{container}/throughput`

Current provisioned throughput and autoscale settings. URI template: `cosmos://{database}/{container}/throughput`

-----

## 6. MCP Prompts Specification

Prompts are reusable templates that guide the agent through multi-step workflows. Implement with `[McpServerPromptType]`.

### 6.1 `explore_data`

Guided data exploration workflow.

```
Arguments:
  - database (string): Target database
  - container (string): Target container
  - goal (string): What the user wants to learn about their data

Template generates a system message instructing the agent to:
1. Read the schema resource for the container
2. Read the indexing policy resource
3. Run a sample query to understand data distribution
4. Analyze partition key cardinality
5. Report findings relevant to the user's goal
```

### 6.2 `optimize_query`

Query optimization assistant.

```
Arguments:
  - database (string): Target database
  - container (string): Target container
  - query (string): The SQL query to optimize

Template generates instructions to:
1. Read the indexing policy
2. Run the query with diagnostics
3. Analyze RU consumption and latency
4. Check if the query is cross-partition
5. Suggest index additions or query rewrites
6. Run the optimized version and compare RU cost
```

### 6.3 `model_data`

Data modeling assistant for new scenarios.

```
Arguments:
  - scenario (string): Description of the application scenario
  - access_patterns (string): How data will be queried

Template generates instructions to:
1. Analyze the access patterns
2. Suggest document structure (embed vs. reference)
3. Recommend partition key
4. Propose indexing policy
5. Estimate RU costs for the described patterns
```

-----

## 7. Security Requirements

### 7.1 Operation Filtering

- Default mode: read-only (query + point reads + schema discovery)
- Writes must be explicitly opted in via `McpOperations.Write`
- Container management (create/delete containers) must be separately opted in via `McpOperations.Admin`
- Individual tools can be disabled: `options.DisabledTools = ["cosmos_delete_item"]`

### 7.2 Query Sanitization

- Reject queries containing DDL keywords (CREATE, ALTER, DROP, TRUNCATE)
- Enforce parameterized queries when values are provided
- Cap query text length
- Cap `MaxItemCount` to prevent unbounded result sets
- Log all queries for audit when enabled

### 7.3 Database/Container Scoping

- `DatabaseFilter` and `ContainerFilter` predicates run before ANY operation
- If a tool targets a filtered-out database/container, return a clear permission error
- Never leak the names of filtered-out databases/containers in list operations

### 7.4 Credentials

- The MCP server NEVER handles credentials directly
- It always uses the `CosmosClient` instance provided by the host application
- Support `DefaultAzureCredential`, connection strings, and key-based auth — whatever the host uses

-----

## 8. Schema Discovery Implementation

The schema inferrer is critical for making the MCP server useful — agents need to know what fields exist before they can write meaningful queries.

```csharp
public class SchemaInferrer
{
    /// Sample N documents from a container and produce a merged JSON Schema.
    ///
    /// Algorithm:
    /// 1. Query: SELECT TOP {sampleSize} * FROM c ORDER BY c._ts DESC
    ///    (biases toward recent docs, which are most representative)
    /// 2. For each document, recursively extract property names, types, and nesting
    /// 3. Merge across all samples:
    ///    - Property appears in all docs → "required"
    ///    - Property type varies → union type ["string", "number"]
    ///    - Nested objects → recursive schema
    ///    - Arrays → infer item schema from first few elements
    /// 4. Annotate with:
    ///    - Partition key path (from ContainerProperties)
    ///    - System properties (_rid, _ts, _etag, _self) marked as system
    ///    - Unique key paths if configured
    /// 5. Cache with the configured TTL
    ///
    /// Output: standard JSON Schema (draft 2020-12) with custom extensions
    /// for Cosmos DB metadata (x-cosmos-partition-key, x-cosmos-system-property)
    public async Task<JsonSchema> InferSchemaAsync(
        Container container,
        int sampleSize = 20,
        CancellationToken ct = default);
}
```

-----

## 9. Technology Stack & Dependencies

```xml
<!-- Target framework -->
<TargetFramework>net8.0</TargetFramework>  <!-- Match the Cosmos SDK's minimum -->

<!-- Required dependencies -->
<PackageReference Include="Microsoft.Azure.Cosmos" Version="3.58.*" />
<PackageReference Include="ModelContextProtocol" Version="1.2.*" />
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.2.*" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.*" />

<!-- Optional: for JSON Schema generation -->
<PackageReference Include="System.Text.Json" Version="8.0.*" />
```

Key design decisions:

- Use `System.Text.Json` exclusively (no Newtonsoft.Json dependency — the Cosmos SDK already drags that in, but this package should not add its own)
- Use the official C# MCP SDK’s attribute-based tool registration (`[McpServerTool]`, `[McpServerToolType]`)
- Support both stdio and Streamable HTTP transports from the same codebase
- Target .NET 8+ (aligns with Cosmos DB SDK minimum and current LTS)

-----

## 10. Build Plan — Phase-by-Phase

### Phase 1: Skeleton & Core Tools (Week 1)

**Goal:** Working MCP server with query and point read capabilities, testable via MCP Inspector.

1. Create solution `Microsoft.Azure.Cosmos.Mcp.sln`
1. Create class library project `Microsoft.Azure.Cosmos.Mcp`
1. Add NuGet references: `ModelContextProtocol`, `Microsoft.Azure.Cosmos`
1. Implement `CosmosMcpServerExtensions` with `AddCosmosMcpServer()` DI registration
1. Implement `CosmosMcpOptions` configuration class
1. Implement `QueryTool` — parameterized SQL execution with RU reporting
1. Implement `PointReadTool` — read by id + partition key
1. Implement `DatabaseListResource` — enumerate databases
1. Implement `ContainerListResource` — enumerate containers with metadata
1. Create a console app host with stdio transport for testing
1. Test with MCP Inspector: list tools, execute a query, read an item
1. Test with Claude Code or VS Code Copilot MCP config

### Phase 2: Schema Discovery & Resources (Week 2)

1. Implement `SchemaInferrer` with document sampling and type merging
1. Implement `SchemaCache` with TTL-based invalidation
1. Implement `SchemaDiscoveryResource`
1. Implement `IndexingPolicyResource`
1. Implement `ThroughputResource`
1. Implement `cosmos_get_schema` tool (wraps SchemaInferrer)
1. Implement `cosmos_list_containers` tool with full metadata
1. Test: agent should be able to discover schema, then write meaningful queries without being told the field names

### Phase 3: Write Operations & Security (Week 2-3)

1. Implement `McpOperations` flags enum and operation filtering
1. Implement `QuerySanitizer`
1. Implement `DatabaseFilter` and `ContainerFilter` predicate pipeline
1. Implement `UpsertTool` (gated behind Write flag)
1. Implement `DeleteTool` (gated behind Write flag)
1. Implement `PatchTool` (gated behind Write flag)
1. Write tests verifying tools are NOT exposed when flags are off
1. Write tests verifying filtered containers don’t leak names

### Phase 4: Vector Search & Prompts (Week 3)

1. Implement `VectorSearchTool` with configurable path/dimensions/distance function
1. Implement `ExploreDataPrompt`
1. Implement `OptimizeQueryPrompt`
1. Implement `ModelDataPrompt`
1. Test the prompt-guided workflows end to end with an agent

### Phase 5: Diagnostics & HTTP Transport (Week 3-4)

1. Implement `DiagnosticsTool` — parse CosmosDiagnostics JSON, extract issues, generate recommendations
1. Add Streamable HTTP transport support via `ModelContextProtocol.AspNetCore`
1. Implement `MapCosmosMcpServer()` endpoint mapping extension
1. Create a sample ASP.NET Core host project
1. Test with remote MCP clients

### Phase 6: Polish & Distribution (Week 4)

1. Add XML doc comments on all public APIs
1. Create README.md with quickstart examples
1. Create `mcp.json` / `.mcp/server.json` configuration examples for VS Code and VS 2026
1. Package as NuGet with proper metadata
1. Consider packaging as a `dotnet tool` for standalone stdio usage
1. Write integration tests against the Cosmos DB emulator

-----

## 11. Testing Strategy

### Unit Tests

- Each tool: verify correct SQL generation, parameter handling, error cases
- Schema inferrer: test with varied document shapes (nested, arrays, mixed types, missing fields)
- Security: verify operation filtering, query sanitization, container scoping

### Integration Tests (Cosmos Emulator)

- Spin up the vNext emulator via Docker/Testcontainers
- Create test database with known data
- Exercise full MCP tool flow: discover → schema → query → read → write → delete
- Verify RU reporting accuracy
- Test vector search with pre-populated embeddings (if emulator supports)

### End-to-End Tests

- Connect via MCP Inspector, verify tool discovery and execution
- Connect via VS Code Copilot MCP config, verify agent can explore data autonomously
- Connect via Claude Code MCP config, verify multi-step workflows

-----

## 12. Sample MCP Client Configurations

### VS Code / Claude Code (stdio)

```json
{
  "servers": {
    "cosmos-db": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "./MyApp.CosmosMcp/"],
      "env": {
        "COSMOS_CONNECTION_STRING": "${env:COSMOS_CONNECTION_STRING}"
      }
    }
  }
}
```

### VS Code / Claude Code (HTTP, if app is already running)

```json
{
  "servers": {
    "cosmos-db": {
      "type": "http",
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

### Published as dotnet tool

```json
{
  "servers": {
    "cosmos-db": {
      "type": "stdio",
      "command": "dnx",
      "args": ["Microsoft.Azure.Cosmos.Mcp", "--connection-string", "${env:COSMOS_CONNECTION_STRING}"]
    }
  }
}
```

-----

## 13. Key Design Principles

1. **Reuse, don’t reinvent.** The existing `CosmosClient` handles auth, retries, connection pooling, region routing. This package just exposes it via MCP.
1. **Secure by default.** Read-only mode unless explicitly configured. Filter databases/containers. Sanitize queries. Never handle credentials.
1. **Token-efficient.** Schema discovery caches results. Query results cap at reasonable limits. Vector embeddings are excluded from output by default. Tools return concise summaries, not raw diagnostics blobs.
1. **10-20 well-designed tools, not 50.** Follow the NDepend guidance: the sweet spot for MCP is focused, high-level tools. Each tool should map to a clear user intent.
1. **Progressive disclosure.** The minimal setup (read-only, all containers) works with two lines of code. Advanced features (write ops, vector search, scoping, diagnostics) are opt-in.
1. **Cross-SDK portability.** Design the tool names, parameter schemas, and return types so they could be implemented identically in Python, Java, Go, and Rust SDKs in the future. Use the same tool names, same parameter names, same return structure.

-----

## 14. Success Criteria for the Prototype

The prototype is successful when:

- [ ] A developer can `dotnet add package Microsoft.Azure.Cosmos.Mcp`, add two lines of config, and have a working MCP server
- [ ] Claude Code or GitHub Copilot can autonomously: discover databases → pick a container → infer schema → write and execute a meaningful query — with no human guidance beyond “explore my data”
- [ ] An agent can answer “what are my top 10 customers by order count?” against an unknown schema by using the schema discovery + query tools
- [ ] Write operations are provably gated — if `AllowedOperations` doesn’t include `Write`, no write tool appears in the MCP tool list
- [ ] Filtered containers never leak — the agent cannot discover or operate on containers excluded by `ContainerFilter`
- [ ] The full workflow works with both stdio and Streamable HTTP transports
- [ ] RU costs are reported with every operation, making cost awareness native to agent interactions