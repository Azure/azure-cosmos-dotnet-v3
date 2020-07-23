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
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [SDK.EmulatorTests.TestClass]
    public class ChangeFeedIteratorCoreTests : BaseCosmosClientHelper
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
        public async Task ChangeFeedIteratorCore_ReadAll()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                changeFeedRequestOptions:
                new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning(),
                }) as ChangeFeedIteratorCore;
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
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            await this.CreateRandomItems(this.LargerContainer, batchSize, randomPartitionKey: true);
            ChangeFeedIteratorCore setIteratorNew = itemsCore.GetChangeFeedStreamIterator(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuation),
                }) as ChangeFeedIteratorCore;

            while (setIteratorNew.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await setIteratorNew.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        totalCount += response.Count;
                    }
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
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            await Task.Delay(1000);
            DateTime now = DateTime.UtcNow;
            await Task.Delay(1000);
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.Container;
            FeedIterator feedIterator = itemsCore.GetChangeFeedStreamIterator(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromTime(now),
                });
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

            Assert.AreEqual(totalCount, batchSize);
        }

        /// <summary>
        /// Verify that we can read the Change Feed for a Partition Key and that does not read other items.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_PartitionKey_ReadAll()
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

            ContainerInternal itemsCore = this.Container;
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginningWithRange(new FeedRangePartitionKey(new PartitionKey(pkToRead))),
                }) as ChangeFeedIteratorCore;
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
                        foreach (ToDoActivity toDoActivity in response)
                        {
                            Assert.AreEqual(pkToRead, toDoActivity.status);
                        }
                    }

                    continuation = responseMessage.ContinuationToken;
                }
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead));
            }

            ChangeFeedIteratorCore setIteratorNew = itemsCore.GetChangeFeedStreamIterator(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuation),
                }) as ChangeFeedIteratorCore;

            while (setIteratorNew.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await setIteratorNew.ReadNextAsync(this.cancellationToken))
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

            Assert.AreEqual(expectedFinalCount, totalCount);
        }

        /// <summary>
        /// Verify that we can read the Change Feed for a Partition Key and that does not read other items.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_PartitionKey_OfT_ReadAll()
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

            ContainerInternal itemsCore = this.Container;
            FeedIterator<ToDoActivity> feedIterator = itemsCore.GetChangeFeedIterator<ToDoActivity>(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginningWithRange(new FeedRangePartitionKey(new PartitionKey(pkToRead))),
                });
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;
                foreach (ToDoActivity toDoActivity in feedResponse)
                {
                    Assert.AreEqual(pkToRead, toDoActivity.status);
                }

                continuation = feedResponse.ContinuationToken;
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            for (int i = 0; i < batchSize; i++)
            {
                await this.Container.CreateItemAsync(this.CreateRandomToDoActivity(pkToRead));
            }

            FeedIterator<ToDoActivity> setIteratorNew = itemsCore.GetChangeFeedIterator<ToDoActivity>(
                changeFeedRequestOptions: new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuation),
                });

            while (setIteratorNew.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await setIteratorNew.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;
                foreach (ToDoActivity toDoActivity in feedResponse)
                {
                    Assert.AreEqual(pkToRead, toDoActivity.status);
                }
            }

            Assert.AreEqual(expectedFinalCount, totalCount);
        }

        /// <summary>
        /// Test to verify that StartFromBeginning works as expected by inserting 25 items, reading them all, then taking the last continuationtoken, 
        /// inserting another 25, and verifying that the iterator continues from the saved token and reads the second 25 for a total of 50 documents.
        /// </summary>
        [TestMethod]
        public async Task ChangeFeedIteratorCore_OfT_ReadAll()
        {
            int totalCount = 0;
            int firstRunTotal = 25;
            int batchSize = 25;

            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            ContainerInternal itemsCore = this.Container;
            FeedIterator<ToDoActivity> feedIterator = itemsCore.GetChangeFeedIterator<ToDoActivity>(changeFeedRequestOptions: new ChangeFeedRequestOptions()
            {
                From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning(),
            });
            string continuation = null;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;
                continuation = feedResponse.ContinuationToken;
            }

            Assert.AreEqual(firstRunTotal, totalCount);

            int expectedFinalCount = 50;

            // Insert another batch of 25 and use the last FeedToken from the first cycle
            await this.CreateRandomItems(this.Container, batchSize, randomPartitionKey: true);
            FeedIterator<ToDoActivity> setIteratorNew = itemsCore.GetChangeFeedIterator<ToDoActivity>(changeFeedRequestOptions: new ChangeFeedRequestOptions()
            {
                From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuation),
            });

            while (setIteratorNew.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await setIteratorNew.ReadNextAsync(this.cancellationToken);
                totalCount += feedResponse.Count;
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

            ContainerInternal itemsCore = this.Container;
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator() as ChangeFeedIteratorCore;

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
                            await this.CreateRandomItems(this.Container, expectedDocuments, randomPartitionKey: true);
                            createdDocuments = true;
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
            await this.CreateRandomItems(this.Container, 2, randomPartitionKey: true);
            ContainerInternal itemsCore = this.Container;
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(changeFeedRequestOptions: new ChangeFeedRequestOptions()
            {
                MaxItemCount = 1,
                From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning(),
            }) as ChangeFeedIteratorCore;

            while (feedIterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                        if (response.Count > 0)
                        {
                            Assert.AreEqual(1, response.Count);
                            return;
                        }
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
            int pkRangesCount = (await this.LargerContainer.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.LargerContainer.LinkUri)).Count;

            int expected = 25;
            int iterations = 0;
            await this.CreateRandomItems(this.LargerContainer, expected, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            string continuation = null;
            int count = 0;
            while (true)
            {
                ChangeFeedRequestOptions requestOptions;
                if (continuation == null)
                {
                    requestOptions = new ChangeFeedRequestOptions()
                    {
                        From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning(),
                    };
                }
                else
                {
                    requestOptions = new ChangeFeedRequestOptions()
                    {
                        From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(continuation),
                    };
                }

                ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(changeFeedRequestOptions: requestOptions) as ChangeFeedIteratorCore;
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
            List<CompositeContinuationToken> previousToken = null;
            await this.CreateRandomItems(this.LargerContainer, expected, randomPartitionKey: true);
            ContainerInternal itemsCore = this.LargerContainer;
            ChangeFeedIteratorCore feedIterator = itemsCore.GetChangeFeedStreamIterator(changeFeedRequestOptions: new ChangeFeedRequestOptions()
            {
                MaxItemCount = 1,
                From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginning(),
            }) as ChangeFeedIteratorCore;
            while (true)
            {
                using (ResponseMessage responseMessage =
                await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    Assert.IsTrue(FeedRangeCompositeContinuation.TryParse(responseMessage.ContinuationToken, out FeedRangeContinuation continuation));
                    FeedRangeCompositeContinuation compositeContinuation = continuation as FeedRangeCompositeContinuation;
                    List<CompositeContinuationToken> deserializedToken = compositeContinuation.CompositeContinuationTokens.ToList();
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

        [TestMethod]
        public async Task GetFeedRangesAsync_MatchesPkRanges()
        {
            int pkRangesCount = (await this.LargerContainer.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.LargerContainer.LinkUri)).Count;
            ContainerInternal itemsCore = this.LargerContainer;
            IEnumerable<FeedRange> tokens = await itemsCore.GetFeedRangesAsync();
            Assert.AreEqual(pkRangesCount, tokens.Count());
        }

        [TestMethod]
        public async Task GetFeedRangesAsync_AllowsParallelProcessing()
        {
            int pkRangesCount = (await this.LargerContainer.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.LargerContainer.LinkUri)).Count;
            ContainerInternal itemsCore = this.LargerContainer;
            IEnumerable<FeedRange> tokens = await itemsCore.GetFeedRangesAsync();
            Assert.IsTrue(pkRangesCount > 1, "Should have created a multi partition container.");
            Assert.AreEqual(pkRangesCount, tokens.Count());
            int totalDocuments = 200;
            await this.CreateRandomItems(this.LargerContainer, totalDocuments, randomPartitionKey: true);
            List<Task<int>> tasks = tokens.Select(token => Task.Run(async () =>
            {
                int count = 0;
                ChangeFeedIteratorCore iteratorForToken =
                    itemsCore.GetChangeFeedStreamIterator(new ChangeFeedRequestOptions()
                    {
                        From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginningWithRange(token),
                    }) as ChangeFeedIteratorCore;
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
        public async Task CannotMixTokensFromOtherContainers()
        {
            IReadOnlyList<FeedRange> tokens = await this.LargerContainer.GetFeedRangesAsync();
            FeedIterator iterator = this.LargerContainer.GetChangeFeedStreamIterator(
                new ChangeFeedRequestOptions()
                {
                    From = ChangeFeedRequestOptions.StartFrom.CreateFromBeginningWithRange(tokens[0]),
                });
            ResponseMessage responseMessage = await iterator.ReadNextAsync();
            iterator = this.Container.GetChangeFeedStreamIterator(new ChangeFeedRequestOptions()
            {
                From = ChangeFeedRequestOptions.StartFrom.CreateFromContinuation(responseMessage.ContinuationToken),
            });
            responseMessage = await iterator.ReadNextAsync();
            Assert.IsNotNull(responseMessage.CosmosException);
            Assert.AreEqual(HttpStatusCode.BadRequest, responseMessage.StatusCode);
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
    }
}