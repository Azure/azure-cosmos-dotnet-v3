//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
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

    [TestClass]
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

        /// <summary>
        /// Migration test from V2 Continuation model for Change Feed
        /// </summary>
        [TestMethod]
        [Timeout(30000)]
        public async Task ChangeFeedIteratorCore_FromV2SDK()
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
                ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions() { StartTime = DateTime.MinValue.ToUniversalTime(), MaxItemCount = 1 };
                ChangeFeedIteratorCore feedIterator = container.GetChangeFeedStreamIterator(feedRange: feedRange, changeFeedRequestOptions: requestOptions) as ChangeFeedIteratorCore;
                ResponseMessage firstResponse = await feedIterator.ReadNextAsync();
                if (firstResponse.IsSuccessStatusCode)
                {
                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(firstResponse.Content).Data;
                    count += response.Count;
                }

                // Construct the continuation's range, using PKRangeId + ETag
                List<dynamic> ct = new List<dynamic>() { new { min = string.Empty, max = string.Empty, token = firstResponse.Headers.ETag } };
                // Extract Etag and manually construct the continuation
                dynamic oldContinuation = new { V = 0, PKRangeId = pkRangeIds.First(), Continuation = ct };
                continuations.Add(JsonConvert.SerializeObject(oldContinuation));
            }

            // Now start the new iterators with the constructed continuations from migration
            foreach (string continuation in continuations)
            {
                ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions() { MaxItemCount = 100 };
                ChangeFeedIteratorCore feedIterator = container.GetChangeFeedStreamIterator(continuationToken: continuation, changeFeedRequestOptions: requestOptions) as ChangeFeedIteratorCore;
                ResponseMessage firstResponse = await feedIterator.ReadNextAsync();
                if (firstResponse.IsSuccessStatusCode)
                {
                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(firstResponse.Content).Data;
                    count += response.Count;
                }
            }

            Assert.AreEqual(expected, count);
        }

        /// <summary>
        /// Migration test from V2 Continuation model for Change Feed
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
