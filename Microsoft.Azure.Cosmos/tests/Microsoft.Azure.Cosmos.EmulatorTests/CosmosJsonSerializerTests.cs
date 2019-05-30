//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosJsonSerializerTests : BaseCosmosClientHelper
    {
        private CosmosJsonSerializerCore jsonSerializer = null;
        private CosmosContainer container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            this.container = response;
            this.jsonSerializer = new CosmosJsonSerializerCore();
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
            
            Mock<CosmosJsonSerializer> mockJsonSerializer = new Mock<CosmosJsonSerializer>();

            //The item object will be serialized with the custom json serializer.
            ToDoActivity testItem = CreateRandomToDoActivity();
            mockJsonSerializer.Setup(x => x.ToStream<ToDoActivity>(It.IsAny<ToDoActivity>())).Callback(()=> toStreamCount++).Returns(this.jsonSerializer.ToStream<ToDoActivity>(testItem));
            mockJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(It.IsAny<Stream>())).Callback<Stream>(x => { x.Dispose(); fromStreamCount++; }).Returns(testItem);

            //Create a new cosmos client with the mocked cosmos json serializer
            CosmosClient mockClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithCustomJsonSerializer(mockJsonSerializer.Object));
            CosmosContainer mockContainer = mockClient.GetContainer(this.database.Id, this.container.Id);

            //Validate that the custom json serializer is used for creating the item
            ItemResponse<ToDoActivity> response = await mockContainer.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            Assert.AreEqual(1, toStreamCount);
            Assert.AreEqual(1, fromStreamCount);

            await mockContainer.DeleteItemAsync<ToDoActivity>(new Cosmos.PartitionKey(testItem.status), testItem.id);
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
