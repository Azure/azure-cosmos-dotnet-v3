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
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils.TestCommon;
    using ConsistencyLevel = ConsistencyLevel;
    using CosmosSystemTextJsonSerializer = Utils.TestCommon.CosmosSystemTextJsonSerializer;
    using Database = Database;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class FaultInjectionProxyTests
    {
        private const int Timeout = 120000;

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
            this.connectionString = TestCommon.GetThinClientConnectionString();

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_THIN_CLIENT to run the tests");
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
                ConnectionMode = ConnectionMode.Gateway,
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
            try
            {
                // Only the uniquely named container created by this test is owned by it.
                if (this.highThroughputContainer != null)
                {
                    await this.highThroughputContainer.DeleteContainerAsync();
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                System.Diagnostics.Trace.TraceInformation("The test-owned container {0} was already absent.", this.highThroughputContainer.Id);
            }
            finally
            {
                this.client?.Dispose();
                this.fiClient?.Dispose();
            }
        }

        //<summary>
        //Tests to to see if fault injection rules are applied to the correct regions
        //</summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test Region rule filtering")]
        [Owner("ntripician")]
        public async Task FIProxyRegion()
        {
            //Get regions for testing
            GlobalEndpointManager globalEndpointManager = this.client.ClientContext.DocumentClient.GlobalEndpointManager;
            Assert.IsNotNull(globalEndpointManager, "Region filtering requires an initialized endpoint manager.");
            (_, List<string> preferredRegions) = await this.GetReadWriteEndpoints(globalEndpointManager);
            Assert.IsTrue(preferredRegions.Count >= 2, "Region filtering requires at least two readable account regions.");
            ReadOnlyDictionary<string, Uri> readEndpoints = globalEndpointManager.GetAvailableReadEndpointsByLocation();
            Assert.IsTrue(preferredRegions.Take(2).All(readEndpoints.ContainsKey),
                "The two readable regions must have resolved read endpoints.");

            //create fault injection rule for local region 
            string localRegionRuleId = "localRegionRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule localRegionRule = new FaultInjectionRuleBuilder(
                id: localRegionRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion(preferredRegions[0])
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            //create fault injection rule for remote region
            string remoteRegionRuleId = "remoteRegionRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule remoteRegionRule = new FaultInjectionRuleBuilder(
                id: remoteRegionRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion(preferredRegions[1])
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            //disable rules until ready to test
            localRegionRule.Disable();
            remoteRegionRule.Disable();

            try
            {
                //create client with fault injection
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { localRegionRule, remoteRegionRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    ApplicationRegion = preferredRegions[0],
                    MaxRetryAttemptsOnRateLimitedRequests = 0,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                globalEndpointManager = this.fiClient.ClientContext.DocumentClient.GlobalEndpointManager;

                await this.WarmUpThinClientAsync();
                localRegionRule.Enable();
                remoteRegionRule.Enable();

                CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId2",
                        new PartitionKey("pk2")));
                this.ValidateHitCount(localRegionRule, 1);
                this.ValidateHitCount(remoteRegionRule, 0);
                this.ValidateFaultInjectionRuleApplication(
                    exception, (int)HttpStatusCode.TooManyRequests,
                    (int)SubStatusCodes.RUBudgetExceeded, localRegionRule, faultInjector);
            }
            finally
            {
                localRegionRule.Disable();
                remoteRegionRule.Disable();

                //ensure rules are created with proper regions
                //must check here since the rules are initialized on first request call
                if (globalEndpointManager != null)
                {
                    Assert.AreEqual(1, localRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[0]], localRegionRule.GetRegionEndpoints()[0]);

                    Assert.AreEqual(1, remoteRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[1]], remoteRegionRule.GetRegionEndpoints()[0]);
                }
            }
        }

        //<summary>
        //Tests to to see if fault injection rules are applied to the correct partitions
        //We will create a container with a split physical partition (which will happen when a container is provisioned with >10k RU/s)
        //We will then create a rule for one of the partitions and ensure it is only applied to requests to that partition
        //Test scenario with >2 partitions is not needed as we only need to test that the rule is applied to the correct partition regardless of number of partitions
        //</summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test Partition rule filtering")]
        [Owner("ntripician")]
        public async Task FIProxyPartitionTest()
        {
            //create container with high throughput to create multiple feed ranges
            await this.InitializeHighThroughputContainerAsync();

            List<FeedRange> feedRanges = (List<FeedRange>)await this.highThroughputContainer.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges.Count > 1);

            FaultInjectionTestObject result1 = await this.GetItemInFeedRangeAsync(feedRanges[0]);
            FaultInjectionTestObject result2 = await this.GetItemInFeedRangeAsync(feedRanges[1]);

            //create fault injection rule for one of the partitions
            string serverErrorFeedRangeRuleId = "serverErrorFeedRangeRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverErrorFeedRangeRule = new FaultInjectionRuleBuilder(
                id: serverErrorFeedRangeRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder(
                                TestCommon.FaultInjectionDatabaseName,
                                this.highThroughputContainer.Id,
                                feedRanges[0])
                                .Build())
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
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
                ConnectionMode = ConnectionMode.Gateway,
                Serializer = this.serializer,
                MaxRetryAttemptsOnRateLimitedRequests = 0,
            };

            this.fiClient = new CosmosClient(
                this.connectionString,
                faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
            this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
            this.fiContainer = this.fiDatabase.GetContainer(this.highThroughputContainer.Id);

            await this.WarmUpThinClientAsync();
            serverErrorFeedRangeRule.Enable();

            try
            {
                CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        result1.Id,
                        new PartitionKey(result1.Pk)));
                this.ValidateHitCount(serverErrorFeedRangeRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                        exception,
                        (int)HttpStatusCode.TooManyRequests,
                        (int)SubStatusCodes.RUBudgetExceeded,
                        serverErrorFeedRangeRule,
                        faultInjector);

                ItemResponse<FaultInjectionTestObject> response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    result2.Id,
                    new PartitionKey(result2.Pk));

                Assert.IsNotNull(response.Diagnostics);
                this.ValidateFaultInjectionRuleNotApplied(response, serverErrorFeedRangeRule, 1);
                this.ValidateHitCount(serverErrorFeedRangeRule, 1);
            }
            finally
            {
                serverErrorFeedRangeRule.Disable();
            }
        }

        private async Task<FaultInjectionTestObject> GetItemInFeedRangeAsync(FeedRange feedRange)
        {
            using FeedIterator<FaultInjectionTestObject> iterator =
                this.highThroughputContainer.GetItemQueryIterator<FaultInjectionTestObject>(
                    feedRange, new QueryDefinition("SELECT * FROM c"));
            while (iterator.HasMoreResults)
            {
                FaultInjectionTestObject item = (await iterator.ReadNextAsync()).FirstOrDefault();
                if (item != null)
                {
                    return item;
                }
            }

            Assert.Fail($"The partition-filter fixture must contain an item in feed range {feedRange}.");
            return null;
        }

        private async Task InitializeHighThroughputContainerAsync()
        {
            if (this.database != null)
            {
                string containerId = TestCommon.FaultInjectionHTPContainerName + "-" + Guid.NewGuid();
                // Retain the cleanup target even if creation succeeds but its response is lost.
                this.highThroughputContainer = this.database.GetContainer(containerId);
                ContainerResponse cr = await this.database.CreateContainerAsync(
                    id: containerId,
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
            }
        }

        //<summary>
        //Tests to see if response delay rule is applied, note that here the request should reach the backend
        //</summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test response delay, request should be sent")]
        [Owner("ntripician")]
        public async Task FIProxyResponseDelay()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = Guid.NewGuid().ToString();
            string pk = "deleteMe";
            TimeSpan responseDelay = TimeSpan.FromSeconds(2);

            //create rule
            string responseDelayRuleId = "responseDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: responseDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(responseDelay)
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
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                await this.WarmUpThinClientAsync(requireWriteProxy: true);
                delayRule.Enable();

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

                FaultInjectionTestObject createdItem = new FaultInjectionTestObject
                {
                    Id = id,
                    Pk = pk
                };

                ItemResponse<FaultInjectionTestObject> createResponse = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                   createdItem,
                   new PartitionKey(pk));

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
                this.ValidateHitCount(delayRule, 1);
                Assert.AreEqual(1, this.GetRuleExecutionCount(faultInjector, delayRule));

                ItemResponse<FaultInjectionTestObject> readResponse = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    id,
                    new PartitionKey(pk));

                //Check the create time is at least as long as the delay in the rule
                Assert.IsTrue(elapsed >= responseDelay, $"Expected at least {responseDelay} of response delay, observed {elapsed}.");
                this.ValidateHitCount(delayRule, 1);
                Assert.IsTrue(readResponse.StatusCode == HttpStatusCode.OK);
            }
            finally
            {
                delayRule.Disable();
                await this.DeleteOwnedItemAsync(id, pk);
            }
        }

        //<summary>
        //Tests to see if response delay rule is applied, note that here the request should NOT reach the backend as delay is applied before sending request
        //</summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test send delay, request should not be sent")]
        [Owner("ntripician")]
        public async Task FIProxySendDelay()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = Guid.NewGuid().ToString();
            string pk = "deleteMe";

            //create rule
            string sendDelayRuleId = "sendDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: sendDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.SendDelay)
                        // ThinClient's first HTTP attempt expires after six seconds, independently of RequestTimeout.
                        .WithDelay(TimeSpan.FromSeconds(15))
                        .WithTimes(10)
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
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                await this.WarmUpThinClientAsync(requireWriteProxy: true);
                delayRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

                FaultInjectionTestObject createdItem = new FaultInjectionTestObject
                {
                    Id = id,
                    Pk = pk
                };

                CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                        createdItem,
                        new PartitionKey(pk)));
                Assert.AreEqual(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
                Assert.AreEqual((int)SubStatusCodes.TransportGenerated503, exception.SubStatusCode);

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                int executions = this.GetRuleExecutionCount(faultInjector, delayRule);
                Assert.IsTrue(executions >= 1, $"The send delay must be applied before timing out. Diagnostics: {exception.Diagnostics}");
                this.ValidateHitCount(delayRule, executions);

                // Every retry must time out before sending; its rule execution is counted separately.
                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                CosmosException notFound = await Assert.ThrowsExceptionAsync<CosmosException>(
                    () => this.container.ReadItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk)));
                Assert.AreEqual(HttpStatusCode.NotFound, notFound.StatusCode);
            }
            finally
            {
                delayRule.Disable();
                await this.DeleteOwnedItemAsync(id, pk);
            }
        }


        //<summary>
        //Tests to see if specific server error responses are applied, tests read and create item
        //</summary>
        [TestMethod]
        [Timeout(Timeout * 100)]
        [Description("Test server error responses")]
        [Owner("ntripician")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Gone, (int)StatusCodes.Gone, (int)SubStatusCodes.ServerGenerated410, DisplayName = "Gone")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.InternalServerError, (int)StatusCodes.InternalServerError, (int)SubStatusCodes.Unknown, DisplayName = "InternalServerError")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.TooManyRequests, (int)StatusCodes.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded, DisplayName = "TooManyRequests")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, (int)StatusCodes.NotFound, (int)SubStatusCodes.ReadSessionNotAvailable, DisplayName = "ReadSessionNotAvailable")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Timeout, (int)StatusCodes.RequestTimeout, (int)SubStatusCodes.Unknown, DisplayName = "Timeout")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsMigrating, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingPartitionMigration, DisplayName = "PartitionIsMigrating")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsSplitting, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingSplit, DisplayName = "PartitionIsSplitting")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Gone, (int)StatusCodes.Gone, (int)SubStatusCodes.ServerGenerated410, DisplayName = "Gone - write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.InternalServerError, (int)StatusCodes.InternalServerError, (int)SubStatusCodes.Unknown, DisplayName = "InternalServerError - write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.TooManyRequests, (int)StatusCodes.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded, DisplayName = "TooManyRequests - write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, (int)StatusCodes.NotFound, (int)SubStatusCodes.ReadSessionNotAvailable, DisplayName = "ReadSessionNotAvailable - write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Timeout, (int)StatusCodes.RequestTimeout, (int)SubStatusCodes.Unknown, DisplayName = "Timeout - write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsMigrating, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingPartitionMigration, DisplayName = "PartitionIsMigrating - write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsSplitting, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingSplit, DisplayName = "PartitionIsSplitting - write")]
        public async Task FIProxyServerResponse(
            FaultInjectionOperationType faultInjectionOperationType, 
            FaultInjectionServerErrorType faultInjectionServerErrorType,
            int statusCodes,
            int subStatusCode)
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = Guid.NewGuid().ToString();
            string pk = "deleteMe";

            string serverErrorResponseRuleId = "serverErrorResponseRule-" + Guid.NewGuid().ToString();
            FaultInjectionRuleBuilder ruleBuilder = new FaultInjectionRuleBuilder(
                id: serverErrorResponseRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                    .WithOperationType(faultInjectionOperationType)
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(faultInjectionServerErrorType)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5));
            if (faultInjectionServerErrorType == FaultInjectionServerErrorType.Gone)
            {
                ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => ruleBuilder.Build());
                StringAssert.Contains(exception.Message, "Gone error type is not supported for Gateway connection type.");
                return;
            }

            FaultInjectionRule serverErrorResponseRule = ruleBuilder.Build();
            serverErrorResponseRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { serverErrorResponseRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    MaxRetryAttemptsOnRateLimitedRequests = 0,
                    RequestTimeout = TimeSpan.FromSeconds(15),
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                await this.WarmUpThinClientAsync(requireWriteProxy: faultInjectionOperationType == FaultInjectionOperationType.CreateItem);
                serverErrorResponseRule.Enable();

                ItemResponse<FaultInjectionTestObject> response = null;
                CosmosException exception = null;

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;


                try
                {
                    if (faultInjectionOperationType == FaultInjectionOperationType.CreateItem)
                    {
                        FaultInjectionTestObject createdItem = new FaultInjectionTestObject
                        {
                            Id = id,
                            Pk = pk
                        };

                        response = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                            createdItem,
                            new PartitionKey(pk));
                    }
                    else
                    {
                        response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                            "testId",
                            new PartitionKey("pk"));
                    }
                }
                catch (CosmosException ex)
                {
                    exception = ex;
                }


                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                Assert.IsTrue(this.GetRuleExecutionCount(faultInjector, serverErrorResponseRule) >= 1,
                    $"The {faultInjectionOperationType} must execute rule {serverErrorResponseRuleId}, even if retried.");
                this.ValidateRuleHit(serverErrorResponseRule, 1);

                if (response != null)
                {
                    Assert.AreNotEqual(FaultInjectionServerErrorType.TooManyRequests, faultInjectionServerErrorType,
                        "Throttling retries are disabled; an injected 429 must not succeed.");
                    Assert.AreEqual(
                        faultInjectionOperationType == FaultInjectionOperationType.CreateItem ? HttpStatusCode.Created : HttpStatusCode.OK,
                        response.StatusCode);
                    Assert.AreEqual(faultInjectionOperationType == FaultInjectionOperationType.CreateItem ? id : "testId", response.Resource.Id);
                }
                else
                {
                    Assert.IsNotNull(exception, "The operation must return a response or a CosmosException.");
                    if (faultInjectionServerErrorType == FaultInjectionServerErrorType.Timeout)
                    {
                        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
                        Assert.AreEqual((int)SubStatusCodes.TransportGenerated503, exception.SubStatusCode);
                    }
                    else
                    {
                        Assert.AreEqual(statusCodes, (int)exception.StatusCode);
                        Assert.AreEqual(subStatusCode, exception.SubStatusCode);
                    }
                }

                if (faultInjectionServerErrorType == FaultInjectionServerErrorType.Timeout)
                {
                    // The HTTP deadline cancels the injected delay before a synthetic 408 can be returned.
                    Assert.IsTrue(elapsed.TotalSeconds >= 6, $"Expected an HTTP timeout before retry/completion, observed {elapsed}.");
                }
                else
                {
                    // WithTimes(1) can let a retry succeed. Validate the injected status/substatus in
                    // this operation's diagnostics rather than requiring its final error message.
                    this.ValidateInjectedResponse(
                        response?.Diagnostics ?? exception.Diagnostics, statusCodes, subStatusCode);
                }
            }
            finally
            {
                serverErrorResponseRule.Disable();
                if (this.container != null && faultInjectionOperationType == FaultInjectionOperationType.CreateItem)
                {
                    await this.DeleteOwnedItemAsync(id, pk);
                }
            }
        }

        /// <summary>
        /// Tests to see if fault injection rules are applied the correct number of times when a hit limit is set
        /// </summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test hit limit")]
        [Owner("ntripician")]
        public async Task FIProxyHitLimit()
        {
            string hitCountRuleId = "hitCountRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule hitCountRule = new FaultInjectionRuleBuilder(
                id: hitCountRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                    .WithOperationType(FaultInjectionOperationType.ReadItem)
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
                    .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .WithHitLimit(2)
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            hitCountRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { hitCountRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    MaxRetryAttemptsOnRateLimitedRequests = 0,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                await this.WarmUpThinClientAsync();
                hitCountRule.Enable();

                //Since the hit limit is 2, the rule should be applied twice and then become invalid
                for (int i = 0; i < 4; i++)
                {
                    int previousExecutions = this.GetRuleExecutionCount(faultInjector, hitCountRule);
                    if (i < 2)
                    {
                        CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                            () => this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                                "testId", new PartitionKey("pk")));
                        this.ValidateFaultInjectionRuleApplication(
                            exception, (int)HttpStatusCode.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded,
                            hitCountRule, faultInjector, previousExecutions);
                    }
                    else
                    {
                        ItemResponse<FaultInjectionTestObject> response =
                            await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
                        this.ValidateFaultInjectionRuleNotApplied(response, hitCountRule, 2);
                    }

                    this.ValidateHitCount(hitCountRule, Math.Min(i + 1, 2));
                    Assert.AreEqual(Math.Min(i + 1, 2), this.GetRuleExecutionCount(faultInjector, hitCountRule));
                }
            }
            finally
            {
                hitCountRule.Disable();
            }
        }

        /// <summary>
        /// Injection rate is set to 0.5, so the rule should be applied ~50% of the time
        /// This test will fail ~1.2% of the time due to the random nature of the test
        /// 98.8% of the time the rule will be applied between 38 and 62 times out of 100 with an injection rate of 50%
        /// </summary>
        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test injection rate")]
        [Owner("ntripician")]
        public async Task FIProxyInjectionRate()
        {
            string thresholdRuleId = "hitCountRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule thresholdRule = new FaultInjectionRuleBuilder(
                id: thresholdRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithInjectionRate(.5)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            thresholdRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { thresholdRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    MaxRetryAttemptsOnRateLimitedRequests = 0,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                await this.WarmUpThinClientAsync();
                thresholdRule.Enable();
                int injectedOperations = 0;

                for (int i = 0; i < 100; i++)
                {
                    int previousExecutions = this.GetRuleExecutionCount(faultInjector, thresholdRule);
                    bool injected = false;
                    try
                    {
                        ItemResponse<FaultInjectionTestObject> response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                            "testId",
                            new PartitionKey("pk"));

                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        Assert.AreEqual("testId", response.Resource.Id);
                    }
                    catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        this.ValidateFaultInjectionRuleApplication(
                            ex, (int)HttpStatusCode.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded,
                            thresholdRule, faultInjector, previousExecutions);
                        injected = true;
                        injectedOperations++;
                    }

                    Assert.AreEqual(injected ? 1 : 0,
                        this.GetRuleExecutionCount(faultInjector, thresholdRule) - previousExecutions,
                        $"Logical read {i} must have exactly one application on 429 and none on success.");
                }

                this.ValidateHitCount(thresholdRule, injectedOperations);
                Assert.IsTrue(injectedOperations >= 38, "This is Expected to fail 0.602% of the time");
                Assert.IsTrue(injectedOperations <= 62, "This is Expected to fail 0.602% of the time");
            }
            finally
            {
                thresholdRule.Disable();
            }
        }

        /// <summary>
        /// Tests to see if fault injection rules are applied to the correct connection type
        /// </summary>
        [TestMethod]
        public async Task FIOnlyGateway()
        {
            string ruleId = "Rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.InternalServerError)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            rule.Disable();

            try
            {
                //Test on direct mode client
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { rule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ConnectionMode = ConnectionMode.Direct,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                ItemResponse<FaultInjectionTestObject> response;

                rule.Enable();

                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    "testId",
                    new PartitionKey("pk"));

                this.ValidateFaultInjectionRuleNotApplied(response, rule);

                rule.Disable();
                this.fiClient.Dispose();

                //Test on gateway mode client
                cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                await this.WarmUpThinClientAsync();
                rule.Enable();

                CosmosDiagnostics diagnostics;
                try
                {
                    response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId", new PartitionKey("pk"));
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.AreEqual("testId", response.Resource.Id);
                    diagnostics = response.Diagnostics;
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.InternalServerError, ex.StatusCode);
                    Assert.AreEqual((int)SubStatusCodes.Unknown, ex.SubStatusCode);
                    diagnostics = ex.Diagnostics;
                }

                Assert.IsTrue(this.GetRuleExecutionCount(faultInjector, rule) >= 1,
                    "The Gateway operation must execute the rule, even if its retry succeeds.");
                this.ValidateInjectedResponse(diagnostics, (int)HttpStatusCode.InternalServerError, (int)SubStatusCodes.Unknown);
            }
            finally
            {
                rule.Disable();
            }
        }

        private async Task WarmUpThinClientAsync(bool requireWriteProxy = false)
        {
            await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>("testId", new PartitionKey("pk"));
            GlobalEndpointManager endpointManager = this.fiClient.ClientContext.DocumentClient.GlobalEndpointManager;
            Assert.IsTrue(endpointManager.HasThinClientReadLocations, "The account must advertise ThinClient read endpoints.");
            if (requireWriteProxy)
            {
                Assert.IsTrue(endpointManager.HasThinClientWriteLocations, "The account must advertise ThinClient write endpoints.");
            }

            IEnumerable<Uri> endpoints = endpointManager.ThinClientReadEndpoints;
            if (requireWriteProxy)
            {
                endpoints = endpoints.Concat(endpointManager.ThinClientWriteEndpoints);
            }

            List<Uri> requiredEndpoints = endpoints.Distinct().ToList();
            Assert.IsTrue(requiredEndpoints.Count > 0, "The proxy test requires advertised regional endpoints.");
            ValueStopwatch stopwatch = ValueStopwatch.StartNew();
            do
            {
                // An overlapping background probe is not awaited by RunThinClientProbeCycleAsync.
                await endpointManager.RunThinClientProbeCycleAsync();
                if (requiredEndpoints.All(endpointManager.IsProxyEndpointHealthy))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(30));

            Assert.Fail($"ThinClient endpoints did not become healthy: {string.Join(", ", requiredEndpoints.Where(endpoint => !endpointManager.IsProxyEndpointHealthy(endpoint)))}");
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
            CosmosException ex,
            int statusCode,
            int subStatusCode,
            FaultInjectionRule rule,
            FaultInjector faultInjector,
            int previousExecutions = 0)
        {
            Assert.AreEqual(previousExecutions + 1, this.GetRuleExecutionCount(faultInjector, rule),
                $"The current operation must execute rule {rule.GetId()} exactly once.");
            Assert.AreEqual(statusCode, (int)ex.StatusCode);
            Assert.AreEqual(subStatusCode, ex.SubStatusCode);
            this.ValidateInjectedResponse(ex.Diagnostics, statusCode, subStatusCode);
        }

        private int GetRuleExecutionCount(FaultInjector faultInjector, FaultInjectionRule rule)
        {
            FaultInjectionApplicationContext context = faultInjector.GetApplicationContext();
            if (context == null || !context.TryGetRuleExecutionsByRuleId(rule.GetId(), out List<(DateTime, Guid)> executions))
            {
                return 0;
            }

            // HTTP injection uses its own request ID, not the final service activity ID.
            foreach ((DateTime _, Guid requestId) in executions)
            {
                Assert.AreNotEqual(Guid.Empty, requestId);
                Assert.AreEqual(rule.GetId(), faultInjector.GetFaultInjectionRuleId(requestId));
            }

            return executions.Count;
        }

        private void ValidateInjectedResponse(CosmosDiagnostics diagnostics, int statusCode, int subStatusCode)
        {
            Assert.IsInstanceOfType(diagnostics, typeof(CosmosTraceDiagnostics));
            IEnumerable<HttpResponseMessage> responses = GetHttpResponses(((CosmosTraceDiagnostics)diagnostics).Value);
            Assert.IsTrue(responses.Any(response =>
                response.RequestMessage != null &&
                response.RequestMessage.Options.TryGetValue(new HttpRequestOptionsKey<bool>(CosmosHttpClientCore.FaultInjectionIsProxy), out bool proxyRequest) &&
                proxyRequest &&
                response.RequestMessage.Options.TryGetValue(new HttpRequestOptionsKey<bool>(CosmosHttpClientCore.FaultInjectionResponse), out bool injectedResponse) &&
                injectedResponse &&
                (int)response.StatusCode == statusCode &&
                (response.Headers.TryGetValues(WFConstants.BackendHeaders.SubStatus, out IEnumerable<string> values)
                    ? values.Single() == subStatusCode.ToString()
                    : subStatusCode == (int)SubStatusCodes.Unknown)),
                $"Expected a marked synthetic ThinClient response {statusCode}/{subStatusCode} in the operation's HTTP diagnostics: {diagnostics}");
        }

        private static IEnumerable<HttpResponseMessage> GetHttpResponses(ITrace trace)
        {
            foreach (ClientSideRequestStatisticsTraceDatum statistics in trace.Data.Values.OfType<ClientSideRequestStatisticsTraceDatum>())
            {
                foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics response in statistics.HttpResponseStatisticsList)
                {
                    if (response.HttpResponseMessage != null)
                    {
                        yield return response.HttpResponseMessage;
                    }
                }
            }

            foreach (ITrace child in trace.Children)
            {
                foreach (HttpResponseMessage response in GetHttpResponses(child))
                {
                    yield return response;
                }
            }
        }

        private async Task DeleteOwnedItemAsync(string id, string pk)
        {
            try
            {
                await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                System.Diagnostics.Trace.TraceInformation("The test-owned item {0} in partition {1} was already absent.", id, pk);
            }
        }
    }
}
