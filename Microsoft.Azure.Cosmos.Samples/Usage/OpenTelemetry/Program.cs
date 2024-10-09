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
    using System.Diagnostics;

    internal class Program
    {
        private static readonly string databaseName = "samples";
        private static readonly string containerName = "otel-sample";
        private static readonly string serviceName = "MySampleService";

        private static TracerProvider _traceProvider;

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
                using ILoggerFactory loggerFactory
                    = LoggerFactory.Create(builder => builder
                                                        .AddConfiguration(configuration.GetSection("Logging"))
                                                        .AddOpenTelemetry(options =>
                                                        {
                                                            options.IncludeFormattedMessage = true;
                                                            options.SetResourceBuilder(resource);
                                                            options.AddAzureMonitorLogExporter(o => o.ConnectionString = aiConnectionString); // Set up exporter of your choice
                                                        }));
                /*.AddFilter(level => level == LogLevel.Error) // Filter  is irrespective of event type or event name*/

                AzureEventSourceLogForwarder logforwader = new AzureEventSourceLogForwarder(loggerFactory);
                logforwader.Start();

                // Configure OpenTelemetry trace provider
                AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);
                _traceProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource("Azure.Cosmos.Operation", // Cosmos DB source for operation level telemetry
                               "Sample.Application") 
                    .AddAzureMonitorTraceExporter(o => o.ConnectionString = aiConnectionString) // Set up exporter of your choice
                    .AddHttpClientInstrumentation() // Added to capture HTTP telemetry
                    .SetResourceBuilder(resource)
                    .Build();
                // </SetUpOpenTelemetry>

                ActivitySource source = new ActivitySource("Sample.Application");
                using (_ = source.StartActivity(".Net SDK : Azure Monitor : Open Telemetry Sample")) // Application level activity to track the entire execution of the application
                {
                    using (_ = source.StartActivity("GATEWAY MODE")) // Activity to track the execution of the gateway mode
                    {
                        await Program.RunCosmosDbOperation(ConnectionMode.Gateway, endpoint, authKey);
                    }
                    using (_ = source.StartActivity("DIRECT MODE")) // Activity to track the execution of the direct mode
                    {
                        await Program.RunCosmosDbOperation(ConnectionMode.Direct, endpoint, authKey);
                    }
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

        private static async Task RunCosmosDbOperation(ConnectionMode connMode, string endpoint, string authKey)
        {
            // <EnableDistributedTracing>
            CosmosClientOptions options = new CosmosClientOptions()
            {
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions()
                {
                    DisableDistributedTracing = false
                },
                ConnectionMode = connMode
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
            catch(Exception)
            {
                Console.WriteLine("Generate exception by reading an invalid key");
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