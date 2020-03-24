//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class QueryFeedTokenTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task GetQueryFeedTokens()
        {
            ContainerCore container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    throughput: 15000);
                container = (ContainerInlineCore)containerResponse;

                int pkRangesCount = (await container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(container.LinkUri)).Count;

                await QueryFeedTokenTests.GetQueryFeedTokens(container, null, null, pkRangesCount);
                await QueryFeedTokenTests.GetQueryFeedTokens(container, new QueryDefinition("select * from c"), null, pkRangesCount);
                await QueryFeedTokenTests.GetQueryFeedTokens(container, new QueryDefinition("select * from c"), new QueryRequestOptions() { PartitionKey = new Cosmos.PartitionKey("value") }, 1);
                await QueryFeedTokenTests.GetQueryFeedTokens(container, new QueryDefinition("SELECT VALUE AVG(c.age) FROM c"), null, 1);
                await QueryFeedTokenTests.GetQueryFeedTokens(container, new QueryDefinition("SELECT DISTINCT VALUE c.age FROM c ORDER BY c.age"), null, 1);
                await QueryFeedTokenTests.GetQueryFeedTokens(container, new QueryDefinition("SELECT c.age, c.name FROM c GROUP BY c.age, c.name"), null, 1);
                await QueryFeedTokenTests.GetQueryFeedTokens(container, new QueryDefinition("select TOP 10 * FROM C"), null, 1);
                await QueryFeedTokenTests.GetQueryFeedTokens(container, new QueryDefinition("select * FROM C OFFSET 10 LIMIT 5"), null, 1);
            }
            finally
            {
                await container?.DeleteContainerAsync();
            }
        }

        private static async Task GetQueryFeedTokens(ContainerCore container, QueryDefinition query, QueryRequestOptions queryRequestOptions,int expectedCount)
        {
            IReadOnlyList<QueryFeedToken> feedTokens = await container.GetQueryFeedTokensAsync(query, queryRequestOptions);
            Assert.AreEqual(expectedCount, feedTokens.Count, $"For query {query?.QueryText}, expected {expectedCount}");
        }

        [TestMethod]
        public async Task GetTargetPartitionKeyRangesAsyncWithFeedToken()
        {
            ContainerCore container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    throughput: 15000);
                container = (ContainerInlineCore)containerResponse;

                // Get all the partition key ranges to verify there is more than one partition
                IRoutingMapProvider routingMapProvider = await this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync();

                ContainerQueryProperties containerQueryProperties = new ContainerQueryProperties(
                    containerResponse.Resource.ResourceId,
                    null,
                    containerResponse.Resource.PartitionKey);

                IReadOnlyList<QueryFeedToken> feedTokens = await container.GetQueryFeedTokensAsync(new QueryDefinition("select * from c"));

                Assert.IsTrue(feedTokens.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                // There should only be one range since we get 1 FeedToken per range
                foreach (QueryFeedToken feedToken in feedTokens)
                {
                    List<PartitionKeyRange> partitionKeyRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                        queryClient: new CosmosQueryClientCore(container.ClientContext, container),
                        resourceLink: container.LinkUri.OriginalString,
                        partitionedQueryExecutionInfo: null,
                        containerQueryProperties: containerQueryProperties,
                        properties: null,
                        queryFeedToken: (feedToken as QueryFeedTokenInternal).QueryFeedToken);

                    Assert.IsTrue(partitionKeyRanges.Count == 1, "Only 1 partition key range should be selected since the FeedToken represents a single range.");
                }
            }
            finally
            {
                await container?.DeleteContainerAsync();
            }
        }

        [TestMethod]
        public async Task ParallelizeQueryThroughTokens()
        {
            ContainerCore container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id",
                    throughput: 15000);
                container = (ContainerInlineCore)containerResponse;

                List<string> generatedIds = Enumerable.Range(0, 1000).Select(n => $"BasicItem{n}").ToList();
                foreach (string id in generatedIds)
                {
                    string item = $@"
                    {{    
                        ""id"": ""{id}""
                    }}";

                    using (ResponseMessage createResponse = await container.CreateItemStreamAsync(
                            QueryFeedTokenTests.GenerateStreamFromString(item),
                            new Cosmos.PartitionKey(id)))
                    {
                        Assert.IsTrue(createResponse.IsSuccessStatusCode);
                    }
                }

                IReadOnlyList<QueryFeedToken> feedTokens = await container.GetQueryFeedTokensAsync(new QueryDefinition("select * from T where STARTSWITH(T.id, \"BasicItem\")"));

                Assert.IsTrue(feedTokens.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                List<Task<List<string>>> tasks = feedTokens.Select(async feedToken =>
                {
                    List<string> results = new List<string>();
                    QueryIterator feedIterator = container.GetItemQueryStreamIterator(feedToken: feedToken, requestOptions: new QueryRequestOptions() { MaxItemCount = 10 }) as QueryIterator;
                    while (feedIterator.HasMoreResults)
                    {
                        using (ResponseMessage responseMessage =
                            await feedIterator.ReadNextAsync(this.cancellationToken))
                        {
                            if (responseMessage.IsSuccessStatusCode)
                            {
                                using (StreamReader reader = new StreamReader(responseMessage.Content))
                                {
                                    string json = await reader.ReadToEndAsync();
                                    JArray documents = (JArray)JObject.Parse(json).SelectToken("Documents");
                                    foreach(JObject document in documents)
                                    {
                                        results.Add(document.SelectToken("id").ToString());
                                    }
                                }
                            }

                            QueryFeedToken finalFeedToken = feedIterator.FeedToken;
                            if (finalFeedToken != null)
                            {
                                // Since we are using FeedTokens for each PKRange, they shouldn't be able to scale
                                Assert.AreEqual(0, finalFeedToken.Scale().Count);
                            }
                        }
                    }

                    return results;
                }).ToList();

                await Task.WhenAll(tasks);

                CollectionAssert.AreEquivalent(generatedIds, tasks.SelectMany(t => t.Result).ToList());
            }
            finally
            {
                await container?.DeleteContainerAsync();
            }
        }

        [TestMethod]
        public async Task CannotMixTokensFromOtherContainers()
        {
            ContainerCore container = null;
            ContainerCore container2 = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id");
                container = (ContainerInlineCore)containerResponse;

                containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id");
                container2 = (ContainerInlineCore)containerResponse;

                List<string> generatedIds = Enumerable.Range(0, 30).Select(n => $"BasicItem{n}").ToList();
                foreach (string id in generatedIds)
                {
                    string item = $@"
                    {{    
                        ""id"": ""{id}""
                    }}";

                    using (ResponseMessage createResponse = await container.CreateItemStreamAsync(
                            QueryFeedTokenTests.GenerateStreamFromString(item),
                            new Cosmos.PartitionKey(id)))
                    {
                        Assert.IsTrue(createResponse.IsSuccessStatusCode);
                    }
                }

                IReadOnlyList<QueryFeedToken> feedTokens = await container.GetQueryFeedTokensAsync(new QueryDefinition("select * from T where STARTSWITH(T.id, \"BasicItem\")"));
                QueryIterator feedIterator = container.GetItemQueryStreamIterator(feedToken: feedTokens[0], requestOptions: new QueryRequestOptions() { MaxItemCount = 10 }) as QueryIterator;
                QueryFeedToken feedTokenFromContainer1 = null;
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage responseMessage =
                        await feedIterator.ReadNextAsync(this.cancellationToken))
                    {
                        feedTokenFromContainer1 = feedIterator.FeedToken;
                        Assert.IsNotNull(feedTokenFromContainer1);
                    }

                    break;
                }

                FeedIteratorInternal feedIterator2 = container2.GetItemQueryStreamIterator(feedToken: feedTokenFromContainer1, requestOptions: new QueryRequestOptions() { MaxItemCount = 10 }) as FeedIteratorInternal;
                while (feedIterator2.HasMoreResults)
                {
                    ResponseMessage responseMessage = await feedIterator2.ReadNextAsync(this.cancellationToken);
                    Assert.IsNotNull(responseMessage.CosmosException);
                    Assert.AreEqual(HttpStatusCode.InternalServerError, responseMessage.StatusCode);
                    break;
                }
            }
            finally
            {
                await container?.DeleteContainerAsync();
                await container2?.DeleteContainerAsync();
            }
        }

        /// <summary>
        /// This test will execute a query once over a multiple partition collection, get the FeedToken, call Scale, and see if the resulting FeedToken produces more.
        /// </summary>
        [TestMethod]
        public async Task CanScale()
        {
            ContainerCore container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id",
                    throughput: 15000);
                container = (ContainerInlineCore)containerResponse;

                List<string> generatedIds = Enumerable.Range(0, 1000).Select(n => $"BasicItem{n}").ToList();
                foreach (string id in generatedIds)
                {
                    string item = $@"
                    {{    
                        ""id"": ""{id}""
                    }}";

                    using (ResponseMessage createResponse = await container.CreateItemStreamAsync(
                            QueryFeedTokenTests.GenerateStreamFromString(item),
                            new Cosmos.PartitionKey(id)))
                    {
                        Assert.IsTrue(createResponse.IsSuccessStatusCode);
                    }
                }

                List<string> results = new List<string>();
                QueryIterator initialIterator = container.GetItemQueryStreamIterator(queryText: "select * from T where STARTSWITH(T.id, \"BasicItem\")") as QueryIterator;
                while (initialIterator.HasMoreResults)
                {
                    using (ResponseMessage responseMessage =
                        await initialIterator.ReadNextAsync(this.cancellationToken))
                    {
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            using (StreamReader reader = new StreamReader(responseMessage.Content))
                            {
                                string json = await reader.ReadToEndAsync();
                                JArray documents = (JArray)JObject.Parse(json).SelectToken("Documents");
                                foreach (JObject document in documents)
                                {
                                    results.Add(document.SelectToken("id").ToString());
                                }
                            }
                        }

                        break;
                    }
                }

                QueryFeedToken baseFeedToken = initialIterator.FeedToken;
                IReadOnlyList<QueryFeedToken> feedTokens = baseFeedToken.Scale();
                Assert.IsTrue(feedTokens.Count > 0, "Should be able to scale");
            }
            finally
            {
                await container?.DeleteContainerAsync();
            }
        }

        private static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}