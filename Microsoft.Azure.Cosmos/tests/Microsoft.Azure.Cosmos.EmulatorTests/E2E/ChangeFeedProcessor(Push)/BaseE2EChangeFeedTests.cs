//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.E2E.ChangeFeedProcessor_Push_
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Newtonsoft.Json;

    public class BaseE2EChangeFeedTests
    {
        private readonly CancellationTokenSource CancellationTokenSource = new();
        public string ConnectionString { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public BaseE2EChangeFeedTests()
        {
            this.CancellationToken = this.CancellationTokenSource.Token;
            this.ConnectionString = Environment.GetEnvironmentVariable("TEST_LIVE_BACKEND_ENDPOINT");
            Debug.WriteLine($"(E2E ChangeFeedProcessor){nameof(this.ConnectionString)}: {this.ConnectionString}");
        }
        
        public CosmosClient CreateCosmosClient(string applicationName)
        {
            return new CosmosClient(
                connectionString: this.ConnectionString,
                clientOptions: new CosmosClientOptions
                {
                    AllowBulkExecution = true,
                    ConnectionMode = ConnectionMode.Gateway,
                    ApplicationName = applicationName,
                });
        }

        public async Task<Database> CreateDatabaseAsync(CosmosClient cosmosClient)
        {
            string databaseId = $"db_{Guid.NewGuid()}";
            Database database = await cosmosClient
                .CreateDatabaseIfNotExistsAsync(
                    throughput: 400,
                    id: databaseId,
                    cancellationToken: this.CancellationToken);

            Debug.WriteLine($"(E2E ChangeFeed){nameof(database)}: {JsonConvert.SerializeObject(database.Id)}");
            return database;
        }

        public async Task<ContainerResponse> CreateAndSplitContainerAsync(
            CosmosClient cosmosClient,
            Database database,
            int documentCount,
            int splitCount,
            CancellationToken cancellationToken)
        {
            string containerId = $"mc_{Guid.NewGuid()}";
            ContainerResponse containerResponse = await database
                .CreateContainerIfNotExistsAsync(
                    containerProperties: new ContainerProperties
                    {
                        Id = containerId,
                        PartitionKeyPath = "/pk"
                    },
                    cancellationToken: cancellationToken);

            Debug.WriteLine($"(E2E ChangeFeed){nameof(containerResponse)}: {JsonConvert.SerializeObject(containerResponse.Resource)}");
            Container container = containerResponse.Container;

            List<Task> tasks = new();

            for (int counter = 0; counter < documentCount; counter++)
            {
                tasks.Add(container.CreateItemAsync<dynamic>(new { id = Guid.NewGuid().ToString(), pk = "Washington" }, cancellationToken: cancellationToken));
            }

            await Task.WhenAll(tasks);

            //   II. Act

            ThroughputResponse throughputResponse = await database.ReplaceThroughputAsync(10100, cancellationToken: cancellationToken);

            Debug.WriteLine($"(E2E ChangeFeed){nameof(throughputResponse)}: {JsonConvert.SerializeObject(throughputResponse.Resource)}");

            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            Stopwatch stopWatch = Stopwatch.StartNew();
            IReadOnlyList<Documents.PartitionKeyRange> overlappingRanges;


            int partitionKeyRangeCount;

            do
            {
                overlappingRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                    collectionRid: containerResponse.Resource.ResourceId,
                    range: FeedRangeEpk.FullRange.Range,
                    trace: NoOpTrace.Singleton,
                    forceRefresh: true);

                partitionKeyRangeCount = overlappingRanges.Count;
                Debug.WriteLine($"(E2E ChangeFeed){nameof(partitionKeyRangeCount)}: {partitionKeyRangeCount}");

                if (stopWatch.Elapsed.TotalMinutes > 20) // failsafe to break loop if it takes longer than 20 minutes to split.
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken); // Don't check for partition split again until 1 minute has passed.

            } while (partitionKeyRangeCount == splitCount); // a partition count greater than 1, means a split has occured.

            Debug.WriteLine($"(E2E ChangeFeed) {nameof(overlappingRanges)}: {JsonConvert.SerializeObject(overlappingRanges)}");

            stopWatch.Stop();
            TimeSpan timeTakenForASplit = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                timeTakenForASplit.Hours, timeTakenForASplit.Minutes, timeTakenForASplit.Seconds,
                timeTakenForASplit.Milliseconds / 10);

            Debug.WriteLine($"(E2E ChangeFeed)Time taken for a split to occur: {elapsedTime}");
            Debug.WriteLine($"(E2E ChangeFeed){nameof(partitionKeyRangeCount)}: {partitionKeyRangeCount}");

            return containerResponse;
        }
    }
}