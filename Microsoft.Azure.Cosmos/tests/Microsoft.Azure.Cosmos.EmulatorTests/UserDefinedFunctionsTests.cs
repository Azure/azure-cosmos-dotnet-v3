//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    [TestClass]
    public sealed class UserDefinedFunctionsTests : BaseCosmosClientHelper
    {
        private CosmosContainerCore container = null;
        private const string function = @"function(amt) { return amt * 0.05; }";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.container = (CosmosContainerCore)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CRUDTest()
        {
            CosmosUserDefinedFunctionSettings settings = new CosmosUserDefinedFunctionSettings
            {
                Id = Guid.NewGuid().ToString(),
                Body = UserDefinedFunctionsTests.function,
            };

            CosmosUserDefinedFunctionResponse response =
                await this.container.UserDefinedFunctions.CreateUserDefinedFunctionAsync(settings);
            double reqeustCharge = response.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            UserDefinedFunctionsTests.ValidateUserDefinedFunctionSettings(settings, response);

            response = await response.UserDefinedFunction.ReadAsync();
            reqeustCharge = response.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            UserDefinedFunctionsTests.ValidateUserDefinedFunctionSettings(settings, response);

            CosmosUserDefinedFunctionSettings updatedSettings = response.Resource;
            updatedSettings.Body = @"function(amt) { return amt * 0.42; }";

            CosmosUserDefinedFunctionResponse replaceResponse = await response.UserDefinedFunction.ReplaceAsync(updatedSettings);
            UserDefinedFunctionsTests.ValidateUserDefinedFunctionSettings(updatedSettings, replaceResponse);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);

            replaceResponse = await replaceResponse.UserDefinedFunction.DeleteAsync();
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, replaceResponse.StatusCode);
        }

        [TestMethod]
        public async Task ValidateUserDefinedFunctionsTest()
        {
            // Prevent failures if previous test did not clean up correctly 
            await this.container.UserDefinedFunctions["calculateTax"].DeleteAsync();

            ToDoActivity item = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                cost = 9001,
                description = "udf_test_item",
                status = "Done", 
                taskNum = 1
            };

            await this.container.Items.CreateItemAsync<ToDoActivity>(item.status, item);

            CosmosUserDefinedFunction cosmosUserDefinedFunction = await this.container.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
                new CosmosUserDefinedFunctionSettings
                {
                    Id = "calculateTax",
                    Body = @"function(amt) { return amt * 0.05; }"
                });
            
             CosmosSqlQueryDefinition sqlQuery = new CosmosSqlQueryDefinition(
             "SELECT t.id, t.status, t.cost, udf.calculateTax(t.cost) as total FROM toDoActivity t where t.cost > @expensive and t.status = @status")
                 .UseParameter("@expensive", 9000)
                 .UseParameter("@status", "Done");
            
             CosmosFeedIterator<dynamic> setIterator = this.container.Items.CreateItemQuery<dynamic>(
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
            await cosmosUserDefinedFunction.DeleteAsync();
        }

        [TestMethod]
        public async Task UserDefinedFunctionsIteratorTest()
        {
            CosmosUserDefinedFunction cosmosUserDefinedFunction = await CreateRandomUdf();

            HashSet<string> settings = new HashSet<string>();
            CosmosFeedIterator<CosmosUserDefinedFunctionSettings> iter = this.container.UserDefinedFunctions.GetUserDefinedFunctionIterator(); ;
            while (iter.HasMoreResults)
            {
                foreach (CosmosUserDefinedFunctionSettings storedProcedureSettingsEntry in await iter.FetchNextSetAsync())
                {
                    settings.Add(storedProcedureSettingsEntry.Id);
                }
            }

            Assert.IsTrue(settings.Contains(cosmosUserDefinedFunction.Id), "The iterator did not return the user defined function definition.");

            // Delete existing user defined functions.
            await cosmosUserDefinedFunction.DeleteAsync();
        }

        private static void ValidateUserDefinedFunctionSettings(CosmosUserDefinedFunctionSettings udfSettings, CosmosUserDefinedFunctionResponse cosmosResponse)
        {
            CosmosUserDefinedFunctionSettings settings = cosmosResponse.Resource;
            Assert.AreEqual(udfSettings.Body, settings.Body,
                "User defined function do not match");
            Assert.AreEqual(udfSettings.Id, settings.Id,
                "User defined function id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.MaxResourceQuota);
            Assert.IsNotNull(cosmosResponse.CurrentResourceQuotaUsage);
        }

        private async Task<CosmosUserDefinedFunctionResponse> CreateRandomUdf()
        {
            string id = Guid.NewGuid().ToString();
            string function = UserDefinedFunctionsTests.function;

            CosmosUserDefinedFunctionSettings settings = new CosmosUserDefinedFunctionSettings
            {
                Id = id,
                Body = function,
            };

            //Create a user defined function 
            CosmosUserDefinedFunctionResponse createResponse = await this.container.UserDefinedFunctions.CreateUserDefinedFunctionAsync(
                userDefinedFunctionSettings: settings,
                cancellationToken: this.cancellationToken);

            ValidateUserDefinedFunctionSettings(settings, createResponse);

            return createResponse;
        }

        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }
    }
}
