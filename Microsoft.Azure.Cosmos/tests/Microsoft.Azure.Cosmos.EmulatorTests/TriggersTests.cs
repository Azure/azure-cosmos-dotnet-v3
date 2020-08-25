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
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public sealed class TriggersTests : BaseCosmosClientHelper
    {
        private ContainerInternal container = null;
        private Scripts scripts = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                SendingRequestEventArgs = this.SendingRequestEventHandlerTriggersVerifier,
            };

            this.cosmosClient = TestCommon.CreateCosmosClient(clientOptions);
            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: this.cancellationToken);

            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.container = (ContainerInlineCore)response;
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
            TriggerProperties settings = new TriggerProperties
            {
                Id = Guid.NewGuid().ToString(),
                Body = TriggersTests.GetTriggerFunction(".05"),
                TriggerOperation = Cosmos.Scripts.TriggerOperation.Create,
                TriggerType = Cosmos.Scripts.TriggerType.Pre
            };

            TriggerResponse triggerResponse =
                await this.scripts.CreateTriggerAsync(settings);
            double reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.Created, triggerResponse.StatusCode);
            Assert.IsNotNull(triggerResponse.Diagnostics);
            string diagnostics = triggerResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            TriggersTests.ValidateTriggerSettings(settings, triggerResponse);

            triggerResponse = await this.scripts.ReadTriggerAsync(settings.Id);
            reqeustCharge = triggerResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, triggerResponse.StatusCode);
            Assert.IsNotNull(triggerResponse.Diagnostics);
            diagnostics = triggerResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
            TriggersTests.ValidateTriggerSettings(settings, triggerResponse);

            TriggerProperties updatedSettings = triggerResponse.Resource;
            updatedSettings.Body = TriggersTests.GetTriggerFunction(".42");

            TriggerResponse replaceResponse = await this.scripts.ReplaceTriggerAsync(updatedSettings);
            TriggersTests.ValidateTriggerSettings(updatedSettings, replaceResponse);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.IsNotNull(replaceResponse.Diagnostics);
            diagnostics = replaceResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));

            replaceResponse = await this.scripts.DeleteTriggerAsync(updatedSettings.Id);
            reqeustCharge = replaceResponse.RequestCharge;
            Assert.IsTrue(reqeustCharge > 0);
            Assert.AreEqual(HttpStatusCode.NoContent, replaceResponse.StatusCode);
            Assert.IsNotNull(replaceResponse.Diagnostics);
            diagnostics = replaceResponse.Diagnostics.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(diagnostics));
            Assert.IsTrue(diagnostics.Contains("StatusCode"));
        }

        [TestMethod]
        public async Task ValidatePreTriggerTest()
        {
            string triggerId = "SetJobNumber";

            // Prevent failures if previous test did not clean up correctly 
            try
            {
                await this.scripts.DeleteTriggerAsync(triggerId);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //swallow
            }

            TriggerProperties trigger = new TriggerProperties
            {
                Id = triggerId,
                TriggerType = TriggerType.Pre,
                TriggerOperation = TriggerOperation.Create,
                Body = @"function setJobNumber() {
                    var context = getContext();
                    var request = context.getRequest();      
                    var containerManager = context.getCollection();
                    var containerLink = containerManager.getSelfLink()

                    var documentToCreate = request.getBody();

                    var jobNumberQuery = ""SELECT VALUE MAX(r.jobNumber) from root r WHERE r.investigationKey = '"" + documentToCreate['investigationKey'] + ""'"";
                    containerManager.queryDocuments(containerLink,
                        jobNumberQuery,
                        function(err, countValue) {
                            if (err) throw new Error(err.message);
                            documentToCreate['jobNumber'] = (countValue.length > 0 ? countValue[0] : 0) + 1;
                        });

                    // update the document that will be created
                    request.setBody(documentToCreate);
                    }",
            };

            TriggerProperties cosmosTrigger = await this.scripts.CreateTriggerAsync(trigger);

            Job value = new Job() { Id = Guid.NewGuid(), InvestigationKey = "investigation~1" };

            // this should create the document successfully with jobnumber of 1
            Job createdItem = await this.container.CreateItemAsync<Job>(item: value, partitionKey: null, requestOptions: new ItemRequestOptions
            {
                PreTriggers = new List<string> { triggerId }
            });

            Assert.AreEqual(value.Id, createdItem.Id);
            Assert.AreEqual(value.InvestigationKey, createdItem.InvestigationKey);
            Assert.AreEqual(1, createdItem.JobNumber);

            List<Job> result = this.container.GetItemLinqQueryable<Job>(allowSynchronousQueryExecution: true).Where(x => x.InvestigationKey == "investigation~1").ToList();
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Job jobFromLinq = result.First();

            Assert.AreEqual(value.Id, jobFromLinq.Id);
            Assert.AreEqual(value.InvestigationKey, jobFromLinq.InvestigationKey);
            Assert.AreEqual(1, jobFromLinq.JobNumber);

            value.Id = Guid.NewGuid();

            // this should create the document successfully with jobnumber of 2
            Job createdItem2 = await this.container.CreateItemAsync<Job>(item: value, partitionKey: null, requestOptions:  new ItemRequestOptions
            {
                PreTriggers = new List<string> { "SetJobNumber" }
            });

            Assert.AreEqual(value.Id, createdItem2.Id);
            Assert.AreEqual(value.InvestigationKey, createdItem2.InvestigationKey);
            Assert.AreEqual(2, createdItem2.JobNumber);

            result = this.container.GetItemLinqQueryable<Job>(allowSynchronousQueryExecution: true).Where(x => x.InvestigationKey == "investigation~1").ToList();
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);
            jobFromLinq = result.First(x => x.JobNumber == 2);

            Assert.AreEqual(value.Id, jobFromLinq.Id);
            Assert.AreEqual(value.InvestigationKey, jobFromLinq.InvestigationKey);
            Assert.AreEqual(2, jobFromLinq.JobNumber);

            // Delete existing user defined functions.
            await this.scripts.DeleteTriggerAsync(triggerId);
        }

        [TestMethod]
        public async Task ValidateTriggersTest()
        {
            // Prevent failures if previous test did not clean up correctly 
            try
            {
                await this.scripts.DeleteTriggerAsync("addTax");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //swallow
            }
            
            ToDoActivity item = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                cost = 9001,
                description = "trigger_test_item",
                status = "Done",
                taskNum = 1
            };

            TriggerProperties cosmosTrigger = await this.scripts.CreateTriggerAsync(
                new TriggerProperties
                {
                    Id = "addTax",
                    Body = TriggersTests.GetTriggerFunction(".20"),
                    TriggerOperation = Cosmos.Scripts.TriggerOperation.All,
                    TriggerType = Cosmos.Scripts.TriggerType.Pre
                });

            ItemRequestOptions options = new ItemRequestOptions()
            {
                PreTriggers = new List<string>() { cosmosTrigger.Id },
            };

            ItemResponse<dynamic> createdItem = await this.container.CreateItemAsync<dynamic>(item, requestOptions: options);

            double itemTax = createdItem.Resource.tax;
            Assert.AreEqual(item.cost * .20, itemTax);
            // Delete existing user defined functions.
            await this.scripts.DeleteTriggerAsync("addTax");
        }

        [TestMethod]
        public async Task TriggersIteratorTest()
        {
            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient(new CosmosClientOptions() { Serializer = new FaultySerializer() }))
            {
                // Should not use the custom serializer for these operations
                Scripts scripts = cosmosClient.GetContainer(this.database.Id, this.container.Id).Scripts;
                TriggerProperties cosmosTrigger = await this.CreateRandomTrigger();

                HashSet<string> settings = new HashSet<string>();
                FeedIterator<TriggerProperties> iter = scripts.GetTriggerQueryIterator<TriggerProperties>(); ;
                while (iter.HasMoreResults)
                {
                    foreach (TriggerProperties storedProcedureSettingsEntry in await iter.ReadNextAsync())
                    {
                        settings.Add(storedProcedureSettingsEntry.Id);
                    }
                }

                Assert.IsTrue(settings.Contains(cosmosTrigger.Id), "The iterator did not return the user defined function definition.");

                // Delete existing trigger
                await scripts.DeleteTriggerAsync(cosmosTrigger.Id);
            }
        }

        private void SendingRequestEventHandlerTriggersVerifier(object sender, Microsoft.Azure.Documents.SendingRequestEventArgs e)
        {
            if (e.IsHttpRequest())
            {
                if (e.HttpRequest.RequestUri.OriginalString.Contains(Microsoft.Azure.Documents.Paths.TriggersPathSegment))
                {
                    Assert.IsFalse(e.HttpRequest.Headers.Contains(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.SessionToken));
                }
            }
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

        private static void ValidateTriggerSettings(TriggerProperties triggerSettings, TriggerResponse cosmosResponse)
        {
            TriggerProperties settings = cosmosResponse.Resource;
            Assert.AreEqual(triggerSettings.Body, settings.Body,
                "Trigger function do not match");
            Assert.AreEqual(triggerSettings.Id, settings.Id,
                "Trigger id do not match");
            Assert.IsTrue(cosmosResponse.RequestCharge > 0);
            Assert.IsNotNull(cosmosResponse.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
            Assert.IsNotNull(cosmosResponse.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));
            SelflinkValidator.ValidateTriggerSelfLink(cosmosResponse.Resource.SelfLink);
        }

        private class Job
        {
            [JsonProperty(PropertyName = "id")]
            public Guid Id { get; set; }
            [JsonProperty(PropertyName = "investigationKey")]
            public string InvestigationKey { get; set; }
            [JsonProperty(PropertyName = "jobNumber")]
            public int JobNumber { get; set; }
        }

        private async Task<TriggerResponse> CreateRandomTrigger()
        {
            string id = Guid.NewGuid().ToString();
            string function = GetTriggerFunction(".05");

            TriggerProperties settings = new TriggerProperties
            {
                Id = id,
                Body = function,
                TriggerOperation = Cosmos.Scripts.TriggerOperation.Create,
                TriggerType = Cosmos.Scripts.TriggerType.Pre
            };

            //Create a user defined function 
            TriggerResponse createResponse = await this.scripts.CreateTriggerAsync(
                triggerProperties: settings,
                cancellationToken: this.cancellationToken);

            ValidateTriggerSettings(settings, createResponse);

            return createResponse;
        }

        private class FaultySerializer : CosmosSerializer
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
