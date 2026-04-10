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
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
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
        private Container highThroughputContainer;


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
            if (this.highThroughputContainer != null)
            {
                await this.highThroughputContainer.DeleteContainerAsync();
            }

            try
            {
                await this.container.DeleteItemAsync<FaultInjectionTestObject>("deleteme", new PartitionKey("deleteme"));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                //ignore
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

                ItemResponse<FaultInjectionTestObject> _ = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                   new FaultInjectionTestObject { Id = "deleteme", Pk = "deleteme" });

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                this.ValidateRuleHit(delayRule, 1);

                //Check the create time is at least as long as the delay in the rule
                Assert.IsTrue(elapsed.TotalSeconds >= 15);
            }
            finally
            {
                delayRule.Disable();
            }
        }

        [TestMethod]
        public async Task AddressRefreshTooManyRequestsTest()
        {
            //create rule
            Uri primaryUri = this.client.DocumentClient.GlobalEndpointManager.WriteEndpoints.First();
            string primaryRegion = this.client.DocumentClient.GlobalEndpointManager.GetLocation(primaryUri);
            string responseDelayRuleId = "responseTooManyRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule tooManyRequestRule = new FaultInjectionRuleBuilder(
                id: responseDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion(primaryRegion)
                        .WithOperationType(FaultInjectionOperationType.MetadataRefreshAddresses)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            tooManyRequestRule.Disable();

            try
            {
                //create client with fault injection
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { tooManyRequestRule });

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

                tooManyRequestRule.Enable();

                ItemResponse<FaultInjectionTestObject> response;
                try
                {
                    response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId",
                        new PartitionKey("pk"));
                }
                catch (CosmosException ex)
                {
                    this.ValidateRuleHit(tooManyRequestRule, 1);
                    this.ValidateFaultInjectionRuleApplication(
                            ex,
                            (int)HttpStatusCode.TooManyRequests,
                            tooManyRequestRule);
                }

                tooManyRequestRule.Disable();
            }
            finally
            {
                tooManyRequestRule.Disable();
            }
        }

        [TestMethod]
        [Description("Test Partition rule filtering")]
        [Owner("ntripician")]
        public async Task FIMetadataAddressRefreshPartitionTest()
        {
            //create container with high throughput to create multiple feed ranges
            await this.InitializeHighThroughputContainerAsync();

            List<FeedRange> feedRanges = (List<FeedRange>)await this.highThroughputContainer.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges.Count > 1);

            string query = "SELECT * FROM c";

            FeedIterator<FaultInjectionTestObject> feedIterator = this.highThroughputContainer.GetItemQueryIterator<FaultInjectionTestObject>(query);

            //get one item from each feed range, since it will be a cross partition query, each page will contain items from different partitions
            FaultInjectionTestObject result1 = (await feedIterator.ReadNextAsync()).First();
            FaultInjectionTestObject result2 = (await feedIterator.ReadNextAsync()).First();

            //create fault injection rule for one of the partitions
            string serverErrorFeedRangeRuleId = "serverErrorFeedRangeRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverErrorFeedRangeRule = new FaultInjectionRuleBuilder(
                id: serverErrorFeedRangeRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder(
                                TestCommon.FaultInjectionDatabaseName,
                                TestCommon.FaultInjectionHTPContainerName,
                                feedRanges[0])
                                .Build())
                        .WithOperationType(FaultInjectionOperationType.MetadataRefreshAddresses)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                    .WithTimes(100)
                    .Build())
            .Build();

            //disable rule until ready to test
            serverErrorFeedRangeRule.Disable();

            //create client with fault injection
            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serverErrorFeedRangeRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                Serializer = this.serializer,
                MaxRetryAttemptsOnRateLimitedRequests = 0,
            };

            this.fiClient = new CosmosClient(
                this.connectionString,
                faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
            this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
            this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionHTPContainerName);

            serverErrorFeedRangeRule.Enable();

            ItemResponse<FaultInjectionTestObject> response;
            try
            {
                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    result1.Id,
                    new PartitionKey(result1.Pk));
            }
            catch (CosmosException ex)
            {
                this.ValidateHitCount(serverErrorFeedRangeRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                        ex,
                        (int)HttpStatusCode.TooManyRequests,
                        serverErrorFeedRangeRule);
            }

            //test that rule is applied to other partition, for metadata operations, the rule should not be applied to all partitions becaseu address calls go to primary replica
            try
            {
                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    result2.Id,
                    new PartitionKey(result2.Pk));
            }
            catch (CosmosException ex)
            {
                this.ValidateRuleHit(serverErrorFeedRangeRule, 2);
                this.ValidateFaultInjectionRuleApplication(
                        ex,
                        (int)HttpStatusCode.TooManyRequests,
                        serverErrorFeedRangeRule);
            }
            finally
            {
                serverErrorFeedRangeRule.Disable();
            }
        }

        private async Task InitializeHighThroughputContainerAsync()
        {
            if (this.database != null)
            {
                ContainerResponse cr = await this.database.CreateContainerIfNotExistsAsync(
                    id: TestCommon.FaultInjectionHTPContainerName,
                    partitionKeyPath: "/pk",
                    throughput: 11000);

                if (cr.StatusCode == HttpStatusCode.Created)
                {
                    this.highThroughputContainer = cr.Container;
                    List<Task> tasks = new List<Task>()
                    {
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId", Pk = "pk" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId2", Pk = "pk2" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId3", Pk = "pk3" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId4", Pk = "pk4" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            //unsued but needed to create multiple feed ranges
                            new FaultInjectionTestObject { Id = "testId5", Pk = "qwertyuiop" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId6", Pk = "asdfghjkl" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId7", Pk = "zxcvbnm" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId8", Pk = "2wsx3edc" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId9", Pk = "5tgb6yhn" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId10", Pk = "7ujm8ik" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId11", Pk = "9ol" }),
                        this.highThroughputContainer.CreateItemAsync<FaultInjectionTestObject>(
                            new FaultInjectionTestObject { Id = "testId12", Pk = "1234567890" })
                    };

                    await Task.WhenAll(tasks);
                }
                else
                {
                    this.highThroughputContainer = this.database.GetContainer(TestCommon.FaultInjectionHTPContainerName);
                }
            }
        }

        [TestMethod]
        public async Task PKRangeResponseDelayTest()
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
                        .WithOperationType(FaultInjectionOperationType.MetadataPartitionKeyRange)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromSeconds(15))
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            string createRuleId = "createRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule createRule = new FaultInjectionRuleBuilder(
                id: createRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion(primaryRegion)
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.PartitionIsSplitting)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            delayRule.Disable();

            try
            {
                //create client with fault injection
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { delayRule, createRule });

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

                ItemResponse<FaultInjectionTestObject> _ = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                   new FaultInjectionTestObject { Id = "deleteme", Pk = "deleteme" });

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                this.ValidateRuleHit(delayRule, 1);

                //Check the create time is at least as long as the delay in the rule
                Assert.IsTrue(elapsed.TotalSeconds >= 15);
            }
            finally
            {
                delayRule.Disable();
            }
        }

        [TestMethod]
        public async Task CollectionReadResponseDelayTest()
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
                        .WithOperationType(FaultInjectionOperationType.MetadataContainer)
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

                ItemResponse<FaultInjectionTestObject> _ = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                   new FaultInjectionTestObject { Id = "deleteme", Pk = "deleteme" });

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                this.ValidateRuleHit(delayRule, 1);

                //Check the create time is at least as long as the delay in the rule
                Assert.IsTrue(elapsed.TotalSeconds >= 6);
            }
            finally
            {
                delayRule.Disable();
            }
        }

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
