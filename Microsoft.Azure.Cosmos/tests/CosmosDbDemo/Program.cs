using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

class Program
{
    // Cosmos DB Emulator values
    private static readonly string EndpointUrl = "https://localhost:8081";
    private static readonly string PrimaryKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private static readonly string DatabaseId = "ToDoList";
    private static readonly string ContainerId = "Items";

    static async Task Main()
    {
        try
        {
            Console.WriteLine("Beginning operations...");

            using CosmosClient client = new(
                accountEndpoint: EndpointUrl,
                authKeyOrResourceToken: PrimaryKey,
                new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway }
            );

            // Create database if it doesn't exist
            Database database = await client.CreateDatabaseIfNotExistsAsync(DatabaseId);
            Console.WriteLine($"Created database: {database.Id}");

            // Create container if it doesn't exist
            Container container = await database.CreateContainerIfNotExistsAsync(ContainerId, "/id");
            Console.WriteLine($"Created container: {container.Id}");

            // Create a sample item
            TodoItem todoItem = new TodoItem
            {
                id = Guid.NewGuid().ToString(),
                Title = "Learn Cosmos DB",
                IsComplete = false
            };

            // Add the item
            await container.CreateItemAsync(todoItem);
            Console.WriteLine($"Created item: {todoItem.id}");

            string[] someStringArray = ["a"];
            IOrderedQueryable<TodoItem> queryable = container.GetItemLinqQueryable<TodoItem>();
            IQueryable<TodoItem> query = queryable.Where(item => someStringArray.Contains(item.id));

            using FeedIterator<TodoItem> feed = query.ToFeedIterator();
            
            while (feed.HasMoreResults)
            {
                foreach(TodoItem item in await feed.ReadNextAsync())
                {
                    Console.WriteLine($"Item: {item.id}");
                }
            }
        }
        catch (CosmosException cosmosEx)
        {
            Console.WriteLine($"Cosmos DB Error: {cosmosEx.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    
    private class TodoItem
    {
#pragma warning disable IDE1006 // Naming Styles
        public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        public string Title { get; set; }
        public bool IsComplete { get; set; }
        public string[] ArrayField { get; set; }
    }
}