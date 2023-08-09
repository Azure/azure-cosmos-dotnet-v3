namespace Cosmos.Samples.ApplicationInsights
{
    using System;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.WorkerService;

    internal class Program
    {
        private static readonly string databaseName = "samples";
        private static readonly string containerName = "ai-sample";

        private static TelemetryClient? _telemetryClient;

        static async Task Main()
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                            .AddJsonFile("AppSettings.json")
                            .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid CosmosDBEndPointUrl in the appSettings.json");
                }

                string authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid CosmosDBAuthorizationKey in the appSettings.json");
                }

                string aiConnectionString = configuration["ApplicationInsightsConnectionString"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret connection string"))
                {
                    throw new ArgumentException("Please specify a valid ApplicationInsightsConnectionString in the appSettings.json");
                }

                // <SetUpApplicationInsights>
                IServiceCollection services = new ServiceCollection();
                services.AddApplicationInsightsTelemetryWorkerService((ApplicationInsightsServiceOptions options) => options.ConnectionString = aiConnectionString);

                IServiceProvider serviceProvider = services.BuildServiceProvider();
                _telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();
                // </SetUpApplicationInsights>

                CosmosClientOptions options = new CosmosClientOptions()
                {
                    IsDistributedTracingEnabled = true // Defaults to true, set to false to disable
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
            finally
            {
                // Explicitly calling Flush() followed by sleep is required for Application Insights logging in console apps to ensure that telemetry is sent to the back-end even if application terminates.
                _telemetryClient?.Flush();
                await Task.Delay(5000);

                Console.WriteLine("End of demo.");
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
        public string Id { get; set; }

        public string Status { get; set; }
    }
}