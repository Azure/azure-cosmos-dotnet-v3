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
        private ContainerCore Container = null;
        private const string PartitionKey = "/id";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 50000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerInlineCore)response;

            DocumentFeedResponse<PartitionKeyRange> pkRangesFeed = await this.cosmosClient.DocumentClient.ReadPartitionKeyRangeFeedAsync(this.Container.LinkUri);
            Assert.IsTrue(pkRangesFeed.Count > 1, "Refresh container throughput to have at-least > 1 pk-range");
        }


        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        public async Task CrossPartitionBiDirectionalItemReadFeedTest(bool useStatelessIteration)
        {
            //create items
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

                using (ResponseMessage createResponse = await this.Container.CreateItemStreamAsync(
                        CosmosReadFeedTests.GenerateStreamFromString(item),
                        new Cosmos.PartitionKey(i.ToString())))
                {
                    Assert.IsTrue(createResponse.IsSuccessStatusCode);
                }
            }

            string lastKnownContinuationToken = null;
            FeedIterator iter = this.Container.Database.GetContainer(this.Container.Id).GetItemQueryStreamIterator(
                continuationToken: lastKnownContinuationToken, 
                requestOptions: requestOptions);

            int count = 0;
            List<string> forwardOrder = new List<string>();
            while (iter.HasMoreResults)
            {
                if (useStatelessIteration)
                {
                    iter = this.Container.Database.GetContainer(this.Container.Id).GetItemQueryStreamIterator(
                        continuationToken: lastKnownContinuationToken,
                        requestOptions: requestOptions);
                }

                using (ResponseMessage response = await iter.ReadNextAsync())
                {
                    Assert.IsNotNull(response);

                    lastKnownContinuationToken = response.Headers.ContinuationToken;
                    
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

            Assert.IsNull(lastKnownContinuationToken);
            Assert.IsNotNull(forwardOrder);
            Assert.AreEqual(total, count);
            Assert.IsFalse(forwardOrder.Where(x => string.IsNullOrEmpty(x)).Any());

            requestOptions.Properties = requestOptions.Properties = new Dictionary<string, object>();
            requestOptions.Properties.Add(HttpConstants.HttpHeaders.EnumerationDirection, (byte)BinaryScanDirection.Reverse);
            count = 0;
            List<string> reverseOrder = new List<string>();

            lastKnownContinuationToken = null;
            iter = this.Container.Database.GetContainer(this.Container.Id)
                    .GetItemQueryStreamIterator(queryDefinition: null, continuationToken: lastKnownContinuationToken, requestOptions: requestOptions);
            while (iter.HasMoreResults)
            {
                if (useStatelessIteration)
                {
                    iter = this.Container.Database.GetContainer(this.Container.Id)
                            .GetItemQueryStreamIterator(queryDefinition: null, continuationToken: lastKnownContinuationToken, requestOptions: requestOptions);
                }

                using (ResponseMessage response = await iter.ReadNextAsync())
                {
                    lastKnownContinuationToken = response.Headers.ContinuationToken;

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

            Assert.IsNull(lastKnownContinuationToken);
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
