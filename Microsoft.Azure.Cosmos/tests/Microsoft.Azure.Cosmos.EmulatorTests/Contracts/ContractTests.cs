//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Contracts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents;
    using System.IO;
    using Newtonsoft.Json.Linq;

    [EmulatorTests.TestClass]
    public class ContractTests : BaseCosmosClientHelper
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
        public async Task ItemStreamContractVerifier()
        {
            string PartitionKey = "/status";
            Container container = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);

            int totalCount = 4;
            Dictionary<string, ToDoActivity> toDoActivities = new Dictionary<string, ToDoActivity>();
            // Create 3 constant items;
            for (int i = 0; i < totalCount; i++)
            {
                ToDoActivity toDoActivity = new ToDoActivity()
                {
                    id = "toDoActivity" + i,
                    status = "InProgress",
                    cost = 9000 + i,
                    description = "Constant to do activity",
                    taskNum = i
                };

                toDoActivities.Add(toDoActivity.id, toDoActivity);

                await container.CreateItemAsync<ToDoActivity>(toDoActivity);
            }

            List<FeedIterator> FeedIterators = new List<FeedIterator>();

            // The stream contract should return the same contract as read feed.
            // {
            //    "_rid": "containerRid",
            //    "Documents": [{
            //        "id": "03230",
            //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
            //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
            //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
            //        "_attachments": "attachments\/",
            //        "_ts": 1501107886
            //    }],
            //    "_count": 1
            // }

            FeedIterator setIterator =
                container.GetItemQueryStreamIterator();
            FeedIterators.Add(setIterator);

            QueryRequestOptions options = new QueryRequestOptions()
            {
                MaxItemCount = 4,
                MaxConcurrency = 1,
            };

            FeedIterator queryIterator = container.GetItemQueryStreamIterator(
                    queryText: @"select * from t where t.id != """" ",
                    requestOptions: options);

            FeedIterators.Add(queryIterator);
            foreach (FeedIterator iterator in FeedIterators)
            {
                int count = 0;
                while (iterator.HasMoreResults)
                {
                    ResponseMessage response = await iterator.ReadNextAsync(this.cancellationToken);
                    response.EnsureSuccessStatusCode();

                    using (StreamReader sr = new StreamReader(response.Content))
                    {
                        string jsonString = await sr.ReadToEndAsync();
                        Assert.IsNotNull(jsonString);
                        JObject jObject = JsonConvert.DeserializeObject<JObject>(jsonString);
                        Assert.IsNotNull(jObject["Documents"]);
                        Assert.IsNotNull(jObject["_rid"]);
                        Assert.IsNotNull(jObject["_count"]);
                        Assert.IsTrue(jObject["_count"].ToObject<int>() >= 0);
                        foreach (JObject item in jObject["Documents"])
                        {
                            count++;
                            Assert.IsNotNull(item["id"]);
                            ToDoActivity createdItem = toDoActivities[item["id"].ToString()];

                            Assert.AreEqual(createdItem.taskNum, item["taskNum"].ToObject<int>());
                            Assert.AreEqual(createdItem.cost, item["cost"].ToObject<double>());
                            Assert.AreEqual(createdItem.description, item["description"].ToString());
                            Assert.AreEqual(createdItem.status, item["status"].ToString());
                            Assert.IsNotNull(item["_rid"]);
                            Assert.IsNotNull(item["_self"]);
                            Assert.IsNotNull(item["_etag"]);
                            Assert.IsNotNull(item["_attachments"]);
                            Assert.IsNotNull(item["_ts"]);
                        }
                    }
                }

                Assert.AreEqual(totalCount, count);
            }
        }

        /// <summary>
        /// Migration test from V2 Continuation model for Change Feed
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeed_FeedRange_FromV2SDK()
        {
            ContainerResponse largerContainer = await this.database.CreateContainerAsync(
               new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/status"),
               throughput: 20000,
               cancellationToken: this.cancellationToken);

            ContainerCore container = (ContainerInlineCore)largerContainer;

            int expected = 100;
            int count = 0;
            await this.CreateRandomItems(container, expected, randomPartitionKey: true);

            IReadOnlyList<FeedRange> feedRanges = await container.GetFeedRangesAsync();
            List<string> continuations = new List<string>();
            // First do one request to construct the old model information based on Etag
            foreach (FeedRange feedRange in feedRanges)
            {
                IEnumerable<string> pkRangeIds = await container.GetPartitionKeyRangesAsync(feedRange);
                ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
                {
                    MaxItemCount = 1
                };
                ChangeFeedIteratorCore feedIterator = container.GetChangeFeedStreamIterator(
                    changeFeedStartFrom: ChangeFeedStartFrom.CreateFromBeginningWithRange(feedRange),
                    changeFeedRequestOptions: requestOptions) as ChangeFeedIteratorCore;
                ResponseMessage firstResponse = await feedIterator.ReadNextAsync();
                if (firstResponse.IsSuccessStatusCode)
                {
                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(firstResponse.Content).Data;
                    count += response.Count;
                }

                FeedRangeEpk feedRangeEpk = feedRange as FeedRangeEpk;

                // Construct the continuation's range, using PKRangeId + ETag
                List<dynamic> ct = new List<dynamic>()
                {
                    new
                    {
                        min = feedRangeEpk.Range.Min,
                        max = feedRangeEpk.Range.Max,
                        token = firstResponse.Headers.ETag
                    }
                };

                // Extract Etag and manually construct the continuation
                dynamic oldContinuation = new
                {
                    V = 0,
                    PKRangeId = pkRangeIds.First(),
                    Continuation = ct
                };
                continuations.Add(JsonConvert.SerializeObject(oldContinuation));
            }

            // Now start the new iterators with the constructed continuations from migration
            foreach (string continuation in continuations)
            {
                ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
                {
                    MaxItemCount = 100
                };
                ChangeFeedIteratorCore feedIterator = container.GetChangeFeedStreamIterator(
                    changeFeedStartFrom: ChangeFeedStartFrom.CreateFromContinuation(continuation),
                    changeFeedRequestOptions: requestOptions) as ChangeFeedIteratorCore;
                ResponseMessage firstResponse = await feedIterator.ReadNextAsync();
                if (firstResponse.IsSuccessStatusCode)
                {
                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(firstResponse.Content).Data;
                    count += response.Count;
                    string migratedContinuation = firstResponse.ContinuationToken;
                    Assert.IsTrue(FeedRangeContinuation.TryParse(migratedContinuation, out FeedRangeContinuation feedRangeContinuation));
                    Assert.IsTrue(feedRangeContinuation.FeedRange is FeedRangeEpk);
                }
            }

            Assert.AreEqual(expected, count);
        }

        /// <summary>
        /// Migration test from V2 Continuation model for Query
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task Query_FeedRange_FromV2SDK()
        {
            ContainerResponse largerContainer = await this.database.CreateContainerAsync(
               new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/status"),
               throughput: 20000,
               cancellationToken: this.cancellationToken);

            ContainerCore container = (ContainerInlineCore)largerContainer;

            int expected = 100;
            int count = 0;
            await this.CreateRandomItems(container, expected, randomPartitionKey: true);

            DocumentFeedResponse<PartitionKeyRange> ranges = await container.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(container.LinkUri);
            foreach (PartitionKeyRange range in ranges)
            {
                string feedRangeSerialization = JsonConvert.SerializeObject(new { PKRangeId = range.Id });
                FeedRange feedRange = FeedRange.FromJsonString(feedRangeSerialization);
                FeedIterator<ToDoActivity> iterator = container.GetItemQueryIterator<ToDoActivity>(feedRange, new QueryDefinition("select * from c"));
                while (iterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> response = await iterator.ReadNextAsync();
                    count += response.Count;
                }
            }

            Assert.AreEqual(expected, count);
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
