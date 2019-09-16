namespace Cosmos.Samples.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Extensions.Configuration;
    using ChangeFeedProcessorLibrary = Microsoft.Azure.Documents.ChangeFeedProcessor;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://azure.microsoft.com/en-us/itemation/articles/itemdb-create-account/
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    // Sample - demonstrates common Change Feed operations
    //
    // 1. Listening for changes that happen after a Change Feed Processor is started.
    //
    // 2. Listening for changes that happen after a certain point in time.
    //
    // 3. Listening for changes that happen since the container was created.
    //
    // 4. Generate Estimator metrics to expose current Change Feed Processor progress
    //
    // 5. Code migration template from existing Change Feed Processor library V2
    //-----------------------------------------------------------------------------------------------------------


    class Program
    {
        private static readonly string monitoredContainer = "monitored";
        private static readonly string leasesContainer = "leases";
        private static readonly string partitionKeyPath = "/id";

        // Async main requires c# 7.1 which is set in the csproj with the LangVersion attribute
        static async Task Main(string[] args)
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

                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    Console.WriteLine($"\n1. Listening for changes that happen after a Change Feed Processor is started.");
                    await Program.RunBasicChangeFeed("changefeed-basic", client);
                    Console.WriteLine($"\n2. Listening for changes that happen after a certain point in time.");
                    await Program.RunStartTimeChangeFeed("changefeed-time", client);
                    Console.WriteLine($"\n3. Listening for changes that happen since the container was created.");
                    await Program.RunStartFromBeginningChangeFeed("changefeed-beginning", client);
                    Console.WriteLine($"\n4. Generate Estimator metrics to expose current Change Feed Processor progress.");
                    await Program.RunEstimatorChangeFeed("changefeed-estimator", client);
                    Console.WriteLine($"\n5. Code migration template from existing Change Feed Processor library V2.");
                    await Program.RunMigrationSample("changefeed-migration", client, configuration);
                }
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Basic change feed functionality.
        /// </summary>
        /// <remarks>
        /// When StartAsync is called, the Change Feed Processor starts an initialization process that can take several milliseconds, 
        /// in which it starts connections and initializes locks in the leases container.
        /// </remarks>
        public static async Task RunBasicChangeFeed(
            string databaseId, 
            CosmosClient client)
        {
            await Program.InitializeAsync(databaseId, client);

            // <BasicInitialization>
            Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilder<ToDoItem>("changeFeedBasic", Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .Build();
            // </BasicInitialization>

            Console.WriteLine("Starting Change Feed Processor...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            Console.WriteLine("Generating 10 items that will be picked up by the delegate...");
            await Program.GenerateItems(10, monitoredContainer);

            // Wait random time for the delegate to output all messages after initialization is done
            await Task.Delay(5000);
            Console.WriteLine("Press any key to continue with the next demo...");
            Console.ReadKey();
            await changeFeedProcessor.StopAsync();
        }

        /// <summary>
        /// StartTime will make the Change Feed Processor start processing changes at a particular point in time, all previous changes are ignored.
        /// </summary>
        /// <remarks>
        /// StartTime only works if the leaseContainer is empty or contains no leases for the particular processor name.
        /// </remarks>
        public static async Task RunStartTimeChangeFeed(
            string databaseId,
            CosmosClient client)
        {
            await Program.InitializeAsync(databaseId, client);
            Console.WriteLine("Generating 5 items that will not be picked up.");
            await Program.GenerateItems(5, client.GetContainer(databaseId, Program.monitoredContainer));
            Console.WriteLine($"Items generated at {DateTime.UtcNow}");
            // Generate a future point in time
            await Task.Delay(2000);
            DateTime particularPointInTime = DateTime.UtcNow;

            Console.WriteLine("Generating 5 items that will be picked up by the delegate...");
            await Program.GenerateItems(5, client.GetContainer(databaseId, Program.monitoredContainer));

            // <TimeInitialization>
            Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilder<ToDoItem>("changeFeedTime", Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .WithStartTime(particularPointInTime)
                    .Build();
            // </TimeInitialization>

            Console.WriteLine($"Starting Change Feed Processor with changes after {particularPointInTime}...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            // Wait random time for the delegate to output all messages after initialization is done
            await Task.Delay(5000);
            Console.WriteLine("Press any key to continue with the next demo...");
            Console.ReadKey();
            await changeFeedProcessor.StopAsync();
        }

        /// <summary>
        /// Reading the Change Feed since the beginning of time.
        /// </summary>
        /// <remarks>
        /// StartTime only works if the leaseContainer is empty or contains no leases for the particular processor name.
        /// </remarks>
        public static async Task RunStartFromBeginningChangeFeed(
            string databaseId,
            CosmosClient client)
        {
            await Program.InitializeAsync(databaseId, client);
            Console.WriteLine("Generating 10 items that will be picked up by the delegate...");
            await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));

            // <StartFromBeginningInitialization>
            Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilder<ToDoItem>("changeFeedBeginning", Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .WithStartTime(DateTime.MinValue.ToUniversalTime())
                    .Build();
            // </StartFromBeginningInitialization>

            Console.WriteLine($"Starting Change Feed Processor with changes since the beginning...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            // Wait random time for the delegate to output all messages after initialization is done
            await Task.Delay(5000);
            Console.WriteLine("Press any key to continue with the next demo...");
            Console.ReadKey();
            await changeFeedProcessor.StopAsync();
        }

        /// <summary>
        /// Exposing progress with the Estimator.
        /// </summary>
        /// <remarks>
        /// The Estimator uses the same processorName and the same lease configuration as the existing processor to measure progress.
        /// </remarks>
        public static async Task RunEstimatorChangeFeed(
            string databaseId,
            CosmosClient client)
        {
            await Program.InitializeAsync(databaseId, client);

            // <StartProcessorEstimator>
            Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilder<ToDoItem>("changeFeedEstimator", Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .Build();
            // </StartProcessorEstimator>

            Console.WriteLine($"Starting Change Feed Processor...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            Console.WriteLine("Generating 10 items that will be picked up by the delegate...");
            await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));

            // Wait random time for the delegate to output all messages after initialization is done
            await Task.Delay(5000);

            // <StartEstimator>
            ChangeFeedProcessor changeFeedEstimator = monitoredContainer
                .GetChangeFeedEstimatorBuilder("changeFeedEstimator", Program.HandleEstimationAsync, TimeSpan.FromMilliseconds(1000))
                .WithLeaseContainer(leaseContainer)
                .Build();
            // </StartEstimator>

            Console.WriteLine($"Starting Change Feed Estimator...");
            await changeFeedEstimator.StartAsync();
            Console.WriteLine("Change Feed Estimator started.");

            Console.WriteLine("Generating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));

            Console.WriteLine("Press any key to continue with the next demo...");
            Console.ReadKey();
            await changeFeedProcessor.StopAsync();
            await changeFeedEstimator.StopAsync();
        }

        /// <summary>
        /// Example of a code migration template from Change Feed Processor V2 to SDK V3.
        /// </summary>
        /// <returns></returns>
        public static async Task RunMigrationSample(
            string databaseId,
            CosmosClient client,
            IConfigurationRoot configuration)
        {
            await Program.InitializeAsync(databaseId, client);

            Console.WriteLine("Generating 10 items that will be picked up by the old Change Feed Processor library...");
            await Program.GenerateItems(10, client.GetContainer(databaseId, Program.monitoredContainer));

            // This is how you would initialize the processor in V2
            // <ChangeFeedProcessorLibrary>
            ChangeFeedProcessorLibrary.DocumentCollectionInfo monitoredCollectionInfo = new ChangeFeedProcessorLibrary.DocumentCollectionInfo()
            {
                DatabaseName = databaseId,
                CollectionName = Program.monitoredContainer,
                Uri = new Uri(configuration["EndPointUrl"]),
                MasterKey = configuration["AuthorizationKey"]
            };

            ChangeFeedProcessorLibrary.DocumentCollectionInfo leaseCollectionInfo = new ChangeFeedProcessorLibrary.DocumentCollectionInfo()
            {
                DatabaseName = databaseId,
                CollectionName = Program.leasesContainer,
                Uri = new Uri(configuration["EndPointUrl"]),
                MasterKey = configuration["AuthorizationKey"]
            };

            ChangeFeedProcessorLibrary.ChangeFeedProcessorBuilder builder = new ChangeFeedProcessorLibrary.ChangeFeedProcessorBuilder();
            var oldChangeFeedProcessor = await builder
                .WithHostName("consoleHost")
                .WithProcessorOptions(new ChangeFeedProcessorLibrary.ChangeFeedProcessorOptions {
                    StartFromBeginning = true,
                    LeasePrefix = "MyLeasePrefix" })
                 .WithProcessorOptions(new ChangeFeedProcessorLibrary.ChangeFeedProcessorOptions()
                 {
                     MaxItemCount = 10,
                     FeedPollDelay = TimeSpan.FromSeconds(1)
                 })
                .WithFeedCollection(monitoredCollectionInfo)
                .WithLeaseCollection(leaseCollectionInfo)
                .WithObserver<ChangeFeedObserver>()
                .BuildAsync();
            // </ChangeFeedProcessorLibrary>

            await oldChangeFeedProcessor.StartAsync();

            // Wait random time for the delegate to output all messages after initialization is done
            await Task.Delay(5000);
            Console.WriteLine("Now we will stop the V2 Processor and start a V3 with the same parameters to pick up from the same state, press any key to continue...");
            Console.ReadKey();
            await oldChangeFeedProcessor.StopAsync();

            Console.WriteLine("Generating 5 items that will be picked up by the new Change Feed Processor...");
            await Program.GenerateItems(5, client.GetContainer(databaseId, Program.monitoredContainer));

            // This is how you would do the same initialization in V3
            // <ChangeFeedProcessorMigrated>
            Container leaseContainer = client.GetContainer(databaseId, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(databaseId, Program.monitoredContainer);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilder<ToDoItem>("MyLeasePrefix", Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .WithMaxItems(10)
                    .WithPollInterval(TimeSpan.FromSeconds(1))
                    .WithStartTime(DateTime.MinValue.ToUniversalTime())
                    .Build();
            // </ChangeFeedProcessorMigrated>

            await changeFeedProcessor.StartAsync();

            // Wait random time for the delegate to output all messages after initialization is done
            await Task.Delay(5000);
            Console.WriteLine("Press any key to continue with the next demo...");
            Console.ReadKey();
            await changeFeedProcessor.StopAsync();
        }

        /// <summary>
        /// The delegate receives batches of changes as they are generated in the change feed and can process them.
        /// </summary>
        // <Delegate>
        static async Task HandleChangesAsync(IReadOnlyCollection<ToDoItem> changes, CancellationToken cancellationToken)
        {
            foreach (ToDoItem item in changes)
            {
                Console.WriteLine($"\tDetected operation for item with id {item.id}, created at {item.creationTime}.");
                // Simulate work
                await Task.Delay(1);
            }
        }
        // </Delegate>

        /// <summary>
        /// The delegate for the Estimator receives a numeric representation of items pending to be read.
        /// </summary>
        // <EstimationDelegate>
        static async Task HandleEstimationAsync(long estimation, CancellationToken cancellationToken)
        {
            if (estimation > 0)
            {
                Console.WriteLine($"\tEstimator detected {estimation} items pending to be read by the Processor.");
            }

            await Task.Delay(0);
        }
        // </EstimationDelegate>

        private static async Task GenerateItems(
            int itemsToInsert, 
            Container container)
        {
            await Task.Delay(500);
            for (int i = 0; i < itemsToInsert; i++)
            {
                string id = Guid.NewGuid().ToString();
                await container.CreateItemAsync<ToDoItem>(
                    new ToDoItem()
                    {
                        id = id,
                        creationTime = DateTime.UtcNow
                    },
                    new PartitionKey(id));
            }
        }

        private static async Task InitializeAsync(
            string databaseId,
            CosmosClient client)
        {
            Database database;
            // Recreate database
            try
            {
                database = await client.GetDatabase(databaseId).ReadAsync();
                await database.DeleteAsync();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
            }

            database = await client.CreateDatabaseAsync(databaseId);

            await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Program.monitoredContainer, Program.partitionKeyPath));

            await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Program.leasesContainer, Program.partitionKeyPath));
        }
    }

    // <Model>
    public class ToDoItem
    {
        public string id { get; set; }

        public DateTime creationTime { get; set; }
    }
    // </Model>

    internal class ChangeFeedObserver : IChangeFeedObserver
    {
        public Task CloseAsync(IChangeFeedObserverContext context, ChangeFeedObserverCloseReason reason)
        {
            return Task.CompletedTask;
        }

        public Task OpenAsync(IChangeFeedObserverContext context)
        {
            return Task.CompletedTask;
        }

        public Task ProcessChangesAsync(IChangeFeedObserverContext context, IReadOnlyList<Microsoft.Azure.Documents.Document> docs, CancellationToken cancellationToken)
        {
            foreach (var doc in docs)
            {
                Console.WriteLine($"\t[OLD Processor] Detected operation for item with id {doc.Id}, created at {doc.GetPropertyValue<DateTime>("creationTime")}.");
            }

            return Task.CompletedTask;
        }
    }
}
