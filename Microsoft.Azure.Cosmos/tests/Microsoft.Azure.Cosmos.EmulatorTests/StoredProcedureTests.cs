//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class StoredProcedureTests : BaseCosmosClientHelper
    {
        private Container container = null;
        private Scripts scripts = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();

            string containerName = Guid.NewGuid().ToString();
            ContainerResponse cosmosContainerResponse = await this.database
                .CreateContainerIfNotExistsAsync(containerName, "/user");
            this.container = cosmosContainerResponse;
            this.scripts = this.container.Scripts;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task SprocContractTest()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = "function() { { var x = 42; } }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));

            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            Assert.IsTrue(storedProcedureResponse.RequestCharge > 0);

            StoredProcedureProperties sprocSettings = storedProcedureResponse;
            Assert.AreEqual(sprocId, sprocSettings.Id);
            Assert.IsNotNull(sprocSettings.ResourceId);
            Assert.IsNotNull(sprocSettings.ETag);
            Assert.IsTrue(sprocSettings.LastModified.HasValue);

            Assert.IsTrue(sprocSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), sprocSettings.LastModified.Value.ToString());
        }

        [TestMethod]
        public async Task CRUDTest()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = "function() { { var x = 42; } }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            double requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            Assert.IsNotNull(storedProcedureResponse.Diagnostics);
            string diagnostics = storedProcedureResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));

            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            storedProcedureResponse = await this.scripts.ReadStoredProcedureAsync(sprocId);
            requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, storedProcedureResponse.StatusCode);
            Assert.IsNotNull(storedProcedureResponse.Diagnostics);
            diagnostics = storedProcedureResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            string updatedBody = @"function(name) { var context = getContext();
                    var response = context.getResponse();
                    response.setBody(""hello there "" + name);
                }";
            StoredProcedureResponse replaceResponse = await this.scripts.ReplaceStoredProcedureAsync(new StoredProcedureProperties(sprocId, updatedBody));
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, updatedBody, replaceResponse);
            requestCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.IsNotNull(replaceResponse.Diagnostics);
            diagnostics = replaceResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, updatedBody, replaceResponse);


            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            requestCharge = deleteResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.IsNotNull(deleteResponse.Diagnostics);
            diagnostics = deleteResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
        }

        [TestMethod]
        public async Task ExecutionLogsTests()
        {
            const string testLogsText = "this is a test";
            const string testPartitionId = "1";
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(name) { var context = getContext(); console.log('" + testLogsText + "'); var response = context.getResponse(); response.setBody('hello there ' + name); }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            double requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            StoredProcedureExecuteResponse<string> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<string>(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                new dynamic[] { Guid.NewGuid().ToString() },
                new StoredProcedureRequestOptions()
                {
                    EnableScriptLogging = true
                });

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);
            Assert.AreEqual(testLogsText, sprocResponse.ScriptLog);
        }

        [TestMethod]
        public async Task ExecutionLogsAsStreamTests()
        {
            const string testLogsText = "this is a test";
            const string testPartitionId = "1";
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(name) { var context = getContext(); console.log('" + testLogsText + "'); var response = context.getResponse(); response.setBody('hello there ' + name); }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            double requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);



            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            ResponseMessage sprocResponse = await this.scripts.ExecuteStoredProcedureStreamAsync(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                new dynamic[] { Guid.NewGuid().ToString() },
                new StoredProcedureRequestOptions()
                {
                    EnableScriptLogging = true
                });

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);
            Assert.AreEqual(testLogsText, Uri.UnescapeDataString(sprocResponse.Headers["x-ms-documentdb-script-log-results"]));
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient(new CosmosClientOptions() { Serializer = new FaultySerializer() }))
            {
                // Should not use the custom serializer for these operations
                Scripts scripts = cosmosClient.GetContainer(this.database.Id, this.container.Id).Scripts;
                string sprocBody = "function() { { var x = 42; } }";
                int numberOfSprocs = 3;
                string[] sprocIds = new string[numberOfSprocs];

                for (int i = 0; i < numberOfSprocs; i++)
                {
                    string sprocId = Guid.NewGuid().ToString();
                    sprocIds[i] = sprocId;

                    StoredProcedureResponse storedProcedureResponse =
                        await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
                    Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
                }

                List<string> readSprocIds = new List<string>();
                FeedIterator<StoredProcedureProperties> iter = scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>();
                while (iter.HasMoreResults)
                {
                    FeedResponse<StoredProcedureProperties> currentResultSet = await iter.ReadNextAsync();
                    {
                        foreach (StoredProcedureProperties storedProcedureSettingsEntry in currentResultSet)
                        {
                            readSprocIds.Add(storedProcedureSettingsEntry.Id);
                        }
                    }
                }

                CollectionAssert.AreEquivalent(sprocIds, readSprocIds);
            }
        }

        [TestMethod]
        public async Task ExecuteNonePkTest()
        {
            // Create a container in v2 without a partition key
            string containerId = "SprocPkNone" + Guid.NewGuid().ToString();
            ContainerCore containerNonePk = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(this.database, containerId);
            Scripts scriptsNonePk = containerNonePk.Scripts;

            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function() {
                var context = getContext();
                var response = context.getResponse();
                var collection = context.getCollection();
                var collectionLink = collection.getSelfLink();

                var filterQuery = 'SELECT * FROM c';

                collection.queryDocuments(collectionLink, filterQuery, { },
                    function(err, documents) {
                        response.setBody(documents);
                    }
                );
            }";

            StoredProcedureResponse storedProcedureResponse =
                await scriptsNonePk.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await containerNonePk.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            StoredProcedureExecuteResponse<JArray> sprocResponse = await scriptsNonePk.ExecuteStoredProcedureAsync<JArray>(
                sprocId,
                Cosmos.PartitionKey.None,
                parameters: null);

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            JArray jArray = sprocResponse;
            Assert.AreEqual(1, jArray.Count);
            Assert.AreEqual(testPartitionId, jArray[0]["id"]);

            StoredProcedureResponse deleteResponse = await scriptsNonePk.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTest()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function() {
                var context = getContext();
                var response = context.getResponse();
                var collection = context.getCollection();
                var collectionLink = collection.getSelfLink();

                var filterQuery = 'SELECT * FROM c';

                collection.queryDocuments(collectionLink, filterQuery, { },
                    function(err, documents) {
                        response.setBody(documents);
                    }
                );
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            StoredProcedureExecuteResponse<JArray> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<JArray>(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                parameters: null);

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            JArray jArray = sprocResponse;
            Assert.AreEqual(1, jArray.Count);

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestAsStream()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function() {
                var context = getContext();
                var response = context.getResponse();
                var collection = context.getCollection();
                var collectionLink = collection.getSelfLink();

                var filterQuery = 'SELECT * FROM c';

                collection.queryDocuments(collectionLink, filterQuery, { },
                    function(err, documents) {
                        response.setBody(documents);
                    }
                );
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            ResponseMessage sprocResponse = await this.scripts.ExecuteStoredProcedureStreamAsync(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                null);

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            using (StreamReader sr = new System.IO.StreamReader(sprocResponse.Content))
            {
                string stringResponse = sr.ReadToEnd();
                JArray jArray = JArray.Parse(stringResponse);
                Assert.AreEqual(1, jArray.Count);
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestAsStreamWithNullRequestStream()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function() {
                var context = getContext();
                var response = context.getResponse();
                var collection = context.getCollection();
                var collectionLink = collection.getSelfLink();

                var filterQuery = 'SELECT * FROM c';

                collection.queryDocuments(collectionLink, filterQuery, { },
                    function(err, documents) {
                        response.setBody(documents);
                    }
                );
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            ResponseMessage sprocResponse = await this.scripts.ExecuteStoredProcedureStreamAsync(
                sprocId,
                null,
                new Cosmos.PartitionKey(testPartitionId));

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            using (StreamReader sr = new System.IO.StreamReader(sprocResponse.Content))
            {
                string stringResponse = sr.ReadToEnd();
                JArray jArray = JArray.Parse(stringResponse);
                Assert.AreEqual(1, jArray.Count);
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task DeleteNonExistingTest()
        {
            string sprocId = Guid.NewGuid().ToString();

            try
            {
                StoredProcedureResponse storedProcedureResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
                Assert.Fail();
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task ImplicitConversionTest()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = "function() { { var x = 42; } }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            StoredProcedureProperties cosmosStoredProcedure = storedProcedureResponse;
            StoredProcedureProperties cosmosStoredProcedureSettings = storedProcedureResponse;

            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            Assert.IsNotNull(cosmosStoredProcedure);
            Assert.IsNotNull(cosmosStoredProcedureSettings);

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithParameter()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(param1) {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(param1);
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            StoredProcedureExecuteResponse<string> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<string>(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                parameters: new dynamic[] { "one" });

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            string stringResponse = sprocResponse.Resource;
            Assert.IsNotNull(stringResponse);
            Assert.AreEqual("one", stringResponse);

            ResponseMessage response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                 sprocId,
                 new Cosmos.PartitionKey(testPartitionId),
                 parameters: new dynamic[] { null });

            using (StreamReader reader = new StreamReader(response.Content))
            {
                string text = await reader.ReadToEndAsync();
                Assert.AreEqual("null", text);
            }

            sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<string>(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                parameters: new dynamic[] { null });

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            stringResponse = sprocResponse.Resource;
            Assert.IsNull(stringResponse);

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithStreamParameter()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(param1) {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(param1);
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            MemoryStream streamPayload = new MemoryStream(Encoding.UTF8.GetBytes(@"[""one""]"));

            ResponseMessage response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                sprocId,
                streamPayload: streamPayload,
                partitionKey: new Cosmos.PartitionKey(testPartitionId));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            using (StreamReader reader = new StreamReader(response.Content))
            {
                string text = await reader.ReadToEndAsync();
                Assert.AreEqual(@"""one""", text);
            }

            streamPayload = new MemoryStream(Encoding.UTF8.GetBytes("[null]"));

            response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                 sprocId,
                 streamPayload,
                 new Cosmos.PartitionKey(testPartitionId));

            using (StreamReader reader = new StreamReader(response.Content))
            {
                string text = await reader.ReadToEndAsync();
                Assert.AreEqual("null", text);
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithMultipleParameters()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(param1, param2, param3) {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(param1+param2+param3);
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            StoredProcedureExecuteResponse<string> sprocResponse2 = await this.scripts.ExecuteStoredProcedureAsync<string>(
                storedProcedureId: sprocId,
                partitionKey: new Cosmos.PartitionKey(testPartitionId),
                parameters: new dynamic[] { "one", "two", "three" },
                requestOptions: null,
                cancellationToken: default(CancellationToken));

            Assert.AreEqual(HttpStatusCode.OK, sprocResponse2.StatusCode);

            string stringResponse2 = sprocResponse2.Resource;
            Assert.IsNotNull(stringResponse2);
            Assert.AreEqual("onetwothree", stringResponse2);

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithMultipleStreamParameters()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(param1, param2, param3) {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(param1+param2+param3);
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            MemoryStream streamPayload = new MemoryStream(Encoding.UTF8.GetBytes(@"[""one"",""two"",""three""]"));

            ResponseMessage response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                storedProcedureId: sprocId,
                streamPayload: streamPayload,
                partitionKey: new Cosmos.PartitionKey(testPartitionId),
                requestOptions: null,
                cancellationToken: default(CancellationToken));

            using (StreamReader reader = new StreamReader(response.Content))
            {
                string text = await reader.ReadToEndAsync();
                Assert.AreEqual(@"""onetwothree""", text);
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithNonObjectStreamParameters()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(param1) {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(param1);
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            MemoryStream[] streamPayloads = new MemoryStream[]
            {
                new MemoryStream(Encoding.UTF8.GetBytes(@"""""")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"""hello""")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"0")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"undefined")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"-1.25")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"false")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"[x:4]")) // invalid json
            };

            foreach (MemoryStream streamPayload in streamPayloads)
            {
                ResponseMessage response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                    storedProcedureId: sprocId,
                    streamPayload: streamPayload,
                    partitionKey: new Cosmos.PartitionKey(testPartitionId),
                    requestOptions: null,
                    cancellationToken: default(CancellationToken));

                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithTechnicallyJSONParseableButNotArraylikeParameters()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(param1) {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(typeof param1);
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            MemoryStream[] streamPayloads = new MemoryStream[]
            {
                new MemoryStream(Encoding.UTF8.GetBytes(@"null")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"{}")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""param1"":4}")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":4}"))
            };

            foreach (MemoryStream streamPayload in streamPayloads)
            {
                ResponseMessage response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                    storedProcedureId: sprocId,
                    streamPayload: streamPayload,
                    partitionKey: new Cosmos.PartitionKey(testPartitionId),
                    requestOptions: null,
                    cancellationToken: default(CancellationToken));

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                using (StreamReader reader = new StreamReader(response.Content))
                {
                    string text = await reader.ReadToEndAsync();
                    Assert.AreEqual(@"""undefined""", text);
                }
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithArraylikeParameters()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(param1, param2, param3) {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(param1 + (param2 || '') + (param3 || ''));
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            MemoryStream[] streamPayloads = new MemoryStream[]
            {
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""onetwothree"", ""length"": 1}")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""onetwothree"", ""length"": ""1""}")), // Javascript treats length string as a numebr
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""onetwothree"", ""length"": 10000}")), // Tons of extra arguments which aren't actually there
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""onetwothree"", ""1"": ""test"", ""length"": 1}")), // function.apply will not see the [1] parameter because length == 1
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""onetwothree"", ""1"": ""test"", ""length"": 1.5}")), // function.apply will not see the [1] parameter because length rounds down to 1
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""one"", ""1"": ""twothree"", ""length"": 2}")),
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""one"", ""2"": ""twothree"", ""length"": 3}")), // Skipping a parameter is okay
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""one"", ""2"": ""three"", ""1"": ""two"", ""length"": 3}")) // out of order is okay because arrays are just numeric indexers in javascript
            };

            foreach (MemoryStream streamPayload in streamPayloads)
            {
                ResponseMessage response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                    storedProcedureId: sprocId,
                    streamPayload: streamPayload,
                    partitionKey: new Cosmos.PartitionKey(testPartitionId),
                    requestOptions: null,
                    cancellationToken: default(CancellationToken));

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

                using (StreamReader reader = new StreamReader(response.Content))
                {
                    string text = await reader.ReadToEndAsync();
                    Assert.AreEqual(@"""onetwothree""", text);
                }
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecuteTestWithNonAppliableArraylikeParameters()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function() {
                var context = getContext();
                var response = context.getResponse();
                response.setBody(true);
            }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            MemoryStream[] streamPayloads = new MemoryStream[]
            {
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""onetwothree"", ""length"": 600000}")), // 600000 parameters -> "out of stack space"
                new MemoryStream(Encoding.UTF8.GetBytes(@"{""0"":""onetwothree"", ""length"": 1e9}")), // 1 billion parameters -> exceeds javascript .apply maximum
            };

            foreach (MemoryStream streamPayload in streamPayloads)
            {
                ResponseMessage response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                    storedProcedureId: sprocId,
                    streamPayload: streamPayload,
                    partitionKey: new Cosmos.PartitionKey(testPartitionId),
                    requestOptions: null,
                    cancellationToken: default(CancellationToken));

                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            }

            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        private static void ValidateStoredProcedureSettings(string id, string body, StoredProcedureResponse cosmosResponse)
        {
            StoredProcedureProperties settings = cosmosResponse.Resource;
            Assert.AreEqual(id, settings.Id,
                "Stored Procedure id do not match");
            Assert.AreEqual(body, settings.Body,
                "Stored Procedure functions do not match");
            SelflinkValidator.ValidateSprocSelfLink(cosmosResponse.Resource.SelfLink);
        }

        private void ValidateStoredProcedureSettings(StoredProcedureProperties storedProcedureSettings, StoredProcedureResponse cosmosResponse)
        {
            StoredProcedureProperties settings = cosmosResponse.Resource;
            Assert.AreEqual(storedProcedureSettings.Body, settings.Body,
                "Stored Procedure functions do not match");
            Assert.AreEqual(storedProcedureSettings.Id, settings.Id,
                "Stored Procedure id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
            Assert.IsNotNull(cosmosResponse.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));
        }

        private sealed class FaultySerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                throw new NotImplementedException();
            }

            public override Stream ToStream<T>(T input)
            {
                throw new NotImplementedException();
            }
        }
    }
}
