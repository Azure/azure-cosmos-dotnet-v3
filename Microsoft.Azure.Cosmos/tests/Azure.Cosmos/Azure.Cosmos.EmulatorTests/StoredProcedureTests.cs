//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Fluent;
    using Azure.Cosmos.Scripts;
    using Azure.Cosmos.Serialization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class StoredProcedureTests : BaseCosmosClientHelper
    {
        private CosmosContainer container = null;
        private CosmosScripts scripts = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();

            string containerName = Guid.NewGuid().ToString();
            CosmosContainerResponse cosmosContainerResponse = await this.database
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

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));

            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            Assert.IsTrue(storedProcedureResponse.GetRawResponse().Headers.GetRequestCharge() > 0);

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

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            double requestCharge = storedProcedureResponse.GetRawResponse().Headers.GetRequestCharge();
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            //Assert.IsNotNull(storedProcedureResponse.Diagnostics);
            //string diagnostics = storedProcedureResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));

            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            storedProcedureResponse = await this.scripts.ReadStoredProcedureAsync(sprocId);
            requestCharge = storedProcedureResponse.GetRawResponse().Headers.GetRequestCharge();
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual((int)HttpStatusCode.OK, storedProcedureResponse.GetRawResponse().Status);
            //Assert.IsNotNull(storedProcedureResponse.Diagnostics);
            //diagnostics = storedProcedureResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            string updatedBody = @"function(name) { var context = getContext();
                    var response = context.getResponse();
                    response.setBody(""hello there "" + name);
                }";
            Response<StoredProcedureProperties> replaceResponse = await this.scripts.ReplaceStoredProcedureAsync(new StoredProcedureProperties(sprocId, updatedBody));
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, updatedBody, replaceResponse);
            requestCharge = replaceResponse.GetRawResponse().Headers.GetRequestCharge();
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual((int)HttpStatusCode.OK, replaceResponse.GetRawResponse().Status);
            //Assert.IsNotNull(replaceResponse.Diagnostics);
            //diagnostics = replaceResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, updatedBody, replaceResponse);


            Response<StoredProcedureProperties> deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            requestCharge = deleteResponse.GetRawResponse().Headers.GetRequestCharge();
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
            //Assert.IsNotNull(deleteResponse.Diagnostics);
            //diagnostics = deleteResponse.Diagnostics.ToString();
            //Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            //Assert.IsTrue(diagnostics.Contains("StatusCode"));
        }

        [TestMethod]
        public async Task ExecutionLogsTests()
        {
            const string testLogsText = "this is a test";
            const string testPartitionId = "1";
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(name) { var context = getContext(); console.log('" + testLogsText + "'); var response = context.getResponse(); response.setBody('hello there ' + name); }";

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            double requestCharge = storedProcedureResponse.GetRawResponse().Headers.GetRequestCharge();
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
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

            Assert.AreEqual((int)HttpStatusCode.OK, sprocResponse.GetRawResponse().Status);
            Assert.AreEqual(testLogsText, sprocResponse.ScriptLog);
        }

        [TestMethod]
        public async Task ExecutionLogsAsStreamTests()
        {
            const string testLogsText = "this is a test";
            const string testPartitionId = "1";
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(name) { var context = getContext(); console.log('" + testLogsText + "'); var response = context.getResponse(); response.setBody('hello there ' + name); }";

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            double requestCharge = storedProcedureResponse.GetRawResponse().Headers.GetRequestCharge();
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);



            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            Response sprocResponse = await this.scripts.ExecuteStoredProcedureStreamAsync(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                new dynamic[] { Guid.NewGuid().ToString() },
                new StoredProcedureRequestOptions()
                {
                    EnableScriptLogging = true
                });

            Assert.AreEqual((int)HttpStatusCode.OK, sprocResponse.Status);
            Assert.IsTrue(sprocResponse.Headers.TryGetValue("x-ms-documentdb-script-log-results", out string headerString));
            Assert.AreEqual(testLogsText, Uri.UnescapeDataString(headerString));
        }

        [TestMethod]
        public async Task IteratorTest()
        {
            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient(new CosmosClientOptions() { Serializer = new FaultySerializer() }))
            {
                // Should not use the custom serializer for these operations
                CosmosScripts scripts = cosmosClient.GetContainer(this.database.Id, this.container.Id).Scripts;

                string sprocBody = "function() { { var x = 42; } }";
                int numberOfSprocs = 3;
                string[] sprocIds = new string[numberOfSprocs];

                for (int i = 0; i < numberOfSprocs; i++)
                {
                    string sprocId = Guid.NewGuid().ToString();
                    sprocIds[i] = sprocId;

                    Response<StoredProcedureProperties> storedProcedureResponse =
                        await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
                    Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
                }

                List<string> readSprocIds = new List<string>();
                await foreach (StoredProcedureProperties storedProcedureSettingsEntry in scripts.GetStoredProcedureQueryResultsAsync<StoredProcedureProperties>())
                {
                    readSprocIds.Add(storedProcedureSettingsEntry.Id);
                }

                CollectionAssert.AreEquivalent(sprocIds, readSprocIds);
            }
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

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual((int)HttpStatusCode.Created, createItemResponse.GetRawResponse().Status);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            StoredProcedureExecuteResponse<JsonElement> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<JsonElement>(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                parameters: null);

            Assert.AreEqual((int)HttpStatusCode.OK, sprocResponse.GetRawResponse().Status);

            JsonElement jArray = sprocResponse;
            Assert.AreEqual(1, jArray.GetArrayLength());

            Response<StoredProcedureProperties> deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
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

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual((int)HttpStatusCode.Created, createItemResponse.GetRawResponse().Status);

            StoredProcedureProperties storedProcedure = storedProcedureResponse;
            Response sprocResponse = await this.scripts.ExecuteStoredProcedureStreamAsync(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                null);

            Assert.AreEqual((int)HttpStatusCode.OK, sprocResponse.Status);

            using (StreamReader sr = new System.IO.StreamReader(sprocResponse.ContentStream))
            {
                string stringResponse = sr.ReadToEnd();
                JsonDocument jArray = JsonDocument.Parse(stringResponse);
                Assert.AreEqual(1, jArray.RootElement.GetArrayLength());
            }

            Response<StoredProcedureProperties> deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task DeleteNonExistingTest()
        {
            string sprocId = Guid.NewGuid().ToString();

            try
            {
                Response<StoredProcedureProperties> storedProcedureResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
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

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            StoredProcedureProperties cosmosStoredProcedure = storedProcedureResponse;
            StoredProcedureProperties cosmosStoredProcedureSettings = storedProcedureResponse;

            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            Assert.IsNotNull(cosmosStoredProcedure);
            Assert.IsNotNull(cosmosStoredProcedureSettings);

            Response<StoredProcedureProperties> deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
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

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual((int)HttpStatusCode.Created, createItemResponse.GetRawResponse().Status);

            StoredProcedureExecuteResponse<string> sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<string>(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                parameters: new dynamic[] { "one" });

            Assert.AreEqual((int)HttpStatusCode.OK, sprocResponse.GetRawResponse().Status);

            string stringResponse = sprocResponse.Value;
            Assert.IsNotNull(stringResponse);
            Assert.AreEqual("one", stringResponse);

            Response response = await this.scripts.ExecuteStoredProcedureStreamAsync(
                 sprocId,
                 new Cosmos.PartitionKey(testPartitionId),
                 parameters: new dynamic[] { null });

            using (StreamReader reader = new StreamReader(response.ContentStream))
            {
                string text = await reader.ReadToEndAsync();
                Assert.AreEqual("null", text);
            }

            sprocResponse = await this.scripts.ExecuteStoredProcedureAsync<string>(
                sprocId,
                new Cosmos.PartitionKey(testPartitionId),
                parameters: new dynamic[] { null });

            Assert.AreEqual((int)HttpStatusCode.OK, sprocResponse.GetRawResponse().Status);

            stringResponse = sprocResponse.Value;
            Assert.IsNull(stringResponse);

            Response<StoredProcedureProperties> deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
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

            Response<StoredProcedureProperties> storedProcedureResponse =
                await this.scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual((int)HttpStatusCode.Created, storedProcedureResponse.GetRawResponse().Status);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            ItemResponse<dynamic> createItemResponse = await this.container.CreateItemAsync<dynamic>(payload);
            Assert.AreEqual((int)HttpStatusCode.Created, createItemResponse.GetRawResponse().Status);

            StoredProcedureExecuteResponse<string> sprocResponse2 = await this.scripts.ExecuteStoredProcedureAsync<string>(
                storedProcedureId: sprocId,
                partitionKey: new Cosmos.PartitionKey(testPartitionId),
                parameters: new dynamic[] { "one", "two", "three" },
                requestOptions: null,
                cancellationToken: default(CancellationToken));

            Assert.AreEqual((int)HttpStatusCode.OK, sprocResponse2.GetRawResponse().Status);

            string stringResponse2 = sprocResponse2.Value;
            Assert.IsNotNull(stringResponse2);
            Assert.AreEqual("onetwothree", stringResponse2);

            Response<StoredProcedureProperties> deleteResponse = await this.scripts.DeleteStoredProcedureAsync(sprocId);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
        }

        private static void ValidateStoredProcedureSettings(string id, string body, Response<StoredProcedureProperties> cosmosResponse)
        {
            StoredProcedureProperties settings = cosmosResponse.Value;
            Assert.AreEqual(id, settings.Id,
                "Stored Procedure id do not match");
            Assert.AreEqual(body, settings.Body,
                "Stored Procedure functions do not match");
        }

        private void ValidateStoredProcedureSettings(StoredProcedureProperties storedProcedureSettings, Response<StoredProcedureProperties> cosmosResponse)
        {
            StoredProcedureProperties settings = cosmosResponse.Value;
            Assert.AreEqual(storedProcedureSettings.Body, settings.Body,
                "Stored Procedure functions do not match");
            Assert.AreEqual(storedProcedureSettings.Id, settings.Id,
                "Stored Procedure id do not match");
            Assert.IsTrue(cosmosResponse.GetRawResponse().Headers.GetRequestCharge() > 0);
        }

        private class FaultySerializer : Azure.Core.ObjectSerializer
        {
            public override object Deserialize(Stream stream, Type returnType)
            {
                throw new NotImplementedException();
            }

            public override ValueTask<object> DeserializeAsync(Stream stream, Type returnType)
            {
                throw new NotImplementedException();
            }

            public override void Serialize(Stream stream, object value, Type inputType)
            {
                throw new NotImplementedException();
            }

            public override ValueTask SerializeAsync(Stream stream, object value, Type inputType)
            {
                throw new NotImplementedException();
            }
        }
    }
}
