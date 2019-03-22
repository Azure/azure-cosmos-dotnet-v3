//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosHandlersTests : BaseCosmosClientHelper
    {
        private CosmosContainerCore Container = null;
        private CosmosJsonSerializer jsonSerializer = null;

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
        public async Task TestCustomPropertyWithHandler()
        { 
            CustomHandler testHandler = new CustomHandler();

            // Add the random guid to the property
            Guid randomGuid = Guid.NewGuid();
            string propertyKey = "Test";
            testHandler.UpdateRequestMessage = x => x.Properties[propertyKey] = randomGuid;

            CosmosClient customClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.AddCustomHandlers(testHandler));

            ToDoActivity testItem = CreateRandomToDoActivity();
            using (CosmosResponseMessage response = await customClient.Databases[this.database.Id].Containers[this.Container.Id].Items.CreateItemStreamAsync(
                partitionKey: testItem.status, 
                streamPayload: this.jsonSerializer.ToStream(testItem)))
            {
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.RequestMessage);
                Assert.IsNotNull(response.RequestMessage.Properties);
                Assert.AreEqual(randomGuid, response.RequestMessage.Properties[propertyKey]);
            }
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

        public class CustomHandler : CosmosRequestHandler
        {
            public Action<CosmosRequestMessage> UpdateRequestMessage = null;

            public override Task<CosmosResponseMessage> SendAsync(CosmosRequestMessage request, CancellationToken cancellationToken)
            {
                if (this.UpdateRequestMessage != null)
                {
                    this.UpdateRequestMessage(request);
                }

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
