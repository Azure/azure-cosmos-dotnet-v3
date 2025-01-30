//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils.TestCommon;
    using ConsistencyLevel = ConsistencyLevel;
    using CosmosSystemTextJsonSerializer = Utils.TestCommon.CosmosSystemTextJsonSerializer;
    using Database = Database;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class FaultInjectionMetadataTests
    {
        private const int Timeout = 66000;

        private string connectionString;
        private CosmosSystemTextJsonSerializer serializer;

        private CosmosClient client;
        private Database database;
        private Container container;

        private CosmosClient fiClient;
        private Database fiDatabase;
        private Container fiContainer;
        //private Container highThroughputContainer;


        [TestInitialize]
        public async Task Initialize()
        {
            //tests use a live account with multi-region enabled
            this.connectionString = TestCommon.GetConnectionString();

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            //serializer settings, not needed for fault injection but used for test objects
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            this.serializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                Serializer = this.serializer,
            };

            this.client = new CosmosClient(this.connectionString, cosmosClientOptions);

            //create a database and container if they do not already exist on test account
            //SDK test account uses strong consistency so haivng pre existing databases helps shorten test time with global replication lag
            (this.database, this.container) = await TestCommon.GetOrCreateMultiRegionFIDatabaseAndContainersAsync(this.client);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            //deletes the high throughput container if it was created to save costs
            //if (this.highThroughputContainer != null)
            //{
            //    await this.highThroughputContainer.DeleteContainerAsync();
            //}

            try
            {
                await this.container.DeleteItemAsync<FaultInjectionTestObject>("deleteme", new PartitionKey("deleteme"));
            }
            finally
            {
                this.client?.Dispose();
                this.fiClient?.Dispose();
            }
        }

        [TestMethod]
        public async Task AddressRefreshResponseDelayTest()
        {
            //create rule

            Uri primaryUri = this.client.DocumentClient.GlobalEndpointManager.WriteEndpoints.First();
            string primaryRegion = this.client.DocumentClient.GlobalEndpointManager.GetLocation(primaryUri);
            string responseDelayRuleId = "responseDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: responseDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion(primaryRegion)
                        .WithOperationType(FaultInjectionOperationType.MetadataRefreshAddresses)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromSeconds(15))
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            delayRule.Disable();

            try
            {
                //create client with fault injection
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { delayRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                delayRule.Enable();

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

                ItemResponse<FaultInjectionTestObject> readResponse = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                   new FaultInjectionTestObject { Id = "deleteme", Pk = "deleteme" });

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                Console.WriteLine(readResponse.Diagnostics.ToString());
                this.ValidateHitCount(delayRule, 1);

                //Check the create time is at least as long as the delay in the rule
                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                this.ValidateHitCount(delayRule, 1);
            }
            finally
            {
                delayRule.Disable();
            }
        }

        //public async Task AddressRefreshTooManyRequestsTest()
        //{
        //}

        private async Task<(List<string>, List<string>)> GetReadWriteEndpoints(GlobalEndpointManager globalEndpointManager)
        {
            AccountProperties accountProperties = await globalEndpointManager.GetDatabaseAccountAsync();
            List<string> writeRegions = accountProperties.WritableRegions.Select(region => region.Name).ToList();
            List<string> readRegions = accountProperties.ReadableRegions.Select(region => region.Name).ToList();
            return (writeRegions, readRegions);
        }

        private void ValidateHitCount(FaultInjectionRule rule, long expectedHitCount)
        {
            Assert.AreEqual(expectedHitCount, rule.GetHitCount());
        }

        private void ValidateRuleHit(FaultInjectionRule rule, long expectedHitCount)
        {
            Assert.IsTrue(expectedHitCount <= rule.GetHitCount());
        }

        private void ValidateFaultInjectionRuleNotApplied(
            ItemResponse<FaultInjectionTestObject> response,
            FaultInjectionRule rule,
            int expectedHitCount = 0)
        {
            Assert.AreEqual(expectedHitCount, rule.GetHitCount());
            Assert.AreEqual(0, response.Diagnostics.GetFailedRequestCount());
            Assert.IsTrue((int)response.StatusCode < 400);
        }

        private void ValidateFaultInjectionRuleApplication(
            DocumentClientException ex,
            int statusCode,
            FaultInjectionRule rule)
        {
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(ex.Message.Contains(rule.GetId()));
            Assert.AreEqual(statusCode, (int)ex.StatusCode);
        }

        private void ValidateFaultInjectionRuleApplication(
            CosmosException ex,
            int statusCode,
            FaultInjectionRule rule)
        {
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(ex.Message.Contains(rule.GetId()));
            Assert.AreEqual(statusCode, (int)ex.StatusCode);
        }

        private void ValidateFaultInjectionRuleApplication(
            DocumentClientException ex,
            int statusCode,
            int subStatusCode,
            FaultInjectionRule rule)
        {
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(ex.Message.Contains(rule.GetId()));
            Assert.AreEqual(statusCode, (int)ex.StatusCode);
            Assert.AreEqual(subStatusCode.ToString(), ex.Headers.Get(WFConstants.BackendHeaders.SubStatus));
        }

        private void ValidateFaultInjectionRuleApplication(
            CosmosException ex,
            int statusCode,
            int subStatusCode,
            FaultInjectionRule rule)
        {
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(ex.Message.Contains(rule.GetId()));
            Assert.AreEqual(statusCode, (int)ex.StatusCode);
            Assert.AreEqual(subStatusCode, ex.SubStatusCode);
        }
    }
}
