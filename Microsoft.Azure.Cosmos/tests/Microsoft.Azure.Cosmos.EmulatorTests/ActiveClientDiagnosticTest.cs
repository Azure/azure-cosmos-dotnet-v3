//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading.Tasks;
    using VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ActiveClientDiagnosticTest : BaseCosmosClientHelper
    {
        private Container container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();

            this.container = await this.database.CreateContainerAsync(
                new ContainerProperties(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk"),
                cancellationToken: this.cancellationToken);
        }

        [TestMethod]
        public async Task SingleClientTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.AreEqual(1, response.Diagnostics.NumberOfActiveClients());
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task MultiClientTest()
        {
            await base.TestInit(); // Initializing 2nd time
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.AreEqual(2, response.Diagnostics.NumberOfActiveClients());
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task MultiClientWithDisposeTest()
        {
            await base.TestInit(); // Initializing 2nd time
            await base.TestInit(); // Initializing 3rd time
            await base.TestInit(); // Initializing 4th time
            await base.TestCleanup(); // Destroying 1 instance
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.AreEqual(3, response.Diagnostics.NumberOfActiveClients());
            
            await base.TestCleanup();
        }
    }
}
