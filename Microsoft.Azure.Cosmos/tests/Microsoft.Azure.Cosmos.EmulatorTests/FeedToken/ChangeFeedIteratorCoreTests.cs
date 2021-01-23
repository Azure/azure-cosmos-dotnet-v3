//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.EmulatorTests.FeedRanges
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;

    [SDK.EmulatorTests.TestClass]
    public class ChangeFeedIteratorCoreTests : BaseCosmosClientHelper
    {
        private static readonly string PartitionKey = "/status";

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

        private async Task<ContainerInternal> InitializeLargeContainerAsync()
        {
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: ChangeFeedIteratorCoreTests.PartitionKey),
                throughput: 20000,
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        private async Task<ContainerInternal> InitializeContainerAsync()
        {
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: ChangeFeedIteratorCoreTests.PartitionKey),
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        /// <summary>
        /// Test to verify that StartFromBeginning works as expected by inserting 25 items, reading them all, then taking the last continuationtoken, 
        /// inserting another 25, and verifying that the iterator continues from the saved token and reads the second 25 for a total of 50 documents.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_ReadAll()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            ContainerInternal itemsCore = await this.InitializeLargeContainerAsync();
            await this.CreateRandomItems(itemsCore, batchSize, randomPartitionKey: true);
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        continuation = responseMessage.ContinuationToken;
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    totalCount += response.Count;
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            await this.CreateRandomItems(itemsCore, batchSize, randomPartitionKey: true);
            ChangeFeedIteratorCore setIteratorNew = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.ContinuationToken(continuation),
                ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;

            while (setIteratorNew.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await setIteratorNew.ReadNextAsync(this.cancellationToken))
                {
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    totalCount += response.Count;
                }
            }

            Assert.AreEqual(expectedFinalCount, totalCount);
        }

        /// <summary>
        /// Test to verify that StarTime works as expected by inserting 50 items in two batches of 25 but capturing the time of just the second batch.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_StartTime()
        {
            int totalCount = 0;
            int batchSize = 25;
            ContainerInternal itemsCore = await this.InitializeContainerAsync();
            await this.CreateRandomItems(itemsCore, batchSize, randomPartitionKey: true);
            await Task.Delay(1000);
            DateTime now = DateTime.UtcNow;
            await Task.Delay(1000);
            await this.CreateRandomItems(itemsCore, batchSize, randomPartitionKey: true);
            
            FeedIterator feedIterator = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Time(now),
                ChangeFeedMode.Incremental);
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    totalCount += response.Count;
                }
            }

            Assert.AreEqual(totalCount, batchSize);
        }

        /// <summary>
        /// Verify that we can read the Change Feed for a Partition Key and that does not read other items.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_PartitionKey_ReadAll()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            string pkToRead = "pkToRead";
            string otherPK = "otherPK";

            ContainerInternal itemsCore = await this.InitializeContainerAsync();
            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(this.CreateRandomToDoActivity(otherPK));
            }

            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(
                    FeedRange.FromPartitionKey(
                        new PartitionKey(pkToRead))),
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions()
                {
                    PageSizeHint = 1,
                }) as ChangeFeedIteratorCore;
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        continuation = responseMessage.ContinuationToken;
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    totalCount += response.Count;
                    foreach (ToDoActivity toDoActivity in response)
                    {
                        Assert.AreEqual(pkToRead, toDoActivity.status);
                    }
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead));
            }

            ChangeFeedIteratorCore setIteratorNew = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.ContinuationToken(continuation),
                ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;

            while (setIteratorNew.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await setIteratorNew.ReadNextAsync(this.cancellationToken))
                {
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    totalCount += response.Count;
                    foreach (ToDoActivity toDoActivity in response)
                    {
                        Assert.AreEqual(pkToRead, toDoActivity.status);
                    }
                }
            }

            Assert.AreEqual(expectedFinalCount, totalCount);
        }

        /// <summary>
        /// Verify that we can read the Change Feed for a Partition Key and that does not read other items.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_PartitionKey_OfT_ReadAll()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            string pkToRead = "pkToRead";
            string otherPK = "otherPK";

            ContainerInternal itemsCore = await this.InitializeContainerAsync();
            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(this.CreateRandomToDoActivity(otherPK));
            }

            FeedIterator<ToDoActivity> feedIterator = itemsCore.GetChangeFeedIterator<ToDoActivity>(
                ChangeFeedStartFrom.Beginning(
                    new FeedRangePartitionKey(
                        new PartitionKey(pkToRead))),
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions()
                {
                    PageSizeHint = 1,
                });
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                try
                {
                    FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                    totalCount += feedResponse.Count;
                    foreach (ToDoActivity toDoActivity in feedResponse)
                    {
                        Assert.AreEqual(pkToRead, toDoActivity.status);
                    }

                    continuation = feedResponse.ContinuationToken;
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotModified)
                {
                    continuation = cosmosException.Headers.ContinuationToken;
                    break;
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead));
            }

            FeedIterator<ToDoActivity> setIteratorNew = itemsCore.GetChangeFeedIterator<ToDoActivity>(
                ChangeFeedStartFrom.ContinuationToken(continuation),
                ChangeFeedMode.Incremental);

            while (setIteratorNew.HasMoreResults)
            {
                try
                {
                    FeedResponse<ToDoActivity> feedResponse = await setIteratorNew.ReadNextAsync(this.cancellationToken);
                    totalCount += feedResponse.Count;
                    foreach (ToDoActivity toDoActivity in feedResponse)
                    {
                        Assert.AreEqual(pkToRead, toDoActivity.status);
                    }
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            Assert.AreEqual(expectedFinalCount, totalCount);
        }

        /// <summary>
        /// Test to verify that StartFromBeginning works as expected by inserting 25 items, reading them all, then taking the last continuationtoken, 
        /// inserting another 25, and verifying that the iterator continues from the saved token and reads the second 25 for a total of 50 documents.
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_OfT_ReadAll()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            ContainerInternal itemsCore = await this.InitializeContainerAsync();
            await this.CreateRandomItems(itemsCore, batchSize, randomPartitionKey: true);
            
            FeedIterator<ToDoActivity> feedIterator = itemsCore.GetChangeFeedIterator<ToDoActivity>(ChangeFeedStartFrom.Beginning(), ChangeFeedMode.Incremental);
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                try
                {
                    FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                    totalCount += feedResponse.Count;
                    continuation = feedResponse.ContinuationToken;
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotModified)
                {
                    continuation = cosmosException.Headers.ContinuationToken;
                    break;
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            await this.CreateRandomItems(itemsCore, batchSize, randomPartitionKey: true);
            FeedIterator<ToDoActivity> setIteratorNew = itemsCore.GetChangeFeedIterator<ToDoActivity>(ChangeFeedStartFrom.ContinuationToken(continuation), ChangeFeedMode.Incremental);

            while (setIteratorNew.HasMoreResults)
            {
                try
                {
                    FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                    totalCount += feedResponse.Count;
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            Assert.AreEqual(expectedFinalCount, totalCount);
        }

        /// <summary>
        /// Test to verify that if we start with an empty collection and we insert items after the first empty iterations, they get picked up in other iterations.
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_EmptyBeginning()
        {
            int totalCount = 0;
            int expectedDocuments = 5;
            bool createdDocuments = false;

            ContainerInternal itemsCore = await this.InitializeContainerAsync();
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;

            while (feedIterator.HasMoreResults
                || (createdDocuments && totalCount == 0))
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                    else
                    {
                        if (!createdDocuments)
                        {
                            await this.CreateRandomItems(itemsCore, expectedDocuments, randomPartitionKey: true);
                            createdDocuments = true;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

            }

            Assert.AreEqual(expectedDocuments, totalCount);
        }

        /// <summary>
        /// Test that verifies that MaxItemCount is honored by checking the count of documents in the responses.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_WithMaxItemCount()
        {
            ContainerInternal itemsCore = await this.InitializeContainerAsync();
            await this.CreateRandomItems(itemsCore, 2, randomPartitionKey: true);
            
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental,
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    PageSizeHint = 1,
                }) as ChangeFeedIteratorCore;

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    if (response.Count > 0)
                    {
                        Assert.AreEqual(1, response.Count);
                        return;
                    }
                }

            }

            Assert.Fail("Found no batch with size 1");
        }

        /// <summary>
        /// Test that does not use FetchNextSetAsync but creates new iterators passing along the previous one's FeedToken.
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_NoFetchNext()
        {
            ContainerInternal itemsCore = await this.InitializeLargeContainerAsync();
            int pkRangesCount = (await itemsCore.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(itemsCore.LinkUri)).Count;

            int expected = 25;
            int iterations = 0;
            await this.CreateRandomItems(itemsCore, expected, randomPartitionKey: true);
            
            string continuation = null;
            int count = 0;
            while (true)
            {
                ChangeFeedStartFrom startFrom;
                if (continuation == null)
                {
                    startFrom = ChangeFeedStartFrom.Beginning();
                }
                else
                {
                    startFrom = ChangeFeedStartFrom.ContinuationToken(continuation);
                }

                ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(startFrom, ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;
                using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
                    }
                    else
                    {
                        if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                        {
                            Assert.Fail(responseMessage.ErrorMessage);
                        }
                    }

                    continuation = responseMessage.ContinuationToken;
                }

                if (count.Equals(expected))
                {
                    break;
                }

                if (iterations++ > pkRangesCount)
                {
                    Assert.Fail("" +
                        "Feed does not contain all elements even after looping through PK ranges. " +
                        "Either the continuation is not moving forward or there is some state problem.");
                }
            }
        }

        /// <summary>
        /// Test that verifies that we do breath first while moving across partitions
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_BreathFirst()
        {
            int expected = 500;
            CosmosObject previousToken = null;
            ContainerInternal itemsCore = await this.InitializeLargeContainerAsync();
            await this.CreateRandomItems(itemsCore, expected, randomPartitionKey: true);
            
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions()
                {
                    PageSizeHint = 1,
                }) as ChangeFeedIteratorCore;
            while (true)
            {
                using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    CosmosObject cosmosObject = CosmosObject.Parse(responseMessage.ContinuationToken);
                    if (!cosmosObject.TryGetValue("Continuation", out CosmosArray cosmosArray))
                    {
                        Assert.Fail();
                        throw new Exception();
                    }

                    CosmosObject currentToken = (CosmosObject)cosmosArray[0];

                    if (previousToken != null)
                    {
                        // Verify that the token, even though it yielded results, it moved to a new range
                        Assert.AreNotEqual(previousToken, currentToken);
                        break;
                    }

                    previousToken = currentToken;

                    if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                    {
                        break;
                    }
                }
            }
        }

        [TestMethod]
        public async Task GetFeedRangesAsync_MatchesPkRanges()
        {
            ContainerInternal itemsCore = await this.InitializeLargeContainerAsync();
            int pkRangesCount = (await itemsCore.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(itemsCore.LinkUri)).Count;
            
            IEnumerable<FeedRange> tokens = await itemsCore.GetFeedRangesAsync();
            Assert.AreEqual(pkRangesCount, tokens.Count());
        }

        [TestMethod]
        public async Task GetFeedRangesAsync_AllowsParallelProcessing()
        {
            ContainerInternal itemsCore = await this.InitializeLargeContainerAsync();
            int pkRangesCount = (await itemsCore.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(itemsCore.LinkUri)).Count;
            
            IEnumerable<FeedRange> tokens = await itemsCore.GetFeedRangesAsync();
            Assert.IsTrue(pkRangesCount > 1, "Should have created a multi partition container.");
            Assert.AreEqual(pkRangesCount, tokens.Count());
            int totalDocuments = 200;
            await this.CreateRandomItems(itemsCore, totalDocuments, randomPartitionKey: true);
            List<Task<int>> tasks = tokens.Select(token => Task.Run(async () =>
            {
                int count = 0;
                ChangeFeedIteratorCore iteratorForToken =
                    itemsCore.GetChangeFeedStreamIterator(
                        ChangeFeedStartFrom.Beginning(token),
                        ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;
                while (true)
                {
                    using (ResponseMessage responseMessage =
                    await iteratorForToken.ReadNextAsync(this.cancellationToken))
                    {
                        if (!responseMessage.IsSuccessStatusCode)
                        {
                            break;
                        }

                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
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

            Assert.AreEqual(totalDocuments, documentsRead);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_CannotMixTokensFromOtherContainers()
        {
            ContainerInternal oneContainer = await this.InitializeContainerAsync();
            ContainerInternal otherContainer = await this.InitializeContainerAsync();
            IReadOnlyList<FeedRange> tokens = await oneContainer.GetFeedRangesAsync();
            FeedIterator iterator = oneContainer.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.Beginning(tokens[0]),
                ChangeFeedMode.Incremental);
            ResponseMessage responseMessage = await iterator.ReadNextAsync();
            iterator = otherContainer.GetChangeFeedStreamIterator(
                ChangeFeedStartFrom.ContinuationToken(responseMessage.ContinuationToken),
                ChangeFeedMode.Incremental);
            responseMessage = await iterator.ReadNextAsync();
            Assert.IsNotNull(responseMessage.CosmosException);
            Assert.AreEqual(HttpStatusCode.BadRequest, responseMessage.StatusCode);
        }

        /// <summary>
        /// This test validates Full Fidelity Change Feed by inserting and deleting documents and verifying all operations are present
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_WithFullFidelity()
        {
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: ChangeFeedIteratorCoreTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);

            ContainerInternal container = (ContainerInternal)response;
            // FF does not work with StartFromBeginning currently, so we capture an initial continuation.
            FeedIterator<ToDoActivityWithMetadata> fullFidelityIterator = container.GetChangeFeedIterator<ToDoActivityWithMetadata>(
                ChangeFeedStartFrom.Now(),
                ChangeFeedMode.FullFidelity);
            string initialContinuation = null;
            while (fullFidelityIterator.HasMoreResults)
            {
                try
                {
                    FeedResponse<ToDoActivityWithMetadata> feedResponse = await fullFidelityIterator.ReadNextAsync(this.cancellationToken);
                    initialContinuation = feedResponse.ContinuationToken;
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotModified)
                {
                    initialContinuation = cosmosException.Headers.ContinuationToken;
                    break;
                }
            }

            // Insert documents and then delete them
            int totalDocuments = 50;
            IList<ToDoActivity> createdItems = await this.CreateRandomItems(container, totalDocuments, randomPartitionKey: true);
            foreach (ToDoActivity item in createdItems)
            {
                await container.DeleteItemAsync<ToDoActivity>(item.id, new PartitionKey(item.status));
            }

            // Resume Change Feed and verify we pickup all the events
            fullFidelityIterator = container.GetChangeFeedIterator<ToDoActivityWithMetadata>(
                ChangeFeedStartFrom.ContinuationToken(initialContinuation),
                ChangeFeedMode.FullFidelity);
            int detectedEvents = 0;
            bool hasInserts = false;
            bool hasDeletes = false;
            while (fullFidelityIterator.HasMoreResults)
            {
                try
                {
                    FeedResponse<ToDoActivityWithMetadata> feedResponse = await fullFidelityIterator.ReadNextAsync(this.cancellationToken);
                    foreach (ToDoActivityWithMetadata item in feedResponse)
                    {
                        Assert.IsNotNull(item.metadata, "Metadata not present");
                        Assert.IsNotNull(item.metadata.operationType, "Metadata has no operationType");
                        hasInserts |= item.metadata.operationType == "create";
                        hasDeletes |= item.metadata.operationType == "delete";
                    }

                    detectedEvents += feedResponse.Count;
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            Assert.AreEqual(2 * totalDocuments, detectedEvents, "Full Fidelity should include inserts and delete events.");
            Assert.IsTrue(hasInserts, "No metadata for create operationType found");
            Assert.IsTrue(hasDeletes, "No metadata for delete operationType found");
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

        public class ToDoActivityWithMetadata : ToDoActivity
        {
            [JsonProperty("_metadata")]
            public ToDoActivityMetadata metadata { get; set; }
        }

        public class ToDoActivityMetadata
        {
            [JsonProperty("operationType")]
            public string operationType { get; set; }
        }
    }
}