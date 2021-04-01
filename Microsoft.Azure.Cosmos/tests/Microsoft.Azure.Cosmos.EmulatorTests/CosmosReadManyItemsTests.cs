//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosReadManyItemsTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                throughput: 20000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;

            // Create items with different pk values
            for (int i = 0; i < 500; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "pk" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task ReadManyTypedTest()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i=0; i<10; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            FeedResponse<ToDoActivity> feedResponse= await this.Container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 10);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);
        }

        [TestMethod]
        public async Task ReadManyStreamTest()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);

                ToDoActivity[] items = this.cosmosClient.ClientContext.SerializerCore.FromFeedStream<ToDoActivity>(
                                        CosmosFeedResponseSerializer.GetStreamWithoutServiceEnvelope(responseMessage.Content));
                Assert.AreEqual(items.Length, 5);
            }
        }

        [TestMethod]
        public async Task ReadManyDoesNotFetchQueryPlan()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);
                Assert.IsFalse(responseMessage.Diagnostics.ToString().Contains("Gateway QueryPlan"));
            }

        }

#if PREVIEW
        [TestMethod]
        public async Task ReadManyMultiplePK()
        {
            IReadOnlyList<string> pkPaths = new List<string> { "/pk", "/description" };
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPaths: pkPaths);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings);

            for (int i = 0; i < 5; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "pk" + i.ToString();
                item.id = i.ToString();
                item.description = "description" + i;
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                PartitionKey partitionKey = new PartitionKeyBuilder()
                                                        .Add("pk" + i)
                                                        .Add("description" + i)
                                                        .Build();

                itemList.Add((i.ToString(), partitionKey));
            }  
        }
#endif
    }
}
