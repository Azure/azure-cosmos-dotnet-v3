namespace ContainerIsFeedRangePartOf
{
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos;

    // ----------------------------------------------------------------------------------------------------------
    // Prerequisites - 
    // 
    // 1. An Azure Cosmos account - 
    //    https://docs.microsoft.com/azure/cosmos-db/create-cosmosdb-resources-portal
    //
    // 2. Microsoft.Azure.Cosmos NuGet package - 
    //    http://www.nuget.org/packages/Microsoft.Azure.Cosmos/ 
    // ----------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This class represents a Cosmos DB client application that demonstrates several key operations such as:
    /// 1. **Creating and Managing a Cosmos DB Container**:
    ///    - The program starts by checking for the existence of a Cosmos DB container and creates it if necessary.
    ///    - It configures the throughput and manages container settings like partition key paths.
    ///
    /// 2. **Seeding Documents to the Cosmos DB Container**:
    ///    - The `SeedDocumentsToContainerAsync` method generates 10,000 documents with random names and inserts them into the container using bulk upsert operations.
    ///    - This method simulates data population for later partition splitting and change feed processing.
    ///
    /// 3. **Handling Partition Splits**:
    ///    - The `WaitForPartitionSplitAsync` method seeds the container and waits for a partition split to occur.
    ///    - It continuously polls the container for changes in feed ranges, using a 20-minute timeout window. If the partition split completes, the updated feed ranges are returned.
    ///
    /// 4. **Starting and Managing a Change Feed Processor**:
    ///    - The `StartChangeFeedProcessorWithFeedRangeComparisonAsync` method demonstrates how to build and start a Change Feed Processor to track changes in the container.
    ///    - The processor listens for changes, compares feed ranges for each change, and processes these changes asynchronously while allowing graceful cancellation (e.g., by pressing the 'ESC' key).
    ///
    /// 5. **Feed Range Comparison**:
    ///    - The program includes the method `GivenContainerWithPartitionKeyExists_WhenFeedRangeWithInclusiveBoundsIsCompared_ThenItShouldBePartOfAnotherFeedRange`, which compares two feed ranges to determine if one is part of the other.
    ///    - This helps verify how feed ranges behave, especially after partition splits.
    ///    - Example output:
    ///    <![CDATA[
    ///    Given FeedRange y: {y}, When compared to FeedRange x: {x}, Then y is part of x.
    ///
    ///    Given FeedRange y: ["Balistreri"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["Balistreri"], When compared to FeedRange x: [,05C1DFFFFFFFFC), Then y is not part of x.
    ///    Given FeedRange y: ["Balistreri"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["Gislason"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["Gislason"], When compared to FeedRange x: [,05C1DFFFFFFFFC), Then y is not part of x.
    ///    Given FeedRange y: ["Gislason"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["McGlynn"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["McGlynn"], When compared to FeedRange x: [,05C1DFFFFFFFFC), Then y is not part of x.
    ///    Given FeedRange y: ["McGlynn"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.Given FeedRange y: ["Balistreri"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["Balistreri"], When compared to FeedRange x: [,05C1DFFFFFFFFC), Then y is not part of x.
    ///    Given FeedRange y: ["Balistreri"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["Gislason"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["Gislason"], When compared to FeedRange x: [,05C1DFFFFFFFFC), Then y is not part of x.
    ///    Given FeedRange y: ["Gislason"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["McGlynn"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    Given FeedRange y: ["McGlynn"], When compared to FeedRange x: [,05C1DFFFFFFFFC), Then y is not part of x.
    ///    Given FeedRange y: ["McGlynn"], When compared to FeedRange x: [05C1DFFFFFFFFC,FF), Then y is part of x.
    ///    ]]>
    ///
    /// 6. **Error Handling and Resource Cleanup**:
    ///    - The program is wrapped in `try-catch-finally` blocks to gracefully handle exceptions, including Cosmos DB connection errors, timeouts, or change feed processor issues.
    ///    - In the `finally` block, the database is deleted, and the Cosmos client is disposed of to release resources properly.
    ///
    /// 7. **Real-time Cancellation**:
    ///    - The program allows real-time cancellation of the Change Feed Processor via the 'ESC' key, using a `CancellationTokenSource`.
    ///
    /// This application demonstrates essential Cosmos DB operations, such as creating containers, handling partition splits, working with the change feed processor, and managing resources with asynchronous programming.
    internal class Program
    {
        private static async Task Main(string[] args)
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

            CosmosClient cosmosClient = new CosmosClient(endpoint, authKey);
            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(id: Guid.NewGuid().ToString());

            Console.WriteLine($"Creating database with ID: {database.Id}. A new database instance is being provisioned to store and manage container data.");

            try
            {
                Container container = await database.CreateContainerIfNotExistsAsync(containerProperties: new ContainerProperties
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKeyPath = "/pk",
                });

                Console.WriteLine($"Creating container with ID: {container.Id}. A new container is being provisioned within the database to store partitioned data for optimized scalability and performance.");

                await Program.UpdateContainerThroughputAsync(container, 12000);

                await Program.WaitForPartitionSplitAsync(container);

                var cancellationTokenSource = new CancellationTokenSource();

                Task changeFeedTask = Program.StartChangeFeedProcessorWithFeedRangeComparisonAsync(
                    cancellationTokenSource.Token,
                    database,
                    container);

                Console.WriteLine("Press 'ESC' to stop the Change Feed Processor. This will safely stop monitoring for changes and gracefully shut down the processor, ensuring all current changes are processed.");

                await Program.SeedDocumentsToContainerAsync(container);

                // Monitor for 'ESC' key press to cancel
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true).Key;
                        if (key == ConsoleKey.Escape)
                        {
                            Console.WriteLine("ESC key pressed. Initiating graceful shutdown of the Change Feed Processor to stop monitoring changes and ensure all pending operations are completed.");
                            cancellationTokenSource.Cancel();
                            break;
                        }
                    }

                    // Give a small delay to avoid tight looping
                    await Task.Delay(100);
                }

                // Wait for the processor to stop gracefully
                await changeFeedTask;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
            finally
            {
                Console.WriteLine($"Initiating deletion of the database with ID: {database.Id}. All data and associated resources within this database will be permanently removed.");

                _ = await database?.DeleteAsync();

                cosmosClient?.Dispose();
            }
        }

        /// <summary>
        /// Reads the current throughput of the specified Cosmos DB container, logs the current throughput value, 
        /// and updates it to a higher throughput to handle increased workloads and ensure optimal performance.
        /// </summary>
        /// <param name="container">The Cosmos DB container whose throughput will be read and updated.</param>
        /// <param name="newThroughput">The new throughput value to set for the container, specified in Request Units (RU/s).</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        private static async Task UpdateContainerThroughputAsync(Container container, int newThroughput)
        {
            // Read and log the current throughput
            int? currentThroughput = await container.ReadThroughputAsync();
            Console.WriteLine($"Current throughput is {currentThroughput} RU/s. This indicates the number of Request Units per second allocated to the container, affecting its performance and scalability.");

            // Set and update the new throughput value
            ThroughputProperties throughputProperties = ThroughputProperties.CreateManualThroughput(newThroughput);
            ThroughputResponse throughputResponse = await container.ReplaceThroughputAsync(throughputProperties);

            // Log the new throughput value and the reason for the increase
            Console.WriteLine($"Throughput successfully updated to {throughputResponse.Resource.Throughput} RU/s. Increasing the throughput is necessary to handle higher workloads, improve performance, and ensure the system can scale effectively with the anticipated data volume.");
        }


        /// <summary>
        /// Seeds a set of documents into the specified Cosmos DB container asynchronously.
        /// Generates 10,000 documents with random first and last names using the Bogus library,
        /// and upserts them into the container.
        /// </summary>
        /// <param name="container">The Cosmos DB container where documents will be upserted.</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        private static async Task SeedDocumentsToContainerAsync(Container container)
        {
            // Prepare a list to hold the asynchronous upsert tasks
            List<Task> tasks = new List<Task>(10000);

            // Loop to create and upsert 10,000 documents with random data
            for (int i = 0; i < 10000; i++)
            {
                // Generate a random first and last name using Bogus
                var name = new Bogus.Faker().Name;
                string firstName = name.FirstName();
                string lastName = name.LastName();

                // Upsert a new document with a unique ID and a partition key based on the last name
                Task<ItemResponse<dynamic>> task = container.UpsertItemAsync<dynamic>(
                    new { id = Guid.NewGuid().ToString(), firstName = firstName, pk = lastName },
                    new PartitionKey(lastName));

                // Add task with error handling continuation
                tasks.Add(task.ContinueWith(t =>
                {
                    if (t.Status != TaskStatus.RanToCompletion)
                    {
                        Console.WriteLine($"An error occurred during document upsert: {t.Exception?.Message}. Please check the document details and ensure the container is configured correctly.");
                    }
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            Console.WriteLine("Documents have been successfully seeded into the container. The container is now populated with sample data, ready for processing and partitioning operations.");
        }

        /// <summary>
        /// Starts the Change Feed Processor for the specified Cosmos DB container asynchronously.
        /// Creates a lease container if it does not exist, listens to the change feed, and compares feed ranges
        /// for each change. Handles cancellation requests to stop the processor gracefully.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <param name="database">The Cosmos DB database that contains the lease container.</param>
        /// <param name="container">The Cosmos DB container that the Change Feed Processor will monitor.</param>
        /// <returns>A Task that represents the asynchronous operation of the Change Feed Processor.</returns>
        private static async Task StartChangeFeedProcessorWithFeedRangeComparisonAsync(
            CancellationToken cancellationToken,
            Database database,
            Container container)
        {
            // Create the lease container if it doesn't exist
            ContainerResponse leaseContainerResponse = await database.CreateContainerIfNotExistsAsync(id: "leaseContainer", partitionKeyPath: "/id");
            Container leaseContainer = leaseContainerResponse.Container;

            // Build the Change Feed Processor
            ChangeFeedProcessor changeFeedProcessor = container.GetChangeFeedProcessorBuilder<dynamic>(
                Guid.NewGuid().ToString(),
                (context, changes, token) => Program.HandleFeedRangeChangesAsync(
                    context,
                    changes,
                    container,
                    feedRanges,
                    token))
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((string leaseToken, Exception exception) =>
                {
                    Console.WriteLine($"Lease with token '{leaseToken}' encountered an error: {exception.Message}. The change feed processor may be unable to track changes for this partition until the issue is resolved.");

                    return Task.CompletedTask;
                })
                .Build();

            // Start the Change Feed Processor
            await changeFeedProcessor.StartAsync();

            // Log the current status
            Console.WriteLine("Change Feed Processor has started. The processor is now actively monitoring the container for changes and will process them in real-time as they occur.");

            try
            {
                // Await cancellation request
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                // Handle cancellation
                Console.WriteLine(exception);
            }

            // Stop the Change Feed Processor when cancellation is requested
            await changeFeedProcessor.StopAsync();

            // Log the current status
            Console.WriteLine("Change Feed Processor has stopped. The monitoring of container changes has been gracefully terminated, ensuring that all current changes were processed before shutdown.");
        }

        /// <summary>
        /// Processes changes from the Cosmos DB Change Feed Processor by extracting the feed range from the context
        /// and comparing each change's partition key feed range with both the context feed range and the provided feed ranges.
        /// </summary>
        /// <param name="context">The context containing details about the change feed processing, including the current feed range.</param>
        /// <param name="changes">The collection of changes detected in the container.</param>
        /// <param name="token">A cancellation token to handle cancellation requests.</param>
        /// <param name="container">The Cosmos DB container where the feed range comparisons will occur.</param>
        /// <param name="feedRanges">A collection of feed ranges available in the specified container.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private static async Task HandleFeedRangeChangesAsync(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<dynamic> changes,
            Container container,
            IReadOnlyList<FeedRange> feedRanges,
            CancellationToken token)
        {
            // Iterate over the detected changes and compare the associated partition feed ranges
            foreach (var change in changes)
            {
                // Create a feed range based on the partition key from the change
                FeedRange partitionFeedRange = FeedRange.FromPartitionKey(
                    new PartitionKeyBuilder()
                        .Add(change.pk.ToString())
                        .Build());

                // Compare the partition feed range with the feed range from the context
                await Program.GivenContainerWithPartitionKeyExists_WhenFeedRangeWithInclusiveBoundsIsCompared_ThenItShouldBePartOfAnotherFeedRange(
                    container: container,
                    x: context.FeedRange,
                    y: partitionFeedRange).ConfigureAwait(false);

                // Compare the partition feed range with each of the provided feed ranges
                foreach (var feedRange in feedRanges)
                {
                    await Program.GivenContainerWithPartitionKeyExists_WhenFeedRangeWithInclusiveBoundsIsCompared_ThenItShouldBePartOfAnotherFeedRange(
                        container: container,
                        x: feedRange,
                        y: partitionFeedRange).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Retrieves the list of feed ranges after a partition split has occurred in the specified container.
        /// The method seeds the container with documents, waits for the partition split to complete, and times out if the split does not occur within a specified period.
        /// </summary>
        /// <param name="container">The Cosmos DB container where the partition split is expected to occur.</param>
        /// <returns>A Task that represents the asynchronous operation and returns the feed ranges after the split has occurred, or null if the operation times out.</returns>
        /// <exception cref="TimeoutException">Thrown if the partition split does not complete within the specified timeout period.</exception>
        private static async Task WaitForPartitionSplitAsync(Container container)
        {
            // Seed the container with documents
            await Program.SeedDocumentsToContainerAsync(container);

            const int timeoutInMinutes = 20;
            DateTime startTime = DateTime.UtcNow;
            IReadOnlyList<FeedRange>? feedRanges = null;

            // Start a stopwatch to track the elapsed time
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                // Check if the timeout period has been exceeded
                if (DateTime.UtcNow - startTime > TimeSpan.FromMinutes(timeoutInMinutes))
                {
                    stopwatch.Stop();
                    throw new TimeoutException($"Partition split did not complete within the allocated timeout of {timeoutInMinutes} minutes. The container's data repartitioning process may be delayed or require manual intervention to handle increased data load and distribute partitions effectively.");
                }

                // Retrieve the feed ranges from the container
                feedRanges = await container.GetFeedRangesAsync();

                // Check if the partition split has completed
                if (feedRanges.Count > 1)
                {
                    // Log the current status
                    Console.WriteLine($"Partition split successfully completed after {stopwatch.Elapsed.TotalSeconds} seconds. The container has now been repartitioned to handle increased load and distribute data more efficiently.");

                    stopwatch.Stop();
                    break;
                }

                // Assert that the container has at least one feed range
                Debug.Assert(feedRanges != null, "The container must have at least one feed range. This indicates that the container is improperly configured or has not been initialized correctly to support partitioning, which is critical for distributing data across multiple partitions.");

                // Log the current status
                Console.WriteLine($"Waiting for partition split to complete. Time elapsed: {stopwatch.Elapsed.TotalSeconds} seconds. Current partition count: {feedRanges.Count}. The container is being monitored to ensure it is correctly partitioned to handle the increasing data load efficiently.");

                // Wait for 1 minute before checking again
                await Task.Delay(60000);
            }

            Debug.Assert(feedRanges.Count > 1, $"The container needs to have at least two feed ranges to ensure proper partitioning and data distribution across multiple logical partitions.");
        }

        /// <summary>
        /// Compares two feed ranges to determine if one is part of the other in the specified Cosmos DB container.
        /// If no exception occurs, the result is printed to the console, indicating whether the second feed range is part of the first.
        /// </summary>
        /// <param name="container">The Cosmos DB container where the feed range comparison is performed.</param>
        /// <param name="x">The feed range that is being compared as the source range.</param>
        /// <param name="y">The feed range that is being compared as the target range.</param>
        /// <returns>A Task that represents the asynchronous operation.</returns>
        static async Task GivenContainerWithPartitionKeyExists_WhenFeedRangeWithInclusiveBoundsIsCompared_ThenItShouldBePartOfAnotherFeedRange(
            Container container,
            FeedRange x,
            FeedRange y)
        {
            try
            {
                // Perform the feed range comparison asynchronously
                bool isPartOf = await container
                    .IsFeedRangePartOfAsync(
                        x: x,
                        y: y)
                    .ConfigureAwait(continueOnCapturedContext: false);

                // Log the comparison result
                Console.WriteLine($"Given FeedRange y: {y}, When compared to FeedRange x: {x}, Then y {(isPartOf ? "is" : "is not")} part of x.");
            }
            catch (Exception ex)
            {
                // Assert that no exception should occur and log if an exception is thrown
                Debug.Assert(ex == null, $"No exception was expected in this scenario. An unexpected exception occurred: {ex}. Please investigate the cause, as this might indicate an issue with the feed range comparison or container configuration.");
            }
        }
    }
}