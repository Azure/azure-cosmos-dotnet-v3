//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    [TestClass]
    public sealed class TriggersTests : BaseCosmosClientHelper
    {
        private CosmosContainerCore container = null;

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
            CosmosTriggerSettings settings = new CosmosTriggerSettings
            {
                Id = Guid.NewGuid().ToString(),
                Body = TriggersTests.GetTriggerFunction(".05"),
                TriggerOperation = TriggerOperation.Create,
                TriggerType = Cosmos.TriggerType.Pre
            };

            CosmosTriggerResponse triggerResponse =
                await this.container.Triggers.CreateTriggerAsync(settings);
            double reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, triggerResponse.StatusCode);
            TriggersTests.ValidateTriggerSettings(settings, triggerResponse);

            triggerResponse = await triggerResponse.Trigger.ReadAsync();
            reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, triggerResponse.StatusCode);
            TriggersTests.ValidateTriggerSettings(settings, triggerResponse);

            CosmosTriggerSettings updatedSettings = triggerResponse.Resource;
            updatedSettings.Body = TriggersTests.GetTriggerFunction(".42");

            CosmosTriggerResponse replaceResponse = await triggerResponse.Trigger.ReplaceAsync(updatedSettings);
            TriggersTests.ValidateTriggerSettings(updatedSettings, replaceResponse);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);

            replaceResponse = await replaceResponse.Trigger.DeleteAsync();
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, replaceResponse.StatusCode);
        }

        [TestMethod]
        public async Task ValidateTriggersTest()
        {
            // Prevent failures if previous test did not clean up correctly 
            await this.container.Triggers["addTax"].DeleteAsync();

            ToDoActivity item = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                cost = 9001,
                description = "trigger_test_item",
                status = "Done",
                taskNum = 1
            };

            CosmosTrigger cosmosTrigger = await this.container.Triggers.CreateTriggerAsync(
                new CosmosTriggerSettings
                {
                    Id = "addTax",
                    Body = TriggersTests.GetTriggerFunction(".20"),
                    TriggerOperation = TriggerOperation.All,
                    TriggerType = Cosmos.TriggerType.Pre
                });
           
            CosmosItemRequestOptions options = new CosmosItemRequestOptions()
            {
               PreTriggers = new List<string>(){cosmosTrigger.Id },
            };

            CosmosItemResponse<dynamic> createdItem = await this.container.Items.CreateItemAsync<dynamic>(item.status, item, options);
            
            double itemTax = createdItem.Resource.tax;
            Assert.AreEqual(item.cost * .20, itemTax);
            // Delete existing user defined functions.
            await cosmosTrigger.DeleteAsync();
        }

        [TestMethod]
        public async Task TriggersIteratorTest()
        {
            CosmosTrigger cosmosTrigger = await CreateRandomTrigger();

            HashSet<string> settings = new HashSet<string>();
            CosmosResultSetIterator<CosmosTriggerSettings> iter = this.container.Triggers.GetTriggerIterator(); ;
            while (iter.HasMoreResults)
            {
                foreach (CosmosTriggerSettings storedProcedureSettingsEntry in await iter.FetchNextSetAsync())
                {
                    settings.Add(storedProcedureSettingsEntry.Id);
                }
            }

            Assert.IsTrue(settings.Contains(cosmosTrigger.Id), "The iterator did not return the user defined function definition.");

            // Delete existing user defined functions.
            await cosmosTrigger.DeleteAsync();
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

        private static void ValidateTriggerSettings(CosmosTriggerSettings triggerSettings, CosmosTriggerResponse cosmosResponse)
        {
            CosmosTriggerSettings settings = cosmosResponse.Resource;
            Assert.AreEqual(triggerSettings.Body, settings.Body,
                "Trigger function do not match");
            Assert.AreEqual(triggerSettings.Id, settings.Id,
                "Trigger id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.MaxResourceQuota);
            Assert.IsNotNull(cosmosResponse.CurrentResourceQuotaUsage);
        }

        private async Task<CosmosTriggerResponse> CreateRandomTrigger()
        {
            string id = Guid.NewGuid().ToString();
            string function = GetTriggerFunction(".05");

            CosmosTriggerSettings settings = new CosmosTriggerSettings
            {
                Id = id,
                Body = function,
                TriggerOperation = TriggerOperation.Create,
                TriggerType = Cosmos.TriggerType.Pre
            };

            //Create a user defined function 
            CosmosTriggerResponse createResponse = await this.container.Triggers.CreateTriggerAsync(
                triggerSettings: settings,
                cancellationToken: this.cancellationToken);

            ValidateTriggerSettings(settings, createResponse);

            return createResponse;
        }
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
