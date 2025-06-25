//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed.LongRunning
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// For future long running tests.
    /// TODO: Setup a PR to include in a CI pipeline.
    /// TODO: Need to determine what database account, connectionString, that these test
    /// can be safely ran against. Make sure the connectionString is added to Environment
    /// variables securely.
    /// </summary>
    [TestClass]
    [TestCategory("LongRunning")]
    public class ChangeFeedEstimatorTests
    {
        private readonly CancellationTokenSource CancellationTokenSource = new();

        private CosmosClient CosmosClient { get; set; }

        private Database Database { get; set; }

        private CancellationToken CancellationToken { get; set; }

        private Container LeaseContainer { get; set; }

        private ContainerResponse MonitoredContainerResponse { get; set; }

        private Container MonitoredContainer { get; set; }

        private static readonly string ConnectionString = "TestCategory_LongRunning_Connectionstring";

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.CancellationToken = this.CancellationTokenSource.Token;

            if (Environment.GetEnvironmentVariable(ChangeFeedEstimatorTests.ConnectionString) == null)
            {
                throw new ArgumentNullException(paramName: nameof(Environment.GetEnvironmentVariable));
            }

            this.CosmosClient = new CosmosClient(connectionString: Environment.GetEnvironmentVariable(ChangeFeedEstimatorTests.ConnectionString));

            this.Database = await this.CosmosClient.CreateDatabaseIfNotExistsAsync(
                id: Guid.NewGuid().ToString(),
                throughput: 400,
                cancellationToken: this.CancellationToken);

            Debug.WriteLine($"The {nameof(this.Database)} '{this.Database.Id}' was created.");

            this.LeaseContainer = await this.Database.CreateContainerIfNotExistsAsync(
                containerProperties: new ContainerProperties
                {
                    Id = "leases",
                    PartitionKeyPath = "/id",
                }, cancellationToken: this.CancellationToken);

            Debug.WriteLine($"The {nameof(this.LeaseContainer)} '{this.LeaseContainer.Id}' was created.");

            this.MonitoredContainerResponse = await this.Database.CreateContainerIfNotExistsAsync(
                containerProperties: new ContainerProperties
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKeyPath = "/pk",
                }, cancellationToken: this.CancellationToken);

            this.MonitoredContainer = this.MonitoredContainerResponse.Container;

            Debug.WriteLine($"The {nameof(this.MonitoredContainer)} '{this.MonitoredContainer.Id}' was created.");
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.Database != null)
            {
                await this.Database.DeleteAsync();

                Debug.WriteLine($"The database is deleted.");
            }

            this.CancellationTokenSource?.Cancel();

            this.CosmosClient.Dispose();
        }


        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4285"/>
        /// Steps
        ///     1. Create a database with 400 RU.
        ///     2. Create a lease container.
        ///     3. Create a monitored container.
        ///     4. Load 100 documents.
        ///     5. Create a CFP instance.
        ///     6. Start the CFP instance.
        ///     7. Stop the CFP instance.
        ///     8. Update the RU to 12K on the database.
        ///     9. Wait until the split happens on the monitored container.
        ///     10. Load 100 more documents.
        ///     11. Create a CFE instance.
        ///         a. (Use Estimator Iterator -> https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-use-change-feed-estimator?tabs=dotnet#as-an-on-demand-detailed-estimation)
        ///     12. Boom!
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Used to repro an issue #4285")]
        public async Task GivenADormantCFPWhenASplitOccursThenFeedEstimatorThrowsAnExceptionAsync()
        {
            string partitionKeyValue = Guid.NewGuid().ToString();

            await LoadDocuments(
                monitoredContainer: this.MonitoredContainer,
                partitionKeyValue: partitionKeyValue,
                cancellationToken: this.CancellationToken);

            ChangeFeedProcessor changeFeedProcessor = this.MonitoredContainer
                .GetChangeFeedProcessorBuilder(
                    processorName: "changeFeedEstimator",
                    onChangesDelegate: (IReadOnlyCollection<dynamic> changes, CancellationToken cancellationToken) => Task.CompletedTask)
                    .WithInstanceName("consoleHost")
                    .WithLeaseContainer(this.LeaseContainer)
                    .WithErrorNotification(errorDelegate: (leaseToken, exception) =>
                    {
                        Console.WriteLine($"{nameof(exception)}: {exception}");
                        Console.WriteLine($"{nameof(leaseToken)}: {leaseToken}");

                        return Task.CompletedTask;
                    })
                    .Build();

            await changeFeedProcessor.StartAsync();

            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await changeFeedProcessor.StopAsync();

            await UpdateThroughput(
                database: this.Database,
                cosmosClient: this.CosmosClient,
                monitoredContainerResponse: this.MonitoredContainerResponse,
                throughtput: 12000);

            await LoadDocuments(
                monitoredContainer: this.MonitoredContainer,
                partitionKeyValue: partitionKeyValue,
                startAt: 100,
                endAt: 200,
                cancellationToken: this.CancellationToken);

            ChangeFeedEstimator changeFeedEstimator = this.MonitoredContainer
                .GetChangeFeedEstimator(
                    processorName: "changeFeedEstimator",
                    leaseContainer: this.LeaseContainer);

            Debug.WriteLine("Checking estimation...");

            using FeedIterator<ChangeFeedProcessorState> estimatorIterator = changeFeedEstimator.GetCurrentStateIterator();

            long actualEstimatedLog = 0;

            while (estimatorIterator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> states = await estimatorIterator.ReadNextAsync(this.CancellationToken);

                foreach (ChangeFeedProcessorState leaseState in states)
                {
                    Debug.WriteLine(JsonConvert.SerializeObject(leaseState));

                    string host = leaseState.InstanceName == null ? $"not owned by any host currently" : $"owned by host {leaseState.InstanceName}";

                    Debug.WriteLine($"Lease [{leaseState.LeaseToken}] {host} reports {leaseState.EstimatedLag} as estimated lag.");

                    actualEstimatedLog = leaseState.EstimatedLag;
                }
            }

            Assert.AreEqual(expected: 1, actual: actualEstimatedLog);
        }

        /// <summary>
        /// Update the throughput and wait for a split.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="cosmosClient">The CosmosClient.</param>
        /// <param name="monitoredContainerResponse">The monitored container's response.</param>
        /// <param name="throughtput">The throughput.</param>
        /// <param name="partitionCount">The number of partitions detected before the split. Loop breaks when the overlapping ranges count is no longer the same as the partition count.</param>
        /// <param name="timeoutInMinutes">The number of minutes to timeout.</param>
        /// <returns></returns>
        private static async Task UpdateThroughput(
            Database database,
            CosmosClient cosmosClient,
            ContainerResponse monitoredContainerResponse,
            int throughtput,
            int partitionCount = 1,
            double timeoutInMinutes = 25)
        {
            _ = await database.ReplaceThroughputAsync(throughput: throughtput);
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            Stopwatch stopWatch = Stopwatch.StartNew();
            IReadOnlyList<Documents.PartitionKeyRange> overlappingRanges;

            do
            {
                overlappingRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                    collectionRid: monitoredContainerResponse.Resource.ResourceId,
                    range: FeedRangeEpk.FullRange.Range,
                    trace: NoOpTrace.Singleton,
                    partitionKeyDefinition: null,
                    forceRefresh: true);

                if (stopWatch.Elapsed.TotalMinutes > timeoutInMinutes) // failsafe to break loop if it takes longer than 'timeoutInMinutes' to split.
                {
                    break;
                }

            } while (overlappingRanges.Count == partitionCount); // when overlapping ranges count no longer equals the partitionCount, break loop.

            Debug.WriteLine($"{nameof(overlappingRanges)}: {JsonConvert.SerializeObject(overlappingRanges)}");

            stopWatch.Stop();
            TimeSpan timeTakenForASplit = stopWatch.Elapsed;
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                timeTakenForASplit.Hours, timeTakenForASplit.Minutes, timeTakenForASplit.Seconds,
                timeTakenForASplit.Milliseconds / 10);

            Debug.WriteLine($"Time taken for a split to occur: {elapsedTime}");
        }

        private static async Task LoadDocuments(
            Container monitoredContainer,
            string partitionKeyValue,
            int endAt = 100,
            int startAt = 0,
            CancellationToken cancellationToken = default)
        {
            for (int counter = startAt; counter < endAt; counter++)
            {
                _ = await monitoredContainer.CreateItemAsync<dynamic>(
                    item: new { id = counter.ToString(), pk = partitionKeyValue },
                    partitionKey: new PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken);
            }
        }
    }
}
