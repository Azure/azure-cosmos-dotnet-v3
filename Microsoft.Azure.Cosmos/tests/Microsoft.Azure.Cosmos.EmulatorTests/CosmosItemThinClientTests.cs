//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;
    using TestObject = MultiRegionSetupHelpers.CosmosIntegrationTestObject;

    [TestClass]
    public class CosmosItemThinClientTests
    {
        private string connectionString;
        private CosmosClient client;
        private Database database;
        private Container container;
        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;

        private const int ItemCount = 1000;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = Environment.GetEnvironmentVariable("COSMOSDB_THINCLIENT");
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_THINCLIENT to run the tests");
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            this.client = new CosmosClient(
                  this.connectionString,
                  new CosmosClientOptions()
                  {
                      ConnectionMode = ConnectionMode.Gateway,
                      Serializer = this.cosmosSystemTextJsonSerializer,
                  });

            string uniqueDbName = "TestDb_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainer_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");
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

        private IEnumerable<TestObject> GenerateItems(string partitionKey)
        {
            List<TestObject> items = new List<TestObject>();
            for (int i = 0; i < ItemCount; i++)
            {
                items.Add(new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = partitionKey,
                    Other = "Test Item " + i
                });
            }

            return items;
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTest()
        {
            string pk = "pk_create";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItemsTest()
        {
            string pk = "pk_read";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (TestObject item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }

            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.ReadItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(item.Id, response.Resource.Id);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemsTest()
        {
            string pk = "pk_replace";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (TestObject item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }

            foreach (TestObject item in items)
            {
                TestObject updatedItem = new TestObject
                {
                    Id = item.Id,
                    Pk = item.Pk,
                    Other = "Updated " + item.Other
                };

                ItemResponse<TestObject> response = await this.container.ReplaceItemAsync(updatedItem, updatedItem.Id, new PartitionKey(updatedItem.Pk));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual("Updated " + item.Other, response.Resource.Other);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task UpsertItemsTest()
        {
            string pk = "pk_upsert";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.UpsertItemAsync(item, new PartitionKey(item.Pk));
                Assert.IsTrue(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task DeleteItemsTest()
        {
            string pk = "pk_delete";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (TestObject item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }

            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.DeleteItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemStreamTest()
        {
            string pk = "pk_create_stream";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            foreach (TestObject item in items)
            {
                using (Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(item))
                {
                    using (ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(item.Pk)))
                    {
                        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItemStreamTest()
        {
            string pk = "pk_read_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (TestObject item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }

            foreach (TestObject item in items)
            {
                using (ResponseMessage response = await this.container.ReadItemStreamAsync(item.Id, new PartitionKey(item.Pk)))
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemStreamTest()
        {
            string pk = "pk_replace_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (TestObject item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }

            foreach (TestObject item in items)
            {
                TestObject updatedItem = new TestObject
                {
                    Id = item.Id,
                    Pk = item.Pk,
                    Other = "Updated " + item.Other
                };

                using (Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(updatedItem))
                {
                    using (ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, updatedItem.Id, new PartitionKey(updatedItem.Pk)))
                    {
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task UpsertItemStreamTest()
        {
            string pk = "pk_upsert_stream";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            foreach (TestObject item in items)
            {
                using (Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(item))
                {
                    using (ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, new PartitionKey(item.Pk)))
                    {
                        Assert.IsTrue(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task DeleteItemStreamTest()
        {
            string pk = "pk_delete_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (TestObject item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }

            foreach (TestObject item in items)
            {
                using (ResponseMessage response = await this.container.DeleteItemStreamAsync(item.Id, new PartitionKey(item.Pk)))
                {
                    Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsTest()
        {
            string pk = "pk_query";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (TestObject item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
            }

            string query = $"SELECT * FROM c WHERE c.partitionKey = '{pk}'";
            FeedIterator<TestObject> iterator = this.container.GetItemQueryIterator<TestObject>(query);

            int count = 0;
            while (iterator.HasMoreResults)
            {
                FeedResponse<TestObject> response = await iterator.ReadNextAsync();
                count += response.Count;
            }

            Assert.AreEqual(ItemCount, count);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsStreamTest()
        {
            string pk = "pk_query_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            foreach (dynamic item in items)
            {
                await this.container.CreateItemAsync(item, new PartitionKey(item.partitionKey));
            }

            QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.partitionKey = @pk").WithParameter("@pk", pk);
            FeedIterator iterator = this.container.GetItemQueryStreamIterator(query);

            int count = 0;
            while (iterator.HasMoreResults)
            {
                using (ResponseMessage response = await iterator.ReadNextAsync())
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                    using (StreamReader reader = new StreamReader(response.Content))
                    {
                        string json = await reader.ReadToEndAsync();
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            count += doc.RootElement.GetProperty("Documents").GetArrayLength();
                        }
                    }
                }
            }

            Assert.AreEqual(ItemCount, count);
        }
    }
}