//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class ReadFeedTokenTests : BaseCosmosClientHelper
    {
        private ContainerCore Container = null;
        private ContainerCore LargerContainer = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);

            ContainerResponse largerContainer = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 20000,
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
        public async Task ReadFeedIteratorCore_AllowsParallelProcessing()
        {
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerCore itemsCore = this.LargerContainer;
            IReadOnlyList<FeedToken> tokens = await itemsCore.GetFeedTokensAsync();

            List<Task<int>> tasks = tokens.Select(token => Task.Run(async () =>
            {
                int count = 0;
                FeedIteratorCore feedIterator = itemsCore.GetItemQueryStreamIterator(token) as FeedIteratorCore;
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage responseMessage =
                        await feedIterator.ReadNextAsync(this.cancellationToken))
                    {
                        Assert.IsNotNull(feedIterator.FeedToken);
                        Assert.IsTrue(feedIterator.TryGetContinuationToken(out string continuationToken));

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
            ContainerCore itemsCore = this.LargerContainer;
            FeedIteratorCore feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 } ) as FeedIteratorCore;
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    Assert.IsNotNull(feedIterator.FeedToken);
                    Assert.IsTrue(feedIterator.TryGetContinuationToken(out string continuationToken));
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
        public async Task ReadFeedIteratorCore_ReadOnlyPartitionKey()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            string pkToRead = "pkToRead";
            string otherPK = "otherPK";

            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(this.CreateRandomToDoActivity(otherPK));
            }

            ContainerCore itemsCore = this.Container;
            FeedIterator feedIterator = itemsCore.GetItemQueryStreamIterator(requestOptions: new QueryRequestOptions() { PartitionKey = new PartitionKey(pkToRead) });
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
                            Assert.AreEqual(pkToRead, toDoActivity.status);
                        }
                    }
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_PassingFeedToken_ReadAll()
        {
            int totalCount = 0;
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerCore itemsCore = this.LargerContainer;

            FeedIteratorCore initialFeedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }) as FeedIteratorCore;
            while (initialFeedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await initialFeedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                    break;
                }
            }

            // Use the previous iterators FeedToken to continue
            FeedIteratorCore feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, feedToken: initialFeedIterator.FeedToken) as FeedIteratorCore;
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    Assert.IsNotNull(feedIterator.FeedToken);
                    Assert.IsTrue(feedIterator.TryGetContinuationToken(out string continuationToken));
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                }
            }

            Assert.AreEqual(batchSize, totalCount);
        }

        /// <summary>
        /// Check to see how the older continuation token approach works when mixed with FeedToken
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadAll_MixContinuationToken()
        {
            int totalCount = 0;
            int batchSize = 1000;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerCore itemsCore = this.LargerContainer;

            // Do a read without FeedToken and get the older CT from Header
            string olderContinuationToken = null;
            FeedIteratorCore feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }) as FeedIteratorCore;
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
            feedIterator = itemsCore.GetItemQueryStreamIterator(queryDefinition: null, continuationToken: olderContinuationToken, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 }) as FeedIteratorCore;
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    Assert.IsNotNull(feedIterator.FeedToken);
                    Assert.IsTrue(feedIterator.TryGetContinuationToken(out string continuationToken));
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                }
            }

            Assert.AreEqual(batchSize, totalCount);
        }


        private async Task<IList<ToDoActivity>> CreateRandomItems(ContainerCore container, int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
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
                    ToDoActivity temp = this.CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await container.CreateItemAsync<ToDoActivity>(item: temp);
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
    }
}