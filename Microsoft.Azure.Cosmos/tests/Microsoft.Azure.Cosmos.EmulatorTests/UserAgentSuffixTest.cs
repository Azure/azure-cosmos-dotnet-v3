//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Database = Database;

    [TestClass]
    public class UserAgentSuffixTest
    {

        private CosmosClient cosmosClient;
        private string databaseName;
        private string containerName;
        private Database database;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient(
                new CosmosClientOptions()
                {
                    EnableContentResponseOnWrite = true
                });

            this.databaseName = Guid.NewGuid().ToString();
            this.containerName = Guid.NewGuid().ToString();

            this.database = await this.cosmosClient.CreateDatabaseAsync(this.databaseName);
            await this.database.CreateContainerAsync(this.containerName, "/pk");

        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            await this.database.DeleteAsync();

            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task UserAgentSuffixWithoutSpecialCharacter()
        {
            (string endpoint, string key) = TestCommon.GetAccountInfo();
            CosmosClient clientWithUserAgentSuffix = new CosmosClientBuilder(endpoint, key)
                .WithApplicationName("TestUserAgent")
                .Build();

            ContainerResponse response = await clientWithUserAgentSuffix
                .GetDatabase(this.databaseName)
                .GetContainer(this.containerName)
                .ReadContainerAsync();

            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource.Id);
            Assert.AreEqual(this.containerName, response.Resource.Id);
        }

        [TestMethod]
        public async Task UserAgentSuffixWithSpecialCharacter()
        {
            (string endpoint, string key) = TestCommon.GetAccountInfo();
            CosmosClient clientWithUserAgentSuffix = new CosmosClientBuilder(endpoint, key)
                .WithApplicationName("TéstUserAgent")
                .Build();

            ContainerResponse response = await clientWithUserAgentSuffix
                .GetDatabase(this.databaseName)
                .GetContainer(this.containerName)
                .ReadContainerAsync();

            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource.Id);
            Assert.AreEqual(this.containerName, response.Resource.Id);
        }
    }
}