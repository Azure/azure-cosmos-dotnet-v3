//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;

    [TestClass]
    public sealed class UserDefinedFunctionsTests : BaseCosmosClientHelper
    {
        private ContainerCore container = null;
        private Scripts scripts = null;
        private const string function = @"function(amt) { return amt * 0.05; }";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.container = (ContainerCore)response;
            this.scripts = this.container.Scripts;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CRUDTest()
        {
            UserDefinedFunctionProperties settings = new UserDefinedFunctionProperties
            {
                Id = Guid.NewGuid().ToString(),
                Body = UserDefinedFunctionsTests.function,
            };

            UserDefinedFunctionResponse response =
                await this.scripts.CreateUserDefinedFunctionAsync(settings);
            double reqeustCharge = response.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            UserDefinedFunctionsTests.ValidateUserDefinedFunctionSettings(settings, response);

            response = await this.scripts.ReadUserDefinedFunctionAsync(settings.Id);
            reqeustCharge = response.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            UserDefinedFunctionsTests.ValidateUserDefinedFunctionSettings(settings, response);

            UserDefinedFunctionProperties updatedSettings = response.Resource;
            updatedSettings.Body = @"function(amt) { return amt * 0.42; }";

            UserDefinedFunctionResponse replaceResponse = await this.scripts.ReplaceUserDefinedFunctionAsync(updatedSettings);
            UserDefinedFunctionsTests.ValidateUserDefinedFunctionSettings(updatedSettings, replaceResponse);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);

            replaceResponse = await this.scripts.DeleteUserDefinedFunctionAsync(settings.Id);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, replaceResponse.StatusCode);
        }

        [TestMethod]
        public async Task ValidateUserDefinedFunctionsTest()
        {
            try
            {
                // Prevent failures if previous test did not clean up correctly 
                await this.scripts.DeleteUserDefinedFunctionAsync("calculateTax");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //swallow
            }

            ToDoActivity item = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                cost = 9001,
                description = "udf_test_item",
                status = "Done", 
                taskNum = 1
            };

            await this.container.CreateItemAsync<ToDoActivity>(item);

            UserDefinedFunctionProperties cosmosUserDefinedFunction = await this.scripts.CreateUserDefinedFunctionAsync(
                new UserDefinedFunctionProperties
                {
                    Id = "calculateTax",
                    Body = @"function(amt) { return amt * 0.05; }"
                });
            
             QueryDefinition sqlQuery = new QueryDefinition(
             "SELECT t.id, t.status, t.cost, udf.calculateTax(t.cost) as total FROM toDoActivity t where t.cost > @expensive and t.status = @status")
                 .WithParameter("@expensive", 9000)
                 .WithParameter("@status", "Done");
            
             FeedIterator<dynamic> feedIterator = this.container.GetItemQueryIterator<dynamic>(
                 queryDefinition: sqlQuery);

            HashSet<string> iterIds = new HashSet<string>();
            while (feedIterator.HasMoreResults)
            {
                foreach (var response in await feedIterator.ReadNextAsync())
                {
                    Assert.IsTrue(response.cost > 9000);
                    Assert.AreEqual(response.cost * .05, response.total);
                    iterIds.Add(response.id.Value);
                }
            }

            Assert.IsTrue(iterIds.Count > 0);
            Assert.IsTrue(iterIds.Contains(item.id));

            // Delete existing user defined functions.
            await this.scripts.DeleteUserDefinedFunctionAsync(cosmosUserDefinedFunction.Id);
        }

        [TestMethod]
        public async Task UserDefinedFunctionsIteratorTest()
        {
            UserDefinedFunctionProperties cosmosUserDefinedFunction = await CreateRandomUdf();

            HashSet<string> settings = new HashSet<string>();
            FeedIterator<UserDefinedFunctionProperties> iter = this.scripts.GetUserDefinedFunctionQueryIterator<UserDefinedFunctionProperties>(); ;
            while (iter.HasMoreResults)
            {
                foreach (UserDefinedFunctionProperties storedProcedureSettingsEntry in await iter.ReadNextAsync())
                {
                    settings.Add(storedProcedureSettingsEntry.Id);
                }
            }

            Assert.IsTrue(settings.Contains(cosmosUserDefinedFunction.Id), "The iterator did not return the user defined function definition.");

            // Delete existing user defined functions.
            await this.scripts.DeleteUserDefinedFunctionAsync(cosmosUserDefinedFunction.Id);
        }

        private static void ValidateUserDefinedFunctionSettings(UserDefinedFunctionProperties udfSettings, UserDefinedFunctionResponse cosmosResponse)
        {
            UserDefinedFunctionProperties settings = cosmosResponse.Resource;
            Assert.AreEqual(udfSettings.Body, settings.Body,
                "User defined function do not match");
            Assert.AreEqual(udfSettings.Id, settings.Id,
                "User defined function id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.MaxResourceQuota);
            Assert.IsNotNull(cosmosResponse.CurrentResourceQuotaUsage);
        }

        private async Task<UserDefinedFunctionResponse> CreateRandomUdf()
        {
            string id = Guid.NewGuid().ToString();
            string function = UserDefinedFunctionsTests.function;

            UserDefinedFunctionProperties settings = new UserDefinedFunctionProperties
            {
                Id = id,
                Body = function,
            };

            //Create a user defined function 
            UserDefinedFunctionResponse createResponse = await this.scripts.CreateUserDefinedFunctionAsync(
                userDefinedFunctionProperties: settings,
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
