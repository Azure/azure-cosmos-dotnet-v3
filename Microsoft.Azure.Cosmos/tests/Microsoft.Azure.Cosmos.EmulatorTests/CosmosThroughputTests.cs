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
        public async Task CreateDropAutoscaleDatabaseStreamApi()
        {
            string databaseId = Guid.NewGuid().ToString();
            using (ResponseMessage response = await this.cosmosClient.CreateDatabaseStreamAsync(
                new DatabaseProperties(databaseId),
                ThroughputProperties.CreateAutoscaleProvionedThroughput(5000)))
            {
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }

            DatabaseCore database = (DatabaseInlineCore)this.cosmosClient.GetDatabase(databaseId);
            ThroughputResponse autoscale = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.AreEqual(5000, autoscale.Resource.MaxAutoscaleThroughput);

            ThroughputResponse autoscaleReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(10000));
            Assert.IsNotNull(autoscaleReplaced);
            Assert.AreEqual(10000, autoscaleReplaced.Resource.MaxAutoscaleThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDropAutoscaleDatabase()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString(),
                ThroughputProperties.CreateAutoscaleProvionedThroughput(5000));

            ThroughputResponse autoscale = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.AreEqual(5000, autoscale.Resource.MaxAutoscaleThroughput);

            ThroughputResponse autoscaleReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(10000));
            Assert.IsNotNull(autoscaleReplaced);
            Assert.AreEqual(10000, autoscaleReplaced.Resource.MaxAutoscaleThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDropFixedDatabase()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString(),
                ThroughputProperties.CreateManualThroughput(5000));

            ThroughputResponse fixedDatabaseThroughput = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(fixedDatabaseThroughput);
            Assert.AreEqual(5000, fixedDatabaseThroughput.Resource.Throughput);
            Assert.IsNull(fixedDatabaseThroughput.Resource.MaxAutoscaleThroughput);
            Assert.IsNull(fixedDatabaseThroughput.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse fixedReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateManualThroughput(6000));
            Assert.IsNotNull(fixedReplaced);
            Assert.AreEqual(6000, fixedReplaced.Resource.Throughput);
            Assert.IsNull(fixedReplaced.Resource.MaxAutoscaleThroughput);
            Assert.IsNull(fixedReplaced.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse fixedReplacedIfExists = await database.ReplaceThroughputPropertiesIfExistsAsync(
                ThroughputProperties.CreateManualThroughput(7000));
            Assert.IsNotNull(fixedReplacedIfExists);
            Assert.AreEqual(7000, fixedReplacedIfExists.Resource.Throughput);
            Assert.IsNull(fixedReplacedIfExists.Resource.MaxAutoscaleThroughput);
            Assert.IsNull(fixedReplacedIfExists.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task DatabaseAutoscaleIfExistsTest()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString(),
                ThroughputProperties.CreateAutoscaleProvionedThroughput(5000));

            Container container = await database.CreateContainerAsync("Test", "/id");
            ContainerCore containerCore = (ContainerInlineCore)container;

            ThroughputResponse throughputResponse = await database.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(5000, throughputResponse.Resource.MaxAutoscaleThroughput);

            throughputResponse = await database.ReplaceThroughputAsync(
                    ThroughputProperties.CreateAutoscaleProvionedThroughput(6000));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(6000, throughputResponse.Resource.MaxAutoscaleThroughput);

            throughputResponse = await containerCore.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            throughputResponse = await containerCore.ReplaceThroughputPropertiesIfExistsAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(6000));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            await database.DeleteAsync();
        }

        [TestMethod]
        [TestCategory("Quarantine")] // Not currently working with emulator
        public async Task ContainerAutoscaleIfExistsTest()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString());

            Container container = await database.CreateContainerAsync(
                containerProperties: new ContainerProperties("Test", "/id"),
                throughputProperties: ThroughputProperties.CreateAutoscaleProvionedThroughput(5000));
            ContainerCore containerCore = (ContainerInlineCore)container;

            ThroughputResponse throughputResponse = await database.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            throughputResponse = await database.ReplaceThroughputPropertiesIfExistsAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(6000));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            throughputResponse = await containerCore.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.IsTrue(throughputResponse.Resource.Throughput > 400);
            Assert.AreEqual(5000, throughputResponse.Resource.MaxAutoscaleThroughput);

            throughputResponse = await containerCore.ReplaceThroughputPropertiesIfExistsAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(6000));
            Assert.IsNotNull(throughputResponse);
            Assert.IsTrue(throughputResponse.Resource.Throughput > 400);
            Assert.AreEqual(6000, throughputResponse.Resource.MaxAutoscaleThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDropAutoscaleAutoUpgradeDatabase()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleAutoUpgradeDatabase) + Guid.NewGuid(),
                ThroughputProperties.CreateAutoscaleProvionedThroughput(
                    maxAutoscaleThroughput: 5000,
                    autoUpgradeMaxThroughputIncrementPercentage: 10));

            // Container is required to validate database throughput upgrade scenarios
            Container container = await database.CreateContainerAsync("Test", "/id");

            ThroughputResponse autoscale = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.AreEqual(5000, autoscale.Resource.MaxAutoscaleThroughput);
            Assert.AreEqual(10, autoscale.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse autoscaleReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(6000));
            Assert.IsNotNull(autoscaleReplaced);
            Assert.AreEqual(6000, autoscaleReplaced.Resource.MaxAutoscaleThroughput);
            Assert.IsNull(autoscaleReplaced.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse autoUpgradeReplace = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(
                    maxAutoscaleThroughput: 7000,
                    autoUpgradeMaxThroughputIncrementPercentage: 20));
            Assert.IsNotNull(autoUpgradeReplace);
            Assert.AreEqual(7000, autoUpgradeReplace.Resource.MaxAutoscaleThroughput);
            Assert.AreEqual(20, autoUpgradeReplace.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            await database.DeleteAsync();
        }

        [TestMethod]
        [TestCategory("Quarantine")] // Not currently working with emulator
        public async Task CreateDropAutoscaleContainerStreamApi()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ThroughputResponse databaseThroughput = await database.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(databaseThroughput);
            Assert.AreEqual(HttpStatusCode.NotFound, databaseThroughput.StatusCode);

            string streamContainerId = Guid.NewGuid().ToString();

            using (ResponseMessage response = await database.CreateContainerStreamAsync(
                 new ContainerProperties(streamContainerId, "/pk"),
                 ThroughputProperties.CreateAutoscaleProvionedThroughput(5000)))
            {
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                ContainerCore streamContainer = (ContainerInlineCore)database.GetContainer(streamContainerId);
                ThroughputResponse autoscaleIfExists = await streamContainer.ReadThroughputIfExistsAsync(requestOptions: null);
                Assert.IsNotNull(autoscaleIfExists);
                Assert.AreEqual(5000, autoscaleIfExists.Resource.MaxAutoscaleThroughput);
            }
        }

        [TestMethod]
        [TestCategory("Quarantine")] // Not currently working with emulator
        public async Task CreateDropAutoscaleContainer()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerCore container = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                ThroughputProperties.CreateAutoscaleProvionedThroughput(5000));
            Assert.IsNotNull(container);

            ThroughputResponse throughputResponse = await container.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(5000, throughputResponse.Resource.MaxAutoscaleThroughput);

            throughputResponse = await container.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleProvionedThroughput(6000));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(6000, throughputResponse.Resource.MaxAutoscaleThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        [TestCategory("Quarantine")] // Not currently working with emulator
        public async Task ReadFixedWithAutoscaleTests()
        {
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerCore autoscaleContainer = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                ThroughputProperties.CreateAutoscaleProvionedThroughput(5000));
            Assert.IsNotNull(autoscaleContainer);

            // Reading a autoscale container with fixed results 
            int? throughput = await autoscaleContainer.ReadThroughputAsync();
            Assert.IsNotNull(throughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ReadAutoscaleWithFixedTests()
        {
            Database database = await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerCore fixedContainer = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                throughput: 1000);
            Assert.IsNotNull(fixedContainer);

            int? throughput = await fixedContainer.ReadThroughputAsync();
            Assert.AreEqual(1000, throughput);

            // Reading a fixed container with autoscale results in max throughput being null
            ThroughputResponse autoscale = await fixedContainer.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.IsNotNull(autoscale.Resource.Throughput);
            Assert.AreEqual(1000, autoscale.Resource.Throughput);
            Assert.IsNull(autoscale.Resource.MaxAutoscaleThroughput);

            await database.DeleteAsync();
        }
    }
}
