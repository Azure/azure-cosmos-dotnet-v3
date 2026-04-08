// ------------------------------------------------------------
// Demo Data Seeder for Cosmos DB MCP Server
// Creates databases, containers, and sample data for the demo.
// ------------------------------------------------------------

using Azure.Identity;
using Microsoft.Azure.Cosmos;
using System.Net;

string endpoint = args.Length > 0 ? args[0] : "https://nalu.documents.azure.com:443/";

string? connectionString = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");

CosmosClient client;
if (!string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("Connecting via connection string...");
    client = new CosmosClient(connectionString);
}
else
{
    string? tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
    Console.WriteLine($"Connecting to {endpoint} via DefaultAzureCredential (tenant: {tenantId ?? "default"})...");
    var credOptions = new DefaultAzureCredentialOptions();
    if (!string.IsNullOrEmpty(tenantId))
    {
        credOptions.TenantId = tenantId;
    }
    client = new CosmosClient(endpoint, new DefaultAzureCredential(credOptions));
}

// ─── Database 1: ecommerce-demo ───
await SeedEcommerceAsync(client);

// ─── Database 2: iot-telemetry-demo ───
await SeedIotTelemetryAsync(client);

// ─── Database 3: support-tickets-demo ───
await SeedSupportTicketsAsync(client);

Console.WriteLine("\n✅ All demo data seeded successfully!");

