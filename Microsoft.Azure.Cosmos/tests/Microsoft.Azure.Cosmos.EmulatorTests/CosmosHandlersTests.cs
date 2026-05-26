//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosHandlersTests : BaseCosmosClientHelper
    {
        private Container Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestCustomPropertyWithHandler()
        { 
            RequestHandlerHelper testHandler = new RequestHandlerHelper();

            // Add the random guid to the property
            Guid randomGuid = Guid.NewGuid();
            string propertyKey = "Test";
            testHandler.UpdateRequestMessage = x => x.Properties[propertyKey] = randomGuid;

            CosmosClient customClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.AddCustomHandlers(testHandler));

            ToDoActivity testItem = CreateRandomToDoActivity();
            using (ResponseMessage response = await customClient.GetContainer(this.database.Id, this.Container.Id).CreateItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream(testItem)))
            {
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.RequestMessage);
                Assert.IsNotNull(response.RequestMessage.Properties);
                Assert.AreEqual(randomGuid, response.RequestMessage.Properties[propertyKey]);
            }
        }

        [TestMethod]
        public async Task TestBatchRequiredHeadersWithHandler()
        {
            RequestHandlerHelper testHandler = new RequestHandlerHelper();

            // Get the headers from request message for testing.
            Headers requestHeaders = null;
            testHandler.UpdateRequestMessage = x => requestHeaders = x.Headers;

            CosmosClient customClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.AddCustomHandlers(testHandler).WithBulkExecution(true));

            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (ResponseMessage response = await customClient.GetContainer(this.database.Id, this.Container.Id).CreateItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream(testItem)))
            {
                Assert.IsNotNull(response);
                Assert.IsNotNull(requestHeaders);

                string isBatchAtomic = requestHeaders[HttpConstants.HttpHeaders.IsBatchAtomic];
                Assert.IsNotNull(isBatchAtomic);
                Assert.IsFalse(bool.Parse(isBatchAtomic));

                string isBatchRequest = requestHeaders[HttpConstants.HttpHeaders.IsBatchRequest];
                Assert.IsNotNull(isBatchRequest);
                Assert.IsTrue(bool.Parse(isBatchRequest));

                string shouldBatchContinueOnError = requestHeaders[HttpConstants.HttpHeaders.ShouldBatchContinueOnError];
                Assert.IsNotNull(shouldBatchContinueOnError);
                Assert.IsTrue(bool.Parse(shouldBatchContinueOnError));
            }
        }

        [TestMethod]
        public async Task QueryMetricsAvailableInsideRequestHandler_SinglePartition()
        {
            // Regression test for issue #5117:
            // response.Diagnostics.GetQueryMetrics() must be non-null when invoked inside
            // a custom RequestHandler.SendAsync for a query operation.
            ConcurrentBag<ServerSideCumulativeMetrics> observedMetrics = new ConcurrentBag<ServerSideCumulativeMetrics>();

            RequestHandlerHelper testHandler = new RequestHandlerHelper
            {
                CallBackOnResponse = (request, response) =>
                {
                    if (request.OperationType == OperationType.Query
                        && response.IsSuccessStatusCode
                        && !string.IsNullOrEmpty(response.Headers?.QueryMetricsText))
                    {
                        ServerSideCumulativeMetrics metrics = response.Diagnostics.GetQueryMetrics();
                        Assert.IsNotNull(metrics, "GetQueryMetrics() must be non-null inside the handler (issue #5117).");
                        observedMetrics.Add(metrics);
                    }

                    return response;
                }
            };

            CosmosClient customClient = TestCommon.CreateCosmosClient(
                builder => builder.AddCustomHandlers(testHandler));

            try
            {
                Container container = customClient.GetContainer(this.database.Id, this.Container.Id);
                QueryMetricsItem item = new QueryMetricsItem
                {
                    id = Guid.NewGuid().ToString(),
                    pk = Guid.NewGuid().ToString(),
                    description = "single-partition"
                };
                await container.CreateItemAsync(item, new Cosmos.PartitionKey(item.pk));

                FeedIterator<QueryMetricsItem> iterator = container.GetItemQueryIterator<QueryMetricsItem>(
                    $"SELECT * FROM c WHERE c.id = '{item.id}'",
                    requestOptions: new QueryRequestOptions
                    {
                        PartitionKey = new Cosmos.PartitionKey(item.pk)
                    });

                int totalItems = 0;
                while (iterator.HasMoreResults)
                {
                    FeedResponse<QueryMetricsItem> page = await iterator.ReadNextAsync();
                    totalItems += page.Count;
                }

                Assert.AreEqual(1, totalItems);
                Assert.IsTrue(
                    observedMetrics.Count >= 1,
                    "Handler must be invoked for at least one query response.");

                foreach (ServerSideCumulativeMetrics metrics in observedMetrics)
                {
                    Assert.IsNotNull(metrics);
                    Assert.AreEqual(
                        1,
                        metrics.PartitionedMetrics.Count,
                        "Single-partition query should report exactly one PartitionedMetrics entry.");
                }
            }
            finally
            {
                customClient.Dispose();
            }
        }

        [TestMethod]
        public async Task QueryMetricsAvailableInsideRequestHandler_CrossPartition()
        {
            // Companion to the single-partition test. Verifies that for cross-partition
            // queries the handler still sees non-null metrics on every query response.
            // Option C semantics: response.Diagnostics walks from the operation root, so
            // each handler invocation sees its own partition's metrics plus any sibling
            // partitions that have already completed at that point. The exact per-page
            // distribution is timing-dependent; what we guarantee is (a) metrics are not
            // null inside the handler and (b) the iterator-level aggregated view exposes
            // metrics from multiple partitions.
            ConcurrentBag<int> observedPartitionCounts = new ConcurrentBag<int>();

            RequestHandlerHelper testHandler = new RequestHandlerHelper
            {
                CallBackOnResponse = (request, response) =>
                {
                    if (request.OperationType == OperationType.Query
                        && response.IsSuccessStatusCode
                        && !string.IsNullOrEmpty(response.Headers?.QueryMetricsText))
                    {
                        ServerSideCumulativeMetrics metrics = response.Diagnostics.GetQueryMetrics();
                        Assert.IsNotNull(metrics, "GetQueryMetrics() must be non-null inside the handler (issue #5117).");
                        observedPartitionCounts.Add(metrics.PartitionedMetrics.Count);
                    }

                    return response;
                }
            };

            CosmosClient customClient = TestCommon.CreateCosmosClient(
                builder => builder.AddCustomHandlers(testHandler));

            Cosmos.Database multiPartitionDatabase = null;
            try
            {
                multiPartitionDatabase = await customClient.CreateDatabaseAsync(
                    "MultiPkDb_" + Guid.NewGuid().ToString());

                Container multiPartitionContainer = await multiPartitionDatabase.CreateContainerAsync(
                    new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"),
                    throughput: 15000);

                IReadOnlyList<FeedRange> feedRanges = await multiPartitionContainer.GetFeedRangesAsync();
                if (feedRanges.Count < 2)
                {
                    Assert.Inconclusive(
                        $"Emulator did not split the 15000 RU container into >= 2 physical partitions (got {feedRanges.Count}); cannot exercise cross-partition fan-out.");
                }

                for (int i = 0; i < 30; i++)
                {
                    QueryMetricsItem item = new QueryMetricsItem
                    {
                        id = Guid.NewGuid().ToString(),
                        pk = Guid.NewGuid().ToString(),
                        description = "cross-partition-fanout"
                    };
                    await multiPartitionContainer.CreateItemAsync(item, new Cosmos.PartitionKey(item.pk));
                }

                FeedIterator<QueryMetricsItem> iterator = multiPartitionContainer.GetItemQueryIterator<QueryMetricsItem>(
                    "SELECT * FROM c",
                    requestOptions: new QueryRequestOptions
                    {
                        MaxConcurrency = -1
                    });

                int totalItems = 0;
                int maxPartitionedCountOnIterator = 0;
                bool iteratorSawMetrics = false;
                while (iterator.HasMoreResults)
                {
                    FeedResponse<QueryMetricsItem> page = await iterator.ReadNextAsync();
                    totalItems += page.Count;

                    ServerSideCumulativeMetrics pageMetrics = page.Diagnostics.GetQueryMetrics();
                    if (pageMetrics != null)
                    {
                        iteratorSawMetrics = true;
                        if (pageMetrics.PartitionedMetrics.Count > maxPartitionedCountOnIterator)
                        {
                            maxPartitionedCountOnIterator = pageMetrics.PartitionedMetrics.Count;
                        }
                    }
                }

                Assert.AreEqual(30, totalItems);

                Assert.IsTrue(
                    observedPartitionCounts.Count >= 2,
                    $"Cross-partition query should invoke the handler for at least 2 partition responses, got {observedPartitionCounts.Count}.");

                foreach (int count in observedPartitionCounts)
                {
                    Assert.IsTrue(
                        count >= 1,
                        "Each handler invocation should see at least one partition's metrics (issue #5117).");
                }

                Assert.IsTrue(
                    iteratorSawMetrics,
                    "Expected at least one page's iterator-level diagnostics to expose query metrics.");
                Assert.IsTrue(
                    maxPartitionedCountOnIterator >= 2,
                    $"Iterator-level diagnostics should aggregate metrics from multiple partitions on at least one page (max observed was {maxPartitionedCountOnIterator}).");
            }
            finally
            {
                if (multiPartitionDatabase != null)
                {
                    await multiPartitionDatabase.DeleteAsync();
                }

                customClient.Dispose();
            }
        }

        private async Task<IList<ToDoActivity>> CreateRandomItems(int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            Assert.IsFalse(!randomPartitionKey && perPKItemCount > 1);

            List<ToDoActivity> createdList = new List<ToDoActivity>();
            for (int i = 0; i < pkCount; i++)
            {
                string pk = "TBD";
                if (randomPartitionKey)
                {
                    pk += Guid.NewGuid().ToString();
                }

                for (int j = 0; j < perPKItemCount; j++)
                {
                    ToDoActivity temp = CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await this.Container.CreateItemAsync<ToDoActivity>(item: temp);
                }
            }

            return createdList;
        }

        private ToDoActivity CreateRandomToDoActivity(string pk = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }

            return new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                description = "CreateRandomToDoActivity",
                status = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }

        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }

        private class QueryMetricsItem
        {
            public string id { get; set; }
            public string pk { get; set; }
            public string description { get; set; }
        }
    }
}
