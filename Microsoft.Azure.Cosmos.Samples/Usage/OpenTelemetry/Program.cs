namespace Cosmos.Samples.OpenTelemetry
{
    using global::OpenTelemetry;
    using global::OpenTelemetry.Trace;
    using global::OpenTelemetry.Resources;
    using System;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Azure;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using Azure.Monitor.OpenTelemetry.Exporter;

    internal class Program
    {
        private static readonly string databaseName = "samples";
        private static readonly string containerName = "otel-sample";
        private static readonly string serviceName = "MySampleService";

        private static TracerProvider? _traceProvider;

        static async Task Main()
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                            .AddJsonFile("AppSettings.json")
                            .Build();

                string endpoint = configuration["CosmosDBEndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid CosmosDBEndPointUrl in the appSettings.json");
                }

                string authKey = configuration["CosmosDBAuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid CosmosDBAuthorizationKey in the appSettings.json");
                }

                string aiConnectionString = configuration["ApplicationInsightsConnectionString"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret connection string"))
                {
                    throw new ArgumentException("Please specify a valid ApplicationInsightsConnectionString in the appSettings.json");
                }

                // <SetUpOpenTelemetry>
                ResourceBuilder resource = ResourceBuilder.CreateDefault().AddService(
                            serviceName: serviceName,
                            serviceVersion: "1.0.0");

                // Set up logging to forward logs to chosen exporter
                using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder
                                                                                        .AddConfiguration(configuration)
                                                                                        .AddOpenTelemetry(options =>
                                                                                            {
                                                                                                options.IncludeFormattedMessage = true;
                                                                                                options.SetResourceBuilder(resource);
                                                                                                options.AddAzureMonitorLogExporter(o => o.ConnectionString = aiConnectionString); // Set up exporter of your choice
                                                                                            }));
                /*.AddFilter(level => level == LogLevel.Error)*/

                AzureEventSourceLogForwarder logforwader = new AzureEventSourceLogForwarder(loggerFactory);
                logforwader.Start();

                // Configure OpenTelemetry trace provider
                AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
                _traceProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource("Azure.Cosmos.Operation") // Cosmos DB source for operation level telemetry
                    .AddAzureMonitorTraceExporter(o => o.ConnectionString = aiConnectionString) // Set up exporter of your choice
                    .SetResourceBuilder(resource)
                    .Build();
                // </SetUpOpenTelemetry>

                // <EnableDistributedTracing>
                CosmosClientOptions options = new CosmosClientOptions()
                {
                    IsDistributedTracingEnabled = true // Defaults to true, set to false to disable
                };
                
                // </EnableDistributedTracing>
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
                _traceProvider?.Dispose();
                // Sleep is required for logging in console apps to ensure that telemetry is sent to the back-end even if application terminates.
                await Task.Delay(5000);

                Console.WriteLine("End of demo.");
            }
        }

        public static async Task RunCrudDemo(Container container)
        {
            // Any operations will automatically generate telemetry 

            for(int i = 1; i <= 5; i++)
            {
                await container.CreateItemAsync(new Item { Id = $"{i}", Status = "new" }, new PartitionKey($"{i}"));
                Console.WriteLine($"Created document with id: {i}");
            }

            for (int i = 1; i <= 5; i++)
            {
                await container.ReadItemAsync<Item>($"{i}", new PartitionKey($"{i}"));
                Console.WriteLine($"Read document with id: {i}");
            }

            try
            {
                await container.ReadItemAsync<Item>($"random key", new PartitionKey($"random partition"));
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception: {ex}");
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