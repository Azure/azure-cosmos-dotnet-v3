namespace ChangeFeedAllVersionsAndDeletes
{
    using System.Net;
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
    // Sample - demonstrates common Change Feed operations using All Versions and Deletes mode
    //
    // 1. Listening for changes that happen after a Change Feed Processor is started.
    //
    // 2. Generate Estimator metrics to expose current Change Feed Processor progress as a push notification
    //
    // 3. Generate Estimator metrics to expose current Change Feed Processor progress on demand
    //
    // 4. Error handling and advanced logging
    //-----------------------------------------------------------------------------------------------------------

    internal class Program
    {
        private static readonly string databaseName = "changefeed-db";
        private static readonly string monitoredContainerPrefix = "monitored-";
        private static readonly string leasesContainer = "leases";
        private static readonly string partitionKeyPath = "/id";
        static async Task Main(string[] _)
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .Build();

                string? endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json");
                }

                string? authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
                }

                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    Console.WriteLine($"\n1. Listening for changes that happen after a Change Feed Processor is started.");
                    await Program.RunBasicChangeFeed(monitoredContainerPrefix + "changefeed-basic", client);
                    Console.WriteLine($"\n2. Generate Estimator metrics to expose current Change Feed Processor progress as a push notification.");
                    await Program.RunEstimatorChangeFeed(monitoredContainerPrefix + "changefeed-estimator", client);
                    Console.WriteLine($"\n3. Generate Estimator metrics to expose current Change Feed Processor progress on demand.");
                    await Program.RunEstimatorPullChangeFeed(monitoredContainerPrefix + "changefeed-estimator-detailed", client);
                    Console.WriteLine($"\n4. Error handling and advanced logging.");
                    await Program.RunWithNotifications(monitoredContainerPrefix + "changefeed-logging", client);
                }
            }
            finally
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Basic change feed functionality using all versions and deletes mode.
        /// </summary>
        /// <remarks>
        /// When StartAsync is called, the Change Feed Processor starts an initialization process that can take several milliseconds, 
        /// in which it starts connections and initializes locks in the leases container.
        /// </remarks>
        public static async Task RunBasicChangeFeed(
            string containerName,
            CosmosClient client)
        {
            await Program.InitializeAsync(containerName, client);

            // <BasicInitialization>
            Container leaseContainer = client.GetContainer(Program.databaseName, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(Program.databaseName, containerName);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<ToDoItem>(processorName: "changeFeedBasic", onChangesDelegate: Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .Build();
            // </BasicInitialization>

            Console.WriteLine("Starting Change Feed Processor...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            Console.WriteLine("Generating 10 items that will be picked up by the delegate...");
            await Program.GenerateItems(10, monitoredContainer);
            Console.WriteLine("Updating 10 items that will be picked up by the delegate...");
            await Program.UpdateItems(10, monitoredContainer);
            Console.WriteLine("Deleting 10 items that will be picked up by the delegate...");
            await Program.DeleteItems(10, monitoredContainer);

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
            string containerName,
            CosmosClient client)
        {
            await Program.InitializeAsync(containerName, client);

            // <StartProcessorEstimator>
            Container leaseContainer = client.GetContainer(Program.databaseName, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(Program.databaseName, containerName);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<ToDoItem>(processorName: "changeFeedEstimator", onChangesDelegate: Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .Build();
            // </StartProcessorEstimator>

            Console.WriteLine($"Starting Change Feed Processor...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            // Wait random time for the delegate to output all messages
            await Task.Delay(1000);

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
            await Program.GenerateItems(10, monitoredContainer);
            Console.WriteLine("Updating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.UpdateItems(10, monitoredContainer);
            Console.WriteLine("Deleting 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.DeleteItems(10, monitoredContainer);

            // Wait random time for the delegate to output all messages
            await Task.Delay(5000);

            Console.WriteLine("Press any key to continue with the next demo...");
            Console.ReadKey();
            await changeFeedProcessor.StopAsync();
            await changeFeedEstimator.StopAsync();
        }

        /// <summary>
        /// Exposing progress with the Estimator with the detailed iterator.
        /// </summary>
        /// <remarks>
        /// The Estimator uses the same processorName and the same lease configuration as the existing processor to measure progress.
        /// The iterator exposes detailed, per-lease, information on estimation and ownership.
        /// </remarks>
        public static async Task RunEstimatorPullChangeFeed(
            string containerName,
            CosmosClient client)
        {
            await Program.InitializeAsync(containerName, client);

            // <StartProcessorEstimatorDetailed>
            Container leaseContainer = client.GetContainer(Program.databaseName, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(Program.databaseName, containerName);
            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<ToDoItem>("changeFeedEstimatorPull", Program.HandleChangesAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .Build();
            // </StartProcessorEstimatorDetailed>

            Console.WriteLine($"Starting Change Feed Processor...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            // Wait some seconds for instances to acquire leases
            await Task.Delay(5000);

            Console.WriteLine("Generating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.GenerateItems(10, monitoredContainer);
            Console.WriteLine("Updating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.UpdateItems(10, monitoredContainer);
            Console.WriteLine("Deleting 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.DeleteItems(10, monitoredContainer);

            // Wait random time for the delegate to output all messages after initialization is done
            await Task.Delay(5000);

            // <StartEstimatorDetailed>
            ChangeFeedEstimator changeFeedEstimator = monitoredContainer
                .GetChangeFeedEstimator("changeFeedEstimator", leaseContainer);
            // </StartEstimatorDetailed>

            // <GetIteratorEstimatorDetailed>
            Console.WriteLine("Checking estimation...");
            using FeedIterator<ChangeFeedProcessorState> estimatorIterator = changeFeedEstimator.GetCurrentStateIterator();
            while (estimatorIterator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> states = await estimatorIterator.ReadNextAsync();
                foreach (ChangeFeedProcessorState leaseState in states)
                {
                    string host = leaseState.InstanceName == null ? $"not owned by any host currently" : $"owned by host {leaseState.InstanceName}";
                    Console.WriteLine($"Lease [{leaseState.LeaseToken}] {host} reports {leaseState.EstimatedLag} as estimated lag.");
                }
            }
            // </GetIteratorEstimatorDetailed>

            Console.WriteLine("Stopping processor.");
            await changeFeedProcessor.StopAsync();

            // Wait for processor to shutdown completely so the next items generate lag
            await Task.Delay(7500);

            Console.WriteLine("Generating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.GenerateItems(10, monitoredContainer);
            Console.WriteLine("Updating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.UpdateItems(10, monitoredContainer);
            Console.WriteLine("Deleting 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.DeleteItems(10, monitoredContainer);

            Console.WriteLine("Checking estimation...");
            using FeedIterator<ChangeFeedProcessorState> estimatorIteratorAfter = changeFeedEstimator.GetCurrentStateIterator();
            while (estimatorIteratorAfter.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> states = await estimatorIteratorAfter.ReadNextAsync();
                foreach (ChangeFeedProcessorState leaseState in states)
                {
                    // Host ownership should be empty as we have already stopped the estimator
                    string host = leaseState.InstanceName == null ? $"not owned by any host currently" : $"owned by host {leaseState.InstanceName}";
                    Console.WriteLine($"Lease [{leaseState.LeaseToken}] {host} reports {leaseState.EstimatedLag} as estimated lag.");
                }
            }

            Console.WriteLine("Press any key to continue with the next demo...");
            Console.ReadKey();
        }

        /// <summary>
        /// Setup notification APIs for events.
        /// </summary>
        public static async Task RunWithNotifications(
            string containerName,
            CosmosClient client)
        {
            await Program.InitializeAsync(containerName, client);

            Container leaseContainer = client.GetContainer(Program.databaseName, Program.leasesContainer);
            Container monitoredContainer = client.GetContainer(Program.databaseName, containerName);

            ManualResetEventSlim manualResetEventSlim = new ManualResetEventSlim();
            Container.ChangeFeedHandler<ChangeFeedItem<ToDoItem>> handleChanges = (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<ToDoItem>> changes, CancellationToken cancellationToken) =>
            {
                Console.WriteLine($"Started handling changes for lease {context.LeaseToken} but throwing an exception to bubble to notifications.");
                manualResetEventSlim.Set();
                throw new Exception("This is an unhandled exception from inside the delegate");
            };

            // <StartWithNotifications>
            Container.ChangeFeedMonitorLeaseAcquireDelegate onLeaseAcquiredAsync = (string leaseToken) =>
            {
                Console.WriteLine($"Lease {leaseToken} is acquired and will start processing");
                return Task.CompletedTask;
            };

            Container.ChangeFeedMonitorLeaseReleaseDelegate onLeaseReleaseAsync = (string leaseToken) =>
            {
                Console.WriteLine($"Lease {leaseToken} is released and processing is stopped");
                return Task.CompletedTask;
            };

            Container.ChangeFeedMonitorErrorDelegate onErrorAsync = (string LeaseToken, Exception exception) =>
            {
                if (exception is ChangeFeedProcessorUserException userException)
                {
                    Console.WriteLine($"Lease {LeaseToken} processing failed with unhandled exception from user delegate {userException.InnerException}");
                }
                else
                {
                    Console.WriteLine($"Lease {LeaseToken} failed with {exception}");
                }

                return Task.CompletedTask;
            };

            ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<ToDoItem>("changeFeedNotifications", handleChanges)
                    .WithLeaseAcquireNotification(onLeaseAcquiredAsync)
                    .WithLeaseReleaseNotification(onLeaseReleaseAsync)
                    .WithErrorNotification(onErrorAsync)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(leaseContainer)
                    .Build();
            // </StartWithNotifications>

            Console.WriteLine($"Starting Change Feed Processor with logging enabled...");
            await changeFeedProcessor.StartAsync();
            Console.WriteLine("Change Feed Processor started.");

            Console.WriteLine("Generating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.GenerateItems(10, monitoredContainer);
            Console.WriteLine("Updating 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.UpdateItems(10, monitoredContainer);
            Console.WriteLine("Deleting 10 items that will be picked up by the delegate and reported by the Estimator...");
            await Program.DeleteItems(10, monitoredContainer);

            // Wait random time for the delegate to output all messages after initialization is done
            manualResetEventSlim.Wait();
            await Task.Delay(1000);
            await changeFeedProcessor.StopAsync();
        }

        /// <summary>
        /// The delegate receives batches of changes as they are generated in the change feed and can process them.
        /// </summary>
        // <Delegate>
        static async Task HandleChangesAsync(ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<ToDoItem>> changes, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Started handling changes for lease {context.LeaseToken}...");
            Console.WriteLine($"Change Feed request consumed {context.Headers.RequestCharge} RU.");
            // SessionToken if needed to enforce Session consistency on another client instance
            Console.WriteLine($"SessionToken ${context.Headers.Session}");

            // We may want to track any operation's Diagnostics that took longer than some threshold
            if (context.Diagnostics.GetClientElapsedTime() > TimeSpan.FromSeconds(1))
            {
                Console.WriteLine($"Change Feed request took longer than expected. Diagnostics:" + context.Diagnostics.ToString());
            }

            foreach (ChangeFeedItem<ToDoItem> item in changes)
            {
                if (item.Metadata.OperationType == ChangeFeedOperationType.Delete)
                {
                    Console.WriteLine($"\tDetected {item.Metadata.OperationType} operation for item.");
                }
                else
                {
                    Console.WriteLine($"\tDetected {item.Metadata.OperationType} operation for item with id {item.Current.id}.");
                }
                // Simulate work
                await Task.Delay(1);
            }
        }
        // </Delegate>

        /// <summary>
        /// The delegate for the Estimator receives a numeric representation of items pending to be read. 
        /// This is an estimate only and is not an exact count of outstanding items.
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

        private static async Task InitializeAsync(string containerName, CosmosClient client)
        {
            Database database = await client.CreateDatabaseIfNotExistsAsync(id: Program.databaseName, throughput: 1000);

            await database.CreateContainerIfNotExistsAsync(new ContainerProperties(containerName, Program.partitionKeyPath));

            await database.CreateContainerIfNotExistsAsync(new ContainerProperties(Program.leasesContainer, Program.partitionKeyPath));
        }

        private static async Task GenerateItems(int itemsToInsert, Container container)
        {
            await Task.Delay(500);
            for (int i = 0; i < itemsToInsert; i++)
            {
                await container.CreateItemAsync<ToDoItem>(
                    new ToDoItem()
                    {
                        id = i.ToString(),
                        CreationTime = DateTime.UtcNow
                    },
                    new PartitionKey(i.ToString()));
            }
        }

        private static async Task UpdateItems(int itemsToUpdate, Container container)
        {
            await Task.Delay(500);
            for (int i = 0; i < itemsToUpdate; i++)
            {
                await container.ReplaceItemAsync<ToDoItem>(
                    new ToDoItem()
                    {
                        id = i.ToString(),
                        CreationTime = DateTime.UtcNow,
                        Status = "updated"
                    },
                    i.ToString());
            }
        }

        private static async Task DeleteItems(int itemsToDelete, Container container)
        {
            await Task.Delay(500);
            for (int i = 0; i < itemsToDelete; i++)
            {
                await container.DeleteItemAsync<ToDoItem>(i.ToString(), new PartitionKey(i.ToString()));
            }
        }
    }

    // <Model>
    public class ToDoItem
    {
        public string? id { get; set; }
        public DateTime CreationTime { get; set; }
        public string? Status { get; set; }
    }
    // </Model>
}
