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
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [SDK.EmulatorTests.TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedIteratorCoreTests : BaseCosmosClientHelper
    {
        private static readonly string PartitionKey = "/pk";

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

        private async Task<ContainerInternal> InitializeFFCFContainerAsync(TimeSpan timeToLive)
        {
            ContainerProperties containerProperties = new(id: Guid.NewGuid().ToString(), partitionKeyPath: @"/id")
            {
                DefaultTimeToLive = Convert.ToInt32(timeToLive.TotalSeconds),
            };

            containerProperties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);

            ContainerResponse response = await this.database.CreateContainerAsync(
                containerProperties,
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
                await itemsCore.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: pkToRead));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: otherPK));
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
                        Assert.AreEqual(pkToRead, toDoActivity.pk);
                    }
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: pkToRead));
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
                        Assert.AreEqual(pkToRead, toDoActivity.pk);
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
                await itemsCore.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: pkToRead));
            }

            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: otherPK));
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
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;
                foreach (ToDoActivity toDoActivity in feedResponse)
                {
                    Assert.AreEqual(pkToRead, toDoActivity.pk);
                }

                continuation = feedResponse.ContinuationToken;

                if (feedResponse.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            for (int i = 0; i < batchSize; i++)
            {
                await itemsCore.CreateItemAsync(ToDoActivity.CreateRandomToDoActivity(pk: pkToRead));
            }

            FeedIterator<ToDoActivity> setIteratorNew = itemsCore.GetChangeFeedIterator<ToDoActivity>(
                ChangeFeedStartFrom.ContinuationToken(continuation),
                ChangeFeedMode.Incremental);

            while (setIteratorNew.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await setIteratorNew.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;
                foreach (ToDoActivity toDoActivity in feedResponse)
                {
                    Assert.AreEqual(pkToRead, toDoActivity.pk);
                }

                if (feedResponse.StatusCode == HttpStatusCode.NotModified)
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
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;
                continuation = feedResponse.ContinuationToken;

                if (feedResponse.StatusCode == HttpStatusCode.NotModified)
                {
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
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;

                if (feedResponse.StatusCode == HttpStatusCode.NotModified)
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
        /// This test validates Incremental Change Feed by inserting and deleting documents and verifying nothing reported
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_DeleteAfterCreate()
        {
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: ChangeFeedIteratorCoreTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            // Insert documents and then delete them
            int totalDocuments = 50;
            IList<ToDoActivity> createdItems = await this.CreateRandomItems(container, totalDocuments, randomPartitionKey: true);
            foreach (ToDoActivity item in createdItems)
            {
                await container.DeleteItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk));
            }
            FeedIterator<ToDoActivityWithMetadata> changefeedIterator = container.GetChangeFeedIterator<ToDoActivityWithMetadata>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);
            while (changefeedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivityWithMetadata> feedResponse = await changefeedIterator.ReadNextAsync(this.cancellationToken);
                Assert.AreEqual(HttpStatusCode.NotModified, feedResponse.StatusCode, "Incremental Change Feed does not present intermediate results and should return nothing.");
                if (feedResponse.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }
            }
        }

        [TestMethod]
        public async Task TestCancellationTokenAsync()
        {
            CancellationTokenRequestHandler cancellationTokenHandler = new CancellationTokenRequestHandler();

            ContainerInternal itemsCore = await this.InitializeContainerAsync();
            await this.CreateRandomItems(itemsCore, 100, randomPartitionKey: true);

            // Inject validating handler
            RequestHandler currentInnerHandler = this.cosmosClient.RequestHandler.InnerHandler;
            this.cosmosClient.RequestHandler.InnerHandler = cancellationTokenHandler;
            cancellationTokenHandler.InnerHandler = currentInnerHandler;

            {
                // Test to see if the token flows to the pipeline
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.Beginning(),
                    ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;
                await feedIterator.ReadNextAsync(cancellationTokenSource.Token);
                Assert.AreEqual(cancellationTokenSource.Token, cancellationTokenHandler.LastUsedToken, "The token passed did not reach the pipeline");
            }

            // See if cancellation token is honored for first request
            try
            {
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.Cancel();
                ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.Beginning(),
                    ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;
                await feedIterator.ReadNextAsync(cancellationTokenSource.Token);

                Assert.Fail("Expected exception.");
            }
            catch (OperationCanceledException)
            {
            }

            // See if cancellation token is honored for second request
            try
            {
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.Cancel();
                ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.Beginning(),
                    ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;
                await feedIterator.ReadNextAsync();
                await feedIterator.ReadNextAsync(cancellationTokenSource.Token);
                Assert.Fail("Expected exception.");
            }
            catch (OperationCanceledException)
            {
            }

            // See if cancellation token is honored mid draining
            try
            {
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.Beginning(),
                    ChangeFeedMode.Incremental) as ChangeFeedIteratorCore;
                await feedIterator.ReadNextAsync(cancellationTokenSource.Token);
                cancellationTokenSource.Cancel();
                await feedIterator.ReadNextAsync(cancellationTokenSource.Token);
                Assert.Fail("Expected exception.");
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// This test validates error with Full Fidelity Change Feed and start from beginning.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_WithFullFidelityReadFromBeginning()
        {
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: ChangeFeedIteratorCoreTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            int totalDocuments = 10;
            await this.CreateRandomItems(container, totalDocuments, randomPartitionKey: true);

            // FF does not work with StartFromBeginning currently, capture error
            FeedIterator<ToDoActivityWithMetadata> fullFidelityIterator = container.GetChangeFeedIterator<ToDoActivityWithMetadata>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.FullFidelity);

            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => fullFidelityIterator.ReadNextAsync());
            Assert.AreEqual(HttpStatusCode.BadRequest, cosmosException.StatusCode, "Full Fidelity Change Feed does not work with StartFromBeginning currently.");
            Assert.IsTrue(cosmosException.Message.Contains("FullFidelity Change Feed must have valid If-None-Match header."));
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRange.FromPartitionKey.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_FeedRange_FromPartitionKey_VerifyingWireFormatTests()
        {
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            ContainerInternal container = await this.InitializeFFCFContainerAsync(ttl);
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();

            PartitionKey partitionKey = new PartitionKey(id);
            ChangeFeedMode changeFeedMode = ChangeFeedMode.FullFidelity;
            ChangeFeedStartFrom changeFeedStartFrom = ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(partitionKey));

            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: changeFeedStartFrom,
                changeFeedMode: changeFeedMode))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);

                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
                        IEnumerable<ChangeFeedItemChanges<Item>> itemChanges = feedResponse.Resource;

                        ChangeFeedIteratorCoreTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 2, actual: itemChanges.Count());

                        foreach(ChangeFeedItemChanges<Item> item in itemChanges)
                        {
                            Item current = item.Current;
                            Item previous = item.Previous;
                            ChangeFeedMetadata metadata = item.Metadata;
                        }

                        ChangeFeedItemChanges<Item> createOperation = itemChanges.ElementAtOrDefault(0);

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.IsNotNull(createOperation.Metadata);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

                        ChangeFeedItemChanges<Item> replaceOperation = itemChanges.ElementAtOrDefault(1);

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.IsNotNull(createOperation.Metadata);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using ChangeFeedStartFrom.Now(ranges[0]).
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_FeedRange_VerifyingWireFormatTests()
        {
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            ContainerInternal container = await this.InitializeFFCFContainerAsync(ttl);
            IReadOnlyList<FeedRange> ranges = await container.GetFeedRangesAsync();
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();

            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(ranges[0]),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
                        List<ChangeFeedItemChanges<Item>> resources = feedResponse.Resource.ToList();

                        ChangeFeedIteratorCoreTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 4, actual: resources.Count);

                        ChangeFeedItemChanges<Item> firstCreateOperation = resources[0];

                        Assert.AreEqual(expected: otherId, actual: firstCreateOperation.Current.Id);
                        Assert.AreEqual(expected: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", actual: firstCreateOperation.Current.Line1);
                        Assert.AreEqual(expected: "Bangkok", actual: firstCreateOperation.Current.City);
                        Assert.AreEqual(expected: "Thailand", actual: firstCreateOperation.Current.State);
                        Assert.AreEqual(expected: "10330", actual: firstCreateOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: firstCreateOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: firstCreateOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsFalse(firstCreateOperation.Metadata.TimeToLiveExpired);

                        ChangeFeedItemChanges<Item> createOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

                        ChangeFeedItemChanges<Item> replaceOperation = resources[2];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

                        ChangeFeedItemChanges<Item> deleteOperation = resources[3];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNotNull(deleteOperation.Previous);
                        Assert.AreEqual(expected: id, actual: deleteOperation.Previous.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: deleteOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: deleteOperation.Previous.City);
                        Assert.AreEqual(expected: "GA", actual: deleteOperation.Previous.State);
                        Assert.AreEqual(expected: "30363", actual: deleteOperation.Previous.ZipCode);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRange.FromPartitionKey.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_FeedRange_FromPartitionKey_Dynamic_VerifyingWireFormatTests()
        {
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            ContainerInternal container = await this.InitializeFFCFContainerAsync(ttl);
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<dynamic> feedIterator = container.GetChangeFeedIterator<dynamic>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(new PartitionKey(id))),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<dynamic>(item: new { id = otherId, line1 = "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", city = "Bangkok", state = "Thailand", zipCode = "10330" }, partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<dynamic>(item: new { id, line1 = "One Microsoft Way", city = "Redmond", state = "WA", zipCode = "98052" }, partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<dynamic>(item: new { id, line1 = "205 16th St NW", city = "Atlanta", state = "GA", zipCode = "30363" }, partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
                        List<ChangeFeedItemChanges<Item>> itemChanges = JsonConvert.DeserializeObject<List<ChangeFeedItemChanges<Item>>>(
                            JsonConvert.SerializeObject(feedResponse.Resource));

                        ChangeFeedIteratorCoreTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 2, actual: itemChanges.Count);

                        ChangeFeedItemChanges<Item> createOperation = itemChanges[0];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.IsNotNull(createOperation.Metadata);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

                        ChangeFeedItemChanges<Item> replaceOperation = itemChanges[1];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.IsNotNull(createOperation.Metadata);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

                        break;
                    }
                }
            }
        }

        private static void AssertGatewayMode<T>(FeedResponse<T> feedResponse)
        {
            string diagnostics = feedResponse.Diagnostics.ToString();
            JToken jsonToken = JToken.Parse(diagnostics);

            Assert.IsNotNull(jsonToken["Summary"]["GatewayCalls"], "'GatewayCalls' is not found in diagnostics. UseGateMode is set to false.");
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

        private class CancellationTokenRequestHandler : RequestHandler
        {
            public CancellationToken LastUsedToken { get; private set; }

            public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                this.LastUsedToken = cancellationToken;
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
    
    public record Item(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("line1")] string Line1,
        [property: JsonProperty("city")] string City,
        [property: JsonProperty("zipCode")] string ZipCode,
        [property: JsonProperty("state")] string State);
}