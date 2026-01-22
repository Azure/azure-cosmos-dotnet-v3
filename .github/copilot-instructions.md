# GitHub Copilot Instructions for Azure Cosmos DB .NET SDK v3

This repository contains the Azure Cosmos DB .NET SDK version 3, a comprehensive client library for interacting with Azure Cosmos DB for NoSQL. These instructions help GitHub Copilot provide better suggestions when working with this codebase.

## Repository Structure

- **`Microsoft.Azure.Cosmos/src/`** - Core SDK source code
- **`Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/`** - Unit tests (no external dependencies)
- **`Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/`** - Integration tests (require Cosmos DB Emulator)
- **`Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Performance.Tests/`** - Performance benchmark tests
- **`Microsoft.Azure.Cosmos.Samples/`** - Sample applications and tools
- **`Microsoft.Azure.Cosmos.Encryption/`** - Encryption functionality
- **`Microsoft.Azure.Cosmos.Encryption.Custom/`** - Custom encryption providers

## Core Architecture Patterns

### Client Hierarchy
```csharp
CosmosClient -> Database -> Container -> Items/Scripts
```

### Key Classes and Their Purposes
- **`CosmosClient`**: Top-level client for account operations, thread-safe singleton
- **`Database`**: Manages containers and users within a database
- **`Container`**: Handles item operations, queries, and scripts
- **`ItemResponse<T>`**: Wraps responses with metadata (status code, headers, diagnostics)
- **`FeedResponse<T>`**: Handles paginated query results
- **`TransactionalBatch`**: Groups operations for atomic execution

### Resource Management Patterns
```csharp
// CosmosClient should be singleton and disposed at application exit
using var cosmosClient = new CosmosClient(connectionString);

// Use ConfigureAwait(false) for library code
var response = await container.ReadItemAsync<T>(id, partitionKey).ConfigureAwait(false);

// Always handle PartitionKey correctly
var partitionKey = new PartitionKey(partitionKeyValue);
```

## Coding Conventions

### Async/Await Patterns
- Async methods should be suffixed with `Async`
- Return `Task<T>` or `ValueTask<T>` for async operations
- Use `CancellationToken` parameters for cancellable operations

### Error Handling
```csharp
try
{
    var response = await container.ReadItemAsync<T>(id, partitionKey);
    return response.Resource;
}
catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // Handle not found specifically
    return null;
}
catch (CosmosException ex)
{
    // Handle other Cosmos-specific errors
    // Check ex.StatusCode, ex.SubStatusCode, ex.ActivityId
    throw;
}
```

### Diagnostics and Observability
- Use `CosmosDiagnostics` for performance insights
- Leverage `ActivityId` for correlation across requests
- Include telemetry data in exceptions and responses
- Use structured logging with categories like `Microsoft.Azure.Cosmos`

### Performance Best Practices
```csharp
// Prefer stream APIs for large payloads
using var responseStream = await container.ReadItemStreamAsync(id, partitionKey);

// Use bulk operations for multiple items
await container.CreateItemAsync(item, requestOptions: new() { EnableContentResponseOnWrite = false });

// Set request options appropriately
var queryOptions = new QueryRequestOptions
{
    PartitionKey = partitionKey,
    MaxItemCount = 100,
    MaxConcurrency = -1
};
```

## Testing Patterns

### Unit Tests (`Microsoft.Azure.Cosmos.Tests`)
- Mock external dependencies using `Mock<T>` from Moq
- Test classes inherit from appropriate base test classes
- Use `[TestMethod]` and `[TestClass]` attributes
- No external service dependencies
- Focus on business logic, validation, and edge cases

```csharp
[TestClass]
public class MyFeatureTests
{
    [TestMethod]
    public async Task MyMethod_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var mockClient = new Mock<IDocumentClient>();
        // ...
        
        // Act
        var result = await myService.MyMethodAsync();
        
        // Assert
        Assert.AreEqual(expected, result);
    }
}
```

### Emulator Tests (`Microsoft.Azure.Cosmos.EmulatorTests`)
- Require Azure Cosmos DB Emulator running
- Test end-to-end scenarios
- Use real service interactions
- Clean up resources after tests

```csharp
[TestClass]
public class MyIntegrationTests : BaseCosmosClientHelper
{
    [TestInitialize]
    public async Task TestInitialize()
    {
        await base.TestInit();
    }
    
    [TestCleanup]
    public async Task TestCleanup()
    {
        await base.TestCleanup();
    }
}
```

## Common SDK Patterns

### CRUD Operations
```csharp
// Create
var createResponse = await container.CreateItemAsync(item, new PartitionKey(item.pk));

// Read
var readResponse = await container.ReadItemAsync<MyItem>(id, new PartitionKey(pk));

// Update/Replace
var replaceResponse = await container.ReplaceItemAsync(updatedItem, id, new PartitionKey(pk));

// Delete
var deleteResponse = await container.DeleteItemAsync<MyItem>(id, new PartitionKey(pk));
```