// ═══════════════════════════════════════════════════════════════
// E-Commerce Database
// ═══════════════════════════════════════════════════════════════
static async Task SeedEcommerceAsync(CosmosClient client)
{
    Console.WriteLine("\n📦 Seeding ecommerce-demo...");
    Database db = client.GetDatabase("ecommerce-demo");

    Console.WriteLine("  Inserting products...");
    Container products = db.GetContainer("products");

    var productData = new Dictionary<string, object>[]
    {
        new() { ["id"] = "prod-001", ["name"] = "Wireless Noise-Cancelling Headphones", ["category"] = "electronics", ["price"] = 299.99, ["rating"] = 4.7, ["inStock"] = true, ["tags"] = new[] { "audio", "wireless", "premium" }, ["brand"] = "SoundMax", ["specs"] = new Dictionary<string, string> { ["battery"] = "30h", ["driver"] = "40mm", ["weight"] = "250g" } },
        new() { ["id"] = "prod-002", ["name"] = "USB-C Hub 7-in-1", ["category"] = "electronics", ["price"] = 49.99, ["rating"] = 4.3, ["inStock"] = true, ["tags"] = new[] { "accessories", "usb-c" }, ["brand"] = "TechLink", ["specs"] = new Dictionary<string, string> { ["ports"] = "7", ["power"] = "100W passthrough", ["hdmi"] = "4K@60Hz" } },
        new() { ["id"] = "prod-003", ["name"] = "Mechanical Keyboard - Cherry MX Blue", ["category"] = "electronics", ["price"] = 159.99, ["rating"] = 4.8, ["inStock"] = false, ["tags"] = new[] { "keyboard", "mechanical", "gaming" }, ["brand"] = "KeyCraft", ["specs"] = new Dictionary<string, string> { ["switches"] = "Cherry MX Blue", ["layout"] = "TKL", ["backlight"] = "RGB" } },
        new() { ["id"] = "prod-004", ["name"] = "Running Shoes - CloudStride Pro", ["category"] = "footwear", ["price"] = 189.99, ["rating"] = 4.5, ["inStock"] = true, ["tags"] = new[] { "running", "performance" }, ["brand"] = "CloudStride", ["specs"] = new Dictionary<string, string> { ["weight"] = "280g", ["drop"] = "8mm", ["cushion"] = "CloudFoam" } },
        new() { ["id"] = "prod-005", ["name"] = "Hiking Boots - TrailMaster X", ["category"] = "footwear", ["price"] = 249.99, ["rating"] = 4.6, ["inStock"] = true, ["tags"] = new[] { "hiking", "waterproof", "outdoor" }, ["brand"] = "TrailMaster", ["specs"] = new Dictionary<string, string> { ["waterproof"] = "true", ["ankle"] = "high", ["sole"] = "Vibram" } },
        new() { ["id"] = "prod-006", ["name"] = "Organic Coffee Beans - Dark Roast", ["category"] = "food", ["price"] = 18.99, ["rating"] = 4.9, ["inStock"] = true, ["tags"] = new[] { "coffee", "organic", "fair-trade" }, ["brand"] = "BeanOrigin", ["specs"] = new Dictionary<string, string> { ["origin"] = "Colombia", ["roast"] = "dark", ["weight"] = "1kg" } },
        new() { ["id"] = "prod-007", ["name"] = "Protein Powder - Vanilla 2lb", ["category"] = "food", ["price"] = 34.99, ["rating"] = 4.2, ["inStock"] = true, ["tags"] = new[] { "supplement", "protein", "fitness" }, ["brand"] = "FitFuel", ["specs"] = new Dictionary<string, string> { ["servings"] = "30", ["protein_per_serving"] = "25g", ["flavor"] = "vanilla" } },
        new() { ["id"] = "prod-008", ["name"] = "Smart Watch - FitTrack Pro", ["category"] = "electronics", ["price"] = 399.99, ["rating"] = 4.4, ["inStock"] = true, ["tags"] = new[] { "smartwatch", "fitness", "health" }, ["brand"] = "FitTrack", ["specs"] = new Dictionary<string, string> { ["battery"] = "7 days", ["display"] = "AMOLED 1.4in", ["gps"] = "true" } },
        new() { ["id"] = "prod-009", ["name"] = "Yoga Mat - Premium 6mm", ["category"] = "fitness", ["price"] = 45.99, ["rating"] = 4.7, ["inStock"] = true, ["tags"] = new[] { "yoga", "exercise", "eco-friendly" }, ["brand"] = "ZenFit", ["specs"] = new Dictionary<string, string> { ["thickness"] = "6mm", ["material"] = "natural rubber", ["length"] = "183cm" } },
        new() { ["id"] = "prod-010", ["name"] = "Standing Desk Converter", ["category"] = "furniture", ["price"] = 279.99, ["rating"] = 4.1, ["inStock"] = true, ["tags"] = new[] { "desk", "ergonomic", "office" }, ["brand"] = "ErgoRise", ["specs"] = new Dictionary<string, string> { ["max_height"] = "20in", ["weight_capacity"] = "35lbs", ["surface"] = "32x22in" } },
    };

    foreach (var p in productData)
    {
        await products.UpsertItemAsync(p, new PartitionKey((string)p["category"]));
    }
    Console.WriteLine($"    ✓ {productData.Length} products upserted");

    // Orders container
    Console.WriteLine("  Inserting orders...");
    Container orders = db.GetContainer("orders");

    var orderData = new[]
    {
        new { id = "ord-1001", customerId = "cust-100", customerName = "Alice Johnson", status = "delivered", orderDate = "2025-12-15", total = 349.98, items = new[] { new { productId = "prod-001", name = "Wireless Headphones", qty = 1, price = 299.99 }, new { productId = "prod-002", name = "USB-C Hub", qty = 1, price = 49.99 } }, shippingAddress = new { city = "Seattle", state = "WA", zip = "98101" } },
        new { id = "ord-1002", customerId = "cust-101", customerName = "Bob Smith", status = "shipped", orderDate = "2026-01-03", total = 189.99, items = new[] { new { productId = "prod-004", name = "Running Shoes", qty = 1, price = 189.99 } }, shippingAddress = new { city = "Portland", state = "OR", zip = "97201" } },
        new { id = "ord-1003", customerId = "cust-100", customerName = "Alice Johnson", status = "processing", orderDate = "2026-03-20", total = 434.98, items = new[] { new { productId = "prod-008", name = "Smart Watch", qty = 1, price = 399.99 }, new { productId = "prod-007", name = "Protein Powder", qty = 1, price = 34.99 } }, shippingAddress = new { city = "Seattle", state = "WA", zip = "98101" } },
        new { id = "ord-1004", customerId = "cust-102", customerName = "Carol Williams", status = "delivered", orderDate = "2025-11-28", total = 529.98, items = new[] { new { productId = "prod-005", name = "Hiking Boots", qty = 1, price = 249.99 }, new { productId = "prod-010", name = "Standing Desk", qty = 1, price = 279.99 } }, shippingAddress = new { city = "Denver", state = "CO", zip = "80201" } },
        new { id = "ord-1005", customerId = "cust-103", customerName = "David Lee", status = "delivered", orderDate = "2026-02-14", total = 205.98, items = new[] { new { productId = "prod-003", name = "Mechanical Keyboard", qty = 1, price = 159.99 }, new { productId = "prod-009", name = "Yoga Mat", qty = 1, price = 45.99 } }, shippingAddress = new { city = "San Francisco", state = "CA", zip = "94102" } },
        new { id = "ord-1006", customerId = "cust-101", customerName = "Bob Smith", status = "cancelled", orderDate = "2026-03-01", total = 299.99, items = new[] { new { productId = "prod-001", name = "Wireless Headphones", qty = 1, price = 299.99 } }, shippingAddress = new { city = "Portland", state = "OR", zip = "97201" } },
        new { id = "ord-1007", customerId = "cust-104", customerName = "Eve Martinez", status = "delivered", orderDate = "2026-01-22", total = 56.97, items = new[] { new { productId = "prod-006", name = "Coffee Beans", qty = 3, price = 18.99 } }, shippingAddress = new { city = "Austin", state = "TX", zip = "73301" } },
        new { id = "ord-1008", customerId = "cust-100", customerName = "Alice Johnson", status = "delivered", orderDate = "2025-10-05", total = 459.98, items = new[] { new { productId = "prod-003", name = "Mechanical Keyboard", qty = 1, price = 159.99 }, new { productId = "prod-001", name = "Wireless Headphones", qty = 1, price = 299.99 } }, shippingAddress = new { city = "Seattle", state = "WA", zip = "98101" } },
    };

    foreach (var o in orderData)
    {
        await orders.UpsertItemAsync(o, new PartitionKey(o.customerId));
    }
    Console.WriteLine($"    ✓ {orderData.Length} orders upserted");
}

