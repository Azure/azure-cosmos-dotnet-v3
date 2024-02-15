//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class AdvancedScenariosEstimatorTests
    {
        private readonly CancellationTokenSource CancellationTokenSource = new();

        private CosmosClient CosmosClient { get; set; }

        private Database Database { get; set; }

        private CancellationToken CancellationToken { get; set; }

        private Container LeaseContainer { get; set; }

        private ContainerResponse MonitoredContainerResponse { get; set; }

        private Container MonitoredContainer { get; set; }

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.CancellationToken = this.CancellationTokenSource.Token;

            if (Environment.GetEnvironmentVariable("TEST_LIVE_BACKEND_ENDPOINT") == null)
            {
                throw new ArgumentNullException(paramName: nameof(Environment.GetEnvironmentVariable));
            }

            this.CosmosClient = new CosmosClient(connectionString: Environment.GetEnvironmentVariable("TEST_LIVE_BACKEND_ENDPOINT"));

            // Step 1: Create a container (database) with 400 RU.

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
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Used to repro an issue #4285")]
        public async Task GivenADormantCFPWhenASplitOccursThenFeedEstimatorThrowsAnExceptionAsync()
        {
            try
            {
                string partitionKeyValue = Guid.NewGuid().ToString();
                await AdvancedScenariosEstimatorTests.LoadDocuments(
                    monitoredContainer: this.MonitoredContainer,
                    partitionKeyValue: partitionKeyValue,
                    cancellationToken: this.CancellationToken);

                ChangeFeedProcessor changeFeedProcessor = this.MonitoredContainer
                    .GetChangeFeedProcessorBuilder<dynamic>(
                        processorName: "changeFeedEstimator",
                        onChangesDelegate: (IReadOnlyCollection<dynamic> changes, CancellationToken cancellationToken) =>
                        {
                            return Task.CompletedTask;
                        })
                        .WithInstanceName("consoleHost")
                        .WithLeaseContainer(this.LeaseContainer)
                        .WithErrorNotification(errorDelegate: (string leaseToken, Exception exception) =>
                        {
                            Console.WriteLine($"{nameof(exception)}: {exception}");
                            Console.WriteLine($"{nameof(leaseToken)}: {leaseToken}");

                            return Task.CompletedTask;
                        })
                        .Build();

                // Step 2. Start CFP.
                await changeFeedProcessor.StartAsync();
                await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
                // Step 3. Stop CFP.
                await changeFeedProcessor.StopAsync();

                // Step 4. Update RU to 12K on container (database).
                await AdvancedScenariosEstimatorTests.UpdateThroughput(
                    database: this.Database,
                    cosmosClient: this.CosmosClient,
                    monitoredContainerResponse: this.MonitoredContainerResponse,
                    throughtput: 12000);

                // Add even more documents??? Missing step from issue description? 
                await AdvancedScenariosEstimatorTests.LoadDocuments(
                    monitoredContainer: this.MonitoredContainer,
                    partitionKeyValue: partitionKeyValue,
                    startAt: 100,
                    endAt: 200,
                    cancellationToken: this.CancellationToken);

                // Print out lease documents just to analyze.
                FeedIterator<dynamic> leaseIterator = this.LeaseContainer.GetItemQueryIterator<dynamic>("SELECT * FROM c");
                while (leaseIterator.HasMoreResults)
                {
                    foreach (dynamic item in await leaseIterator.ReadNextAsync(this.CancellationToken))
                    {
                        Debug.WriteLine($"lease document: {item}");
                    }
                }

                // Step 5. Use Estimator iterator on demand detailed estimation.
                // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-use-change-feed-estimator?tabs=dotnet#as-an-on-demand-detailed-estimation
                ChangeFeedEstimator changeFeedEstimator = this.MonitoredContainer
                    .GetChangeFeedEstimator(
                        processorName: "changeFeedEstimator",
                        leaseContainer: this.LeaseContainer);

                Debug.WriteLine("Checking estimation...");

                using FeedIterator<ChangeFeedProcessorState> estimatorIterator = changeFeedEstimator.GetCurrentStateIterator();
                while (estimatorIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedProcessorState> states = await estimatorIterator.ReadNextAsync(this.CancellationToken);
                    foreach (ChangeFeedProcessorState leaseState in states)
                    {
                        string host = leaseState.InstanceName == null ? $"not owned by any host currently" : $"owned by host {leaseState.InstanceName}";
                        Debug.WriteLine($"Lease [{leaseState.LeaseToken}] {host} reports {leaseState.EstimatedLag} as estimated lag.");
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.WriteLine($"{nameof(exception)}: {exception}");
            }
        }

        private static async Task UpdateThroughput(
            Database database,
            CosmosClient cosmosClient,
            ContainerResponse monitoredContainerResponse,
            int throughtput,
            int splitCount = 1)
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
                    forceRefresh: true);

                if (stopWatch.Elapsed.TotalMinutes > 25) // failsafe to break loop if it takes longer than 20 minutes to split.
                {
                    break;
                }

            } while (overlappingRanges.Count == splitCount);

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
            // Add some documents??? Missing step from issue description? 
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