### Querying
```csharp
// SQL query with parameters
var query = new QueryDefinition("SELECT * FROM c WHERE c.category = @category")
    .WithParameter("@category", categoryValue);

await foreach (var item in container.GetItemQueryIterator<MyItem>(query))
{
    // Process items
}

// LINQ queries
var linqQuery = container.GetItemLinqQueryable<MyItem>()
    .Where(x => x.Category == categoryValue);
```

### Change Feed Processing
```csharp
var processor = container
    .GetChangeFeedProcessorBuilder<MyItem>("myProcessor", HandleChangesAsync)
    .WithInstanceName("myInstance")
    .WithLeaseContainer(leaseContainer)
    .Build();

await processor.StartAsync();
```

### Batch Operations
```csharp
var batch = container.CreateTransactionalBatch(new PartitionKey(pk));
batch.CreateItem(item1);
batch.ReplaceItem(item2.Id, item2);
batch.DeleteItem(item3.Id);

var batchResponse = await batch.ExecuteAsync();
```

## Security Considerations

### Authentication
```csharp
// Connection string (for development)
var client = new CosmosClient(connectionString);

// Account key
var client = new CosmosClient(endpoint, accountKey);

// Azure AD with DefaultAzureCredential
var client = new CosmosClient(endpoint, new DefaultAzureCredential());

// Custom token credential
var client = new CosmosClient(endpoint, tokenCredential);
```

### Resource Tokens
```csharp
// For fine-grained access control
var client = new CosmosClient(endpoint, resourceToken);
```

## Configuration Patterns

### CosmosClientOptions
```csharp
var options = new CosmosClientOptions
{
    ApplicationRegion = Regions.EastUS2,
    ConnectionMode = ConnectionMode.Direct,
    MaxRetryAttemptsOnRateLimitedRequests = 9,
    MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),
    RequestTimeout = TimeSpan.FromSeconds(60),
    MaxRequestsPerTcpConnection = 10,
    MaxTcpConnectionsPerEndpoint = 16,
    EnableContentResponseOnWrite = false,
    ConsistencyLevel = ConsistencyLevel.Session
};
```

## Documentation Standards

### XML Documentation
- Use `<summary>`, `<param>`, `<returns>`, `<exception>` tags
- Include `<example>` sections with CDATA for code samples
- Reference related types using `<see cref="TypeName"/>`
- Document thread safety, performance implications, and best practices

### Code Comments
- Explain complex business logic and algorithms
- Document performance considerations and trade-offs
- Include references to Azure Cosmos DB documentation
- Use TODO comments sparingly and with tracking items

## Dependencies and Compatibility

### Target Frameworks
- .NET Standard 2.0 for maximum compatibility
- .NET 6.0+ for performance tests and samples

### Key Dependencies
- `Microsoft.Azure.Cosmos.Direct` - Direct transport protocol
- `Microsoft.HybridRow` - HybridRow serialization for batch operations
- `Azure.Core` - Azure SDK common functionality
- `System.Text.Json` - JSON serialization (preferred over Newtonsoft.Json)

### Compatibility Considerations
- Maintain backward compatibility for public APIs
- Use appropriate nullable reference type annotations
- Follow semantic versioning for breaking changes
- Support long-term service (LTS) versions of .NET

## Performance Guidelines

### Memory Management
- Prefer streaming APIs for large payloads
- Dispose of resources properly using `using` statements
- Avoid creating unnecessary allocations in hot paths
- Use object pooling for frequently allocated objects

### Network Optimization
- Use bulk operations when possible
- Implement proper retry policies
- Configure connection pooling appropriately
- Monitor and optimize partition key distribution

### Monitoring and Telemetry
- Use `CosmosDiagnostics` to track request charges and latency
- Implement proper logging for debugging and monitoring
- Use Application Insights or similar for production monitoring
- Track key metrics like RU consumption and throttling

## Common Anti-Patterns to Avoid

1. **Creating multiple CosmosClient instances** - Use singleton pattern
2. **Ignoring partition key design** - Leads to hot partitions and poor performance
3. **Not handling CosmosException properly** - Missing important error context
4. **Using synchronous APIs** - Blocks threads unnecessarily
5. **Not disposing resources** - Can lead to connection pool exhaustion
6. **Hardcoding configuration values** - Use configuration patterns instead
7. **Queries not draining results following continuations** - Always iterate through all pages of query results

## Migration Guidance

### From SDK v2 to v3
- Update namespace from `Microsoft.Azure.Documents` to `Microsoft.Azure.Cosmos`
- Replace `DocumentClient` with `CosmosClient`
- Update async patterns to use new response types
- Migrate connection policies to `CosmosClientOptions`

### Best Practices for Upgrades
- Test thoroughly with both unit and integration tests
- Monitor performance metrics after migration
- Update documentation and samples
- Provide migration guides for breaking changes

This guidance helps ensure consistent, high-quality code that follows Azure Cosmos DB .NET SDK best practices and conventions.