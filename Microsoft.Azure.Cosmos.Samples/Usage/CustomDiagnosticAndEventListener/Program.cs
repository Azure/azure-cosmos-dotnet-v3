namespace Cosmos.Samples.ApplicationInsights
{
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;
    using Sample.Listeners;

    internal class Program
    {
        private static readonly string databaseName = "samples";
        private static readonly string containerName = "custom-listener-sample";

        static async Task Main()
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                           .AddJsonFile("AppSettings.json")
                           .Build();

            string? endpoint = configuration["EndPointUrl"];
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException("Please specify a valid CosmosDBEndPointUrl in the appSettings.json");
            }

            string? authKey = configuration["AuthorizationKey"];
            if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
            {
                throw new ArgumentException("Please specify a valid CosmosDBAuthorizationKey in the appSettings.json");
            }

            using CustomDiagnosticAndEventListener listener 
                = new CustomDiagnosticAndEventListener(
                    diagnosticSourceName: "Azure.Cosmos.Operation", 
                    eventSourceName: "Azure-Cosmos-Operation-Request-Diagnostics");

            CosmosClientOptions options = new CosmosClientOptions()
            {
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions()
                {
                    // Defaults to false, set to true to disable
                    DisableDistributedTracing = false,
                }
            };
            using (CosmosClient client = new CosmosClient(endpoint, authKey, options))
            {
                Console.WriteLine($"Getting container reference for {containerName}.");

                ContainerProperties properties = new ContainerProperties(containerName, partitionKeyPath: "/id");

                await client.CreateDatabaseIfNotExistsAsync(databaseName);
                Container container = await client.GetDatabase(databaseName).CreateContainerIfNotExistsAsync(properties);

                await Program.RunCrudDemo(container);
            }
        }

        public static async Task RunCrudDemo(Container container)
        {
            // Any operations will automatically generate telemetry 

            for (int i = 1; i <= 5; i++)
            {
                await container.CreateItemAsync(new Item { Id = $"{i}", Status = "new" }, new PartitionKey($"{i}"));
                Console.WriteLine($"Created document with id: {i}");
            }

            for (int i = 1; i <= 5; i++)
            {
                await container.ReadItemAsync<Item>($"{i}", new PartitionKey($"{i}"));
                Console.WriteLine($"Read document with id: {i}");
            }

            for (int i = 1; i <= 5; i++)
            {
                await container.ReplaceItemAsync(new Item { Id = $"{i}", Status = "updated" }, $"{i}", new PartitionKey($"{i}"));
                Console.WriteLine($"Updated document with id: {i}");
            }

            for (int i = 1; i <= 5; i++)
            {
                await container.DeleteItemAsync<Item>($"{i}", new PartitionKey($"{i}"));
                Console.WriteLine($"Deleted document with id: {i}");
            }
        }
    }

    internal class Item
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        public string? Status { get; set; }
    }
}