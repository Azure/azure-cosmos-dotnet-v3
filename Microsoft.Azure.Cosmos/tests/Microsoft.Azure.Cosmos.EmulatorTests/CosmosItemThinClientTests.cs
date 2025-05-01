//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
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

        private const int ItemCount = 1000;

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
            string uniqueDbName = "TestDb_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainer_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/partitionKey");
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

        private IEnumerable<dynamic> GenerateItems(string partitionKey)
        {
            List<dynamic> items = new List<dynamic>();
            for (int i = 0; i < ItemCount; i++)
            {
                items.Add(new
                {
                    id = Guid.NewGuid().ToString(),
                    partitionKey,
                    name = "Test Item " + i
                });
            }

            return items;
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTest()
        {
            string pk = "pk_create";
            IEnumerable<dynamic> items = this.GenerateItems(pk);

            foreach (dynamic item in items)
            {
                ItemResponse<dynamic> response = await this.container.CreateItemAsync(item, new PartitionKey(item.partitionKey));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItemsTest()
        {
            string pk = "pk_read";
            List<dynamic> items = this.GenerateItems(pk).ToList();

            foreach (dynamic item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.partitionKey));
            }

            foreach (dynamic item in items)
            {
                ItemResponse<dynamic> response = await this.container.ReadItemAsync<dynamic>(item.id, new PartitionKey(item.partitionKey));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(item.id, response.Resource.id);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemsTest()
        {
            string pk = "pk_replace";
            List<dynamic> items = this.GenerateItems(pk).ToList();

            foreach (dynamic item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.partitionKey));
            }

            foreach (dynamic item in items)
            {
                dynamic updatedItem = new
                {
                    item.id,
                    item.partitionKey,
                    name = "Updated " + item.name
                };

                ItemResponse<dynamic> response = await this.container.ReplaceItemAsync(updatedItem, updatedItem.id, new PartitionKey(updatedItem.partitionKey));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual("Updated " + item.name, response.Resource.name);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task UpsertItemsTest()
        {
            string pk = "pk_upsert";
            IEnumerable<dynamic> items = this.GenerateItems(pk);

            foreach (dynamic item in items)
            {
                ItemResponse<dynamic> response = await this.container.UpsertItemAsync(item, new PartitionKey(item.partitionKey));
                Assert.IsTrue(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task DeleteItemsTest()
        {
            string pk = "pk_delete";
            List<dynamic> items = this.GenerateItems(pk).ToList();

            foreach (dynamic item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.partitionKey));
            }

            foreach (dynamic item in items)
            {
                ItemResponse<dynamic> response = await this.container.DeleteItemAsync<dynamic>(item.id, new PartitionKey(item.partitionKey));
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            }
        }
    }
}