// ═══════════════════════════════════════════════════════════════
// IoT Telemetry Database
// ═══════════════════════════════════════════════════════════════
static async Task SeedIotTelemetryAsync(CosmosClient client)
{
    Console.WriteLine("\n🌡️  Seeding iot-telemetry-demo...");
    Database db = client.GetDatabase("iot-telemetry-demo");

    Console.WriteLine("  Inserting sensor readings...");
    Container readings = db.GetContainer("sensor-readings");

    var devices = new[] { "device-temp-001", "device-temp-002", "device-humidity-001", "device-pressure-001" };
    var locations = new[] { "Building-A Floor-1", "Building-A Floor-2", "Building-B Floor-1", "Building-B Floor-2" };
    var random = new Random(42);
    int count = 0;

    for (int di = 0; di < devices.Length; di++)
    {
        string device = devices[di];
        string sensorType = device.Contains("temp") ? "temperature" : device.Contains("humidity") ? "humidity" : "pressure";
        string location = locations[di];

        for (int hour = 0; hour < 48; hour++)
        {
            DateTime ts = DateTime.UtcNow.AddHours(-48 + hour);
            double value = sensorType switch
            {
                "temperature" => 20 + random.NextDouble() * 10 + (hour % 24 > 8 && hour % 24 < 18 ? 5 : 0),
                "humidity" => 40 + random.NextDouble() * 30,
                "pressure" => 1010 + random.NextDouble() * 20,
                _ => 0
            };

            string status = value > (sensorType == "temperature" ? 30 : sensorType == "humidity" ? 65 : 1025) ? "warning" : "normal";

            var reading = new
            {
                id = $"{device}-{ts:yyyyMMddHH}",
                deviceId = device,
                sensorType,
                location,
                timestamp = ts.ToString("o"),
                value = Math.Round(value, 2),
                unit = sensorType == "temperature" ? "°C" : sensorType == "humidity" ? "%" : "hPa",
                status,
                batteryLevel = Math.Max(10.0, 100 - hour * 0.5)
            };

            await readings.UpsertItemAsync(reading, new PartitionKey(device));
            count++;
        }
    }
    Console.WriteLine($"    ✓ {count} sensor readings upserted");

    // Device registry
    Console.WriteLine("  Inserting device registry...");
    Container registry = db.GetContainer("device-registry");

    var deviceEntries = new[]
    {
        new { id = "device-temp-001", location = "Building-A Floor-1", type = "temperature", model = "TempSense Pro 3000", firmware = "2.4.1", installedDate = "2025-06-15", calibrationDue = "2026-06-15", status = "active" },
        new { id = "device-temp-002", location = "Building-A Floor-2", type = "temperature", model = "TempSense Pro 3000", firmware = "2.4.0", installedDate = "2025-08-20", calibrationDue = "2026-08-20", status = "active" },
        new { id = "device-humidity-001", location = "Building-B Floor-1", type = "humidity", model = "HumidWatch X1", firmware = "1.8.3", installedDate = "2025-03-10", calibrationDue = "2026-03-10", status = "needs_calibration" },
        new { id = "device-pressure-001", location = "Building-B Floor-2", type = "pressure", model = "BaroTrack Elite", firmware = "3.1.0", installedDate = "2025-09-01", calibrationDue = "2026-09-01", status = "active" },
    };

    foreach (var d in deviceEntries)
    {
        await registry.UpsertItemAsync(d, new PartitionKey(d.location));
    }
    Console.WriteLine($"    ✓ {deviceEntries.Length} device registry entries upserted");
}

