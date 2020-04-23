//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Azure.Cosmos.Serialization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosJsonSerializerTests : BaseCosmosClientHelper
    {
        private CosmosContainer container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new CosmosContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            this.container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestCustomJsonSerializer()
        {
            int toStreamCount = 0;
            int fromStreamCount = 0;

            Mock<CosmosSerializer> mockJsonSerializer = new Mock<CosmosSerializer>();

            //The item object will be serialized with the custom json serializer.
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            mockJsonSerializer.Setup(x => x.ToStream<ToDoActivity>(It.IsAny<ToDoActivity>()))
                .Callback(() => toStreamCount++)
                .Returns(TestCommon.Serializer.Value.ToStream<ToDoActivity>(testItem));

            mockJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(It.IsAny<Stream>()))
                .Callback<Stream>(x => { x.Dispose(); fromStreamCount++; })
                .Returns(testItem);

            //Create a new cosmos client with the mocked cosmos json serializer
            CosmosClient mockClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithCustomSerializer(mockJsonSerializer.Object));
            CosmosContainer mockContainer = mockClient.GetContainer(this.database.Id, this.container.Id);

            //Validate that the custom json serializer is used for creating the item
            ItemResponse<ToDoActivity> response = await mockContainer.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.Created, response.GetRawResponse().Status);

            Assert.AreEqual(1, toStreamCount);
            Assert.AreEqual(1, fromStreamCount);

            await mockContainer.DeleteItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.status));
        }

        /// <summary>
        /// Verify that null attributes get serialized by default.
        /// </summary>
        [TestMethod]
        public async Task DefaultNullValueHandling()
        {
            ToDoActivity document = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                description = default(string),
                status = "TBD",
                taskNum = 42,
                cost = double.MaxValue
            };

            await this.container.UpsertItemAsync(document);

            Response cosmosResponseMessage = await this.container.ReadItemStreamAsync(document.id, new PartitionKey(document.status));
            StreamReader reader = new StreamReader(cosmosResponseMessage.ContentStream);
            string text = reader.ReadToEnd();

            Assert.IsTrue(text.IndexOf(nameof(document.description)) > -1, "Stored item doesn't contains null attributes");
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
