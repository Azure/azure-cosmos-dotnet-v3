//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class StoredProcedureTests : BaseCosmosClientHelper
    {
        private CosmosContainer container = null;
        private CosmosStoredProcedureRequestOptions requestOptions = new CosmosStoredProcedureRequestOptions();

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();

            string containerName = Guid.NewGuid().ToString();
            CosmosContainerResponse cosmosContainerResponse = await this.database.Containers
                .CreateContainerIfNotExistsAsync(containerName, "/user");
            this.container = cosmosContainerResponse;
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

            CosmosStoredProcedureResponse storedProcedureResponse =
                await this.container.StoredProcedures.CreateStoredProcedureAsync(sprocId, sprocBody);

            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            Assert.IsTrue(storedProcedureResponse.RequestCharge > 0);

            CosmosStoredProcedureSettings sprocSettings = storedProcedureResponse;
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

            CosmosStoredProcedureResponse storedProcedureResponse =
                await this.container.StoredProcedures.CreateStoredProcedureAsync(sprocId, sprocBody);
            double requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            storedProcedureResponse = await storedProcedureResponse.StoredProcedure.ReadAsync();
            requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            string updatedBody = @"function(name) { var context = getContext();
                    var response = context.getResponse();
                    response.setBody(""hello there "" + name);
                }";
            CosmosStoredProcedureResponse replaceResponse = await storedProcedureResponse.StoredProcedure.ReplaceAsync(updatedBody);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, updatedBody, replaceResponse);
            requestCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, updatedBody, replaceResponse);


            storedProcedureResponse = await replaceResponse.StoredProcedure.DeleteAsync();
            requestCharge = storedProcedureResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, storedProcedureResponse.StatusCode);
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

                CosmosStoredProcedureResponse storedProcedureResponse =
                    await this.container.StoredProcedures.CreateStoredProcedureAsync(sprocId, sprocBody);
                Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            }

            List<string> readSprocIds = new List<string>();
            CosmosResultSetIterator<CosmosStoredProcedureSettings> iter = this.container.StoredProcedures.GetStoredProcedureIterator();
            while (iter.HasMoreResults)
            {
                CosmosQueryResponse<CosmosStoredProcedureSettings> currentResultSet = await iter.FetchNextSetAsync();
                {
                    foreach (CosmosStoredProcedureSettings storedProcedureSettingsEntry in currentResultSet)
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

            CosmosStoredProcedureResponse storedProcedureResponse =
                await this.container.StoredProcedures.CreateStoredProcedureAsync(sprocId, sprocBody);
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            CosmosItemResponse<dynamic> createItemResponse = await this.container.Items.CreateItemAsync<dynamic>(testPartitionId, payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            CosmosStoredProcedure storedProcedure = storedProcedureResponse;
            CosmosItemResponse<JArray> sprocResponse = await storedProcedure.ExecuteAsync<object, JArray>(testPartitionId, null);
            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            JArray jArray = sprocResponse;
            Assert.AreEqual(1, jArray.Count);

            storedProcedureResponse = await storedProcedureResponse.StoredProcedure.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, storedProcedureResponse.StatusCode);
        }

        [TestMethod]
        public async Task DeleteNonExistingTest()
        {
            string sprocId = Guid.NewGuid().ToString();

            CosmosStoredProcedure storedProcedure = this.container.StoredProcedures[sprocId];
            CosmosStoredProcedureResponse storedProcedureResponse = storedProcedureResponse = await storedProcedure.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NotFound, storedProcedureResponse.StatusCode);
        }

        [TestMethod]
        public async Task ImplicitConversionTest()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = "function() { { var x = 42; } }";

            CosmosStoredProcedureResponse storedProcedureResponse =
                await this.container.StoredProcedures.CreateStoredProcedureAsync(sprocId, sprocBody);
            CosmosStoredProcedure cosmosStoredProcedure = storedProcedureResponse;
            CosmosStoredProcedureSettings cosmosStoredProcedureSettings = storedProcedureResponse;

            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            Assert.IsNotNull(cosmosStoredProcedure);
            Assert.IsNotNull(cosmosStoredProcedureSettings);

            storedProcedureResponse = await storedProcedureResponse.StoredProcedure.DeleteAsync();
            cosmosStoredProcedure = storedProcedureResponse;
            cosmosStoredProcedureSettings = storedProcedureResponse;
            Assert.IsNotNull(cosmosStoredProcedure);
            Assert.IsNull(cosmosStoredProcedureSettings);
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

            CosmosStoredProcedureResponse storedProcedureResponse =
                await this.container.StoredProcedures.CreateStoredProcedureAsync(sprocId, sprocBody);
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);
            StoredProcedureTests.ValidateStoredProcedureSettings(sprocId, sprocBody, storedProcedureResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, user = testPartitionId };
            CosmosItemResponse<dynamic> createItemResponse = await this.container.Items.CreateItemAsync<dynamic>(testPartitionId, payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            CosmosStoredProcedure storedProcedure = storedProcedureResponse.StoredProcedure;
            CosmosItemResponse<string> sprocResponse = await storedProcedure.ExecuteAsync<string[], string>(testPartitionId, new string[] { "one" });
            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            string stringResponse = sprocResponse.Resource;
            Assert.IsNotNull(stringResponse);
            Assert.AreEqual("one", stringResponse);

            CosmosItemResponse<string> sprocResponse2 = await storedProcedure.ExecuteAsync<string, string>(testPartitionId, "one");
            Assert.AreEqual(HttpStatusCode.OK, sprocResponse2.StatusCode);

            string stringResponse2 = sprocResponse2.Resource;
            Assert.IsNotNull(stringResponse2);
            Assert.AreEqual("one", stringResponse2);

            storedProcedureResponse = await storedProcedureResponse.StoredProcedure.DeleteAsync();
            Assert.AreEqual(HttpStatusCode.NoContent, storedProcedureResponse.StatusCode);
        }

        private static void ValidateStoredProcedureSettings(string id, string body, CosmosStoredProcedureResponse cosmosResponse)
        {
            CosmosStoredProcedureSettings settings = cosmosResponse.Resource;
            Assert.AreEqual(id, settings.Id,
                "Stored Procedure id do not match");
            Assert.AreEqual(body, settings.Body,
                "Stored Procedure functions do not match");
        }

        private void ValidateStoredProcedureSettings(CosmosStoredProcedureSettings storedProcedureSettings, CosmosStoredProcedureResponse cosmosResponse)
        {
            CosmosStoredProcedureSettings settings = cosmosResponse.Resource;
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