// ═══════════════════════════════════════════════════════════════
// Support Tickets Database
// ═══════════════════════════════════════════════════════════════
static async Task SeedSupportTicketsAsync(CosmosClient client)
{
    Console.WriteLine("\n🎫 Seeding support-tickets-demo...");
    Database db = client.GetDatabase("support-tickets-demo");

    Console.WriteLine("  Inserting tickets...");
    Container tickets = db.GetContainer("tickets");

    var ticketData = new[]
    {
        new { id = "ticket-5001", priority = "high", title = "Production database latency spike", customer = "Contoso Ltd", assignee = "eng-team-alpha", status = "investigating", createdAt = "2026-04-07T14:30:00Z", category = "performance", description = "Customer reports 500ms+ latency on point reads that normally take <10ms. Started after their latest deployment.", tags = new[] { "latency", "production", "p1" }, commentCount = 2 },
        new { id = "ticket-5002", priority = "high", title = "429 throttling during peak hours", customer = "Fabrikam Inc", assignee = "eng-team-beta", status = "in_progress", createdAt = "2026-04-06T09:15:00Z", category = "throttling", description = "Getting 429s between 9am-11am EST daily. Current provisioned throughput is 10,000 RU/s.", tags = new[] { "429", "throttling", "autoscale" }, commentCount = 1 },
        new { id = "ticket-5003", priority = "medium", title = "Cross-partition query optimization needed", customer = "Northwind Traders", assignee = "eng-team-alpha", status = "open", createdAt = "2026-04-05T16:00:00Z", category = "query_optimization", description = "Their main dashboard query scans all partitions and costs 800+ RUs per execution. Need to review partition key strategy.", tags = new[] { "cross-partition", "query", "optimization" }, commentCount = 0 },
        new { id = "ticket-5004", priority = "medium", title = "Schema migration guidance for v2 to v3", customer = "AdventureWorks", assignee = "eng-team-gamma", status = "waiting_customer", createdAt = "2026-04-04T11:00:00Z", category = "migration", description = "Customer wants to restructure their document model from flat to hierarchical. Need guidance on partition key change strategy. 50M documents to migrate.", tags = new[] { "migration", "schema", "partition-key" }, commentCount = 2 },
        new { id = "ticket-5005", priority = "low", title = "Request: Enable vector search on existing container", customer = "WingTip Toys", assignee = "eng-team-beta", status = "open", createdAt = "2026-04-03T13:45:00Z", category = "feature_request", description = "Customer wants to add vector search capability to their product catalog container for semantic product search.", tags = new[] { "vector-search", "feature-request" }, commentCount = 0 },
        new { id = "ticket-5006", priority = "high", title = "Data inconsistency after region failover", customer = "Contoso Ltd", assignee = "eng-team-alpha", status = "resolved", createdAt = "2026-03-28T08:00:00Z", category = "consistency", description = "After a planned failover from East US to West US, customer noticed stale reads for ~30 seconds. Running Session consistency. Root cause: client was not passing session token header on retry.", tags = new[] { "failover", "consistency", "multi-region" }, commentCount = 2 },
        new { id = "ticket-5007", priority = "low", title = "Indexing policy review for cost optimization", customer = "Fabrikam Inc", assignee = "eng-team-gamma", status = "resolved", createdAt = "2026-03-25T10:00:00Z", category = "cost_optimization", description = "Customer paying for indexes on fields they never query. Removed 12 unused index paths, saving ~15% on write RUs.", tags = new[] { "indexing", "cost", "optimization" }, commentCount = 1 },
        new { id = "ticket-5008", priority = "medium", title = "Bulk import performance tuning", customer = "Northwind Traders", assignee = "eng-team-beta", status = "in_progress", createdAt = "2026-04-01T09:00:00Z", category = "performance", description = "Bulk import of 10M documents taking 6 hours. Target is under 2 hours. Using AllowBulkExecution=true. Currently getting ~3000 docs/sec.", tags = new[] { "bulk", "import", "performance" }, commentCount = 1 },
    };

    foreach (var t in ticketData)
    {
        await tickets.UpsertItemAsync(t, new PartitionKey(t.priority));
    }
    Console.WriteLine($"    ✓ {ticketData.Length} support tickets upserted");
}
