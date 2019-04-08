//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosItemChangeFeedTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private CosmosDefaultJsonSerializer jsonSerializer = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
            this.jsonSerializer = new CosmosDefaultJsonSerializer();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task StandByFeedIterator()
        {
            var count = 0;
            var lastcontinuation = string.Empty;
            var firstRunTotal = 25;
            var batchSize = 25;
            Documents.Routing.Range<string> firstRange = null;
            Documents.Routing.Range<string> currentRange = null;

            var pkRanges = await this.Container.Client.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);

            await CreateRandomItems(batchSize, randomPartitionKey: true);
            CosmosItemsCore itemsCore = (CosmosItemsCore)this.Container.Items;
            CosmosFeedResultSetIterator setIterator = itemsCore.GetStandByFeedIterator(requestOptions: new CosmosChangeFeedRequestOptions() { StartFromBeginning = true });

            while (setIterator.HasMoreResults)
            {
                using (CosmosResponseMessage responseMessage =
                    await setIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.Continuation;
                    var deserializedToken = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(lastcontinuation);
                    currentRange = deserializedToken[0].Range;
                    Assert.AreEqual(pkRanges.Count, deserializedToken.Count);
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponse<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
                    }

                    if (currentRange.Equals(firstRange) && responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified) break;
                    if (firstRange == null)
                    {
                        firstRange = currentRange;
                    }

                }

            }
            Assert.AreEqual(firstRunTotal, count);

            var secondRunTotal = 50;
            firstRange = null;
            currentRange = null;

            await CreateRandomItems(batchSize, randomPartitionKey: true);
            CosmosFeedResultSetIterator setIteratorNew =
                itemsCore.GetStandByFeedIterator(lastcontinuation);

            while (setIteratorNew.HasMoreResults)
            {
                using (CosmosResponseMessage responseMessage =
                    await setIteratorNew.FetchNextSetAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.Continuation;
                    currentRange = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(lastcontinuation)[0].Range;

                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponse<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
                    }

                    if (currentRange.Equals(firstRange) && responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified) break;
                    if (firstRange == null)
                    {
                        firstRange = currentRange;
                    }
                }

            }

            Assert.AreEqual(secondRunTotal, count);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public async Task StandByFeedIterator_WithInexistentRange()
        {
            var count = 0;
            var lastcontinuation = string.Empty;
            var firstRunTotal = 25;
            var batchSize = 25;
            Documents.Routing.Range<string> firstRange = null;
            Documents.Routing.Range<string> currentRange = null;
            List<CompositeContinuationToken> lastToken = null;

            await CreateRandomItems(batchSize, randomPartitionKey: true);
            CosmosItemsCore itemsCore = (CosmosItemsCore)this.Container.Items;
            CosmosFeedResultSetIterator setIterator = itemsCore.GetStandByFeedIterator(requestOptions: new CosmosChangeFeedRequestOptions() { StartFromBeginning = true });

            while (setIterator.HasMoreResults)
            {
                using (CosmosResponseMessage responseMessage =
                    await setIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.Continuation;
                    lastToken = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(lastcontinuation);
                    currentRange = lastToken[0].Range;

                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponse<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
                    }

                    if (currentRange.Equals(firstRange) && responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified) break;
                    if (firstRange == null)
                    {
                        firstRange = currentRange;
                    }

                }

            }
            Assert.AreEqual(firstRunTotal, count);

            // Add some random range, this will force the failure
            lastToken.Add(new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("whatever", "random", true, false),
                Token = "oops"
            });
            lastcontinuation = JsonConvert.SerializeObject(lastToken);

            var secondRunTotal = 50;
            firstRange = null;
            currentRange = null;

            await CreateRandomItems(batchSize, randomPartitionKey: true);
            CosmosFeedResultSetIterator setIteratorNew =
                itemsCore.GetStandByFeedIterator(lastcontinuation);

            while (setIteratorNew.HasMoreResults)
            {
                using (CosmosResponseMessage responseMessage =
                    await setIteratorNew.FetchNextSetAsync(this.cancellationToken))
                {
                    lastcontinuation = responseMessage.Headers.Continuation;
                    currentRange = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(lastcontinuation)[0].Range;

                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponse<ToDoActivity>>(responseMessage.Content).Data;
                        count += response.Count;
                    }

                    if (currentRange.Equals(firstRange)) break;
                    if (firstRange == null)
                    {
                        firstRange = currentRange;
                    }
                }

            }

            Assert.AreEqual(secondRunTotal, count);
        }

        [TestMethod]
        public async Task StandByFeedIterator_WithMaxItemCount()
        {
            await CreateRandomItems(2, randomPartitionKey: true);
            CosmosItemsCore itemsCore = (CosmosItemsCore)this.Container.Items;
            CosmosFeedResultSetIterator setIterator = itemsCore.GetStandByFeedIterator(maxItemCount : 1, requestOptions: new CosmosChangeFeedRequestOptions() { StartFromBeginning = true });

            while (setIterator.HasMoreResults)
            {
                using (CosmosResponseMessage responseMessage =
                    await setIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    if (responseMessage.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponse<ToDoActivity>>(responseMessage.Content).Data;
                        Assert.AreEqual(1, response.Count);
                        return;
                    }
                }

            }

            Assert.Fail("Found no batch with size 1");
        }

        [TestMethod]
        public async Task StandByFeedIterator_NoFetchNext()
        {
            var pkRanges = await this.Container.Client.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);

            int expected = 25;
            int iterations = 0;
            await CreateRandomItems(expected, randomPartitionKey: true);
            CosmosItemsCore itemsCore = (CosmosItemsCore)this.Container.Items;
            string continuationToken = null;
            int count = 0;
            while (true)
            {
                CosmosChangeFeedRequestOptions requestOptions;
                if (string.IsNullOrEmpty(continuationToken))
                {
                    requestOptions = new CosmosChangeFeedRequestOptions() { StartFromBeginning = true};
                }
                else
                {
                    requestOptions = new CosmosChangeFeedRequestOptions() { };
                }

                CosmosFeedResultSetIterator setIterator = itemsCore.GetStandByFeedIterator(continuationToken, requestOptions: requestOptions);
                using (CosmosResponseMessage iterator =
                    await setIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    continuationToken = iterator.Headers.Continuation;
                    if (iterator.IsSuccessStatusCode)
                    {
                        Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponse<ToDoActivity>>(iterator.Content).Data;
                        count += response.Count;
                    }
                }

                if(count > expected)
                {
                    Assert.Fail($"{count} does not equal {expected}");
                }

                if (count.Equals(expected))
                {
                    break;
                }

                if (iterations++ > pkRanges.Count)
                {
                    Assert.Fail("Feed does not contain all elements even after looping through PK ranges. Either the continuation is not moving forward or there is some state problem.");

                }
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void StandByFeedIterator_ValidatesContinuationTokenAndStartFromBeginning()
        {
            CosmosItemsCore itemsCore = (CosmosItemsCore)this.Container.Items;
            CosmosFeedResultSetIterator setIterator = itemsCore.GetStandByFeedIterator("someContinuation", requestOptions: new CosmosChangeFeedRequestOptions()
            {
                StartFromBeginning = true
            });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void StandByFeedIterator_ValidatesContinuationTokenAndStartTime()
        {
            CosmosItemsCore itemsCore = (CosmosItemsCore)this.Container.Items;
            CosmosFeedResultSetIterator setIterator = itemsCore.GetStandByFeedIterator("someContinuation", requestOptions: new CosmosChangeFeedRequestOptions()
            {
                StartTime = new DateTime(1985, 1, 1)
            });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void StandByFeedIterator_ValidatesStartTimeAndStartFromBeginning()
        {
            CosmosItemsCore itemsCore = (CosmosItemsCore)this.Container.Items;
            CosmosFeedResultSetIterator setIterator = itemsCore.GetStandByFeedIterator(requestOptions: new CosmosChangeFeedRequestOptions()
            {
                StartFromBeginning = true,
                StartTime = new DateTime(1985, 1, 1)
            });
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

                    await this.Container.Items.CreateItemAsync<ToDoActivity>(partitionKey: temp.status, item: temp);
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
