//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosNullReferenceExceptionTests : BaseCosmosClientHelper
    {
        private ContainerInternal container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.container = (ContainerInternal)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CosmosEndToEndNullReferenceExceptionTestAsync()
        {
            string errorMessage = Guid.NewGuid().ToString();
            RequestHandlerHelper requestHandlerHelper = new RequestHandlerHelper
            {
                UpdateRequestMessage = (request) => throw new NullReferenceException(errorMessage)
            };

            using CosmosClient client = TestCommon.CreateCosmosClient(builder => builder.AddCustomHandlers(requestHandlerHelper));
            Container containerWithNullRef = client.GetContainer(this.database.Id, this.container.Id);

            try
            {
                ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
                await containerWithNullRef.CreateItemAsync(toDoActivity);
                Assert.Fail("Create should throw a null reference exception");
            }
            catch(NullReferenceException nre)
            {
                Assert.AreEqual(typeof(CosmosNullReferenceException), nre.GetType());
                Assert.IsTrue(nre.Message.Contains("CreateItemAsync"));
                string cosmosToString = nre.ToString();
                Assert.IsFalse(cosmosToString.Contains("Microsoft.Azure.Cosmos.CosmosNullReferenceException"), $"The internal wrapper exception should not be exposed to users. {cosmosToString}");
                Assert.IsTrue(cosmosToString.Contains(errorMessage));
                Assert.IsTrue(cosmosToString.Contains("CreateItemAsync"));
            }

            try
            {
                FeedIterator<ToDoActivity> iterator = containerWithNullRef.GetItemQueryIterator<ToDoActivity>("select * from T");
                await iterator.ReadNextAsync();
                Assert.Fail("Create should throw a null reference exception");
            }
            catch (NullReferenceException nre)
            {
                Assert.AreEqual(typeof(CosmosNullReferenceException), nre.GetType());
                Assert.IsTrue(nre.Message.Contains("Typed FeedIterator ReadNextAsync"));
                string cosmosToString = nre.ToString();
                Assert.IsFalse(cosmosToString.Contains("Microsoft.Azure.Cosmos.CosmosNullReferenceException"), $"The internal wrapper exception should not be exposed to users. {cosmosToString}");
                Assert.IsTrue(cosmosToString.Contains(errorMessage));
                Assert.IsTrue(cosmosToString.Contains("Typed FeedIterator ReadNextAsync"));
            }
        }
    }
}
