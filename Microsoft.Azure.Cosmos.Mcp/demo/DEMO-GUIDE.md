# Cosmos DB MCP Server — Demo Guide

This guide walks through a live demo of the SDK-embedded MCP server, showing how AI agents can explore, query, and operate on Cosmos DB data with zero infrastructure.

---

## Setup (One-Time)

### 1. Seed the demo data

```bash
cd demo/SeedData

# Option A: Connection string (recommended for demo accounts)
COSMOS_CONNECTION_STRING="AccountEndpoint=https://nalu.documents.azure.com:443/;AccountKey=YOUR_KEY" dotnet run

# Option B: AAD/Entra ID auth
AZURE_TENANT_ID="your-tenant-id" dotnet run https://nalu.documents.azure.com:443/
```

This creates 3 databases with realistic data:

| Database | Containers | Documents | Scenario |
|----------|-----------|-----------|----------|
| **ecommerce-demo** | `products` (by category), `orders` (by customerId) | 10 products, 8 orders | E-commerce app with nested specs, tags, shipping |
| **iot-telemetry-demo** | `sensor-readings` (by deviceId), `device-registry` (by location) | 192 readings, 4 devices | IoT monitoring with time-series data, warnings, TTL |
| **support-tickets-demo** | `tickets` (by priority) | 8 tickets | SDK support triage with real-world categories |

### 2. Configure the MCP server

```bash
cd samples/CosmosMcp.StdioHost

# Set the connection string
export COSMOS_CONNECTION_STRING="AccountEndpoint=https://nalu.documents.azure.com:443/;AccountKey=YOUR_KEY"
```

### 3. Point your MCP client at it

Add to your MCP config (VS Code `.vscode/mcp.json`, Claude Code, etc.):

```json
{
  "servers": {
    "cosmos-db": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "path/to/Microsoft.Azure.Cosmos.Mcp/samples/CosmosMcp.StdioHost/"],
      "env": {
        "COSMOS_CONNECTION_STRING": "AccountEndpoint=https://nalu.documents.azure.com:443/;AccountKey=YOUR_KEY"
      }
    }
  }
}
```

---

## Demo Scenarios

### 🎯 Scenario 1: "Explore my data" (Customer value)

**What to show:** An agent autonomously discovers databases, infers schema, and answers questions — with no prior knowledge of the data.

**Prompt to the agent:**
> I just connected to a Cosmos DB account. Explore it and tell me what data is in there.

**What happens behind the scenes:**
1. Agent calls `cosmos_list_databases` → finds 3 databases
2. Agent calls `cosmos_list_containers` for each → discovers containers + partition keys
3. Agent calls `cosmos_get_schema` → infers field types, required fields, nested objects
4. Agent runs sample queries to understand data distribution
5. Agent reports a summary of all databases, document shapes, and interesting patterns

**Why this matters for customers:**
- Zero learning curve — no need to memorize Cosmos SQL syntax or field names
- Instant onboarding for new team members joining a project
- Data discovery without leaving the IDE

---

### 🎯 Scenario 2: "Answer a business question" (Customer value)

**Prompt:**
> What are my top 3 customers by total order value? Include their names and cities.

**What happens:**
1. Agent reads schema of `orders` container
2. Writes and executes:
   ```sql
   SELECT c.customerName, c.shippingAddress.city, SUM(c.total) as totalSpend
   FROM c GROUP BY c.customerName, c.shippingAddress.city
   ```
3. Returns results with RU cost

**Expected output:**
| Customer | City | Total Spend |
|----------|------|------------|
| Alice Johnson | Seattle | $1,244.94 |
| Carol Williams | Denver | $529.98 |
| Bob Smith | Portland | $489.98 |

**Follow-up prompts to try:**
- "Which products are out of stock?"
- "Show me all orders from Alice Johnson"
- "What's the average order value by status?"

---

### 🎯 Scenario 3: "IoT monitoring" (Customer value)

**Prompt:**
> Are any IoT sensors showing warnings? What's the temperature trend over the last 24 hours?

**What happens:**
1. Agent discovers `iot-telemetry-demo` database, sees `sensor-readings` container
2. Queries for warning status:
   ```sql
   SELECT c.deviceId, c.sensorType, c.value, c.unit, c.timestamp
   FROM c WHERE c.status = 'warning' ORDER BY c.timestamp DESC
   ```
3. Queries temperature trend for device-temp-001
4. Reports findings with pattern analysis

**Why this matters:** Real-time IoT data exploration without building dashboards first.

---

### 🎯 Scenario 4: "Support ticket triage" (SDK Team value)

