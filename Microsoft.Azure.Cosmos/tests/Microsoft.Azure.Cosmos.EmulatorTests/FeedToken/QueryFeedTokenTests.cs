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
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [SDK.EmulatorTests.TestClass]
    public class QueryFeedRangeTests : BaseCosmosClientHelper
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
        public async Task GetTargetPartitionKeyRangesAsyncWithFeedRange()
        {
            ContainerInternal container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    throughput: 15000);
                container = (ContainerInlineCore)containerResponse;

                // Get all the partition key ranges to verify there is more than one partition
                IRoutingMapProvider routingMapProvider = await this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

                ContainerQueryProperties containerQueryProperties = new ContainerQueryProperties(
                    containerResponse.Resource.ResourceId,
                    null,
                    containerResponse.Resource.PartitionKey);

                IReadOnlyList<FeedRange> feedTokens = await container.GetFeedRangesAsync();

                Assert.IsTrue(feedTokens.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                // There should only be one range since we get 1 FeedRange per range
                foreach (FeedRange feedToken in feedTokens)
                {
                    List<PartitionKeyRange> partitionKeyRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                        queryClient: new CosmosQueryClientCore(container.ClientContext, container),
                        resourceLink: container.LinkUri,
                        partitionedQueryExecutionInfo: null,
                        containerQueryProperties: containerQueryProperties,
                        properties: null,
                        feedRangeInternal: feedToken as FeedRangeInternal,
                        NoOpTrace.Singleton);

                    Assert.IsTrue(partitionKeyRanges.Count == 1, "Only 1 partition key range should be selected since the FeedRange represents a single range.");
                }
            }
            finally
            {
                await container?.DeleteContainerAsync();
            }
        }

        [TestMethod]
        public async Task InexistentPKRangeId()
        {
            ContainerInternal container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk");
                container = (ContainerInlineCore)containerResponse;

                string feedRangeSerialization = JsonConvert.SerializeObject(new { PKRangeId = "10" });
                FeedRange feedRange = FeedRange.FromJsonString(feedRangeSerialization);

                FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>(
                        queryDefinition: new QueryDefinition("select * from T where STARTSWITH(T.id, \"BasicItem\")"),
                        feedRange: feedRange,
                        continuationToken: null);

                CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => feedIterator.ReadNextAsync());
                Assert.AreEqual(HttpStatusCode.Gone, exception.StatusCode);
                Assert.AreEqual((int)Documents.SubStatusCodes.PartitionKeyRangeGone, exception.SubStatusCode);
            }
            finally
            {
                await container?.DeleteContainerAsync();
            }
        }

        [TestMethod]
        public async Task ParallelizeQueryThroughTokens()
        {
            ContainerInternal container = null;
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
                            QueryFeedRangeTests.GenerateStreamFromString(item),
                            new Cosmos.PartitionKey(id)))
                    {
                        Assert.IsTrue(createResponse.IsSuccessStatusCode);
                    }
                }

                IReadOnlyList<FeedRange> feedTokens = await container.GetFeedRangesAsync();

                Assert.IsTrue(feedTokens.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                List<Task<List<string>>> tasks = feedTokens.Select(async feedToken =>
                {
                    List<string> results = new List<string>();
                    FeedIteratorInternal feedIterator = container.GetItemQueryStreamIterator(
                        queryDefinition: new QueryDefinition("select * from T where STARTSWITH(T.id, \"BasicItem\")"),
                        feedRange: feedToken,
                        continuationToken: null,
                        requestOptions: new QueryRequestOptions() { MaxItemCount = 10 }) as FeedIteratorInternal;
                    string continuation = null;
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

                            continuation = responseMessage.ContinuationToken;
                            break;
                        }
                    }

                    feedIterator = container.GetItemQueryStreamIterator(queryDefinition: new QueryDefinition("select * from T where STARTSWITH(T.id, \"BasicItem\")"), feedRange: feedToken, continuationToken: continuation, requestOptions: new QueryRequestOptions() { MaxItemCount = 10 }) as FeedIteratorInternal;
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
                                    foreach (JObject document in documents)
                                    {
                                        results.Add(document.SelectToken("id").ToString());
                                    }
                                }
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
        public async Task ParallelizeQueryThroughTokens_OfT()
        {
            ContainerInternal container = null;
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
                            QueryFeedRangeTests.GenerateStreamFromString(item),
                            new Cosmos.PartitionKey(id)))
                    {
                        Assert.IsTrue(createResponse.IsSuccessStatusCode);
                    }
                }

                IReadOnlyList<FeedRange> feedTokens = await container.GetFeedRangesAsync();

                Assert.IsTrue(feedTokens.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                List<Task<List<string>>> tasks = feedTokens.Select(async feedToken =>
                {
                    List<string> results = new List<string>();
                    FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>(queryDefinition: new QueryDefinition("select * from T where STARTSWITH(T.id, \"BasicItem\")"), feedRange: feedToken, requestOptions: new QueryRequestOptions() { MaxItemCount = 10 });
                    string continuation = null;
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync();
                        foreach (ToDoActivity toDoActivity in response)
                        {
                            results.Add(toDoActivity.id);
                        }

                        continuation = response.ContinuationToken;
                        break;
                    }

                    feedIterator = container.GetItemQueryIterator<ToDoActivity>(queryDefinition: new QueryDefinition("select * from T where STARTSWITH(T.id, \"BasicItem\")"), feedRange: feedToken, continuationToken: continuation, requestOptions: new QueryRequestOptions() { MaxItemCount = 10 });
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync();
                        foreach (ToDoActivity toDoActivity in response)
                        {
                            results.Add(toDoActivity.id);
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