//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.E2E.ChangeFeedProcessor_Push_
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// The intention of these tests is to assert that change feed processor (push model) is functioning as expected
    /// for LatestVersions and AllVersionsAndDeletes ChangeFeedMode requests against a test live database account endpoint.
    /// </summary>
    [TestClass]
    [TestCategory("E2E ChangeFeedProcessor")]
    public class E2EChangeFeedProcessorTests : BaseE2EChangeFeedTests
    {
        [TestMethod]
        [Owner("philipthomas")]
        [DataRow(10, 1)]
        public async Task GivenSplitContainerWhenChangeFeedEstimatorPreLoadedDocumentsThenExpectsDocumentsTestAsync(
            int documentCount,
            int splitCount)
        {
            // AAA
            //   I. Arrange
            CosmosClient cosmosClient = this.CreateCosmosClient("Microsoft.Azure.Cosmos.SDK.EmulatorTests.E2E ChangeFeedProcessor");
            Database database = await this.CreateDatabaseAsync(cosmosClient);

            try
            {
                string leaseContainerId = $"lc_{Guid.NewGuid()}"; ;
                ContainerResponse leaseContainerResponse = await database.CreateContainerIfNotExistsAsync(
                    id: leaseContainerId,
                    partitionKeyPath: "/id",
                    cancellationToken: this.CancellationToken);
                Container leaseContainer = leaseContainerResponse.Container;

                ContainerResponse monitoredContainerResponse = await this.CreateAndSplitContainerAsync(
                    cosmosClient: cosmosClient,
                    database: database,
                    documentCount: documentCount,
                    splitCount: splitCount,
                    cancellationToken: this.CancellationToken);
                Container monitoredContainer = monitoredContainerResponse.Container;

                int actualDocumentCount = await this.ReadDocumentsAsync(
                    database: database,
                    monitoredContainer: monitoredContainer,
                    leaseContainer: leaseContainer,
                    retryAttempts: 0);

                //   III. Assert

                Assert.AreEqual(
                    expected: documentCount,
                    actual: actualDocumentCount);

                Debug.WriteLine($"(E2E ChangeFeed){nameof(documentCount)}: {documentCount}");
            }
            catch (CosmosException cosmosException)
            {
                Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(cosmosException)}: {cosmosException}");
                Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(cosmosException.Diagnostics)}: {cosmosException.Diagnostics}");

                Assert.Fail();
            }
            finally
            {
                _ = database.DeleteAsync(cancellationToken: this.CancellationToken);

                Debug.WriteLine($"(E2E ChangeFeedProcessor)The database with an id of '{database.Id}' has been removed.");
            }
        }

        private async Task<int> ReadDocumentsAsync(
            Database database,
            Container monitoredContainer,
            Container leaseContainer,
            int retryAttempts)
        {
            bool shouldRetry = default;
            int actualDocumentCount = 0;

            do
            {
                try
                {
                    ChangeFeedProcessor changeFeedProcessor = monitoredContainer
                        .GetChangeFeedProcessorBuilder<dynamic>(
                            processorName: "changeFeedEstimator",
                            onChangesDelegate: async (changes, cancellationToken) =>
                            {
                                Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(changes)}: {JsonConvert.SerializeObject(changes)}");
                                actualDocumentCount += changes.Count;
                                await Task.Delay(0, cancellationToken);
                            })
                        .WithInstanceName("consoleHost")
                        .WithLeaseContainer(leaseContainer)
                        .WithStartFromBeginning()
                        .Build();

                    Debug.Assert(changeFeedProcessor != null);

                    await changeFeedProcessor.StartAsync();
                    Debug.WriteLine($"(E2E ChangeFeedProcessor){changeFeedProcessor}StartAsync");

                    ChangeFeedProcessor changeFeedEstimator = this.CreateChangeFeedEstimatorWithNewCosmosClient(
                        leaseContainerId: leaseContainer.Id,
                        databaseId: database.Id,
                        monitoredContainerId: monitoredContainer.Id);

                    Debug.Assert(changeFeedEstimator != null);

                    await changeFeedEstimator.StartAsync();
                    Debug.WriteLine($"(E2E ChangeFeedProcessor){changeFeedEstimator}.StartAsync");

                    //////List<Task> tasks = new();

                    //////for (int counter = 0; counter < documentCount; counter++)
                    //////{
                    //////    tasks.Add(monitoredContainer.CreateItemAsync<dynamic>(new { id = Guid.NewGuid().ToString(), pk = "Washington" }, cancellationToken: this.CancellationToken));
                    //////}

                    //////await Task.WhenAll(tasks);
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
                catch (CosmosException ex)
                {
                    Debug.WriteLine($"(E2E ChangeFeedProcessor){ex}");
                    Debug.WriteLine($"(E2E ChangeFeedProcessor){ex.Diagnostics}");

                    if (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && ex.SubStatusCode == 3101)
                    {
                        shouldRetry = true;
                        retryAttempts++;

                        Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(retryAttempts)}: {retryAttempts}");

                        await Task.Delay(TimeSpan.FromSeconds(10), this.CancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
            } while (shouldRetry == true || retryAttempts > 10);

            return actualDocumentCount;
        }

        private ChangeFeedProcessor CreateChangeFeedEstimatorWithNewCosmosClient(
            string leaseContainerId, 
            string databaseId, 
            string monitoredContainerId)
        {
            CosmosClient cosmosClient = this.CreateCosmosClient("Microsoft.Azure.Cosmos.SDK.EmulatorTests.E2E ChangeFeedProcessor");
            Container monitoredContainer = cosmosClient.GetContainer(
                databaseId: databaseId,
                containerId: monitoredContainerId);
            Container leaseContainer = cosmosClient.GetContainer(
                databaseId: databaseId,
                containerId: leaseContainerId);

            Debug.Assert(monitoredContainer != null);
            Debug.Assert(leaseContainer != null);

            return monitoredContainer
                .GetChangeFeedEstimatorBuilder(
                    processorName: "changeFeedEstimator",
                    estimationDelegate: async (estimation, cancellationToken) =>
                    {
                        Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(estimation)}: {estimation}");

                        await Task.Delay(0, cancellationToken);
                    },
                    estimationPeriod: TimeSpan.FromMinutes(1))
                .WithLeaseContainer(leaseContainer)
                .WithStartFromBeginning()
                .WithErrorNotification(async (leaseToken, exception) =>
                {
                    Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(leaseToken)}: {leaseToken}");
                    Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(exception)}: {exception}");

                    await Task.Delay(TimeSpan.FromMinutes(1));
                })
                .Build();
        }

        //////private static async Task<ContainerResponse> CreateAndSplitContainerAsync(
        //////    CosmosClient cosmosClient,
        //////    Database database,
        //////    int documentCount,
        //////    int splitCount,
        //////    CancellationToken cancellationToken)
        //////{
        //////    string containerId = $"mc_{Guid.NewGuid()}";
        //////    ContainerResponse containerResponse = await database
        //////        .CreateContainerIfNotExistsAsync(
        //////            containerProperties: new ContainerProperties
        //////            {
        //////                Id = containerId,
        //////                PartitionKeyPath = "/pk",
        //////            },
        //////            cancellationToken: cancellationToken);

        //////    Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(containerResponse)}: {JsonConvert.SerializeObject(containerResponse.Resource)}");
        //////    Container container = containerResponse.Container;

        //////    List<Task> tasks = new();

        //////    for (int counter = 0; counter < documentCount; counter++)
        //////    {
        //////        tasks.Add(container.CreateItemAsync<dynamic>(new { id = Guid.NewGuid().ToString(), pk = "Washington" }, cancellationToken: cancellationToken));
        //////    }

        //////    await Task.WhenAll(tasks);

        //////    //   II. Act

        //////    ThroughputResponse throughputResponse = await database.ReplaceThroughputAsync(10100, cancellationToken: cancellationToken);

        //////    Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(throughputResponse)}: {JsonConvert.SerializeObject(throughputResponse.Resource)}");

        //////    Routing.PartitionKeyRangeCache partitionKeyRangeCache = await cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
        //////    Stopwatch stopWatch = Stopwatch.StartNew();
        //////    IReadOnlyList<Documents.PartitionKeyRange> overlappingRanges;

        //////    int partitionKeyRangeCount;

        //////    do
        //////    {
        //////        overlappingRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
        //////            collectionRid: containerResponse.Resource.ResourceId,
        //////            range: FeedRangeEpk.FullRange.Range,
        //////            trace: NoOpTrace.Singleton,
        //////            forceRefresh: true);

        //////        partitionKeyRangeCount = overlappingRanges.Count;
        //////        Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(partitionKeyRangeCount)}: {partitionKeyRangeCount}");

        //////        if (stopWatch.Elapsed.TotalMinutes > 20) // failsafe to break loop if it takes longer than 20 minutes to split.
        //////        {
        //////            break;
        //////        }

        //////        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken); // Don't check for partition split again until 1 minute has passed.

        //////    } while (partitionKeyRangeCount == splitCount); // a partition count greater than 1, means a split has occured.

        //////    Debug.WriteLine($"(E2E ChangeFeed) {nameof(overlappingRanges)}: {JsonConvert.SerializeObject(overlappingRanges)}");

        //////    stopWatch.Stop();
        //////    TimeSpan timeTakenForASplit = stopWatch.Elapsed;
        //////    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
        //////        timeTakenForASplit.Hours, timeTakenForASplit.Minutes, timeTakenForASplit.Seconds,
        //////        timeTakenForASplit.Milliseconds / 10);

        //////    Debug.WriteLine($"(E2E ChangeFeed)Time taken for a split to occur: {elapsedTime}");
        //////    Debug.WriteLine($"(E2E ChangeFeed){nameof(partitionKeyRangeCount)}: {partitionKeyRangeCount}");

        //////    return containerResponse;
        //////}
    }
}
