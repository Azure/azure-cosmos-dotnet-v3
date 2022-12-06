namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
    using global::Azure;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using Telemetry;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DependencyCollector;
    using TraceLevel = Cosmos.Tracing.TraceLevel;
    using Microsoft.Azure.Documents.Client;

    [VisualStudio.TestTools.UnitTesting.TestClass]
    [TestCategory("UpdateContract")]
    public sealed class E2ETests
    {
        public static CosmosClient client;
        public static CosmosClient bulkClient;
        public static CosmosClient miscCosmosClient;

        public static Database database;
        public static Container container;

        //private static OpenTelemetryListener testListener;

        private static readonly TimeSpan delayTime = TimeSpan.FromSeconds(2);
        //private static readonly RequestHandler requestHandler = new RequestHandlerSleepHelper(delayTime);

        private const double DiagnosticsLatencyThresholdValue = .0001; 

        [TestMethod]
        public async Task NewTest_TCP()
        {
            try
            {
                TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();

                configuration.ConnectionString = "InstrumentationKey=0b7bcdc4-cb13-44c5-9544-aba02b8b8123;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/";
                // configuration.TelemetryInitializers.Add(new HttpDependenciesParsingTelemetryInitializer());

                TelemetryClient telemetryClient = new TelemetryClient(configuration);

                using (InitializeDependencyTracking(configuration))
                {
                    telemetryClient.TrackTrace("Hello World!");

                    using CosmosClient client = new(
                    accountEndpoint: "https://cosmosdbaavasthy.documents.azure.com:443/",
                    authKeyOrResourceToken: "GuDON7mQabFeo1KQUZSV3N3D4srOuJFNheIPIumYIogKIHAyevrxPF52ddFDvQXRPfrNUVvjRh5JBDCWpSKo3A==",
                    new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                        ConnectionMode = ConnectionMode.Direct,
                        ConnectionProtocol = Protocol.Tcp,
                        EnableDistributedTracing = true
                    });

                    Database database = await client.CreateDatabaseIfNotExistsAsync(
                        id: "adventureworks"
                    );
                    // Container reference with creation if it does not alredy exist
                    Container container = await database.CreateContainerIfNotExistsAsync(
                        id: "products",
                        partitionKeyPath: "/category",
                        throughput: 400
                    );

                    // Create new object and upsert (create or replace) to container
                    Product newItem = new(
                        Id: "68719518391",
                        Category: "gear-surf-surfboards",
                        Name: "Yamba Surfboard",
                        Quantity: 12,
                        Sale: false
                    );

                    ItemResponse<Product> createdItem = await container.UpsertItemAsync<Product>(
                        item: newItem,
                        partitionKey: new PartitionKey("gear-surf-surfboards")
                    );

                    TimeSpan interval = new TimeSpan(0, 0, 0, 1);

                    // Point read item from container using the id and partitionKey
                    Product readItem = await container.ReadItemAsync<Product>(
                        id: "68719518391",
                        partitionKey: new PartitionKey("gear-surf-surfboards")
                    );

                    // Create query using a SQL string and parameters
                    QueryDefinition query = new QueryDefinition(
                        query: "SELECT * FROM products p WHERE p.category = @key"
                    )
                        .WithParameter("@key", "gear-surf-surfboards");

                    using FeedIterator<Product> feed = container.GetItemQueryIterator<Product>(
                        queryDefinition: query
                    );

                    while (feed.HasMoreResults)
                    {
                        FeedResponse<Product> response = await feed.ReadNextAsync();
                        foreach (Product item in response)
                        {
                            Console.WriteLine($"Found item:\t{item.Name}");
                        }
                    }

                }
                // activity.Stop();
                // before exit, flush the remaining data
                telemetryClient.Flush();

                Task.Delay(5000).Wait();
            }
            catch (CosmosException cosmosException)
            {
                Console.WriteLine("The current UI culture is {0}",
                                   Thread.CurrentThread.CurrentUICulture.Name);
                string a = cosmosException.Diagnostics.ToString();
                //Console.WriteLine($"Error log:\t{cosmosException.ToString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Custom {0}", ex.Message.ToString());
            }
        }

        [TestMethod]
        public async Task NewTest_HTTP()
        {
            try
            {
                TelemetryConfiguration configuration = TelemetryConfiguration.CreateDefault();

                configuration.ConnectionString = "InstrumentationKey=0b7bcdc4-cb13-44c5-9544-aba02b8b8123;IngestionEndpoint=https://eastus-8.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus.livediagnostics.monitor.azure.com/";
                // configuration.TelemetryInitializers.Add(new HttpDependenciesParsingTelemetryInitializer());

                TelemetryClient telemetryClient = new TelemetryClient(configuration);

                using (InitializeDependencyTracking(configuration))
                {
                    telemetryClient.TrackTrace("Hello World!");

                    using CosmosClient client = new(
                    accountEndpoint: "https://cosmosdbaavasthy.documents.azure.com:443/",
                    authKeyOrResourceToken: "GuDON7mQabFeo1KQUZSV3N3D4srOuJFNheIPIumYIogKIHAyevrxPF52ddFDvQXRPfrNUVvjRh5JBDCWpSKo3A==",
                    new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
                        ConnectionMode = ConnectionMode.Gateway,
                        ConnectionProtocol = Protocol.Https,
                        EnableDistributedTracing = true
                    });

                    Database database = await client.CreateDatabaseIfNotExistsAsync(
                        id: "adventureworks"
                    );
                    // Container reference with creation if it does not alredy exist
                    Container container = await database.CreateContainerIfNotExistsAsync(
                        id: "products",
                        partitionKeyPath: "/category",
                        throughput: 400
                    );

                    // Create new object and upsert (create or replace) to container
                    Product newItem = new(
                        Id: "68719518391",
                        Category: "gear-surf-surfboards",
                        Name: "Yamba Surfboard",
                        Quantity: 12,
                        Sale: false
                    );

                    ItemResponse<Product> createdItem = await container.UpsertItemAsync<Product>(
                        item: newItem,
                        partitionKey: new PartitionKey("gear-surf-surfboards")
                    );

                    TimeSpan interval = new TimeSpan(0, 0, 0, 1);

                    // Point read item from container using the id and partitionKey
                    Product readItem = await container.ReadItemAsync<Product>(
                        id: "68719518391",
                        partitionKey: new PartitionKey("gear-surf-surfboards")
                    );

                    // Create query using a SQL string and parameters
                    QueryDefinition query = new QueryDefinition(
                        query: "SELECT * FROM products p WHERE p.category = @key"
                    )
                        .WithParameter("@key", "gear-surf-surfboards");

                    using FeedIterator<Product> feed = container.GetItemQueryIterator<Product>(
                        queryDefinition: query
                    );

                    while (feed.HasMoreResults)
                    {
                        FeedResponse<Product> response = await feed.ReadNextAsync();
                        foreach (Product item in response)
                        {
                            Console.WriteLine($"Found item:\t{item.Name}");
                        }
                    }

                }
                // activity.Stop();
                // before exit, flush the remaining data
                telemetryClient.Flush();

                Task.Delay(5000).Wait();
            }
            catch (CosmosException cosmosException)
            {
                Console.WriteLine("The current UI culture is {0}",
                                   Thread.CurrentThread.CurrentUICulture.Name);
                string a = cosmosException.Diagnostics.ToString();
                //Console.WriteLine($"Error log:\t{cosmosException.ToString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Custom {0}", ex.Message.ToString());
            }
        }
        static DependencyTrackingTelemetryModule InitializeDependencyTracking(TelemetryConfiguration configuration)
        {
            DependencyTrackingTelemetryModule module = new DependencyTrackingTelemetryModule();

            // prevent Correlation Id to be sent to certain endpoints. You may add other domains as needed.
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.windows.net");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.chinacloudapi.cn");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.cloudapi.de");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("core.usgovcloudapi.net");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("localhost");
            module.ExcludeComponentCorrelationHttpHeadersOnDomains.Add("127.0.0.1");

            // enable known dependency tracking, note that in future versions, we will extend this list. 
            // please check default settings in https://github.com/microsoft/ApplicationInsights-dotnet-server/blob/develop/WEB/Src/DependencyCollector/DependencyCollector/ApplicationInsights.config.install.xdt

            module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.ServiceBus");
            module.IncludeDiagnosticSourceActivities.Add("Microsoft.Azure.EventHubs");

            // initialize the module
            module.Initialize(configuration);

            return module;
        }
    }
}

