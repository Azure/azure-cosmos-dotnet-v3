namespace Cosmos.Samples.Shared
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates how to configure a custom serializer that leverages System.Text.Json using Azure.Core
    // ----------------------------------------------------------------------------------------------------------
    public class Program
    {
        private static readonly string databaseId = "samples";
        private static readonly string containerId = "system-text-json-samples";
        private static CosmosClient client;
        private static Database database;
        private static Container container;

        public static async Task Main(string[] args)
        {
            try
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

                // <CosmosClientOptionsConfiguration>
                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ApplicationName = "SystemTextJsonSample",
                    Serializer = cosmosSystemTextJsonSerializer
                };
                // </CosmosClientOptionsConfiguration>

                //Read the Cosmos endpointUrl and authorisationKeys from configuration
                //These values are available from the Azure Management Portal on the Cosmos Account Blade under "Keys"
                //NB > Keep these values in a safe & secure location. Together they provide Administrative access to your Cosmos account
                client = new CosmosClient(endpoint, authKey, cosmosClientOptions);
                
                // Create required database and container
                await Program.SetupAsync();

                await Program.RunDemo();
            }
            catch (CosmosException cre)
            {
                Console.WriteLine(cre.ToString());
            }
            catch (Exception e)
            {
                Exception baseException = e.GetBaseException();
                Console.WriteLine("Error: {0}, Message: {1}", e.Message, baseException.Message);
            }
            finally
            {
                await Program.CleanupAsync();
                client?.Dispose();
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        private static async Task RunDemo()
        {
            ToDoActivity activeActivity = new ToDoActivity()
            {
                Id = Guid.NewGuid().ToString(),
                ActivityId = Guid.NewGuid().ToString(),
                PartitionKey = "myPartitionKey",
                Status = "Active"
            };

            ToDoActivity completedActivity = new ToDoActivity()
            {
                Id = Guid.NewGuid().ToString(),
                ActivityId = Guid.NewGuid().ToString(),
                PartitionKey = "myPartitionKey",
                Status = "Completed"
            };

            // Create items that use System.Text.Json serialization attributes
            ItemResponse<ToDoActivity> createActiveActivity = await container.CreateItemAsync(activeActivity, new PartitionKey(activeActivity.PartitionKey));

            Console.WriteLine($"Created Active activity with id {createActiveActivity.Resource.Id} that cost {createActiveActivity.RequestCharge}");

            ItemResponse <ToDoActivity> createCompletedActivity = await container.CreateItemAsync(completedActivity, new PartitionKey(completedActivity.PartitionKey));

            Console.WriteLine($"Created Completed activity with id {createCompletedActivity.Resource.Id} that cost {createCompletedActivity.RequestCharge}");

            // Execute queries materializing responses using System.Text.Json
            using FeedIterator<ToDoActivity> iterator = container.GetItemQueryIterator<ToDoActivity>("select * from c where c.status = 'Completed'");
            while (iterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> queryResponse = await iterator.ReadNextAsync();
                Console.WriteLine($"Obtained {queryResponse.Count} results on query for {queryResponse.RequestCharge}");
            }

            // Read items materializing responses using System.Text.Json
            ItemResponse<ToDoActivity> readActiveActivity = await container.ReadItemAsync<ToDoActivity>(activeActivity.Id, new PartitionKey(completedActivity.PartitionKey));

            Console.WriteLine($"Read Active activity with id {activeActivity.Id} that cost {readActiveActivity.RequestCharge}");

            // Using TransactionalBatch to atomically create multiple items as a single transaction
            string batchPartitionKey = "myPartitionKey";
            ToDoActivity newActivity = new ToDoActivity()
            {
                Id = Guid.NewGuid().ToString(),
                ActivityId = Guid.NewGuid().ToString(),
                PartitionKey = batchPartitionKey,
                Status = "Active"
            };

            ToDoActivity anotherNewActivity = new ToDoActivity()
            {
                Id = Guid.NewGuid().ToString(),
                ActivityId = Guid.NewGuid().ToString(),
                PartitionKey = batchPartitionKey,
                Status = "Active"
            };

            TransactionalBatchResponse batchResponse = await container.CreateTransactionalBatch(new PartitionKey(batchPartitionKey))
                .CreateItem(newActivity)
                .CreateItem(anotherNewActivity)
                .ExecuteAsync();

            if (batchResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"Completed transactional batch that cost {batchResponse.RequestCharge}");
            }
        }

        private static async Task SetupAsync()
        {
            Program.database = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            Program.container = await Program.database.CreateContainerIfNotExistsAsync(containerId, "/partitionKey");
        }

        private static async Task CleanupAsync()
        {
            if (Program.database != null)
            {
                await Program.database.DeleteAsync();
            }
        }
    }

    // <Model>
    public class ToDoActivity
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("partitionKey")]
        public string PartitionKey { get; set; }

        [JsonPropertyName("activityId")]
        public string ActivityId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
    // </Model>
}
