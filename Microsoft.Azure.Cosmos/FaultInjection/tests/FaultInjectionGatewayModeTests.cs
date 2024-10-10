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
    using CosmosSystemTextJsonSerializer = Utils.TestCommon.CosmosSystemTextJsonSerializer;
    using Database = Database;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class FaultInjectionGatewayModeTests
    {
        private const int Timeout = 66000;

        private string? connectionString;
        private CosmosSystemTextJsonSerializer? serializer;

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
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", string.Empty);

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            
            this.serializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                Serializer = this.serializer,

            };

            this.client = new CosmosClient(this.connectionString, cosmosClientOptions);

            (this.database, this.container) = await TestCommon.GetOrCreateMultiRegionFIDatabaseAndContainers(this.client);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.highThroughputContainer != null)
            {
                await this.highThroughputContainer.DeleteContainerAsync();
            }
            this.client?.Dispose();
            this.fiClient?.Dispose();
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test Region rule filtering")]
        [Owner("ntripician")]
        public async Task FIGatewayRegion()
        {
            List<string> preferredRegions = new List<string>() { };
            List<string> readRegions;
            ReadOnlyDictionary<string, Uri> readEndpoints = new ReadOnlyDictionary<string, Uri>(new Dictionary<string, Uri>());

            GlobalEndpointManager? globalEndpointManager = this.client?.ClientContext.DocumentClient.GlobalEndpointManager;
            if (globalEndpointManager != null)
            {
                readEndpoints = globalEndpointManager.GetAvailableReadEndpointsByLocation();
                (_, readRegions) = await this.GetReadWriteEndpoints(globalEndpointManager);

                preferredRegions = new List<string>(readRegions);
            }

            string localRegionRuleId = "localRegionRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule localRegionRule = new FaultInjectionRuleBuilder(
                id: localRegionRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion(preferredRegions[0])
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

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

            localRegionRule.Disable();
            remoteRegionRule.Disable();

            try
            {
                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { localRegionRule, remoteRegionRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                await this.fiClient.InitilizeFaultInjectionAsync();

                this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
                this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionContainerName);

                globalEndpointManager = this.fiClient?.ClientContext.DocumentClient.GlobalEndpointManager;

                if (globalEndpointManager != null)
                {
                    Assert.AreEqual(1, localRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[0]], localRegionRule.GetRegionEndpoints()[0]);

                    Assert.AreEqual(1, remoteRegionRule.GetRegionEndpoints().Count);
                    Assert.AreEqual(readEndpoints[preferredRegions[1]], remoteRegionRule.GetRegionEndpoints()[0]);
                }

                localRegionRule.Enable();
                remoteRegionRule.Enable();

                try
                {
                    ItemResponse<FaultInjectionTestObject>? response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
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
                localRegionRule.Disable();
                remoteRegionRule.Disable();
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test Partition rule filtering")]
        [Owner("ntripician")]
        public async Task FIGatewayPartitionTest()
        {
            await this.InitializeHighThroughputContainerAsync();

            List<FeedRange> feedRanges = (List<FeedRange>)await this.highThroughputContainer.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges.Count > 1);

            string query = "SELECT * FROM c";

            FeedIterator<FaultInjectionTestObject> feedIterator = this.highThroughputContainer.GetItemQueryIterator<FaultInjectionTestObject>(query);

            FaultInjectionTestObject result1 = (await feedIterator.ReadNextAsync()).First();
            FaultInjectionTestObject result2 = (await feedIterator.ReadNextAsync()).First();

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
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                    .WithTimes(100)
                    .Build())
            .Build();

            serverErrorFeedRangeRule.Disable();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serverErrorFeedRangeRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                Serializer = this.serializer,
                MaxRetryAttemptsOnRateLimitedRequests = 0,
            };

            this.fiClient = new CosmosClient(
                this.connectionString,
                faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
            await this.fiClient.InitilizeFaultInjectionAsync();
            this.fiDatabase = this.fiClient.GetDatabase(TestCommon.FaultInjectionDatabaseName);
            this.fiContainer = this.fiDatabase.GetContainer(TestCommon.FaultInjectionHTPContainerName);

            GlobalEndpointManager? globalEndpointManager = this.fiClient?.ClientContext.DocumentClient.GlobalEndpointManager;
            List<Uri> readRegions = new List<Uri>();
            if (globalEndpointManager != null)
            {
                foreach (Uri regionEndpoint in globalEndpointManager.AccountReadEndpoints)
                {
                    readRegions.Add(regionEndpoint);
                }
            }

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

            try
            {
                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
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
        [Timeout(Timeout)]
        [Description("Test response delay, request should be sent")]
        [Owner("ntripician")]
        public async Task FIGatewayResponseDelay()
        {

            string id = "id";
            string pk = "deleteMe";

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
                        .WithDelay(TimeSpan.FromSeconds(10))
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            delayRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { delayRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                await this.fiClient.InitilizeFaultInjectionAsync();
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                delayRule.Enable();
                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

                FaultInjectionTestObject createdItem = new FaultInjectionTestObject
                {
                    Id = id,
                    Pk = pk
                };

                await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                   createdItem,
                   new PartitionKey(pk));

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                this.ValidateHitCount(delayRule, 1);

                ItemResponse<FaultInjectionTestObject> readResponse = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    id,
                    new PartitionKey(pk));

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
        [Description("Test send delay, request should not be sent")]
        [Owner("ntripician")]
        public async Task FIGatewaySendDelay()
        {
            string id = "id";
            string pk = "deleteMe";

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
                        .WithDelay(TimeSpan.FromSeconds(66))
                        .WithTimes(10)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            delayRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { delayRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    EnableContentResponseOnWrite = true,
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                await this.fiClient.InitilizeFaultInjectionAsync();
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

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

                try
                {
                    ItemResponse<FaultInjectionTestObject> readResponse = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    id,
                    new PartitionKey(pk));
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
                }

                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                this.ValidateHitCount(delayRule, 1);
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
        [Description("Test server error responses")]
        [Owner("ntripician")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Gone, (int)StatusCodes.Gone, DisplayName = "Gone")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.InternalServerEror, (int)StatusCodes.InternalServerError, DisplayName = "InternalServerError")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.TooManyRequests, (int)StatusCodes.TooManyRequests, DisplayName = "TooManyRequests")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, (int)StatusCodes.NotFound, DisplayName = "ReadSessionNotAvailable")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Timeout, (int)StatusCodes.RequestTimeout, DisplayName = "Timeout")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsMigrating, (int)StatusCodes.Gone, DisplayName = "PartitionIsMigrating")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsSplitting, (int)StatusCodes.Gone, DisplayName = "PartitionIsSplitting")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Gone, (int)StatusCodes.Gone, DisplayName = "Gone")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.InternalServerEror, (int)StatusCodes.InternalServerError, DisplayName = "InternalServerError")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.TooManyRequests, (int)StatusCodes.TooManyRequests, DisplayName = "TooManyRequests")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, DisplayName = "ReadSessionNotAvailable")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Timeout, (int)StatusCodes.RequestTimeout, DisplayName = "Timeout")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsMigrating, (int)StatusCodes.Gone, DisplayName = "PartitionIsMigrating")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsSplitting, (int)StatusCodes.Gone, DisplayName = "PartitionIsSplitting")]
        public async Task FIGatewayServerResponse(
            FaultInjectionOperationType faultInjectionOperationType, 
            FaultInjectionServerErrorType faultInjectionServerErrorType,
            int statusCodes)
        {
            string id = "id";
            string pk = "deleteMe";

            string serverErrorResponseRuleId = "serverErrorResponseRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serverErrorResponseRule = new FaultInjectionRuleBuilder(
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
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            serverErrorResponseRule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { serverErrorResponseRule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer,
                    MaxRetryAttemptsOnRateLimitedRequests = 0,
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                await this.fiClient.InitilizeFaultInjectionAsync();
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                serverErrorResponseRule.Enable();

                ItemResponse<FaultInjectionTestObject> response;

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
                            new PartitionKey("/pk"));
                    }
                }
                catch (CosmosException ex)
                {
                    this.ValidateRuleHit(serverErrorResponseRule, 1);
                    this.ValidateFaultInjectionRuleApplication(
                        ex,
                        statusCodes,
                        serverErrorResponseRule);
                }
                catch (DocumentClientException ex)
                {
                    this.ValidateRuleHit(serverErrorResponseRule, 1);
                    this.ValidateFaultInjectionRuleApplication(
                        ex,
                        statusCodes,
                        serverErrorResponseRule);
                }


                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();

                if (faultInjectionServerErrorType == FaultInjectionServerErrorType.Timeout)
                {
                    ChaosInterceptor? interceptor = faultInjector.GetChaosInterceptor() as ChaosInterceptor;

                    Assert.IsNotNull(interceptor);
                    Assert.IsTrue(
                        elapsed.TotalSeconds
                        >= interceptor.GetRequestTimeout().TotalSeconds);
                }
            }
            finally
            {
                serverErrorResponseRule.Disable();
                if (this.container != null && faultInjectionOperationType == FaultInjectionOperationType.CreateItem)
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
        [Description("Test hit limit")]
        [Owner("ntripician")]
        public async Task FIGatewayHitLimit()
        {
            string hitCountRuleId = "hitCountRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule hitCountRule = new FaultInjectionRuleBuilder(
                id: hitCountRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                    .WithConnectionType(FaultInjectionConnectionType.Gateway)
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
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                await this.fiClient.InitilizeFaultInjectionAsync();
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                hitCountRule.Enable();

                ItemResponse<FaultInjectionTestObject> response;

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
        [Description("Test injection rate")]
        [Owner("ntripician")]
        public async Task FIGatewayInjectionRate()
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
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                await this.fiClient.InitilizeFaultInjectionAsync();
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

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
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.InternalServerEror)
                        .WithTimes(1)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            rule.Disable();

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { rule });

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Direct,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                await this.fiClient.InitilizeFaultInjectionAsync();
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                ItemResponse<FaultInjectionTestObject> response;

                rule.Enable();

                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    "testId",
                    new PartitionKey("pk"));

                this.ValidateFaultInjectionRuleNotApplied(response, rule);

                rule.Disable();
                this.fiClient.Dispose();

                cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                rule.Enable();

                try
                {
                    response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    "testId",
                    new PartitionKey("pk"));
                }
                catch (CosmosException ex)
                {
                    this.ValidateFaultInjectionRuleApplication(ex, (int)HttpStatusCode.Gone, rule);
                }
            }
            finally
            {
                rule.Disable();
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
            ItemResponse<FaultInjectionTestObject> response,
            int statusCode,
            int subStatusCode,
            FaultInjectionRule rule)
        {
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(1 <= response.Diagnostics.GetFailedRequestCount());
            Assert.AreEqual(statusCode, response.StatusCode);
            Assert.AreEqual(subStatusCode, response.Headers.SubStatusCode);
        }

        private void ValidateFaultInjectionRuleApplication(
            DocumentClientException ex,
            int statusCode,
            FaultInjectionRule rule)
        {
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(ex.Message.Contains(rule.GetId()));
            Assert.AreEqual(statusCode, (int?)ex.StatusCode);
        }

        private void ValidateFaultInjectionRuleApplication(
            CosmosException ex,
            int statusCode,
            FaultInjectionRule rule)
        {
            Assert.IsTrue(1 <= rule.GetHitCount());
            Assert.IsTrue(ex.Message.Contains(rule.GetId()));
            Assert.AreEqual(statusCode, (int?)ex.StatusCode);
        }
    }
}
