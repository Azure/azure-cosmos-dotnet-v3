//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Drawing;
    using System.Net;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosItemThinClientTests
    {
        private string connectionString;
        private CosmosClient client;
        private Database database;
        private Container container;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = Environment.GetEnvironmentVariable("COSMOSDB_CONNECTION_STRING");
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_CONNECTION_STRING to run the tests");
            }

            this.client = new CosmosClient(this.connectionString);
            this.database = await this.client.CreateDatabaseIfNotExistsAsync("TestDatabase");
            this.container = await this.database.CreateContainerIfNotExistsAsync("TestContainer", "/partitionKey");
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }

            this.client?.Dispose();
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemTest()
        {
            // Arrange
            dynamic testItem = new { id = Guid.NewGuid().ToString(), partitionKey = "pk1", name = "Test Item" };

            // Act
            ItemResponse<dynamic> response = await this.container.CreateItemAsync(testItem, new PartitionKey(testItem.partitionKey));

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual(testItem.id, response.Resource.id);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemTest()
        {
            // Arrange
            dynamic testItem = new { id = Guid.NewGuid().ToString(), partitionKey = "pk1", name = "Test Item" };
            await this.container.CreateItemAsync(testItem, new PartitionKey(testItem.partitionKey));

            dynamic updatedItem = new { testItem.id, partitionKey = "pk1", name = "Updated Item" };

            // Act
            ItemResponse<dynamic> response = await this.container.ReplaceItemAsync(updatedItem, updatedItem.id, new PartitionKey(updatedItem.partitionKey));

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(updatedItem.name, response.Resource.name);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task DeleteItemTest()
        {
            // Arrange
            dynamic testItem = new { id = Guid.NewGuid().ToString(), partitionKey = "pk1", name = "Test Item" };
            await this.container.CreateItemAsync(testItem, new PartitionKey(testItem.partitionKey));

            // Act
            ItemResponse<dynamic> response = await this.container.DeleteItemAsync<dynamic>(testItem.id, new PartitionKey(testItem.partitionKey));

            // Assert
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItemTest()
        {
            // Arrange
            dynamic testItem = new { id = Guid.NewGuid().ToString(), partitionKey = "pk1", name = "Test Item" };
            await this.container.CreateItemAsync(testItem, new PartitionKey(testItem.partitionKey));

            // Act
            ItemResponse<dynamic> response = await this.container.ReadItemAsync<dynamic>(testItem.id, new PartitionKey(testItem.partitionKey));

            // Assert
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(testItem.id, response.Resource.id);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task UpsertItemTest()
        {
            // Arrange
            dynamic testItem = new { id = Guid.NewGuid().ToString(), partitionKey = "pk1", name = "Test Item" };

            // Act
            ItemResponse<dynamic> response = await this.container.UpsertItemAsync(testItem, new PartitionKey(testItem.partitionKey));

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.AreEqual(testItem.id, response.Resource.id);
        }
    }
}