**Prompt:**
> Show me all open high-priority support tickets and summarize the issues.

**Expected result:**
The agent queries `support-tickets-demo/tickets` and summarizes:
- **ticket-5001:** Production latency spike at Contoso (investigating)
- **ticket-5002:** 429 throttling at Fabrikam during peak hours (in progress)

**Follow-up:**
> What's the most common category of support tickets?

> Are there any patterns — which customers have the most tickets?

---

### 🎯 Scenario 5: "Query optimization" (SDK Team value)

**Use the optimize_query prompt:**
> Use the optimize_query prompt for ecommerce-demo/products with this query:
> `SELECT * FROM c WHERE c.price > 100 ORDER BY c.rating DESC`

**What happens:**
1. Agent reads the indexing policy — sees composite index on (category, price) but not (price, rating)
2. Runs the query, notes RU cost
3. Suggests adding a composite index on `/price ASC, /rating DESC`
4. Compares before/after RU cost

**Why this matters for the SDK team:**
- Demonstrates the SDK's query + diagnostics APIs in a consumable way
- Shows how RU cost transparency helps customers optimize
- Natural showcase for indexing policy features

---

### 🎯 Scenario 6: "Data modeling advice" (SDK Team value)

**Use the model_data prompt:**
> I'm building a food delivery app. Access patterns: (1) Get restaurant by ID, (2) List restaurants near a location, (3) Get all orders for a customer, (4) Get active orders for a restaurant. Recommend a data model.

**What happens:**
The agent generates concrete recommendations for partition keys, document structure (embed menus in restaurant docs vs. separate), indexing policy, and estimated RU costs — all grounded in Cosmos DB best practices.

**Why this matters:** Turns the SDK into a consulting tool, not just an API.

---

### 🎯 Scenario 7: "Write operations" (Live coding)

**⚠️ Enable writes first** by uncommenting `options.AllowedOperations = McpOperations.All;` in the stdio host.

**Prompt:**
> Add a new product to the ecommerce-demo: a wireless mouse called "ClickPro X2", category electronics, price $59.99, rating 4.6, in stock.

**What happens:**
1. Agent calls `cosmos_upsert_item` with a properly structured document
2. Returns the created document with RU cost
3. Subsequent query confirms it's there

**Follow-up:**
> Update the price of ClickPro X2 to $54.99 using a patch operation.

Agent uses `cosmos_patch_item` with `[{"op": "replace", "path": "/price", "value": 54.99}]`

---

### 🎯 Scenario 8: "Security guardrails" (Trust & Safety)

**Show that the MCP server is secure by default:**

1. **Read-only mode:** Without `McpOperations.Write`, the agent literally cannot see write tools — they aren't registered
2. **Query sanitization:** Try asking the agent to run `DROP` or `DELETE FROM` — it's rejected
3. **Container filtering:** Configure `ContainerFilter = (db, c) => c.Id != "tickets"` and show that the agent can't discover or query the tickets container

---

## Key Talking Points

### For Customers
- **"Two lines of code"** — `AddCosmosMcpServer()` + `WithStdioTransport()` is all you need
- **RU transparency** — every operation reports its cost, making AI agents cost-aware
- **Secure by default** — read-only unless explicitly enabled, query sanitization, container scoping
- **Works with your existing auth** — reuses the `CosmosClient` you already have

### For the SDK Team
- **SDK adoption driver** — makes the .NET SDK the easiest way to connect AI to Cosmos DB
- **Showcases SDK features** — schema discovery, patch operations, diagnostics, indexing policies all get natural exposure
- **Cross-SDK portable** — tool names and parameter schemas are designed to be identical across Python/Java/Go/Rust SDKs
- **Dogfooding** — using MCP internally for support ticket triage and customer debugging

### vs. MCPToolKit (the standalone server)
- MCPToolKit = production deployment for platform teams (Docker, Bicep, Entra ID)
- This = developer inner loop for individual engineers (NuGet, 2 lines, instant)
- They're complementary, not competing

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| "CosmosClient not registered" | Ensure `builder.Services.AddSingleton(new CosmosClient(...))` is called before `AddCosmosMcpServer()` |
| Agent can't see write tools | Set `options.AllowedOperations = McpOperations.All` (or include `McpOperations.Write`) |
| "Query contains disallowed keywords" | Only SELECT queries are allowed — this is intentional security |
| 429 errors during demo | The demo databases use 400 RU/s — increase or use autoscale |
| AAD auth fails with 403/5300 | The account needs RBAC data plane enabled, or use connection string auth instead |
