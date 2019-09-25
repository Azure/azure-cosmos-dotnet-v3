namespace Cosmos.Samples.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Samples.Handlers.Models;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Azure.Cosmos.Fluent;
    using Newtonsoft.Json.Schema;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://azure.microsoft.com/en-us/itemation/articles/itemdb-create-account/
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates how to work with custom Handlers in the SDK pipeline
    //
    // 1. LoggingHandler that will log all requests to Application Insights
    // 2. ConcurrencyHandler that will act upon requests that violate ETag concurrency
    // 3. ThrottlingHandler that will use Polly to handle retries on 429s
    // 4. SchemaValidationHandler that will validate items against a JSON schema


    public class Program
    {
        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        // <Main>
        public static async Task Main(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                   .AddJsonFile("appSettings.json")
                   .Build();

            string endpoint = configuration["EndPointUrl"];
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
            }

            string authKey = configuration["AuthorizationKey"];
            if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
            {
                throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
            }

            // Connecting to Emulator. Change if you want a live account
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(endpoint, authKey);

            // Declare a JSON schema to use with the schema validation handler
            var myContainerSchema = JSchema.Parse(@"{
  'type': 'object',
  'properties': {
    'name': {'type': 'string'}
  },
  'required': ['name']
}");

            cosmosClientBuilder.AddCustomHandlers(
                new LoggingHandler(),
                new ConcurrencyHandler(),
                new ThrottlingHandler(),
                new SchemaValidationHandler((database: "mydb", container: "mycoll2", schema: myContainerSchema))
                );

            CosmosClient client = cosmosClientBuilder.Build();

            DatabaseResponse databaseResponse = await client.CreateDatabaseIfNotExistsAsync("mydb");
            Database database = databaseResponse.Database;

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync("mycoll", "/id");
            Container container = containerResponse.Container;

            Item item = new Item()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Item",
                Description = "Some random test item",
                Completed = false
            };

            // Create
            await container.CreateItemAsync<Item>(item, new PartitionKey(item.Id));

            item.Completed = true;

            // Replace
            await container.ReplaceItemAsync<Item>(item, item.Id, new PartitionKey(item.Id));

            // Querying
            FeedIterator<Item> query = container.GetItemQueryIterator<Item>(new QueryDefinition("SELECT * FROM c"), requestOptions: new QueryRequestOptions() { MaxConcurrency = 1});
            List<Item> results = new List<Item>();
            while (query.HasMoreResults)
            {
                FeedResponse<Item> response = await query.ReadNextAsync();

                results.AddRange(response.ToList());
            }

            // Read Item

            ItemResponse<Item> cosmosItemResponse = await container.ReadItemAsync<Item>(item.Id, new PartitionKey(item.Id));

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions()
            {
                IfMatchEtag = cosmosItemResponse.ETag
            };

            // Concurrency

            List<Task<ItemResponse<Item>>> tasks = new List<Task<ItemResponse<Item>>>
            {
                UpdateItemForConcurrency(container, itemRequestOptions, item),
                UpdateItemForConcurrency(container, itemRequestOptions, item)
            };

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (CosmosException ex)
            {
                // Verify that our custom handler caught the scenario
                Debug.Assert(999.Equals(ex.SubStatusCode));
            }

            // Delete
            await container.DeleteItemAsync<Item>(item.Id, new PartitionKey(item.Id));

            // Schema validation

            containerResponse = await database.CreateContainerIfNotExistsAsync("mycoll2", "/id");
            container = containerResponse.Container;

            // Insert an item with invalid schema
            var writeSucceeded = true;
            try
            {
                await container.CreateItemAsync(new { id = "12345" });
            }
            catch (InvalidItemSchemaException)
            {
                writeSucceeded = false;
            }
            Debug.Assert(!writeSucceeded);

            // Insert an item with valid schema
            try
            {
                await container.CreateItemAsync(new { id = "12345", name = "Youri" });
                writeSucceeded = true;
            }
            catch (InvalidItemSchemaException)
            {
                writeSucceeded = false;
            }
            Debug.Assert(writeSucceeded);

            // Update an item with invalid schema
            try
            {
                await container.ReplaceItemAsync(new { id = "12345" }, "12345");
                writeSucceeded = true;
            }
            catch (InvalidItemSchemaException)
            {
                writeSucceeded = false;
            }
            Debug.Assert(!writeSucceeded);

            // Update an item with valid schema
            try
            {
                await container.ReplaceItemAsync(new { id = "12345", name = "Vladimir" }, "12345");
                writeSucceeded = true;
            }
            catch (InvalidItemSchemaException)
            {
                writeSucceeded = false;
            }
            Debug.Assert(writeSucceeded);
        }
        // </Main>

        // <UpdateItemForConcurrency>
        private static Task<ItemResponse<Item>> UpdateItemForConcurrency(Container container, ItemRequestOptions itemRequestOptions, Item item)
        {
            item.Description = $"Updating description {Guid.NewGuid().ToString()}";
            return container.ReplaceItemAsync<Item>(
                item,
                item.Id,
                new PartitionKey(item.Id),
                itemRequestOptions);
        }
        // </UpdateItemForConcurrency>
    }
}
