//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosHandlersTests : BaseCosmosClientHelper
    {
        private Container Container = null;

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
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestCustomPropertyWithHandler()
        {
            RequestHandlerHelper testHandler = new RequestHandlerHelper();

            // Add the random guid to the property
            Guid randomGuid = Guid.NewGuid();
            string propertyKey = "Test";
            testHandler.UpdateRequestMessage = x => x.Properties[propertyKey] = randomGuid;

            CosmosClient customClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.AddCustomHandlers(testHandler));

            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (ResponseMessage response = await customClient.GetContainer(this.database.Id, this.Container.Id).CreateItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream(testItem)))
            {
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.RequestMessage);
                Assert.IsNotNull(response.RequestMessage.Properties);
                Assert.AreEqual(randomGuid, response.RequestMessage.Properties[propertyKey]);
            }
        }

        [TestMethod]
        public async Task TestBatchRequiredHeadersWithHandler()
        {
            RequestHandlerHelper testHandler = new RequestHandlerHelper();

            // Get the headers from request message for testing.
            Headers requestHeaders = null;
            testHandler.UpdateRequestMessage = x => requestHeaders = x.Headers;

            CosmosClient customClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.AddCustomHandlers(testHandler).WithBulkExecution(true));

            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (ResponseMessage response = await customClient.GetContainer(this.database.Id, this.Container.Id).CreateItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(testItem.status),
                streamPayload: TestCommon.SerializerCore.ToStream(testItem)))
            {
                Assert.IsNotNull(response);
                Assert.IsNotNull(requestHeaders);

                string isBatchAtomic = requestHeaders[HttpConstants.HttpHeaders.IsBatchAtomic];
                Assert.IsNotNull(isBatchAtomic);
                Assert.IsFalse(bool.Parse(isBatchAtomic));

                string isBatchRequest = requestHeaders[HttpConstants.HttpHeaders.IsBatchRequest];
                Assert.IsNotNull(isBatchRequest);
                Assert.IsTrue(bool.Parse(isBatchRequest));

                string shouldBatchContinueOnError = requestHeaders[HttpConstants.HttpHeaders.ShouldBatchContinueOnError];
                Assert.IsNotNull(shouldBatchContinueOnError);
                Assert.IsTrue(bool.Parse(shouldBatchContinueOnError));
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
                    ToDoActivity temp = this.CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await this.Container.CreateItemAsync<ToDoActivity>(item: temp);
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
