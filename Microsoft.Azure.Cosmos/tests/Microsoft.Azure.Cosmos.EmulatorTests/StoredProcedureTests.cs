//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class StoredProcedureTests : BaseCosmosClientHelper
    {
        private static CosmosSerializer cosmosJsonSerializer = new CosmosJsonSerializerCore();
        private Container container = null;
        private Scripts scripts = null;
        private StoredProcedureRequestOptions requestOptions = new StoredProcedureRequestOptions();

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
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            storedProcedureResponse = await this.scripts.ReadStoredProcedureAsync(sprocId);
            requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, storedProcedureResponse.StatusCode);
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
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, updatedBody, replaceResponse);


            StoredProcedureResponse deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            requestCharge = deleteResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task ExecutionLogsTests()
        {
            const string testLogsText = "this is a test";
            const string testPartitionId = "1";
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(name) { var context = getContext(); console.log('"+ testLogsText + "'); var response = context.getResponse(); response.setBody('hello there ' + name); }";

            StoredProcedureResponse storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            double requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            StoredProcedureExecuteResponse<string> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<string, string>(
                sprocId, 
                Guid.NewGuid().ToString(),
                new Cosmos.PartitionKey(testPartitionId), 
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
                cosmosJsonSerializer.ToStream(new string[] { Guid.NewGuid().ToString() }),
                new Cosmos.PartitionKey(testPartitionId),
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
            string sprocBody = "function() { { var x = 42; } }";
            int numberOfSprocs = 3;
            string[] sprocIds = new string[numberOfSprocs];

            for (int i = 0; i < numberOfSprocs; i++)
            {
                string sprocId = Guid.NewGuid().ToString();
                sprocIds[i] = sprocId;

                StoredProcedureResponse storedProcedureResponse =
                    await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
                Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            }

            List<string> readSprocIds = new List<string>();
            FeedIterator<StoredProcedureProperties> iter = this.scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>();
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
            StoredProcedureExecuteResponse<JArray> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<object, JArray>(sprocId, null, new Cosmos.PartitionKey(testPartitionId));
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
            ResponseMessage sprocResponse = await this.scripts.ExecuteStoredProcedureStreamAsync(sprocId, null, new Cosmos.PartitionKey(testPartitionId));
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

            StoredProcedureResponse storedProcedureResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual(HttpStatusCode.NotFound, storedProcedureResponse.StatusCode);
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

           StoredProcedureExecuteResponse<string> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<string[], string>(sprocId, new string[] { "one" }, new Cosmos.PartitionKey(testPartitionId));
            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            string stringResponse = sprocResponse.Resource;
            Assert.IsNotNull(stringResponse);
            Assert.AreEqual("one", stringResponse);

            StoredProcedureExecuteResponse<string> sprocResponse2 = await this.scripts.ExecuteStoredProcedureAsync<string, string>(sprocId, "one", new Cosmos.PartitionKey(testPartitionId));
            Assert.AreEqual(HttpStatusCode.OK, sprocResponse2.StatusCode);

            string stringResponse2 = sprocResponse2.Resource;
            Assert.IsNotNull(stringResponse2);
            Assert.AreEqual("one", stringResponse2);

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
        }

        private void ValidateStoredProcedureSettings(StoredProcedureProperties storedProcedureSettings, StoredProcedureResponse cosmosResponse)
        {
            StoredProcedureProperties settings = cosmosResponse.Resource;
            Assert.AreEqual(storedProcedureSettings.Body, settings.Body,
                "Stored Procedure functions do not match");
            Assert.AreEqual(storedProcedureSettings.Id, settings.Id,
                "Stored Procedure id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.MaxResourceQuota);
            Assert.IsNotNull(cosmosResponse.CurrentResourceQuotaUsage);
        }
    }
}
