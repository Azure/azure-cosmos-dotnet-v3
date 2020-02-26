//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
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

                IReadOnlyList<FeedToken> feedTokens = await container.GetFeedTokensAsync();

                Assert.IsTrue(feedTokens.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                // There should only be one range since we get 1 FeedToken per range
                foreach (FeedToken feedToken in feedTokens)
                {
                    List<PartitionKeyRange> partitionKeyRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                        queryClient: new CosmosQueryClientCore(container.ClientContext, container),
                        resourceLink: container.LinkUri.OriginalString,
                        partitionedQueryExecutionInfo: null,
                        containerQueryProperties: containerQueryProperties,
                        properties: null,
                        feedTokenInternal: feedToken as FeedTokenInternal);

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

                IReadOnlyList<FeedToken> feedTokens = await container.GetFeedTokensAsync();

                Assert.IsTrue(feedTokens.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                List<Task<List<string>>> tasks = feedTokens.Select(async feedToken =>
                {
                    List<string> results = new List<string>();
                    FeedIterator feedIterator = container.GetItemQueryStreamIterator(queryText: "select * from T where STARTSWITH(T.id, \"BasicItem\")", feedToken: feedToken);
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