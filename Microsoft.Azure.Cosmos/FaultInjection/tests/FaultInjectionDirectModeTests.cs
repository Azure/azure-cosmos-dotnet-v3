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
    public class FaultInjectionDirectModeTests
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

        private static readonly IReadOnlyList<OperationType> operations = new List<OperationType>
        {
            OperationType.Read,
            OperationType.Replace,
            OperationType.Create,
            OperationType.Delete,
            OperationType.Query,
            OperationType.Patch
        };

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
            this.client.Dispose();
            this.fiClient.Dispose();
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests filtering rules on operation type")]
        [DataRow(0, DisplayName = "Read")]
        [DataRow(1, DisplayName = "Replace")]
        [DataRow(2, DisplayName = "Create")]
        [DataRow(3, DisplayName = "Delete")]
        [DataRow(4, DisplayName = "Query")]
        [DataRow(5, DisplayName = "Patch")]

        public async Task FaultInjectionServerErrorRule_OperationTypeTest(int operation)
        {
            OperationType operationType = operations[operation];

            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            //Test Server gone, operation type will be ignored after getting the address
            string serverGoneRuleId = "serverGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverGoneRule = new FaultInjectionRuleBuilder(
                id: serverGoneRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            string tooManyRequestsRuleId = "tooManyRequestsRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule tooManyRequestsRule = new FaultInjectionRuleBuilder(
                id: tooManyRequestsRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            serverGoneRule.Disable();
            tooManyRequestsRule.Disable();

            FaultInjectionTestObject createdItem = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            try
            {
                //create client with fault injection
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serverGoneRule, tooManyRequestsRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                Assert.AreEqual(0, serverGoneRule.GetAddresses().Count);

                if (operationType != OperationType.Create)
                {
                    await this.container.CreateItemAsync(createdItem);
                }

                serverGoneRule.Enable();

                await this.PerformDocumentOperationAndCheckApplication(this.fiContainer, operationType, createdItem, serverGoneRule, (int)StatusCodes.Gone, (int)SubStatusCodes.ServerGenerated410);

                serverGoneRule.Disable();

                try
                {
                    if (operationType == OperationType.Delete)
                    {
                        await this.container.CreateItemAsync(createdItem);
                    }

                    if (operationType == OperationType.Create)
                    {
                        await this.container.DeleteItemAsync<FaultInjectionTestObject>(
                                createdItem.Id,
                                new PartitionKey(createdItem.Pk));
                    }
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }


                Assert.AreEqual(0, tooManyRequestsRule.GetAddresses().Count);

                tooManyRequestsRule.Enable();

                bool ruleApplied = operationType == OperationType.Read;

                await this.PerformDocumentOperationAndCheckApplication(this.fiContainer, operationType, createdItem, tooManyRequestsRule, (int)StatusCodes.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded, ruleApplied);

                if (operationType == OperationType.Read)
                {
                    this.ValidateHitCount(tooManyRequestsRule, 1);
                }
                else
                {
                    this.ValidateHitCount(tooManyRequestsRule, 0);
                }
            }
            finally
            {
                serverGoneRule.Disable();
                tooManyRequestsRule.Disable();

                if (this.container != null)
                {
                    try
                    {
                        await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                    }
                    catch (CosmosException)
                    {
                        // Ignore the exception
                    }
                }
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [Description("Tests filtering rule applications on physical endpoint - must be a single master account")]
        [DataRow(0, DisplayName = "Read")]
        [DataRow(1, DisplayName = "Replace")]
        [DataRow(2, DisplayName = "Create")]
        [DataRow(3, DisplayName = "Delete")]
        [DataRow(4, DisplayName = "Query")]
        [DataRow(5, DisplayName = "Patch")]

        public async Task FaultInjectionServerErrorRule_OperationTypeAddressTest(int operation)
        {
            OperationType operationType = operations[operation];

            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            List<string> preferredRegions = new List<string>() { };
            List<string> writeRegions = new List<string>();
            List<string> readRegions;

            GlobalEndpointManager globalEndpointManager = this.client.DocumentClient.GlobalEndpointManager;

            //create preferred regions so read and write requests are sent to different regions
            if (globalEndpointManager != null)
            {
                (writeRegions, readRegions) = await this.GetReadWriteEndpoints(globalEndpointManager);

                for (int i = 0; i < readRegions.Count; i++)
                {
                    if (writeRegions != null && writeRegions.Contains(readRegions[i]))
                    {
                        preferredRegions.Add(readRegions[i].ToString());
                    }
                    else
                    {
                        preferredRegions.Insert(0, readRegions[i].ToString());
                    }
                }
            }

            FaultInjectionTestObject item = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            string writeRegionServerGoneRuleId = "writeRegionServerGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule writeRegionServerGoneRule = new FaultInjectionRuleBuilder(
                id: writeRegionServerGoneRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            string primaryReplicaServerGoneRuleId = "primaryReplicaServerGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule primaryReplicaServerGoneRule = new FaultInjectionRuleBuilder(
                id: primaryReplicaServerGoneRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder(
                                TestCommon.FaultInjectionDatabaseName,
                                TestCommon.FaultInjectionContainerName,
                                FeedRange.FromPartitionKey(new PartitionKey(item.Pk)))
                                .WithReplicaCount(3)
                                .Build())
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            writeRegionServerGoneRule.Disable();
            primaryReplicaServerGoneRule.Disable();

            try
            {
                List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { writeRegionServerGoneRule, primaryReplicaServerGoneRule };
                FaultInjector faultInjector = new FaultInjector(ruleList);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ApplicationPreferredRegions = preferredRegions,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                if (operationType != OperationType.Create) await this.fiContainer.CreateItemAsync(item);
                else await this.fiContainer.ReadThroughputAsync();

                Assert.AreEqual(writeRegions.Count + 1, writeRegionServerGoneRule.GetRegionEndpoints().Count);

                globalEndpointManager = this.fiClient.DocumentClient.GlobalEndpointManager;

                writeRegionServerGoneRule.Enable();

                bool ruleApplied = operationType .IsWriteOperation();
                await this.PerformDocumentOperationAndCheckApplication(
                    this.fiContainer,
                    operationType,
                    item,
                    writeRegionServerGoneRule,
                    (int)StatusCodes.Gone,
                    (int)SubStatusCodes.ServerGenerated410,
                    ruleApplied);

                writeRegionServerGoneRule.Disable();

                Assert.AreEqual(globalEndpointManager.WriteEndpoints.Count + 1, primaryReplicaServerGoneRule.GetRegionEndpoints().Count);
                foreach (Uri region in globalEndpointManager.WriteEndpoints)
                {
                    Assert.IsTrue(primaryReplicaServerGoneRule.GetRegionEndpoints().Contains(region));
                }

                Assert.AreEqual(globalEndpointManager.WriteEndpoints.Count, primaryReplicaServerGoneRule.GetAddresses().Count);
            }
            finally
            {
                writeRegionServerGoneRule.Disable();
                primaryReplicaServerGoneRule.Disable();

                if (this.container != null)
                {
                    try
                    {
                        await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                    }
                    catch (CosmosException)
                    {
                        // Ignore the exception
                    }
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests filtering on region")]
        public async Task FaultInjectionServerErrorRule_RegionTest()
        {
            //Get regions for testing
            List<string> preferredRegions = new List<string>() { };
            List<string> readRegions;
            ReadOnlyDictionary<string, Uri> readEndpoints = new ReadOnlyDictionary<string, Uri>(new Dictionary<string, Uri>());

            GlobalEndpointManager globalEndpointManager = this.client.ClientContext.DocumentClient.GlobalEndpointManager;
            if (globalEndpointManager != null)
            {
                readEndpoints = globalEndpointManager.GetAvailableReadEndpointsByLocation();
                (_, readRegions) = await this.GetReadWriteEndpoints(globalEndpointManager);

                preferredRegions = new List<string>(readRegions);
            }

            //create fault injection rule for local region 
            string localRegionRuleId = "localRegionRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule localRegionRule = new FaultInjectionRuleBuilder(
                id: localRegionRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion(preferredRegions[0])
                        .WithConnectionType(FaultInjectionConnectionType.Direct)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
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
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
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
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                globalEndpointManager = this.fiClient.ClientContext.DocumentClient.GlobalEndpointManager;

                localRegionRule.Enable();
                remoteRegionRule.Enable();

                try
                {
                    //test that request to local region fails
                    ItemResponse<FaultInjectionTestObject> response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId2",
                    new PartitionKey("pk2"));
                }
                catch (DocumentClientException ex)
                {
                    this.ValidateHitCount(localRegionRule, 1);
                    this.ValidateHitCount(remoteRegionRule, 0);
                    this.ValidateFaultInjectionRuleApplication(
                        ex,
                        (int)HttpStatusCode.Gone,
                        localRegionRule);
                }
            }
            finally
            {
                //ensure rules are created with proper regions
                //must check here since the rules are initialized on first request call
                if (globalEndpointManager != null)
                {
                    Assert.AreEqual(1, localRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[0]], localRegionRule.GetRegionEndpoints()[0]);

                    Assert.AreEqual(1, remoteRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[1]], remoteRegionRule.GetRegionEndpoints()[0]);
                }

                localRegionRule.Disable();
                remoteRegionRule.Disable();
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests filtering on partition")]

        public async Task FaultInjectionServerErrorRule_PartitionTest()
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
                        .WithConnectionType(FaultInjectionConnectionType.Direct)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                    .WithTimes(1)
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
                FaultInjector = faultInjector,
                Serializer = this.serializer,
                MaxRetryAttemptsOnRateLimitedRequests = 0,
            };

            this.fiClient = new CosmosClient(
                this.connectionString,
                cosmosClientOptions);
            this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
            this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionHTPContainerName);

            serverErrorFeedRangeRule.Enable();

            //Test that rule is applied to the correct partition
            ItemResponse<FaultInjectionTestObject> response;

            response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    result1.Id,
                    new PartitionKey(result1.Pk));

            this.ValidateHitCount(serverErrorFeedRangeRule, 1);
            this.ValidateFaultInjectionRuleApplication(
                    response.Diagnostics,
                    (int)HttpStatusCode.TooManyRequests,
                    (int)SubStatusCodes.RUBudgetExceeded,
                    serverErrorFeedRangeRule);

            //test that rule is not applied to other partition
            try
            {
                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    result2.Id,
                    new PartitionKey(result2.Pk));

                Assert.IsNotNull(response.Diagnostics);
                Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);
                this.ValidateHitCount(serverErrorFeedRangeRule, 1);
            }
            finally
            {
                serverErrorFeedRangeRule.Disable();
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests send delay")]

        public async Task FaultInjectionServerErrorRule_ServerSendDelay()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            //create rule
            string sendDelayRuleId = "sendDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: sendDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.SendDelay)
                        .WithDelay(TimeSpan.FromSeconds(6))//request timeout is 65s
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
                    FaultInjector = faultInjector,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                delayRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

                FaultInjectionTestObject createdItem = new FaultInjectionTestObject
                {
                    Id = id,
                    Pk = pk
                };

                try
                {
                    ItemResponse<FaultInjectionTestObject> ir = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                    createdItem,
                    new PartitionKey(pk));
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.RequestTimeout, ex.StatusCode);
                }

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                this.ValidateHitCount(delayRule, 1);

                //Check the create time is at least as long as the delay in the rule
                Assert.IsTrue(elapsed.TotalSeconds >= 6);
            }
            finally
            {
                delayRule.Disable();
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests response delay")]

        public async Task FaultInjectionServerErrorRule_ServerResponseDelay()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            //create rule
            string responseDelayRuleId = "responseDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: responseDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromSeconds(10))
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
                    FaultInjector = faultInjector,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                delayRule.Enable();

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

                FaultInjectionTestObject createdItem = new FaultInjectionTestObject
                {
                    Id = id,
                    Pk = pk
                };

                try
                {
                    await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                        createdItem,
                        new PartitionKey(pk));
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.RequestTimeout, ex.StatusCode);
                }

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                this.ValidateHitCount(delayRule, 1);

                ItemResponse<FaultInjectionTestObject> readResponse = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    id,
                    new PartitionKey(pk));

                //Check the create time is at least as long as the delay in the rule
                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                this.ValidateHitCount(delayRule, 1);
                Assert.IsTrue(readResponse.StatusCode == HttpStatusCode.OK);
            }
            finally
            {
                delayRule.Disable();
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests response delay")]

        public async Task FaultInjectionServerErrorRule_ServerTimeout()
        {
            string timeoutRuleId = "timeoutRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule timeoutRule = new FaultInjectionRuleBuilder(
                id: timeoutRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Timeout)
                        .WithDelay(TimeSpan.FromSeconds(6))
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            timeoutRule.Disable();

            try
            {
                //create client with fault injection
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { timeoutRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                timeoutRule.Enable();

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

                try
                {
                    await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId",
                        new PartitionKey("pk"));
                }
                catch (CosmosException ex)
                {
                    this.ValidateFaultInjectionRuleApplication(
                        ex,
                        (int)HttpStatusCode.RequestTimeout,
                        timeoutRule);
                }

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                this.ValidateHitCount(timeoutRule, 1);
            }
            finally
            {
                timeoutRule.Disable();
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests injection a connection timeout")]
        public async Task FaultInjectionServerErrorRule_ConnecitonTimeout()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            string connectionTimeoutRuleId = "serverConnectionTimeoutRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule connectionTimeoutRule = new FaultInjectionRuleBuilder(
                id: connectionTimeoutRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
                        .WithDelay(TimeSpan.FromSeconds(2))
                        .WithTimes(10)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            FaultInjectionTestObject createdItem = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            connectionTimeoutRule.Disable();

            try
            {
                //create client with fault injection
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { connectionTimeoutRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                    OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1)
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                connectionTimeoutRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;
                ItemResponse<FaultInjectionTestObject> itemResponse = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(createdItem);
                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                Assert.IsTrue(elapsed.TotalSeconds >= 2);
                Assert.IsNotNull(itemResponse);
                Assert.IsTrue(connectionTimeoutRule.GetHitCount() == 1 || connectionTimeoutRule.GetHitCount() == 2);
            }
            finally
            {
                connectionTimeoutRule.Disable();
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests filtering connection delay")]

        public async Task FaultInjectionServerErrorRule_ConnecitonDelay()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            string connectionDelayRuleId = "serverConnectionDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule connectionDelayRule = new FaultInjectionRuleBuilder(
                id: connectionDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ConnectionDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(100))
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            FaultInjectionTestObject createdItem = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            connectionDelayRule.Disable();

            try
            {
                //create client with fault injection
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { connectionDelayRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                    OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1)
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                connectionDelayRule.Enable();

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;
                ItemResponse<FaultInjectionTestObject> itemResponse = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(createdItem);
                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                Assert.IsTrue(elapsed.TotalMilliseconds >= 100);

                Assert.IsNotNull(itemResponse);
                Assert.IsTrue(connectionDelayRule.GetHitCount() == 1 || connectionDelayRule.GetHitCount() == 2);
                Assert.IsTrue((int)itemResponse.StatusCode == (int)StatusCodes.Created);
            }
            finally
            {
                connectionDelayRule.Disable();

                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests injecting a server error response")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Gone, 410, 21005, DisplayName = "Gone")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.InternalServerError, 500, 0, DisplayName = "InternalServerError")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.RetryWith, 449, 0, DisplayName = "RetryWith")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.TooManyRequests, 429, 3200, DisplayName = "TooManyRequests")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, 404, 1002, DisplayName = "ReadSessionNotAvailable")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Timeout, 410, 20001, DisplayName = "Timeout")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsMigrating, 410, 1008, DisplayName = "PartitionIsMigrating")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsSplitting, 410, 1007, DisplayName = "PartitionIsSplitting")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Gone, 410, 21005, DisplayName = "Gone Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.InternalServerError, 500, 0, DisplayName = "InternalServerError Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.RetryWith, 449, 0, DisplayName = "RetryWith Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.TooManyRequests, 429, 3200, DisplayName = "TooManyRequests Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, 404, 1002, DisplayName = "ReadSessionNotAvailable Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Timeout, 410, 20001, DisplayName = "Timeout Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsMigrating, 410, 1008, DisplayName = "PartitionIsMigrating Write")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsSplitting, 410, 1007, DisplayName = "PartitionIsSplitting Write")]
        public async Task FaultInjectionServerErrorRule_ServerErrorResponseTest(
            FaultInjectionOperationType faultInjectionOperationType,
            FaultInjectionServerErrorType serverErrorType,
            int errorStatusCode,
            int substatusCode)
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            OperationType operationType = faultInjectionOperationType == FaultInjectionOperationType.ReadItem
                ? OperationType.Read
                : OperationType.Create;

            FaultInjectionTestObject createdItem = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            if (operationType != OperationType.Create) await this.container.CreateItemAsync(createdItem);

            string serverErrorResponseRuleId = "serverErrorResponseRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverErrorResponseRule = new FaultInjectionRuleBuilder(
                id: serverErrorResponseRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                    .WithOperationType(faultInjectionOperationType)
                    .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(serverErrorType)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            serverErrorResponseRule.Disable();

            try
            {
                //create client with fault injection
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serverErrorResponseRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                serverErrorResponseRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;
                
                await this.PerformDocumentOperationAndCheckApplication(
                    this.fiContainer, 
                    operationType, 
                    createdItem, 
                    serverErrorResponseRule, 
                    errorStatusCode, 
                    substatusCode);
                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                if (serverErrorType == FaultInjectionServerErrorType.Timeout)
                {
                    ChaosInterceptor interceptor = faultInjector.GetChaosInterceptor() as ChaosInterceptor;

                    Assert.IsNotNull(interceptor);
                    Assert.IsTrue(
                        elapsed.TotalSeconds
                        >= interceptor.GetRequestTimeout().TotalSeconds);
                }

                this.ValidateHitCount(serverErrorResponseRule, 1);
            }
            finally
            {
                serverErrorResponseRule.Disable();
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests hit count limit")]

        public async Task FaultInjectionServerErrorRule_HitCountTest()
        {
            string hitCountRuleId = "hitCountRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule hitCountRule = new FaultInjectionRuleBuilder(
                id: hitCountRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                    .WithConnectionType(FaultInjectionConnectionType.Direct)
                    .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
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
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                hitCountRule.Enable();

                ItemResponse<FaultInjectionTestObject> response;

                //Since the hit limit is 2, the rule should be applied twice and then become invalid
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId",
                        new PartitionKey("pk"));
                        Assert.IsNotNull(response);

                        if (i > 2)
                        {
                            this.ValidateFaultInjectionRuleNotApplied(response, hitCountRule, 2);
                        }
                    }
                    catch (DocumentClientException ex)
                    {
                        this.ValidateFaultInjectionRuleApplication(ex, (int)HttpStatusCode.Gone, hitCountRule);
                        this.ValidateHitCount(hitCountRule, i + 1);
                    }
                }
            }
            finally
            {
                hitCountRule.Disable();
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests endpoint filtering with including primary replica")]

        public async Task FaultInjectionServerErrorRule_IncludePrimaryTest()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            //create container with high throughput to create multiple feed ranges
            await this.InitializeHighThroughputContainerAsync();

            List<FeedRange> feedRanges =  (List<FeedRange>)await this.highThroughputContainer.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges != null && feedRanges.Count > 0);

            string includePrimaryServerGoneRuleId = "includePrimaryServerGoneRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule includePrimaryServerGoneRule = new FaultInjectionRuleBuilder(
                id: includePrimaryServerGoneRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithEndpoint(
                            new FaultInjectionEndpointBuilder(
                                    TestCommon.FaultInjectionDatabaseName, 
                                    TestCommon.FaultInjectionHTPContainerName,
                                    feedRanges[1])
                                .WithReplicaCount(1)
                                .WithIncludePrimary(true)
                                .Build())
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            FaultInjectionTestObject createdItem = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            includePrimaryServerGoneRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { includePrimaryServerGoneRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionHTPContainerName);

                includePrimaryServerGoneRule.Enable();
                await this.PerformDocumentOperationAndCheckApplication(
                    this.fiContainer, 
                    OperationType.Create, 
                    createdItem, 
                    includePrimaryServerGoneRule, 
                    410, 
                    21005);

                this.ValidateHitCount(includePrimaryServerGoneRule, 1);

                await this.PerformDocumentOperationAndCheckApplication(
                    this.fiContainer,
                    OperationType.Upsert,
                    createdItem,
                    includePrimaryServerGoneRule,
                    410,
                    21005);

                this.ValidateHitCount(includePrimaryServerGoneRule, 2);
            }
            finally
            {
                includePrimaryServerGoneRule.Disable();
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests apply percent")]
        public async Task FaultInjectionServerErrorRule_InjectionRateTest()
        {
            string thresholdRuleId = "hitCountRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule thresholdRule = new FaultInjectionRuleBuilder(
                id: thresholdRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
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
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);
                this.fiDatabase = this.fiClient.GetDatabase(this.database.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container.Id);

                ItemResponse<FaultInjectionTestObject> response;

                thresholdRule.Enable();

                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                            "testId",
                            new PartitionKey("pk"));

                        Assert.IsNotNull(response);
                    }
                    catch (Exception)
                    {
                        //ignore
                    }

                }

                Assert.IsTrue(thresholdRule.GetHitCount() >= 38, "This is Expected to fail 0.602% of the time");
                Assert.IsTrue(thresholdRule.GetHitCount() <= 62, "This is Expected to fail 0.602% of the time");
            }
            finally
            {
                thresholdRule.Disable();
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests fault injection connection error rules")]

        public async Task FaultInjectionConnectionErrorRule_Test()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";
            string id2 = "id2";
            string pk2 = "deleteMe2";

            string ruldId = "connectionErrorRule-close-" + Guid.NewGuid().ToString();
            FaultInjectionRule connectionErrorRule = new FaultInjectionRuleBuilder(
                id: ruldId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionConnectionErrorType.ReceiveStreamClosed)
                        .WithInterval(TimeSpan.FromSeconds(1))
                        .WithThreshold(1.0)
                        .Build())
                .WithDuration(TimeSpan.FromSeconds(30))
                .Build();

            FaultInjectionTestObject createdItem = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };

            FaultInjectionTestObject createdItem2 = new FaultInjectionTestObject
            {
                Id = id2,
                Pk = pk2
            };

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { connectionErrorRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                ChaosInterceptor interceptor = faultInjector.GetChaosInterceptor() as ChaosInterceptor;
                Assert.IsNotNull(interceptor);

                try
                {
                    await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(createdItem);
                }
                catch
                {
                    //ignore
                }

                FaultInjectionDynamicChannelStore channelStore = interceptor.GetChannelStore();
                Assert.IsTrue(channelStore.GetAllChannels().Count > 0);
                List<Guid> channelGuids = channelStore.GetAllChannelIds();

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));

                    await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(createdItem2);
                }
                catch
                {
                    //ignore
                }

                Assert.IsTrue(connectionErrorRule.GetHitCount() >= 1);

                await Task.Delay(TimeSpan.FromSeconds(2));

                for (int i = 0; i < 10; i++)
                {
                    await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        id,
                        new PartitionKey(pk));
                }

                int hitCount = (int)connectionErrorRule.GetHitCount();
                connectionErrorRule.Disable();

                Assert.IsTrue(connectionErrorRule.GetHitCount() == hitCount);

                bool disposedChannel = false;
                foreach (Guid channelGuid in channelGuids)
                {
                    disposedChannel = disposedChannel || channelStore.GetAllChannelIds().Contains(channelGuid);
                }
                Assert.IsTrue(disposedChannel);

            }
            finally
            {
                connectionErrorRule.Disable();
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id2, new PartitionKey(pk2));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Owner("nalutripician")]
        [Description("Tests ReadFeed FaultInjection")]
        public async Task FaultInjectionServerErrorRule_ReadFeedTest()
        {
            //id and partitionkey of item that is to be created, will want to delete after test
            string id = "id";
            string pk = "deleteMe";

            string readFeedId = "readFeadRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule readFeedRule = new FaultInjectionRuleBuilder(
                id: readFeedId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadFeed)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            string changeFeedContainerName = "changeFeedContainer-" + Guid.NewGuid().ToString();
            ContainerProperties containerProperties = new ContainerProperties
            {
                Id = changeFeedContainerName,
                PartitionKeyPath = "/partitionKey"
            };

            FaultInjectionTestObject createdItem = new FaultInjectionTestObject
            {
                Id = id,
                Pk = pk
            };
            
            await this.container.CreateItemAsync<FaultInjectionTestObject>(createdItem);
            Container leaseContainer = await this.database.CreateContainerIfNotExistsAsync(containerProperties, 400);

            readFeedRule.Disable();

            try
            {
                List<FaultInjectionRule> ruleList = new List<FaultInjectionRule> { readFeedRule };
                FaultInjector faultInjector = new FaultInjector(ruleList);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    FaultInjector = faultInjector,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    cosmosClientOptions);
                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);
                leaseContainer = this.fiDatabase.GetContainer(changeFeedContainerName);

                ManualResetEvent changeFeedRan = new ManualResetEvent(false);

                ChangeFeedProcessor changeFeedProcessor = this.fiContainer.GetChangeFeedProcessorBuilder<FaultInjectionTestObject>(
                TestCommon.FaultInjectionDatabaseName,
                (ChangeFeedProcessorContext context, IReadOnlyCollection<FaultInjectionTestObject> docs, CancellationToken token) =>
                {
                    Assert.Fail("Change Feed Should Fail");
                    return Task.CompletedTask;
                })
                .WithInstanceName(TestCommon.FaultInjectionContainerName)
                .WithLeaseContainer(leaseContainer)
                .WithStartFromBeginning()
                .WithErrorNotification((string lease, Exception exception) =>
                {
                    if (exception is CosmosException cosmosException)
                    {
                        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, cosmosException.StatusCode);
                    }
                    else
                    {
                        Assert.Fail("Unexpected Exception");
                    }

                    changeFeedRan.Set();
                    return Task.CompletedTask;
                })
                .Build();

                readFeedRule.Enable();

                await changeFeedProcessor.StartAsync();

                await Task.Delay(1000);

                try
                {
                    bool wasProcessed = changeFeedRan.WaitOne(60000);
                    Assert.IsTrue(wasProcessed, "Timed out waiting for handler to execute");
                }
                finally
                {
                    await changeFeedProcessor.StopAsync();
                    readFeedRule.Disable();
                }

            }
            finally
            {
                readFeedRule.Disable();
                try
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
                catch (CosmosException)
                {
                    // Ignore the exception
                }
                leaseContainer = this.fiDatabase.GetContainer(changeFeedContainerName);
                await leaseContainer.DeleteContainerAsync();
            }
        }


        private async Task PerformDocumentOperationAndCheckApplication(
            Container testContainer, 
            OperationType operationType, 
            FaultInjectionTestObject item,
            FaultInjectionRule rule,
            int expectedStatusCode,
            int expectedSubStatusCodes,
            bool ruleApplied = true)
        {
            try
            {
                if (operationType == OperationType.Query)
                {
                    QueryRequestOptions queryOptions = new QueryRequestOptions();
                    string query = String.Format("SELECT * FROM c WHERE c.Id = '{0}'", item.Id);
                    FeedResponse<FaultInjectionTestObject> queryResponse = await testContainer.GetItemQueryIterator<FaultInjectionTestObject>(query, requestOptions: queryOptions).ReadNextAsync();

                    if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                        queryResponse.Diagnostics,
                        expectedStatusCode,
                        expectedSubStatusCodes,
                        rule); 
                    else this.ValidateFaultInjectionRuleNotApplied(
                            queryResponse,
                            rule);
                }

                ItemResponse<FaultInjectionTestObject> itemResponse;

                if (operationType == OperationType.Read
                    || operationType == OperationType.Delete
                    || operationType == OperationType.Replace
                    || operationType == OperationType.Patch
                    || operationType == OperationType.Create
                    || operationType == OperationType.Upsert)
                {
                    if (operationType == OperationType.Read)
                    {
                        itemResponse = await testContainer.ReadItemAsync<FaultInjectionTestObject>(item.Id, new PartitionKey(item.Pk));

                        if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                            itemResponse.Diagnostics,
                            expectedStatusCode,
                            expectedSubStatusCodes,
                            rule);
                        else this.ValidateFaultInjectionRuleNotApplied(
                            itemResponse,
                            rule);
                    }

                    if (operationType == OperationType.Replace)
                    {
                        itemResponse = await testContainer.ReplaceItemAsync<FaultInjectionTestObject>(
                            item,
                            item.Id,
                            new PartitionKey(item.Pk));

                        if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                            itemResponse.Diagnostics,
                            expectedStatusCode,
                            expectedSubStatusCodes,
                            rule);
                        else this.ValidateFaultInjectionRuleNotApplied(
                            itemResponse,
                            rule);
                    }

                    if (operationType == OperationType.Delete)
                    {
                        itemResponse = await testContainer.DeleteItemAsync<FaultInjectionTestObject>(item.Id, new PartitionKey(item.Pk));

                        if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                            itemResponse.Diagnostics,
                            expectedStatusCode,
                            expectedSubStatusCodes,
                            rule);
                        else this.ValidateFaultInjectionRuleNotApplied(
                            itemResponse,
                            rule);
                    }

                    if (operationType == OperationType.Create)
                    {
                       itemResponse = await testContainer.CreateItemAsync<FaultInjectionTestObject>(item, new PartitionKey(item.Pk));

                        if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                            itemResponse.Diagnostics,
                            expectedStatusCode,
                            expectedSubStatusCodes,
                            rule);
                        else this.ValidateFaultInjectionRuleNotApplied(
                            itemResponse,
                            rule);

                    }

                    if (operationType == OperationType.Upsert)
                    {
                        itemResponse = await testContainer.UpsertItemAsync<FaultInjectionTestObject>(item, new PartitionKey(item.Pk));

                        if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                            itemResponse.Diagnostics,
                            expectedStatusCode,
                            expectedSubStatusCodes,
                            rule);
                        else this.ValidateFaultInjectionRuleNotApplied(
                            itemResponse,
                            rule);
                    }

                    if (operationType == OperationType.Patch)
                    {
                        List<PatchOperation> patchOperations = new List<PatchOperation>
                        {
                            PatchOperation.Add("/" + Guid.NewGuid().ToString(), Guid.NewGuid().ToString())
                        };

                        itemResponse = await testContainer.PatchItemAsync<FaultInjectionTestObject>(
                            item.Id,
                            new PartitionKey(item.Pk),
                            patchOperations);

                        if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                            itemResponse.Diagnostics,
                            expectedStatusCode,
                            expectedSubStatusCodes,
                            rule);
                        else this.ValidateFaultInjectionRuleNotApplied(
                            itemResponse,
                            rule);
                    }
                }
                else if (operationType != OperationType.Query)
                {
                    throw new ArgumentException($"Invalid Operation Type {operationType}");
                }
            }
            catch (CosmosException ex)
            {
                if (ruleApplied) this.ValidateFaultInjectionRuleApplication(
                    ex,
                    expectedStatusCode,
                    expectedSubStatusCodes,
                    rule);
                else this.ValidateFaultInjectionRuleNotApplied(
                    ex,
                    rule);
            }
            catch (DocumentClientException ex)
            {
                this.ValidateFaultInjectionRuleApplication(
                    ex,
                    expectedStatusCode,
                    expectedSubStatusCodes,
                    rule);
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

        private void ValidateFaultInjectionRuleNotApplied(
            CosmosException ex,
            FaultInjectionRule rule)
        {
            string diagnosticsString = ex.Diagnostics.ToString();
            Assert.AreEqual(0, rule.GetHitCount());
            Assert.AreEqual(0, ex.Diagnostics.GetFailedRequestCount());
            Assert.IsTrue(
                diagnosticsString.Contains("200")
                || diagnosticsString.Contains("201")
                || diagnosticsString.Contains("204"));
        }

        private void ValidateFaultInjectionRuleNotApplied(
            ItemResponse<FaultInjectionTestObject> response,
            FaultInjectionRule rule,
            int expectedHitCount = 0)
        {
            Assert.AreEqual(expectedHitCount, rule.GetHitCount());
            Assert.AreEqual(expectedHitCount, response.Diagnostics.GetFailedRequestCount());
            Assert.IsTrue((int)response.StatusCode < 400);
        }

        private void ValidateFaultInjectionRuleNotApplied(
            FeedResponse<FaultInjectionTestObject> response,
            FaultInjectionRule rule,
            int expectedHitCount = 0)
        {
            Assert.AreEqual(expectedHitCount, rule.GetHitCount());
            Assert.AreEqual(expectedHitCount, response.Diagnostics.GetFailedRequestCount());
            Assert.IsTrue((int)response.StatusCode < 400);
        }

        private void ValidateFaultInjectionRuleApplication(
            CosmosDiagnostics diagnostics,
            int statusCode,
            int subStatusCode,
            FaultInjectionRule rule)
        {
            string diagnosticsString = diagnostics.ToString();
            Console.WriteLine(diagnostics.ToString());
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(1 <= diagnostics.GetFailedRequestCount());
            Assert.IsTrue(diagnosticsString.Contains(statusCode.ToString()));
            Assert.IsTrue(diagnosticsString.Contains(subStatusCode.ToString()));
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
    }
}
