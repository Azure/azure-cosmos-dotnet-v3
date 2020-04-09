//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosThroughputTests
    {
        private CosmosClient cosmosClient = null;

        [TestInitialize]
        public void TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task CreateDropAutopilotDatabase()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString(),
                new AutopilotThroughputProperties(5000));

            AutopilotThroughputResponse autopilot = await database.ReadAutopilotThroughputAsync();
            Assert.IsNotNull(autopilot);
            Assert.AreEqual(5000, autopilot.Resource.MaxThroughput);

            AutopilotThroughputResponse autopilotReplaced = await database.ReplaceAutopilotThroughputAsync(
                new AutopilotThroughputProperties(10000));
            Assert.IsNotNull(autopilotReplaced);
            Assert.AreEqual(10000, autopilotReplaced.Resource.MaxThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        [Ignore] // Not currently working with emulator
        public async Task CreateDropAutopilotContainer()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerCore container = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                new AutopilotThroughputProperties(5000));
            Assert.IsNotNull(container);

            AutopilotThroughputResponse autopilot = await container.ReadAutopilotThroughputAsync();
            Assert.IsNotNull(autopilot);
            Assert.AreEqual(5000, autopilot.Resource.MaxThroughput);

            AutopilotThroughputResponse autopilotReplaced = await container.ReplaceAutopilotThroughputAsync(
                new AutopilotThroughputProperties(10000));
            Assert.IsNotNull(autopilotReplaced);
            Assert.AreEqual(10000, autopilotReplaced.Resource.MaxThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        [Ignore] // Not currently working with emulator
        public async Task ReadFixedWithAutopilotTests()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerCore autopilotContainer = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                new AutopilotThroughputProperties(5000));
            Assert.IsNotNull(autopilotContainer);

            // Reading a autopilot container with fixed results 
            int? throughput = await autopilotContainer.ReadThroughputAsync();
            Assert.IsNotNull(throughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ReadAutoPilotWithFixedTests()
        {
            Database database = await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerCore fixedContainer = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                throughput: 1000);
            Assert.IsNotNull(fixedContainer);

            int? throughput = await fixedContainer.ReadThroughputAsync();
            Assert.AreEqual(1000, throughput);

            // Reading a fixed container with autopilot results in max throughput being null
            AutopilotThroughputResponse autopilot = await fixedContainer.ReadAutopilotThroughputAsync();
            Assert.IsNotNull(autopilot);
            Assert.IsNotNull(autopilot.Resource.Throughput);
            Assert.AreEqual(1000, autopilot.Resource.Throughput);
            Assert.IsNull(autopilot.Resource.MaxThroughput);

            await database.DeleteAsync();
        }
    }
}
