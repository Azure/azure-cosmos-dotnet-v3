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
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosItemChangeFeedTests : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;
        private ContainerInternal LargerContainer = null;

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

        /// <summary>
        /// Test to verify that StartFromBeginning works as expected by inserting 25 items, reading them all, then taking the last continuationtoken, 
        /// inserting another 25, and verifying that the iterator continues from the saved token and reads the second 25 for a total of 50 documents.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator()
        {
            int totalCount = 0;
            string lastcontinuation = string.Empty;
            int firstRunTotal = 25;
            int batchSize = 25;

            int pkRangesCount = (await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri)).Count;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            ContainerCore itemsCore = (ContainerCore)this.Container;
            FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(
                ChangeFeedStartFrom.Beginning());

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.ContinuationToken;
                    Assert.AreEqual(responseMessage.ContinuationToken, responseMessage.Headers.ContinuationToken);
                    List<CompositeContinuationToken> deserializedToken = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(lastcontinuation);
                    Assert.AreEqual(pkRangesCount, deserializedToken.Count);
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    totalCount += response.Count;
                }

            }
            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last continuation token from the first cycle
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            FeedIterator setIteratorNew =
                itemsCore.GetStandByFeedIterator(
                    ChangeFeedStartFrom.ContinuationToken(lastcontinuation));

            while (setIteratorNew.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await setIteratorNew.ReadNextAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.ContinuationToken;
                    Assert.AreEqual(responseMessage.ContinuationToken, responseMessage.Headers.ContinuationToken);
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
        /// Test to verify that if we start with an empty collection and we insert items after the first empty iterations, they get picked up in other iterations.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator_EmptyBeginning()
        {
            int totalCount = 0;
            int expectedDocuments = 5;
            bool createdDocuments = false;

            ContainerInternal itemsCore = this.Container;
            FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(
                ChangeFeedStartFrom.Beginning());

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    Assert.AreEqual(responseMessage.ContinuationToken, responseMessage.Headers.ContinuationToken);
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
                    else
                    {
                        if (!createdDocuments)
                        {
                            await this.CreateRandomItems(this.Container, expectedDocuments, randomPartitionKey: true);
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
        /// Test that verifies that, if the token contains an invalid range, we throw.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public async Task StandByFeedIterator_WithInexistentRange()
        {
            // Add some random range, this will force the failure
            List<CompositeContinuationToken> corruptedTokens = new List<CompositeContinuationToken>
            {
                new CompositeContinuationToken()
                {
                    Range = new Documents.Routing.Range<string>("whatever", "random", true, false),
                    Token = "oops"
                }
            };

            string corruptedTokenSerialized = JsonConvert.SerializeObject(corruptedTokens);

            ContainerInternal itemsCore = this.Container;
            FeedIterator setIteratorNew =
                itemsCore.GetStandByFeedIterator(
                    ChangeFeedStartFrom.ContinuationToken(corruptedTokenSerialized));
            _ = await setIteratorNew.ReadNextAsync(this.cancellationToken);

            Assert.Fail("Should have thrown.");
        }

        /// <summary>
        /// Test that verifies that MaxItemCount is honored by checking the count of documents in the responses.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator_WithMaxItemCount()
        {
            await this.CreateRandomItems(this.Container, 2, randomPartitionKey: true);
            ContainerCore itemsCore = (ContainerCore)this.Container;
            FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(
                ChangeFeedStartFrom.Beginning(),
                requestOptions: new ChangeFeedRequestOptions()
                {
                    PageSizeHint = 1
                });

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    Assert.IsTrue(responseMessage.IsSuccessStatusCode, responseMessage.ErrorMessage);
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
        /// Test that does not use FetchNextSetAsync but creates new iterators passing along the previous one's continuationtoken.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator_NoFetchNext()
        {
            int pkRangesCount = (await this.Container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri)).Count;

            int expected = 25;
            int iterations = 0;
            await this.CreateRandomItems(this.Container, expected, randomPartitionKey: true);
            ContainerInternal itemsCore = this.Container;
            string continuationToken = null;
            int count = 0;
            while (true)
            {
                FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(
                    ChangeFeedStartFrom.Beginning());
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    continuationToken = responseMessage.Headers.ContinuationToken;
                    Assert.AreEqual(responseMessage.ContinuationToken, responseMessage.Headers.ContinuationToken);
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
                    }
                }

                if (count > expected)
                {
                    Assert.Fail($"{count} does not equal {expected}");
                }

                if (count.Equals(expected))
                {
                    break;
                }

                if (iterations++ > pkRangesCount)
                {
                    Assert.Fail("Feed does not contain all elements even after looping through PK ranges. Either the continuation is not moving forward or there is some state problem.");

                }
            }
        }

        /// <summary>
        /// Test that verifies that we do breath first while moving across partitions
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task StandByFeedIterator_BreathFirst()
        {
            int expected = 500;
            List<CompositeContinuationToken> previousToken = null;
            await this.CreateRandomItems(this.LargerContainer, expected, randomPartitionKey: true);
            ContainerCore itemsCore = (ContainerCore)this.LargerContainer;
            FeedIterator feedIterator = itemsCore.GetStandByFeedIterator(
                ChangeFeedStartFrom.Beginning());
            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    string continuationToken = responseMessage.Headers.ContinuationToken;
                    List<CompositeContinuationToken> deserializedToken = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(responseMessage.Headers.ContinuationToken);
                    if (previousToken != null)
                    {
                        // Verify that the token, even though it yielded results, it moved to a new range
                        Assert.AreNotEqual(previousToken[0].Range.Min, deserializedToken[0].Range.Min);
                        Assert.AreNotEqual(previousToken[0].Range.Max, deserializedToken[0].Range.Max);
                        break;
                    }

                    previousToken = deserializedToken;
                }
            }
        }

        /// <summary>
        /// Verifies that the internal delegate for PKRangeCache gets called with forceRefresh true after a split.
        /// </summary>
        [TestMethod]
        public async Task StandByFeedIterator_VerifyRefreshIsCalledOnSplit()
        {
            CosmosChangeFeedResultSetIteratorCoreMock iterator = new CosmosChangeFeedResultSetIteratorCoreMock(
                (ContainerCore)this.Container,
                ChangeFeedStartFrom.Beginning(),
                new ChangeFeedRequestOptions()
                {
                    PageSizeHint = 100,
                });
            using (ResponseMessage responseMessage =
                    await iterator.ReadNextAsync(this.cancellationToken))
            {
                Assert.IsTrue(iterator.HasCalledForceRefresh);
                Assert.IsTrue(iterator.Iteration > 1);
                Assert.AreEqual(responseMessage.StatusCode, System.Net.HttpStatusCode.NotModified);
            }
        }

        [TestMethod]
        public async Task GetChangeFeedTokensAsync_MatchesPkRanges()
        {
            int pkRangesCount = (await this.LargerContainer.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.LargerContainer.LinkUri)).Count;
            ContainerInternal itemsCore = this.LargerContainer;
            IEnumerable<string> tokens = await itemsCore.GetChangeFeedTokensAsync();
            Assert.AreEqual(pkRangesCount, tokens.Count());
        }

        [TestMethod]
        public async Task GetChangeFeedTokensAsync_AllowsParallelProcessing()
        {
            int pkRangesCount = (await this.LargerContainer.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.LargerContainer.LinkUri)).Count;
            ContainerInternal itemsCore = this.LargerContainer;
            IEnumerable<string> tokens = await itemsCore.GetChangeFeedTokensAsync();
            Assert.IsTrue(pkRangesCount > 1, "Should have created a multi partition container.");
            Assert.AreEqual(pkRangesCount, tokens.Count());
            int totalDocuments = 200;
            await this.CreateRandomItems(this.LargerContainer, totalDocuments, randomPartitionKey: true);
            List<Task<int>> tasks = tokens.Select(token => Task.Run(async () =>
            {
                int count = 0;
                FeedIterator iteratorForToken =
                    itemsCore.GetStandByFeedIterator(
                        ChangeFeedStartFrom.ContinuationToken(token));
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
        public async Task GetChangeFeedTokensAsync_DrainFromJustOnePartition()
        {
            int pkRangesCount = (await this.LargerContainer.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.LargerContainer.LinkUri)).Count;
            ContainerInternal itemsCore = this.LargerContainer;
            IEnumerable<string> tokens = await itemsCore.GetChangeFeedTokensAsync();
            Assert.IsTrue(pkRangesCount > 1, "Should have created a multi partition container.");
            Assert.AreEqual(pkRangesCount, tokens.Count());
            int totalDocuments = 200;
            await this.CreateRandomItems(this.LargerContainer, totalDocuments, randomPartitionKey: true);
            string token = tokens.First();

            int count = 0;
            FeedIterator iteratorForToken =
                itemsCore.GetStandByFeedIterator(
                    ChangeFeedStartFrom.ContinuationToken(token));
            while (iteratorForToken.HasMoreResults)
            {
                using (ResponseMessage responseMessage = await iteratorForToken.ReadNextAsync(this.cancellationToken))
                {
                    if (!responseMessage.IsSuccessStatusCode)
                    {
                        break;
                    }

                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    count += response.Count;
                }
            }

            Assert.IsTrue(count > 0 && count < totalDocuments);
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

        private class CosmosChangeFeedResultSetIteratorCoreMock : StandByFeedIteratorCore
        {
            public int Iteration = 0;
            public bool HasCalledForceRefresh = false;

            internal CosmosChangeFeedResultSetIteratorCoreMock(
                ContainerCore container,
                ChangeFeedStartFrom changeFeedStartFrom,
                ChangeFeedRequestOptions options) : base(
                    clientContext: container.ClientContext,
                    container: container,
                    changeFeedStartFrom: changeFeedStartFrom,
                    options: options)
            {
                List<CompositeContinuationToken> compositeContinuationTokens = new List<CompositeContinuationToken>()
                {
                    new CompositeContinuationToken()
                    {
                        Token = null,
                        Range = new Documents.Routing.Range<string>("A", "B", true, false)
                    }
                };

                string serialized = JsonConvert.SerializeObject(compositeContinuationTokens);

                this.compositeContinuationToken = StandByFeedContinuationToken.CreateAsync("containerRid", serialized, (string containerRid, Documents.Routing.Range<string> ranges, bool forceRefresh) =>
                {
                    IReadOnlyList<Documents.PartitionKeyRange> filteredRanges = new List<Documents.PartitionKeyRange>()
                    {
                        new Documents.PartitionKeyRange() { MinInclusive = "A", MaxExclusive ="B", Id = "0" }
                    };

                    if (forceRefresh)
                    {
                        this.HasCalledForceRefresh = true;
                    }

                    return Task.FromResult(filteredRanges);
                }).Result;
            }

            internal override Task<ResponseMessage> NextResultSetDelegateAsync(
                ChangeFeedRequestOptions options,
                CancellationToken cancellationToken)
            {
                if (this.Iteration++ == 0)
                {
                    ResponseMessage httpResponse = new ResponseMessage(System.Net.HttpStatusCode.Gone);
                    httpResponse.Headers.Add(Documents.WFConstants.BackendHeaders.SubStatus, ((uint)Documents.SubStatusCodes.PartitionKeyRangeGone).ToString(CultureInfo.InvariantCulture));

                    return Task.FromResult(httpResponse);
                }

                return Task.FromResult(new ResponseMessage(System.Net.HttpStatusCode.NotModified));
            }
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