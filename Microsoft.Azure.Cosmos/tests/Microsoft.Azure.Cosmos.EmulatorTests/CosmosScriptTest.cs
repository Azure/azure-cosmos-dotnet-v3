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
    public sealed class CosmosScriptTest : BaseCosmosClientHelper
    {
        private CosmosContainer container = null;
        private CosmosStoredProcedureRequestOptions requestOptions = new CosmosStoredProcedureRequestOptions();
        private const string udf = @"function(amt) { return amt * 0.05; }";
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();

            string containerName = Guid.NewGuid().ToString();
            CosmosContainerResponse cosmosContainerResponse = await this.database.Containers
                .CreateContainerIfNotExistsAsync(containerName, "/status");
            this.container = cosmosContainerResponse;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CRUDStoreProcedureTest()
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = "function() { { var x = 42; } }";
            CosmosScriptResponse scriptResponse = await this.container.Scripts.CreateAsync(scriptSettings: new CosmosScriptSettings(sprocId, sprocBody, CosmosScriptType.StoredProcedure));
            double requestCharge = scriptResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, scriptResponse.StatusCode);
            ValidateScriptSettings(sprocId, sprocBody, scriptResponse);

            scriptResponse = await this.container.Scripts.ReadAsync(sprocId, CosmosScriptType.StoredProcedure);
            Assert.AreEqual(HttpStatusCode.OK, scriptResponse.StatusCode);
            Assert.IsTrue(requestCharge > 0);
            ValidateScriptSettings(sprocId, sprocBody, scriptResponse);

            string updatedBody = @"function(name) { var context = getContext();
                    var response = context.getResponse();
                    response.setBody(""hello there "" + name);
                }";
            CosmosScriptSettings scriptSettings = scriptResponse.ScriptSettings;
            scriptSettings.Body = updatedBody;
            CosmosScriptResponse replaceResponse = await this.container.Scripts.ReplaceAsync(scriptSettings);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            ValidateScriptSettings(sprocId, updatedBody, replaceResponse);

            scriptResponse = await this.container.Scripts.DeleteAsync(replaceResponse.ScriptSettings.Id, replaceResponse.ScriptSettings.Type);
            Assert.AreEqual(HttpStatusCode.NoContent, scriptResponse.StatusCode);
            Assert.IsTrue(scriptResponse.RequestCharge > 0);
        }

        [TestMethod]
        public async Task CRUDTriggerTest()
        {
            CosmosScriptSettings settings = new CosmosScriptSettings
            {
                Id = Guid.NewGuid().ToString(),
                Body = GetTriggerFunction(".05"),
                Type = CosmosScriptType.PreTrigger,
            };
            CosmosScriptResponse scriptResponse = await this.container.Scripts.CreateAsync(settings);
            double requestCharge = scriptResponse.RequestCharge;
            Assert.IsTrue(requestCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, scriptResponse.StatusCode);
            Assert.AreEqual(TriggerType.Pre, scriptResponse.ScriptSettings.TriggerType);
            ValidateScriptSettings(settings.Id, settings.Body, scriptResponse);

            scriptResponse = await this.container.Scripts.ReadAsync(settings.Id, settings.Type);
            Assert.AreEqual(HttpStatusCode.OK, scriptResponse.StatusCode);
            Assert.IsTrue(requestCharge > 0);
            ValidateScriptSettings(settings.Id, settings.Body, scriptResponse);

            CosmosScriptSettings updatedSettings = scriptResponse.ScriptSettings;
            updatedSettings.Body = GetTriggerFunction(".42");
            CosmosScriptResponse replaceResponse = await this.container.Scripts.ReplaceAsync(updatedSettings);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.IsTrue(replaceResponse.RequestCharge > 0);

            scriptResponse = await this.container.Scripts.DeleteAsync(replaceResponse.ScriptSettings.Id, replaceResponse.ScriptSettings.Type);
            Assert.AreEqual(HttpStatusCode.NoContent, scriptResponse.StatusCode);
            Assert.IsTrue(scriptResponse.RequestCharge > 0);
        }

        [TestMethod]
        public async Task CRUDUserDefinedFunctionTest()
        {
            CosmosScriptSettings settings = new CosmosScriptSettings
            {
                Id = Guid.NewGuid().ToString(),
                Body = udf,
                Type = CosmosScriptType.UserDefinedFunction
            };

            CosmosScriptResponse scriptResponse = await this.container.Scripts.CreateAsync(settings);

            scriptResponse = await this.container.Scripts.ReadAsync(settings.Id, CosmosScriptType.UserDefinedFunction);
            Assert.AreEqual(HttpStatusCode.OK, scriptResponse.StatusCode);
            Assert.IsTrue(scriptResponse.RequestCharge > 0);
            ValidateScriptSettings(settings.Id, settings.Body, scriptResponse);


            CosmosScriptSettings updatedSettings = scriptResponse.ScriptSettings;
            updatedSettings.Body = @"function(amt) { return amt * 0.42; }";
            CosmosScriptResponse replaceResponse = await this.container.Scripts.ReplaceAsync(updatedSettings);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.IsTrue(replaceResponse.RequestCharge > 0);

            scriptResponse = await this.container.Scripts.DeleteAsync(replaceResponse.ScriptSettings.Id, replaceResponse.ScriptSettings.Type);
            Assert.AreEqual(HttpStatusCode.NoContent, scriptResponse.StatusCode);
            Assert.IsTrue(scriptResponse.RequestCharge > 0);
        }

        [TestMethod]
        public async Task StoredProcedureIteratorTest()
        {
            string sprocBody = "function() { { var x = 42; } }";
            int numberOfSprocs = 3;
            string[] sprocIds = new string[numberOfSprocs];

            for (int i = 0; i < numberOfSprocs; i++)
            {
                string sprocId = Guid.NewGuid().ToString();
                sprocIds[i] = sprocId;

                CosmosScriptResponse scriptResponse =
                    await this.container.Scripts.CreateAsync(new CosmosScriptSettings(sprocId, sprocBody, CosmosScriptType.StoredProcedure));
                Assert.AreEqual(HttpStatusCode.Created, scriptResponse.StatusCode);
            }

            List<string> readSprocIds = new List<string>();
            CosmosResultSetIterator<CosmosScriptSettings> iter = this.container.Scripts.GetScriptIterator(CosmosScriptType.StoredProcedure);
            while (iter.HasMoreResults)
            {
                CosmosQueryResponse<CosmosScriptSettings> currentResultSet = await iter.FetchNextSetAsync();
                {
                    foreach (CosmosScriptSettings scriptSettingsEntry in currentResultSet)
                    {
                        readSprocIds.Add(scriptSettingsEntry.Id);
                    }
                }
            }

            CollectionAssert.AreEquivalent(sprocIds, readSprocIds);
        }

        [TestMethod]
        public async Task TriggersIteratorTest()
        {
            CosmosScriptResponse cosmosTrigger = await CreateRandomTrigger();

            HashSet<string> settings = new HashSet<string>();
            CosmosResultSetIterator<CosmosScriptSettings> iter = this.container.Scripts.GetScriptIterator(CosmosScriptType.PreTrigger);//Need to discuss
            while (iter.HasMoreResults)
            {
                CosmosQueryResponse<CosmosScriptSettings> currentResultSet = await iter.FetchNextSetAsync();
                foreach (CosmosScriptSettings storedProcedureSettingsEntry in await iter.FetchNextSetAsync())
                {
                    settings.Add(storedProcedureSettingsEntry.Id);
                }
            }

            Assert.IsTrue(settings.Contains(cosmosTrigger.ScriptSettings.Id), "The iterator did not return the user defined function definition.");

            // Delete existing user defined functions.
            await this.container.Scripts.DeleteAsync(cosmosTrigger.ScriptSettings.Id, CosmosScriptType.PostTrigger);
        }

        [TestMethod]
        public async Task UserDefinedFunctionsIteratorTest()
        {
            CosmosScriptResponse cosmosUserDefinedFunction = await CreateRandomUdf();

            HashSet<string> settings = new HashSet<string>();
            CosmosResultSetIterator<CosmosScriptSettings> iter = this.container.Scripts.GetScriptIterator(CosmosScriptType.UserDefinedFunction); ;
            while (iter.HasMoreResults)
            {
                foreach (CosmosScriptSettings storedProcedureSettingsEntry in await iter.FetchNextSetAsync())
                {
                    settings.Add(storedProcedureSettingsEntry.Id);
                }
            }

            Assert.IsTrue(settings.Contains(cosmosUserDefinedFunction.ScriptSettings.Id), "The iterator did not return the user defined function definition.");

            // Delete existing user defined functions.
            await this.container.Scripts.DeleteAsync(cosmosUserDefinedFunction.ScriptSettings.Id, CosmosScriptType.UserDefinedFunction);
        }

        [TestMethod]
        public async Task ValidateTriggersTest()
        {
            // Prevent failures if previous test did not clean up correctly 
            await this.container.Scripts.DeleteAsync("addTax", CosmosScriptType.PreTrigger);

            ToDoActivity item = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                cost = 9001,
                description = "trigger_test_item",
                status = "Done",
                taskNum = 1
            };

            CosmosScriptResponse cosmosScriptResponse = await this.container.Scripts.CreateAsync(
                new CosmosScriptSettings
                {
                    Id = "addTax",
                    Body = GetTriggerFunction(".20"),
                    Type = CosmosScriptType.PreTrigger
                });

            CosmosItemRequestOptions options = new CosmosItemRequestOptions()
            {
                PreTriggers = new List<string>() { cosmosScriptResponse.ScriptSettings.Id },
            };

            CosmosItemResponse<dynamic> createdItem = await this.container.Items.CreateItemAsync<dynamic>(item.status, item, options);

            double itemTax = createdItem.Resource.tax;
            Assert.AreEqual(item.cost * .20, itemTax);
            // Delete existing user defined functions.
            await this.container.Scripts.DeleteAsync("addTax", CosmosScriptType.PreTrigger);
        }

        [TestMethod]
        public async Task ValidateUserDefinedFunctionsTest()
        {
            // Prevent failures if previous test did not clean up correctly 
            await this.container.Scripts.DeleteAsync("calculateTax", CosmosScriptType.UserDefinedFunction);

            ToDoActivity item = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                cost = 9001,
                description = "udf_test_item",
                status = "Done",
                taskNum = 1
            };

            await this.container.Items.CreateItemAsync<ToDoActivity>(item.status, item);

            CosmosScriptResponse cosmosScriptResponse = await this.container.Scripts.CreateAsync(
                new CosmosScriptSettings
                {
                    Id = "calculateTax",
                    Body = @"function(amt) { return amt * 0.05; }",
                    Type = CosmosScriptType.UserDefinedFunction
                });

            CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition(
            "SELECT t.id, t.status, t.cost, udf.calculateTax(t.cost) as total FROM toDoActivity t where t.cost > @expensive and t.status = @status")
                .UseParameter("@expensive", 9000)
                .UseParameter("@status", "Done");

            CosmosResultSetIterator<dynamic> setIterator = this.container.Items.CreateItemQuery<dynamic>(
                sqlQueryDefinition: sqlQuery,
                partitionKey: "Done");

            HashSet<string> iterIds = new HashSet<string>();
            while (setIterator.HasMoreResults)
            {
                foreach (var response in await setIterator.FetchNextSetAsync())
                {
                    Assert.IsTrue(response.cost > 9000);
                    Assert.AreEqual(response.cost * .05, response.total);
                    iterIds.Add(response.id.Value);
                }
            }

            Assert.IsTrue(iterIds.Count > 0);
            Assert.IsTrue(iterIds.Contains(item.id));

            // Delete existing user defined functions.
            await this.container.Scripts.DeleteAsync("calculateTax", CosmosScriptType.UserDefinedFunction);
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

            CosmosScriptResponse scriptResponse =
                await this.container.Scripts.CreateAsync(new CosmosScriptSettings(sprocId, sprocBody, CosmosScriptType.StoredProcedure));
            Assert.AreEqual(HttpStatusCode.Created, scriptResponse.StatusCode);
            ValidateScriptSettings(sprocId, sprocBody, scriptResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, status = testPartitionId };
            CosmosItemResponse<dynamic> createItemResponse = await this.container.Items.CreateItemAsync<dynamic>(testPartitionId, payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            CosmosItemResponse<JArray> sprocResponse = await this.container.Scripts.ExecuteAsync<object, JArray>(scriptResponse.ScriptSettings.Id, scriptResponse.ScriptSettings.Type, testPartitionId, null);
            Assert.AreEqual(HttpStatusCode.OK, sprocResponse.StatusCode);

            JArray jArray = sprocResponse;
            Assert.AreEqual(1, jArray.Count);

            scriptResponse = await this.container.Scripts.DeleteAsync(scriptResponse.ScriptSettings.Id, scriptResponse.ScriptSettings.Type);
            Assert.AreEqual(HttpStatusCode.NoContent, scriptResponse.StatusCode);
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

            CosmosScriptResponse scriptResponse =
                await this.container.Scripts.CreateAsync(new CosmosScriptSettings(sprocId, sprocBody, CosmosScriptType.StoredProcedure));
            Assert.AreEqual(HttpStatusCode.Created, scriptResponse.StatusCode);
            ValidateScriptSettings(sprocId, sprocBody, scriptResponse);

            // Insert document and then query
            string testPartitionId = Guid.NewGuid().ToString();
            var payload = new { id = testPartitionId, status = testPartitionId };
            CosmosItemResponse<dynamic> createItemResponse = await this.container.Items.CreateItemAsync<dynamic>(testPartitionId, payload);
            Assert.AreEqual(HttpStatusCode.Created, createItemResponse.StatusCode);

            CosmosItemResponse<string> response = await this.container.Scripts.ExecuteAsync<string[], string>(scriptResponse.ScriptSettings.Id, scriptResponse.ScriptSettings.Type, testPartitionId, new string[] { "one" });
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            string stringResponse = response.Resource;
            Assert.IsNotNull(stringResponse);
            Assert.AreEqual("one", stringResponse);

            CosmosItemResponse<string> sprocResponse2 = await this.container.Scripts.ExecuteAsync<string, string>(scriptResponse.ScriptSettings.Id, scriptResponse.ScriptSettings.Type, testPartitionId, "one");
            Assert.AreEqual(HttpStatusCode.OK, sprocResponse2.StatusCode);

            string stringResponse2 = sprocResponse2.Resource;
            Assert.IsNotNull(stringResponse2);
            Assert.AreEqual("one", stringResponse2);

            scriptResponse = await this.container.Scripts.DeleteAsync(scriptResponse.ScriptSettings.Id, scriptResponse.ScriptSettings.Type);
            Assert.AreEqual(HttpStatusCode.NoContent, scriptResponse.StatusCode);
        }

        [TestMethod]
        public async Task DeleteNonExistingTest()
        {
            foreach (CosmosScriptType scriptType in Enum.GetValues(typeof(CosmosScriptType)))
            {
                string sprocId = Guid.NewGuid().ToString();
                CosmosScriptResponse scriptResponse = await this.container.Scripts.DeleteAsync(sprocId, scriptType);
                Assert.AreEqual(HttpStatusCode.NotFound, scriptResponse.StatusCode);
            }
        }

        [TestMethod]
        public async Task ImplicitConversionTest()
        {
            foreach (CosmosScriptType scriptType in Enum.GetValues(typeof(CosmosScriptType)))
            {
                string sprocId = Guid.NewGuid().ToString();
                string sprocBody = "function() { { var x = 42; } }";

                CosmosScriptResponse scriptResponse =
                    await this.container.Scripts.CreateAsync(new CosmosScriptSettings(sprocId, sprocBody, scriptType));
                CosmosScriptSettings scriptSettings = scriptResponse;

                Assert.AreEqual(HttpStatusCode.Created, scriptResponse.StatusCode);
                Assert.IsNotNull(scriptResponse);
                Assert.IsNotNull(scriptSettings);

                scriptResponse = await this.container.Scripts.DeleteAsync(sprocId, scriptType);
                scriptSettings = scriptResponse;
                Assert.IsNotNull(scriptResponse);
                Assert.IsNull(scriptSettings);
            }
        }

        private static void ValidateScriptSettings(string id, string body, CosmosScriptResponse cosmosResponse)
        {
            CosmosScriptSettings settings = cosmosResponse.Resource;
            Assert.AreEqual(id, settings.Id,
                "Script id do not match");
            Assert.AreEqual(body, settings.Body,
                "Script functions do not match");
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
        private static string GetTriggerFunction(string taxPercentage)
        {
            return @"function AddTax() {
                var item = getContext().getRequest().getBody();

                // Validate/calculate the tax.
                item.tax = calculateTax(item.cost);
                
                // Insert auto-created field 'createdTime'.
                item.createdTime = new Date();

                // Update the request -- this is what is going to be inserted.
                getContext().getRequest().setBody(item);
                function calculateTax(amt) {
                    // Simple input validation.

                    return amt * " + taxPercentage + @";
                }
            }";
        }
        private async Task<CosmosScriptResponse> CreateRandomUdf()
        {
            string id = Guid.NewGuid().ToString();
            string function = udf;

            CosmosUserDefinedFunctionSettings settings = new CosmosUserDefinedFunctionSettings
            {
                Id = id,
                Body = function,
            };

            //Create a user defined function 
            CosmosScriptResponse createResponse = await this.container.Scripts.CreateAsync(
                new CosmosScriptSettings(settings.Id, settings.Body, CosmosScriptType.UserDefinedFunction),
                cancellationToken: this.cancellationToken);

            //ValidateUserDefinedFunctionSettings(settings, createResponse);

            return createResponse;
        }
        private async Task<CosmosScriptResponse> CreateRandomTrigger()
        {
            string id = Guid.NewGuid().ToString();
            string function = GetTriggerFunction(".05");

            CosmosScriptSettings settings = new CosmosScriptSettings
            {
                Id = id,
                Body = function,
                Type = CosmosScriptType.PostTrigger
            };

            CosmosScriptResponse scriptResponse =
                   await this.container.Scripts.CreateAsync(settings);

            ValidateScriptSettings(settings.Id, settings.Body, scriptResponse);

            return scriptResponse;
        }
    }
}
