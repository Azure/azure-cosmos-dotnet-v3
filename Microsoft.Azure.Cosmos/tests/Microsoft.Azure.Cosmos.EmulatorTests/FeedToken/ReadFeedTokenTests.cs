//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.FeedRanges
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [SDK.EmulatorTests.TestClass]
    [TestCategory("ReadFeed")]
    public class ReadFeedRangeTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;
        private ContainerInternal LargerContainer = null;

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

            ContainerResponse largerContainer = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 15000,
                cancellationToken: this.cancellationToken);

            this.Container = (ContainerInlineCore)response;
            this.LargerContainer = (ContainerInlineCore)largerContainer;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        [Ignore]
        public async Task ReadFeedIteratorCore_AllowsParallelProcessing()
        {
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            IReadOnlyList<FeedRange> tokens = await itemsCore.GetFeedRangesAsync();

            List<Task<int>> tasks = tokens.Select(token => Task.Run(async () =>
            {
                int count = 0;
                FeedIterator feedIterator = itemsCore.GetItemQueryStreamIterator(
                    queryDefinition: null,
                    feedRange: token,
                    continuationToken: null,
                    requestOptions: null);
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage responseMessage =
                        await feedIterator.ReadNextAsync(this.cancellationToken))
                    {
                        if (responseMessage.IsSuccessStatusCode)
                        {
                            Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                            count += response.Count;
                        }
                    }
                }

                return count;

            })).ToList();

            await Task.WhenAll(tasks);

            int documentsRead = 0;
            foreach (Task<int> task in tasks)
            {
                documentsRead += task.Result;
            }

            Assert.AreEqual(batchSize, documentsRead);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadAll()
        {
            int totalCount = 0;
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            FeedIterator feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 } );
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                }
            }

            Assert.AreEqual(batchSize, totalCount);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadAll_StopResume()
        {
            int totalCount = 0;
            int batchSize = 50;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            FeedIterator feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }

                    continuation = responseMessage.ContinuationToken;
                    break;
                }
            }

            feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, continuationToken: continuation, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                }
            }

            Assert.AreEqual(batchSize, totalCount);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_OfT_ReadAll_StopResume()
        {
            int totalCount = 0;
            int batchSize = 50;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            FeedIterator<ToDoActivity> feedIterator = itemsCore.GetItemQueryIterator<ToDoActivity>(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                totalCount += response.Count;
                continuation = response.ContinuationToken;
                break;
            }

            feedIterator = itemsCore.GetItemQueryIterator<ToDoActivity>(queryDefinition: null, continuationToken: continuation, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                totalCount += response.Count;
            }

            Assert.AreEqual(batchSize, totalCount);
        }

        [TestMethod]
        [Ignore]
        public async Task ReadFeedIteratorCore_OfT_WithFeedRange_ReadAll_StopResume()
        {
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            IReadOnlyList<FeedRange> tokens = await itemsCore.GetFeedRangesAsync();

            List<Task<int>> tasks = tokens.Select(token => Task.Run(async () =>
            {
                int count = 0;
                FeedIterator<ToDoActivity> feedIterator = itemsCore.GetItemQueryIterator<ToDoActivity>(queryDefinition: null, feedRange: token);
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                    count += response.Count;
                    continuation = response.ContinuationToken;
                    break;
                }

                feedIterator = itemsCore.GetItemQueryIterator<ToDoActivity>(queryDefinition: null, continuationToken: continuation);
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                    count += response.Count;
                }

                return count;

            })).ToList();

            await Task.WhenAll(tasks);

            int documentsRead = 0;
            foreach (Task<int> task in tasks)
            {
                documentsRead += task.Result;
            }

            Assert.AreEqual(batchSize, documentsRead);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_MigrateFromOlder()
        {
            int totalCount = 0;
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            FeedIterator feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            ResponseMessage firstResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
            feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, continuationToken: firstResponse.Headers.ContinuationToken, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                }
            }

            Assert.AreEqual(batchSize - 1 /* from first request */, totalCount);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadOnlyPartitionKey()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            string pkToRead = "pkToRead";
            string otherPK = "otherPK";

            for (int i = 0; i < batchSize; i++)
            {
                await this.LargerContainer.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: pkToRead));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await this.LargerContainer.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: otherPK));
            }

            ContainerInternal itemsCore = this.LargerContainer;
            FeedIterator feedIterator = itemsCore.GetItemQueryStreamIterator(
                requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey(pkToRead), MaxItemCount = 1 });
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                        foreach (ToDoActivity toDoActivity in response)
                        {
                            Assert.AreEqual(pkToRead, toDoActivity.pk);
                        }
                    }
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            string continuationToken = null;
            totalCount = 0;
            do
            {
                feedIterator = itemsCore.GetItemQueryStreamIterator(
                    requestOptions: new QueryRequestOptions() 
                    { 
                        PartitionKey = new PartitionKey(pkToRead),
                        MaxItemCount = 1,
                    },
                    continuationToken: continuationToken);

                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    responseMessage.EnsureSuccessStatusCode();

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    totalCount += response.Count;
                    foreach (ToDoActivity toDoActivity in response)
                    {
                        Assert.AreEqual(pkToRead, toDoActivity.pk);
                    }

                    continuationToken = responseMessage.ContinuationToken;
                }
            }
            while (continuationToken != null);

            Assert.AreEqual(firstRunTotal, totalCount);
        }

        /// <summary>
        /// Check to see how the older continuation token approach works when mixed with FeedRange
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadAll_MixContinuationToken()
        {
            int totalCount = 0;
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;

            // Do a read without FeedRange and get the older CT from Header
            string olderContinuationToken = null;
            FeedIterator feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    olderContinuationToken = responseMessage.Headers.ContinuationToken;
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                    break;
                }
            }

            // start a new iterator using the older CT and expect it to continue
            feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, continuationToken: olderContinuationToken, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                }
            }

            Assert.AreEqual(batchSize, totalCount);
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        public async Task ReadFeedIteratorCore_CrossPartitionBiDirectional(bool useStatelessIteration)
        {
            ContainerInternal container = null;

            try
            {
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/id"),
                    throughput: 30000,
                    cancellationToken: this.cancellationToken);
                container = (ContainerInlineCore)containerResponse;

                // Create Items
                const int total = 30;
                QueryRequestOptions requestOptions = new QueryRequestOptions()
                {
                    MaxItemCount = 10
                };

                List<string> items = new List<string>();

                for (int i = 0; i < total; i++)
                {
                    string item = $@"
                    {{    
                        ""id"": ""{i}""
                    }}";

                    using (ResponseMessage createResponse = await container.CreateItemStreamAsync(
                        ReadFeedRangeTests.GenerateStreamFromString(item),
                        new Cosmos.PartitionKey(i.ToString())))
                    {
                        Assert.IsTrue(createResponse.IsSuccessStatusCode);
                    }
                }

                string continuation = null;
                FeedIterator iter = container.GetItemQueryStreamIterator(
                    continuationToken: continuation,
                    requestOptions: requestOptions);

                int count = 0;
                List<string> forwardOrder = new List<string>();
                while (iter.HasMoreResults)
                {
                    if (useStatelessIteration)
                    {
                        iter = container.GetItemQueryStreamIterator(
                            continuationToken: continuation,
                            requestOptions: requestOptions);
                    }

                    using (ResponseMessage response = await iter.ReadNextAsync())
                    {
                        Assert.IsNotNull(response);

                        continuation = response.ContinuationToken;

                        using (StreamReader reader = new StreamReader(response.Content))
                        {
                            string json = await reader.ReadToEndAsync();
                            JArray documents = (JArray)JObject.Parse(json).SelectToken("Documents");
                            count += documents.Count;
                            if (documents.Any())
                            {
                                forwardOrder.Add(documents.First().SelectToken("id").ToString());
                            }
                        }
                    }
                }

                Assert.IsNotNull(forwardOrder);
                Assert.AreEqual(total, count);
                Assert.IsFalse(forwardOrder.Where(x => string.IsNullOrEmpty(x)).Any());

                requestOptions.Properties = requestOptions.Properties = new Dictionary<string, object>()
                {
                    { Documents.HttpConstants.HttpHeaders.EnumerationDirection, (byte)BinaryScanDirection.Reverse },
                };
                count = 0;
                List<string> reverseOrder = new List<string>();

                continuation = null;
                iter = container
                        .GetItemQueryStreamIterator(queryDefinition: null, continuationToken: continuation, requestOptions: requestOptions);
                while (iter.HasMoreResults)
                {
                    if (useStatelessIteration)
                    {
                        iter = container
                                .GetItemQueryStreamIterator(queryDefinition: null, continuationToken: continuation, requestOptions: requestOptions);
                    }

                    using (ResponseMessage response = await iter.ReadNextAsync())
                    {
                        continuation = response.ContinuationToken;

                        Assert.IsNotNull(response);
                        using (StreamReader reader = new StreamReader(response.Content))
                        {
                            string json = await reader.ReadToEndAsync();
                            JArray documents = (JArray)JObject.Parse(json).SelectToken("Documents");
                            count += documents.Count;
                            if (documents.Any())
                            {
                                reverseOrder.Add(documents.First().SelectToken("id").ToString());
                            }
                        }
                    }
                }

                Assert.IsNotNull(reverseOrder);

                Assert.AreEqual(total, count);
                forwardOrder.Reverse();

                CollectionAssert.AreEqual(forwardOrder, reverseOrder);
                Assert.IsFalse(reverseOrder.Where(x => string.IsNullOrEmpty(x)).Any());
            }
            finally
            {
                await container?.DeleteContainerStreamAsync();
            }
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_Trace()
        {
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            FeedIteratorInternal feedIterator = (FeedIteratorInternal)itemsCore.GetItemQueryStreamIterator(
                queryDefinition: null, 
                requestOptions: new QueryRequestOptions() { MaxItemCount = int.MaxValue });
            ITrace rootTrace;
            int childCount = 0;
            using (rootTrace = Trace.GetRootTrace("Cross Partition Read Feed"))
            {
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync(rootTrace, this.cancellationToken))
                    {
                        responseMessage.EnsureSuccessStatusCode();
                        childCount++;
                    }
                }
            }

            string trace = TraceWriter.TraceToText(rootTrace);

            Console.WriteLine(trace);

            Assert.AreEqual(childCount, rootTrace.Children.Count);
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


        private async Task<IList<ToDoActivity>> CreateRandomItems(ContainerInternal container, int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
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
                    ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity(pk: pk);

                    createdList.Add(temp);

                    await container.CreateItemAsync<ToDoActivity>(item: temp);
                }
            }

            return createdList;
        }

        // Copy of Friends
        public enum BinaryScanDirection : byte
        {
            Invalid = 0x00,
            Forward = 0x01,
            Reverse = 0x02,
        }
    }
}