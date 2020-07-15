//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
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
                ThroughputProperties.CreateAutoscaleThroughput(5000)))
            {
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }

            DatabaseInternal database = (DatabaseInlineCore)this.cosmosClient.GetDatabase(databaseId);
            ThroughputResponse autoscale = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.AreEqual(5000, autoscale.Resource.AutoscaleMaxThroughput);

            ThroughputResponse autoscaleReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleThroughput(10000));
            Assert.IsNotNull(autoscaleReplaced);
            Assert.AreEqual(10000, autoscaleReplaced.Resource.AutoscaleMaxThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDropAutoscaleDatabase()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString(),
                ThroughputProperties.CreateAutoscaleThroughput(5000));

            ThroughputResponse autoscale = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.AreEqual(5000, autoscale.Resource.AutoscaleMaxThroughput);

            ThroughputResponse autoscaleReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleThroughput(10000));
            Assert.IsNotNull(autoscaleReplaced);
            Assert.AreEqual(10000, autoscaleReplaced.Resource.AutoscaleMaxThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDropFixedDatabase()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString(),
                ThroughputProperties.CreateManualThroughput(5000));

            ThroughputResponse fixedDatabaseThroughput = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(fixedDatabaseThroughput);
            Assert.AreEqual(5000, fixedDatabaseThroughput.Resource.Throughput);
            Assert.IsNull(fixedDatabaseThroughput.Resource.AutoscaleMaxThroughput);
            Assert.IsNull(fixedDatabaseThroughput.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse fixedReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateManualThroughput(6000));
            Assert.IsNotNull(fixedReplaced);
            Assert.AreEqual(6000, fixedReplaced.Resource.Throughput);
            Assert.IsNull(fixedReplaced.Resource.AutoscaleMaxThroughput);
            Assert.IsNull(fixedReplaced.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse fixedReplacedIfExists = await database.ReplaceThroughputPropertiesIfExistsAsync(
                ThroughputProperties.CreateManualThroughput(7000));
            Assert.IsNotNull(fixedReplacedIfExists);
            Assert.AreEqual(7000, fixedReplacedIfExists.Resource.Throughput);
            Assert.IsNull(fixedReplacedIfExists.Resource.AutoscaleMaxThroughput);
            Assert.IsNull(fixedReplacedIfExists.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task DatabaseAutoscaleIfExistsTest()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString(),
                ThroughputProperties.CreateAutoscaleThroughput(5000));

            Container container = await database.CreateContainerAsync("Test", "/id");
            ContainerInternal containerCore = (ContainerInlineCore)container;

            ThroughputResponse throughputResponse = await database.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(5000, throughputResponse.Resource.AutoscaleMaxThroughput);

            throughputResponse = await database.ReplaceThroughputAsync(
                    ThroughputProperties.CreateAutoscaleThroughput(6000));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(6000, throughputResponse.Resource.AutoscaleMaxThroughput);

            throughputResponse = await containerCore.ReadThroughputIfExistsAsync(
                requestOptions: null,
                default(CancellationToken));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            throughputResponse = await containerCore.ReplaceThroughputIfExistsAsync(
                ThroughputProperties.CreateAutoscaleThroughput(6000),
                requestOptions: null,
                default(CancellationToken));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ContainerAutoscaleIfExistsTest()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString());

            Container container = await database.CreateContainerAsync(
                containerProperties: new ContainerProperties("Test", "/id"),
                throughputProperties: ThroughputProperties.CreateAutoscaleThroughput(5000));
            ContainerInternal containerCore = (ContainerInlineCore)container;

            ThroughputResponse throughputResponse = await database.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            throughputResponse = await database.ReplaceThroughputPropertiesIfExistsAsync(
                ThroughputProperties.CreateAutoscaleThroughput(6000));
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, throughputResponse.StatusCode);
            Assert.IsNull(throughputResponse.Resource);

            throughputResponse = await containerCore.ReadThroughputIfExistsAsync(
                requestOptions: null,
                default(CancellationToken));
            Assert.IsNotNull(throughputResponse);
            Assert.IsTrue(throughputResponse.Resource.Throughput > 400);
            Assert.AreEqual(5000, throughputResponse.Resource.AutoscaleMaxThroughput);

            throughputResponse = await containerCore.ReplaceThroughputIfExistsAsync(
                ThroughputProperties.CreateAutoscaleThroughput(6000),
                requestOptions: null,
                default(CancellationToken));
            Assert.IsNotNull(throughputResponse);
            Assert.IsTrue(throughputResponse.Resource.Throughput > 400);
            Assert.AreEqual(6000, throughputResponse.Resource.AutoscaleMaxThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ContainerBuilderAutoscaleTest()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleDatabase) + Guid.NewGuid().ToString());

            {
                Container container = await database.DefineContainer("Test", "/id")
                    .CreateAsync(throughputProperties: ThroughputProperties.CreateAutoscaleThroughput(5000));

                ThroughputProperties throughputProperties = await container.ReadThroughputAsync(requestOptions: null);
                Assert.IsNotNull(throughputProperties);
                Assert.IsTrue(throughputProperties.Throughput > 400);
                Assert.AreEqual(5000, throughputProperties.AutoscaleMaxThroughput);
            }

            {
                Container container2 = await database.DefineContainer("Test2", "/id")
                    .CreateIfNotExistsAsync(throughputProperties: ThroughputProperties.CreateAutoscaleThroughput(5000));

                ThroughputProperties throughputProperties = await container2.ReadThroughputAsync(requestOptions: null);
                Assert.IsNotNull(throughputProperties);
                Assert.IsTrue(throughputProperties.Throughput > 400);
                Assert.AreEqual(5000, throughputProperties.AutoscaleMaxThroughput);


                container2 = await database.DefineContainer(container2.Id, "/id")
                        .CreateIfNotExistsAsync(throughputProperties: ThroughputProperties.CreateAutoscaleThroughput(5000));
                throughputProperties = await container2.ReadThroughputAsync(requestOptions: null);
                Assert.IsNotNull(throughputProperties);
                Assert.IsTrue(throughputProperties.Throughput > 400);
                Assert.AreEqual(5000, throughputProperties.AutoscaleMaxThroughput);
            }

            {
                Container container3 = await database.DefineContainer("Test3", "/id")
                    .CreateAsync(throughputProperties: ThroughputProperties.CreateManualThroughput(500));

                ThroughputProperties throughputProperties = await container3.ReadThroughputAsync(requestOptions: null);
                Assert.IsNotNull(throughputProperties);
                Assert.IsNull(throughputProperties.AutoscaleMaxThroughput);
                Assert.AreEqual(500, throughputProperties.Throughput);

                container3 = await database.DefineContainer(container3.Id, "/id")
                       .CreateIfNotExistsAsync(throughputProperties: ThroughputProperties.CreateManualThroughput(500));
                throughputProperties = await container3.ReadThroughputAsync(requestOptions: null);
                Assert.IsNotNull(throughputProperties);
                Assert.IsNull(throughputProperties.AutoscaleMaxThroughput);
                Assert.AreEqual(500, throughputProperties.Throughput);
            }

            {
                Container container4 = await database.DefineContainer("Test4", "/id")
                    .CreateIfNotExistsAsync(throughputProperties: ThroughputProperties.CreateManualThroughput(500));

                ThroughputProperties throughputProperties = await container4.ReadThroughputAsync(requestOptions: null);
                Assert.IsNotNull(throughputProperties);
                Assert.IsNull(throughputProperties.AutoscaleMaxThroughput);
                Assert.AreEqual(500, throughputProperties.Throughput);
            }

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExistTest()
        {
            string dbName = nameof(CreateDatabaseIfNotExistTest) + Guid.NewGuid();
            DatabaseResponse databaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                dbName,
                ThroughputProperties.CreateAutoscaleThroughput(autoscaleMaxThroughput: 5000));
            Assert.AreEqual(HttpStatusCode.Created, databaseResponse.StatusCode);

            // Container is required to validate database throughput upgrade scenarios
            Container container = await databaseResponse.Database.CreateContainerAsync("Test", "/id");

            ThroughputResponse autoscale = await databaseResponse.Database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.IsNotNull(autoscale.Resource.Throughput);
            Assert.AreEqual(5000, autoscale.Resource.AutoscaleMaxThroughput);

            databaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                dbName,
                ThroughputProperties.CreateAutoscaleThroughput(
                    autoscaleMaxThroughput: 5000));
            Assert.AreEqual(HttpStatusCode.OK, databaseResponse.StatusCode);

            autoscale = await databaseResponse.Database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.IsNotNull(autoscale.Resource.Throughput);
            Assert.AreEqual(5000, autoscale.Resource.AutoscaleMaxThroughput);
        }

        [TestMethod]
        public async Task CreateContainerIfNotExistTest()
        {
            string dbName = nameof(CreateContainerIfNotExistTest) + Guid.NewGuid();
            DatabaseCore database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(dbName);

            ContainerProperties containerProperties = new ContainerProperties("Test", "/id");
            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                containerProperties,
                ThroughputProperties.CreateAutoscaleThroughput(5000));
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            ThroughputResponse autoscale = await containerResponse.Container.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.IsNotNull(autoscale.Resource.Throughput);
            Assert.AreEqual(5000, autoscale.Resource.AutoscaleMaxThroughput);

            containerResponse = await database.CreateContainerIfNotExistsAsync(
                 containerProperties,
                 ThroughputProperties.CreateAutoscaleThroughput(5000));
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropAutoscaleAutoUpgradeDatabase()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                nameof(CreateDropAutoscaleAutoUpgradeDatabase) + Guid.NewGuid(),
                ThroughputProperties.CreateAutoscaleThroughput(
                    maxAutoscaleThroughput: 5000,
                    autoUpgradeMaxThroughputIncrementPercentage: 10));

            // Container is required to validate database throughput upgrade scenarios
            Container container = await database.CreateContainerAsync("Test", "/id");

            ThroughputResponse autoscale = await database.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(autoscale);
            Assert.AreEqual(5000, autoscale.Resource.AutoscaleMaxThroughput);
            Assert.AreEqual(10, autoscale.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse autoscaleReplaced = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleThroughput(6000));
            Assert.IsNotNull(autoscaleReplaced);
            Assert.AreEqual(6000, autoscaleReplaced.Resource.AutoscaleMaxThroughput);
            Assert.IsNull(autoscaleReplaced.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            ThroughputResponse autoUpgradeReplace = await database.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleThroughput(
                    maxAutoscaleThroughput: 7000,
                    autoUpgradeMaxThroughputIncrementPercentage: 20));
            Assert.IsNotNull(autoUpgradeReplace);
            Assert.AreEqual(7000, autoUpgradeReplace.Resource.AutoscaleMaxThroughput);
            Assert.AreEqual(20, autoUpgradeReplace.Resource.AutoUpgradeMaxThroughputIncrementPercentage);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDropAutoscaleContainerStreamApi()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ThroughputResponse databaseThroughput = await database.ReadThroughputIfExistsAsync(requestOptions: null);
            Assert.IsNotNull(databaseThroughput);
            Assert.AreEqual(HttpStatusCode.NotFound, databaseThroughput.StatusCode);

            string streamContainerId = Guid.NewGuid().ToString();

            using (ResponseMessage response = await database.CreateContainerStreamAsync(
                 new ContainerProperties(streamContainerId, "/pk"),
                 ThroughputProperties.CreateAutoscaleThroughput(5000)))
            {
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

                ContainerInternal streamContainer = (ContainerInlineCore)database.GetContainer(streamContainerId);
                ThroughputResponse autoscaleIfExists = await streamContainer.ReadThroughputIfExistsAsync(
                    requestOptions: null,
                    default(CancellationToken));
                Assert.IsNotNull(autoscaleIfExists);
                Assert.AreEqual(5000, autoscaleIfExists.Resource.AutoscaleMaxThroughput);
            }
        }

        [TestMethod]
        public async Task CreateDropAutoscaleContainer()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerInternal container = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                ThroughputProperties.CreateAutoscaleThroughput(5000));
            Assert.IsNotNull(container);

            ThroughputResponse throughputResponse = await container.ReadThroughputAsync(requestOptions: null);
            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(5000, throughputResponse.Resource.AutoscaleMaxThroughput);

            throughputResponse = await container.ReplaceThroughputAsync(
                ThroughputProperties.CreateAutoscaleThroughput(6000),
                requestOptions: null,
                cancellationToken: default(CancellationToken));

            Assert.IsNotNull(throughputResponse);
            Assert.AreEqual(6000, throughputResponse.Resource.AutoscaleMaxThroughput);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ReadFixedWithAutoscaleTests()
        {
            DatabaseInternal database = (DatabaseInlineCore)await this.cosmosClient.CreateDatabaseAsync(
                Guid.NewGuid().ToString());

            ContainerInternal autoscaleContainer = (ContainerInlineCore)await database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                ThroughputProperties.CreateAutoscaleThroughput(5000));
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

            ContainerInternal fixedContainer = (ContainerInlineCore)await database.CreateContainerAsync(
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
            Assert.IsNull(autoscale.Resource.AutoscaleMaxThroughput);

            await database.DeleteAsync();
        }
    }
}
