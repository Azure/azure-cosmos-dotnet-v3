//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Documents;

    [TestClass]
    public class CosmosReadFeedTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private const string PartitionKey = "/id";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 50000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;

            FeedResponse<PartitionKeyRange> pkRangesFeed = await this.cosmosClient.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);
            Assert.IsTrue(pkRangesFeed.Count > 1, "Refresh container throughput to have at-least > 1 pk-range");
        }


        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CrossPartitionBiDirectionalItemReadFeedTest()
        {
            //create items
            const int total = 30;
            const int maxItemCount = 30;
            List<string> items = new List<string>();

            for (int i = 0; i < total; i++)
            {
                string item = $@"
                    {{    
                        ""id"": ""{i}""
                    }}";

                using (CosmosResponseMessage createResponse = await this.Container.Items.CreateItemStreamAsync(
                        i.ToString(),
                        CosmosReadFeedTests.GenerateStreamFromString(item),
                        requestOptions: new CosmosItemRequestOptions()))
                {
                    Assert.IsTrue(createResponse.IsSuccessStatusCode);
                }
            }

            CosmosFeedResultSetIterator iter = this.Container.Database.Containers[this.Container.Id].Items
                                .GetItemStreamIterator(maxItemCount);
            int count = 0;
            List<string> forwardOrder = new List<string>();
            while (iter.HasMoreResults)
            {
                using (CosmosResponseMessage response = await iter.FetchNextSetAsync())
                {
                    Assert.IsNotNull(response);
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

            CosmosItemRequestOptions queryOptions = new CosmosItemRequestOptions();
            queryOptions.Properties = queryOptions.Properties = new Dictionary<string, object>();
            queryOptions.Properties.Add(HttpConstants.HttpHeaders.EnumerationDirection, (byte)BinaryScanDirection.Reverse);
            iter = this.Container.Database.Containers[this.Container.Id].Items.GetItemStreamIterator(maxItemCount, null, queryOptions);
            count = 0;
            List<string> reverseOrder = new List<string>();

            while (iter.HasMoreResults)
            {
                using (CosmosResponseMessage response = await iter.FetchNextSetAsync())
                {
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

        private static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
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
