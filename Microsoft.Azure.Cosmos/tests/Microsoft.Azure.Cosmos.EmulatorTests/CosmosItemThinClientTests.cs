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
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
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
        private MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private const int ItemCount = 100;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
            this.connectionString = Environment.GetEnvironmentVariable("COSMOSDB_THINCLIENT");

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

            if (this.client != null)
            {
                this.client.Dispose();
            }
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

        private async Task<List<TestObject>> CreateItemsSafeAsync(IEnumerable<TestObject> items)
        {
            List<TestObject> itemsCreated = new List<TestObject>();
            foreach (TestObject item in items)
            {
                try
                {
                    ItemResponse<TestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        itemsCreated.Add(item);
                    }
                }
                catch (CosmosException)
                {
                }
            }
            return itemsCreated;
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task HttpRequestVersionIsTwoPointZeroWhenUsingThinClientMode()
        {
            Version expectedGatewayVersion = new(1, 1);
            Version expectedThinClientVersion = new(2, 0);

            List<Version> postRequestVersions = new();

            CosmosClientBuilder builder = new CosmosClientBuilder(this.connectionString)
                .WithConnectionModeGateway()
                .WithSendingRequestEventArgs((sender, e) =>
                {
                    if (e.HttpRequest.Method == HttpMethod.Post)
                    {
                        postRequestVersions.Add(e.HttpRequest.Version);
                    }
                });

            using CosmosClient client = builder.Build();

            string dbId = "HttpVersionTestDb_" + Guid.NewGuid();
            Cosmos.Database database = await client.CreateDatabaseIfNotExistsAsync(dbId);
            Container container = await database.CreateContainerIfNotExistsAsync("HttpVersionTestContainer", "/pk");

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();

            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response);

            Assert.AreEqual(3, postRequestVersions.Count, "Expected exactly 3 POST requests (DB, Container, Item).");

            Assert.AreEqual(expectedGatewayVersion, postRequestVersions[0], "Expected HTTP/1.1 for CreateDatabaseAsync.");
            Assert.AreEqual(expectedGatewayVersion, postRequestVersions[1], "Expected HTTP/1.1 for CreateContainerAsync.");
            Assert.AreEqual(expectedThinClientVersion, postRequestVersions[2], "Expected HTTP/2.0 for CreateItemAsync.");

            await database.DeleteAsync();
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
                string diagnostics = response.Diagnostics.ToString();
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTestWithThinClientFlagEnabledAndAccountDisabled()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
            string connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", string.Empty);

            if (string.IsNullOrEmpty(connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            this.client = new CosmosClient(
                  connectionString,
                  new CosmosClientOptions()
                  {
                      ConnectionMode = ConnectionMode.Gateway,
                      Serializer = this.cosmosSystemTextJsonSerializer,
                  });

            string uniqueDbName = "TestDb2_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainer2_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

            string pk = "pk_create";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                string diagnostics = response.Diagnostics.ToString();
                Assert.IsFalse(diagnostics.Contains("|F4"), "Diagnostics User Agent should NOT contain '|F4' for Gateway");
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTestWithDirectMode_ThinClientFlagEnabledAndAccountEnabled()
        {
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
                      ConnectionMode = ConnectionMode.Direct,
                      Serializer = this.cosmosSystemTextJsonSerializer,
                  });

            string uniqueDbName = "TestDb2_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainer2_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

            string pk = "pk_create";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                JsonDocument doc = JsonDocument.Parse(response.Diagnostics.ToString());
                string connectionMode = doc.RootElement
                    .GetProperty("data")
                    .GetProperty("Client Configuration")
                    .GetProperty("ConnectionMode")
                    .GetString();

                Assert.AreEqual("Direct", connectionMode, "Diagnostics should have ConnectionMode set to 'Direct'");
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTestWithThinClientFlagDisabledAccountEnabled()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");

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

            string uniqueDbName = "TestDbTCDisabled_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainerTCDisabled_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

            string pk = "pk_create";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                string diagnostics = response.Diagnostics.ToString();
                Assert.IsFalse(diagnostics.Contains("|F4"), "Diagnostics User Agent should NOT contain '|F4' for Gateway");
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItemsTest()
        {
            string pk = "pk_read";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                ItemResponse<TestObject> response = await this.container.ReadItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                string diagnostics = response.Diagnostics.ToString();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(item.Id, response.Resource.Id);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemsTest()
        {
            string pk = "pk_replace";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                TestObject updatedItem = new TestObject
                {
                    Id = item.Id,
                    Pk = item.Pk,
                    Other = "Updated " + item.Other
                };

                ItemResponse<TestObject> response = await this.container.ReplaceItemAsync(updatedItem, updatedItem.Id, new PartitionKey(updatedItem.Pk));
                string diagnostics = response.Diagnostics.ToString();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual("Updated " + item.Other, response.Resource.Other);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
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
                string diagnostics = response.Diagnostics.ToString();
                Assert.IsTrue(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task DeleteItemsTest()
        {
            string pk = "pk_delete";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                ItemResponse<TestObject> response = await this.container.DeleteItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                string diagnostics = response.Diagnostics.ToString();
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
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
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                        Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
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

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                using (ResponseMessage response = await this.container.ReadItemStreamAsync(item.Id, new PartitionKey(item.Pk)))
                {
                    string diagnostics = response.Diagnostics.ToString();
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemStreamTest()
        {
            string pk = "pk_replace_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
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
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
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
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.IsTrue(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
                        Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
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

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                using (ResponseMessage response = await this.container.DeleteItemStreamAsync(item.Id, new PartitionKey(item.Pk)))
                {
                    string diagnostics = response.Diagnostics.ToString();
                    Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsTest()
        {
            string pk = "pk_query";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            string query = $"SELECT * FROM c WHERE c.pk = '{pk}'";
            FeedIterator<TestObject> iterator = this.container.GetItemQueryIterator<TestObject>(query);

            int count = 0;
            while (iterator.HasMoreResults)
            {
                FeedResponse<TestObject> response = await iterator.ReadNextAsync();
                count += response.Count;
            }

            Assert.AreEqual(createdItems.Count, count);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsStreamTest()
        {
            string pk = "pk_query_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.pk = @pk").WithParameter("@pk", pk);
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

            Assert.AreEqual(createdItems.Count, count);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task BulkCreateItemsTest()
        {
            CosmosClient bulkClient = new CosmosClient(
                this.connectionString,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.cosmosSystemTextJsonSerializer,
                    AllowBulkExecution = true,
                });

            string pk = "pk_bulk";
            List<TestObject> items = this.GenerateItems(pk).ToList();
            List<Task<ItemResponse<TestObject>>> tasks = new List<Task<ItemResponse<TestObject>>>();

            Container bulkContainer = bulkClient.GetContainer(this.database.Id, this.container.Id);

            foreach (TestObject item in items)
            {
                tasks.Add(bulkContainer.CreateItemAsync(item, new PartitionKey(item.Pk)));
            }

            await Task.WhenAll(tasks);

            foreach (Task<ItemResponse<TestObject>> task in tasks)
            {
                Assert.AreEqual(HttpStatusCode.Created, task.Result.StatusCode);
            }

            bulkClient.Dispose();
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TransactionalBatchCreateItemsTest()
        {
            string pk = "pk_batch";
            List<TestObject> items = this.GenerateItems(pk).Take(100).ToList();

            TransactionalBatch batch = this.container.CreateTransactionalBatch(new PartitionKey(pk));

            foreach (TestObject item in items)
            {
                batch.CreateItem(item);
            }

            TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();
            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            for (int i = 0; i < items.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Created, batchResponse[i].StatusCode);
            }
        }
    }
}