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

        /// <summary>
        /// Tests that the user agent suffix can be used without special characters.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("nalutripician")]
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
        [Owner("nalutripician")]
        /// <summary>
        /// Tests that the user agent suffix can contain special characters.
        /// User Agent Names should support any ascii character except for control characters.
        /// </summary>
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

        [TestMethod]
        [Owner("nalutripician")]
        /// <summary>
        /// Tests that the user agent suffix can contain special characters.
        /// User Agent Names should support any unicode character except for control characters.
        /// </summary>
        public async Task UserAgentSuffixWithUnicodeCharacter()
        {
            (string endpoint, string key) = TestCommon.GetAccountInfo();
            CosmosClient clientWithUserAgentSuffix = new CosmosClientBuilder(endpoint, key)
                .WithApplicationName("UnicodeChar鱀InUserAgent")
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
        [Owner("nalutripician")]
        /// <summary>
        /// Tests that the user agent suffix can contain special characters.
        /// User Agent Names should support any ascii character except for control characters.
        /// </summary>
        public async Task UserAgentSuffixWithWhitespaceAndAsciiCharacter()
        {
            (string endpoint, string key) = TestCommon.GetAccountInfo();
            CosmosClient clientWithUserAgentSuffix = new CosmosClientBuilder(endpoint, key)
                .WithApplicationName("UserAgent with space<>\"{}\\[];:@=(),$%_^()*&")
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