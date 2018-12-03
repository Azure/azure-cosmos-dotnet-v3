using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Cosmos.Samples.Handlers.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Cosmos.Samples.Handlers
{
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


    class Program
    {
        static async Task Main(string[] args)
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
            var cosmosConfiguration = new CosmosConfiguration(endpoint,
                authKey);

            cosmosConfiguration.AddCustomHandlers(
                new LoggingHandler(), 
                new ConcurrencyHandler(),
                new ThrottlingHandler()
                );

            var client = new CosmosClient(cosmosConfiguration);

            CosmosDatabaseResponse databaseResponse = await client.Databases.CreateDatabaseIfNotExistsAsync("mydb");
            CosmosDatabase database = databaseResponse.Database;
            
            var containerResponse = await database.Containers.CreateContainerIfNotExistsAsync("mycoll", "/id");
            var container = containerResponse.Container;

            var item = new Item()
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
            var query = container.Items.CreateItemQuery<Item>(new CosmosSqlQueryDefinition("SELECT * FROM c"), maxConcurrency: 1);
            List<Item> results = new List<Item>();
            while (query.HasMoreResults)
            {
                var response = await query.FetchNextSetAsync();

                results.AddRange(response.ToList());
            }

            // Read Item

            var cosmosItemResponse = await container.Items.ReadItemAsync<Item>(item.Id, item.Id);

            var accessCondition = new AccessCondition
            {
                Condition = cosmosItemResponse.ETag,
                Type = AccessConditionType.IfMatch
            };

            // Concurrency

            var tasks = new List<Task<CosmosItemResponse<Item>>>();

            tasks.Add(UpdateItemForConcurrency(container, accessCondition, item));
            tasks.Add(UpdateItemForConcurrency(container, accessCondition, item));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (CosmosException ex)
            {
                // Verify that our custom handler catched the scenario
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
