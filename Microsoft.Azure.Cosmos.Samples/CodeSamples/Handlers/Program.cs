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


    public class Program
    {
        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute 
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

            cosmosClientBuilder.AddCustomHandlers(
                new LoggingHandler(),
                new ConcurrencyHandler(),
                new ThrottlingHandler()
                );

            CosmosClient client = cosmosClientBuilder.Build();

            CosmosDatabaseResponse databaseResponse = await client.Databases.CreateDatabaseIfNotExistsAsync("mydb");
            CosmosDatabase database = databaseResponse.Database;

            CosmosContainerResponse containerResponse = await database.Containers.CreateContainerIfNotExistsAsync("mycoll", "/id");
            CosmosContainer container = containerResponse.Container;

            Item item = new Item()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Item",
                Description = "Some random test item",
                Completed = false
            };

            // Create
            await container.Items.CreateItemAsync<Item>(item.Id, item);

            item.Completed = true;

            // Replace
            await container.Items.ReplaceItemAsync<Item>(item.Id, item.Id, item);

            // Querying
            CosmosResultSetIterator<Item> query = container.Items.CreateItemQuery<Item>(new CosmosSqlQueryDefinition("SELECT * FROM c"), maxConcurrency: 1);
            List<Item> results = new List<Item>();
            while (query.HasMoreResults)
            {
                CosmosQueryResponse<Item> response = await query.FetchNextSetAsync();

                results.AddRange(response.ToList());
            }

            // Read Item

            CosmosItemResponse<Item> cosmosItemResponse = await container.Items.ReadItemAsync<Item>(item.Id, item.Id);

            AccessCondition accessCondition = new AccessCondition
            {
                Condition = cosmosItemResponse.ETag,
                Type = AccessConditionType.IfMatch
            };

            // Concurrency

            List<Task<CosmosItemResponse<Item>>> tasks = new List<Task<CosmosItemResponse<Item>>>
            {
                UpdateItemForConcurrency(container, accessCondition, item),
                UpdateItemForConcurrency(container, accessCondition, item)
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
            await container.Items.DeleteItemAsync<Item>(item.Id, item.Id);
        }

        private static Task<CosmosItemResponse<Item>> UpdateItemForConcurrency(CosmosContainer container, AccessCondition accessCondition, Item item)
        {
            item.Description = $"Updating description {Guid.NewGuid().ToString()}";
            return container.Items.ReplaceItemAsync<Item>(
                item.Id,
                item.Id,
                item, new CosmosItemRequestOptions()
                {
                    AccessCondition = accessCondition
                });
        }
    }
}
