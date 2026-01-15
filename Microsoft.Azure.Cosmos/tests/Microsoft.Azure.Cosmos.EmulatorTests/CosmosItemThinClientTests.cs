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
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
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
        public async Task RegionalDatabaseAccountNameIsEmptyInPayload()
        {
            byte[] capturedPayload = null;
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            // Initialize the serializer locally
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            CosmosSystemTextJsonSerializer serializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            CosmosClientBuilder builder = new CosmosClientBuilder(this.connectionString)
                .WithConnectionModeGateway()
                .WithCustomSerializer(serializer)
                .WithSendingRequestEventArgs(async (sender, e) =>
                {
                    if (e.HttpRequest.Version == new Version(2, 0))
                    {
                        if (e.HttpRequest.Content != null)
                        {
                            capturedPayload = await e.HttpRequest.Content.ReadAsByteArrayAsync();
                        }
                    }
                });

            using CosmosClient client = builder.Build();
            string uniqueDbName = "TestRegional_" + Guid.NewGuid().ToString();
            Database database = await client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestRegionalContainer_" + Guid.NewGuid().ToString();
            Container container = await database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

            string pk = "pk_regional";
            TestObject testItem = this.GenerateItems(pk).First();

            // Act
            ItemResponse<TestObject> response = await container.CreateItemAsync(testItem, new PartitionKey(testItem.Pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            // Assert
            Assert.IsNotNull(capturedPayload, "The request payload was not captured.");


            // The RNTBD protocol serializes an empty string as a token with a length of 0.
            // For `regionalDatabaseAccountName`, which is a SmallString (type 0x02), this is
            // serialized as two bytes: 0x02 (type) and 0x00 (length).
            // This byte pair represents an empty string value in RNTBD’s small-string encoding.
            byte[] emptyStringToken = { 0x02, 0x00 };

            bool foundEmptyStringToken = false;
            for (int i = 0; i <= capturedPayload.Length - emptyStringToken.Length; i++)
            {
                if (capturedPayload[i] == emptyStringToken[0] && capturedPayload[i + 1] == emptyStringToken[1])
                {
                    foundEmptyStringToken = true;
                    break;
                }
            }

            Assert.IsTrue(foundEmptyStringToken, "The RNTBD payload should contain a token representing an empty string for the regional account name.");

            // Cleanup
            await database.DeleteAsync();
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestThinClientWithExecuteStoredProcedureAsync()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "true");

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

            string uniqueDbName = "TestDbStoreProc_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestDbStoreProcContainer_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");


            string sprocId = "testSproc_" + Guid.NewGuid().ToString();
            string sprocBody = @"function(itemToCreate) {
            var context = getContext();
            var collection = context.getCollection();
            var response = context.getResponse();
        
            if (!itemToCreate) throw new Error('Item is undefined or null.');
        
            // Create a document
            var accepted = collection.createDocument(
                collection.getSelfLink(),
                itemToCreate,
                function(err, newItem) {
                    if (err) throw err;
                
                    // Query the created document
                    var query = 'SELECT * FROM c WHERE c.id = ""' + newItem.id + '""';
                    var isAccepted = collection.queryDocuments(
                        collection.getSelfLink(),
                        query,
                        function(queryErr, documents) {
                            if (queryErr) throw queryErr;
                            response.setBody({
                                created: newItem,
                                queried: documents[0]
                            });
                        }
                    );
                    if (!isAccepted) throw 'Query not accepted';
                });
        
            if (!accepted) throw new Error('Create was not accepted.');
        }";

            // Create stored procedure
            Scripts.StoredProcedureResponse createResponse = await this.container.Scripts.CreateStoredProcedureAsync(
                new Scripts.StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            // Execute stored procedure
            string testPartitionId = Guid.NewGuid().ToString();
            TestObject testItem = new TestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = testPartitionId,
                Other = "Created by Stored Procedure"
            };

            Scripts.StoredProcedureExecuteResponse<dynamic> executeResponse =
                await this.container.Scripts.ExecuteStoredProcedureAsync<dynamic>(
                    sprocId,
                    new PartitionKey(testPartitionId),
                    new dynamic[] { testItem });

            Assert.AreEqual(HttpStatusCode.OK, executeResponse.StatusCode);
            Assert.IsNotNull(executeResponse.Resource);
            string diagnostics = executeResponse.Diagnostics.ToString();
            Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");

            // Delete stored procedure
            await this.container.Scripts.DeleteStoredProcedureAsync(sprocId);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestThinClientWithExecuteStoredProcedureStreamAsync()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "true");

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

            string uniqueDbName = "TestDbStoreProc_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestDbStoreProcContainer_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");


            string sprocId = "testSproc_" + Guid.NewGuid().ToString();
            string sprocBody = @"function(itemToCreate) {
            var context = getContext();
            var collection = context.getCollection();
            var response = context.getResponse();
        
            if (!itemToCreate) throw new Error('Item is undefined or null.');
        
            // Create a document
            var accepted = collection.createDocument(
                collection.getSelfLink(),
                itemToCreate,
                function(err, newItem) {
                    if (err) throw err;
                
                    // Query the created document
                    var query = 'SELECT * FROM c WHERE c.id = ""' + newItem.id + '""';
                    var isAccepted = collection.queryDocuments(
                        collection.getSelfLink(),
                        query,
                        function(queryErr, documents) {
                            if (queryErr) throw queryErr;
                            response.setBody({
                                created: newItem,
                                queried: documents[0]
                            });
                        }
                    );
                    if (!isAccepted) throw 'Query not accepted';
                });
        
            if (!accepted) throw new Error('Create was not accepted.');
        }";

            // Create stored procedure
            Scripts.StoredProcedureResponse createResponse = await this.container.Scripts.CreateStoredProcedureAsync(
                new Scripts.StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            // Execute stored procedure
            string testPartitionId = Guid.NewGuid().ToString();
            TestObject testItem = new TestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = testPartitionId,
                Other = "Created by Stored Procedure"
            };

            using (ResponseMessage executeResponse =
                await this.container.Scripts.ExecuteStoredProcedureStreamAsync(
                    sprocId,
                    new PartitionKey(testPartitionId),
                    new dynamic[] { testItem }))
            {
                Assert.AreEqual(HttpStatusCode.OK, executeResponse.StatusCode);
                Assert.IsNotNull(executeResponse.Content);
                string diagnostics = executeResponse.Diagnostics.ToString();
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
            }

            // Delete stored procedure
            await this.container.Scripts.DeleteStoredProcedureAsync(sprocId);
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
            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
            AzureKeyCredential masterKeyCredential = new AzureKeyCredential(authKey);

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            this.client = new CosmosClient(
                  endpoint,
                  masterKeyCredential,
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
        public async Task QueryItemsTestWithStrongConsistency()
        {
            string connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_THINCLIENTSTRONG", string.Empty);
            if (string.IsNullOrEmpty(connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_THINCLIENTSTRONG to run the tests");
            }
            this.client = new CosmosClient(
                 connectionString,
                 new CosmosClientOptions()
                 {
                     ConnectionMode = ConnectionMode.Gateway,
                     RequestTimeout = TimeSpan.FromSeconds(60),
                     ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.Strong
                 });

            string uniqueDbName = "TestDbTC_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainerTC_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

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
        public async Task QueryItemsTestWithSessionConsistency()
        {
            this.client = new CosmosClient(
                 this.connectionString,
                 new CosmosClientOptions()
                 {
                     ConnectionMode = ConnectionMode.Gateway,
                     RequestTimeout = TimeSpan.FromSeconds(60),
                     ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.Session
                 });

            string uniqueDbName = "TestDbTC_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainerTC_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

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

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task RegionalFailoverWithHttpRequestException_EnsuresThinClientHeaderInRefreshRequest()
        {
            // Arrange
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            bool headerFoundInRefreshRequest = false;
            int accountRefreshCount = 0;
            bool hasThrown = false;

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            CosmosSystemTextJsonSerializer serializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            FaultInjectionDelegatingHandler faultHandler = new FaultInjectionDelegatingHandler(
                (request) =>
                {
                    // Check for account refresh requests (GET to "/" with HTTP/1.1)
                    if (request.Method == HttpMethod.Get &&
                        request.RequestUri.AbsolutePath == "/" &&
                        request.Version == new Version(1, 1))
                    {
                        accountRefreshCount++;

                        // Only check header after we've thrown the exception
                        if (hasThrown)
                        {
                            if (request.Headers.TryGetValues(
                                ThinClientConstants.EnableThinClientEndpointDiscoveryHeaderName,
                                out IEnumerable<string> headerValues))
                            {
                                if (headerValues.Contains("True"))
                                {
                                    headerFoundInRefreshRequest = true;
                                }
                            }
                        }
                    }

                    // Throw HttpRequestException only ONCE on ThinClient POST requests
                    if (!hasThrown &&
                        request.Method == HttpMethod.Post &&
                        request.Version == new Version(2, 0))
                    {
                        hasThrown = true;
                        throw new HttpRequestException("Simulated endpoint failure");
                    }
                });

            CosmosClientBuilder builder = new CosmosClientBuilder(this.connectionString)
                .WithConnectionModeGateway()
                .WithCustomSerializer(serializer)
                .WithHttpClientFactory(() => new HttpClient(faultHandler));

            using CosmosClient client = builder.Build();

            string uniqueDbName = "TestFailoverDb_" + Guid.NewGuid().ToString();
            Database database = await client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestFailoverContainer_" + Guid.NewGuid().ToString();
            Container container = await database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

            string pk = "pk_failover_test";
            TestObject testItem = this.GenerateItems(pk).First();

            // Act - CreateItemAsync will fail once, then SDK retries and succeeds
            ItemResponse<TestObject> response = await container.CreateItemAsync(testItem, new PartitionKey(testItem.Pk));

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, "Request should succeed after retry");
            Assert.IsTrue(hasThrown, "Exception should have been thrown once");
            Assert.IsTrue(headerFoundInRefreshRequest, "Account refresh after HttpRequestException should contain thin client header");

            // Cleanup
            await database.DeleteAsync();
        }

        /// <summary>
        /// DelegatingHandler that intercepts HTTP requests and can inject faults
        /// </summary>
        private class FaultInjectionDelegatingHandler : DelegatingHandler
        {
            private readonly Action<HttpRequestMessage> requestCallback;

            public FaultInjectionDelegatingHandler(Action<HttpRequestMessage> requestCallback)
                : base(new HttpClientHandler())
            {
                this.requestCallback = requestCallback;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                // Invoke callback which can inspect request or throw exceptions
                this.requestCallback?.Invoke(request);

                // If no exception was thrown, proceed with the actual request
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}