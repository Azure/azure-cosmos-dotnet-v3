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
    using System.Net.Sockets;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;
    using Database = Database;
    using static Microsoft.Azure.Cosmos.FaultInjection.Tests.Utils.TestCommon;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class FaultInjectionGatewayModeTests
    {
        private const int Timeout = 60000;

        private string? connectionString;
        private CosmosSystemTextJsonSerializer? serializer;

        private CosmosClient? client;
        private Database? database;
        private Container? container;

        private CosmosClient? fiClient;
        private Database? fiDatabase;
        private Container? fiContainer;


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
        public void Cleanup()
        {
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
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

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
                
                if(this.fiContainer != null)
                {
                    ItemResponse<FaultInjectionTestObject>? response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "id",
                        new PartitionKey("pk"));

                    this.ValidateHitCount(localRegionRule, 1);
                    this.ValidateHitCount(remoteRegionRule, 0);
                    this.ValidateFaultInjectionRuleApplication(
                        response,
                        (int)HttpStatusCode.Gone,
                        (int)SubStatusCodes.ServerGenerated410,
                        localRegionRule);
                }
                else
                {
                    Assert.Fail("Container is null");
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
            if (this.container != null)
            {
                List<FeedRange> feedRanges = (List<FeedRange>)await this.container.GetFeedRangesAsync();
                Assert.IsTrue(feedRanges.Count > 1);

                string query = "SELECT * FROM c";
                QueryRequestOptions queryOptions = new QueryRequestOptions
                {
                    FeedRange = feedRanges[0]
                };

                JObject query0 = (await this.container.GetItemQueryIterator<JObject>(query, requestOptions: queryOptions).ReadNextAsync()).First();

                queryOptions.FeedRange = feedRanges[1];
                JObject query1 = (await this.container.GetItemQueryIterator<JObject>(query, requestOptions: queryOptions).ReadNextAsync()).First();

                string serverErrorFeedRangeRuleId = "serverErrorFeedRangeRule-" + Guid.NewGuid().ToString();
                FaultInjectionRule serverErrorFeedRangeRule = new FaultInjectionRuleBuilder(
                    id: serverErrorFeedRangeRuleId,
                    condition:
                        new FaultInjectionConditionBuilder()
                            .WithEndpoint(
                                new FaultInjectionEndpointBuilder("testDb", "testContianer", feedRanges[0])
                                    .Build())
                            .WithConnectionType(FaultInjectionConnectionType.Gateway)
                            .Build(),
                    result:
                        FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.TooManyRequests)
                        .WithTimes(1)
                        .Build())
                .Build();

                serverErrorFeedRangeRule.Disable();

                List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serverErrorFeedRangeRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                GlobalEndpointManager? globalEndpointManager = this.fiClient?.ClientContext.DocumentClient.GlobalEndpointManager;
                List<Uri> readRegions = new List<Uri>();
                if (globalEndpointManager != null) { readRegions = (List<Uri>)globalEndpointManager.ReadEndpoints.AsEnumerable(); }

                Assert.IsTrue(serverErrorFeedRangeRule.GetRegionEndpoints().Count == readRegions.Count);

                foreach (Uri regionEndpoint in readRegions)
                {
                    Assert.IsTrue(serverErrorFeedRangeRule.GetRegionEndpoints().Contains(regionEndpoint));
                }

                Assert.IsTrue(
                    serverErrorFeedRangeRule.GetAddresses().Count >= 3 * readRegions.Count
                    && serverErrorFeedRangeRule.GetAddresses().Count <= 5 * readRegions.Count);

                serverErrorFeedRangeRule.Enable();

                ItemResponse<FaultInjectionTestObject>? response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        (string)query0["id"],
                        new PartitionKey((string)query0["Pk"]));

                Assert.IsNotNull(response);

                this.ValidateHitCount(serverErrorFeedRangeRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    response,
                    (int)StatusCodes.TooManyRequests,
                    (int)SubStatusCodes.Unknown,
                    serverErrorFeedRangeRule);

                try
                {
                    response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        (string)query1["id"],
                        new PartitionKey((string)query1["Pk"]));

                    Assert.IsNotNull(response.Diagnostics);
                    this.ValidateFaultInjectionRuleNotApplied(response, serverErrorFeedRangeRule, 1);
                    this.ValidateHitCount(serverErrorFeedRangeRule, 1);
                }
                finally
                {
                    serverErrorFeedRangeRule.Disable();
                }
            }
            else
            {
                Assert.Fail("Container is null");
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test response delay, request should be sent")]
        [Owner("ntripician")]
        public async Task FIGatewayResponseDelay()
        {
            string id = Guid.NewGuid().ToString();
            string pk = Guid.NewGuid().ToString();

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
                    OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1)
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
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

                _ = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                   createdItem,
                   new PartitionKey(pk));

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                ItemResponse<FaultInjectionTestObject> readResponse = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    id,
                    new PartitionKey(pk));

                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                this.ValidateHitCount(delayRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    readResponse,
                    (int)HttpStatusCode.OK,
                    (int)SubStatusCodes.Unknown,
                    delayRule);
            }
            finally
            {
                delayRule.Disable();
                if (this.container != null)
                {
                    await this.container.DeleteItemAsync<FaultInjectionTestObject>(id, new PartitionKey(pk));
                }
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test send delay, request should not be sent")]
        [Owner("ntripician")]
        public async Task FIGatewaySendDelay()
        {
            string id = Guid.NewGuid().ToString();
            string pk = Guid.NewGuid().ToString();

            string sendDelayRuleId = "sendDelayRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: sendDelayRuleId,
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
                    OpenTcpConnectionTimeout = TimeSpan.FromSeconds(1)
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
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

                ItemResponse<FaultInjectionTestObject> _ = await this.fiContainer.CreateItemAsync<FaultInjectionTestObject>(
                    createdItem,
                    new PartitionKey(pk));

                elapsed = stopwatch.Elapsed;
                stopwatch.Stop();
                delayRule.Disable();

                ItemResponse<FaultInjectionTestObject> readResponse = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    id,
                    new PartitionKey(pk));

                Assert.IsTrue(elapsed.TotalSeconds >= 6);
                this.ValidateHitCount(delayRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    readResponse,
                    (int)HttpStatusCode.NotFound,
                    (int)SubStatusCodes.Unknown,
                    delayRule);
            }
            finally
            {
                delayRule.Disable();
            }
        }

        [TestMethod]
        [Timeout(Timeout)]
        [Description("Test server error responses")]
        [Owner("ntripician")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Gone, (int)StatusCodes.Gone, (int)SubStatusCodes.ServerGenerated410, DisplayName = "Gone")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.InternalServerEror, (int)StatusCodes.InternalServerError, (int)SubStatusCodes.Unknown, DisplayName = "InternalServerError")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.TooManyRequests, (int)StatusCodes.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded, DisplayName = "TooManyRequests")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, (int)StatusCodes.NotFound, (int)SubStatusCodes.ReadSessionNotAvailable, DisplayName = "ReadSessionNotAvailable")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.Timeout, (int)StatusCodes.RequestTimeout, (int)SubStatusCodes.Unknown, DisplayName = "Timeout")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsMigrating, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingPartitionMigration, DisplayName = "PartitionIsMigrating")]
        [DataRow(FaultInjectionOperationType.ReadItem, FaultInjectionServerErrorType.PartitionIsSplitting, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingSplit, DisplayName = "PartitionIsSplitting")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Gone, (int)StatusCodes.Gone, (int)SubStatusCodes.ServerGenerated410, DisplayName = "Gone")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.InternalServerEror, (int)StatusCodes.InternalServerError, (int)SubStatusCodes.Unknown, DisplayName = "InternalServerError")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.TooManyRequests, (int)StatusCodes.TooManyRequests, (int)SubStatusCodes.RUBudgetExceeded, DisplayName = "TooManyRequests")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.ReadSessionNotAvailable, (int)StatusCodes.NotFound, (int)SubStatusCodes.ReadSessionNotAvailable, DisplayName = "ReadSessionNotAvailable")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.Timeout, (int)StatusCodes.RequestTimeout, (int)SubStatusCodes.Unknown, DisplayName = "Timeout")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsMigrating, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingPartitionMigration, DisplayName = "PartitionIsMigrating")]
        [DataRow(FaultInjectionOperationType.CreateItem, FaultInjectionServerErrorType.PartitionIsSplitting, (int)StatusCodes.Gone, (int)SubStatusCodes.CompletingSplit, DisplayName = "PartitionIsSplitting")]
        public async Task FIGatewayServerResponse(
            FaultInjectionOperationType faultInjectionOperationType, 
            FaultInjectionServerErrorType faultInjectionServerErrorType,
            int statusCodes,
            int subStatusCodes)
        {
            string id = Guid.NewGuid().ToString();
            string pk = Guid.NewGuid().ToString();

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
                    Serializer = this.serializer
                };

                this.fiClient = new CosmosClient(
                    this.connectionString,
                    faultInjector.GetFaultInjectionClientOptions(cosmosClientOptions));
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                serverErrorResponseRule.Enable();

                ItemResponse<FaultInjectionTestObject> response;

                ValueStopwatch stopwatch = ValueStopwatch.StartNew();
                TimeSpan elapsed;

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
                Assert.IsNotNull(response);

                this.ValidateHitCount(serverErrorResponseRule, 1);
                this.ValidateFaultInjectionRuleApplication(
                    response,
                    statusCodes,
                    subStatusCodes,
                    serverErrorResponseRule);
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
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                hitCountRule.Enable();

                ItemResponse<FaultInjectionTestObject> response;

                for (int i = 0; i < 3; i++)
                {
                    response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId",
                        new PartitionKey("/pk"));
                    Assert.IsNotNull(response);

                    if (i < 2)
                    {
                        this.ValidateFaultInjectionRuleApplication(
                            response,
                            (int)HttpStatusCode.Gone, (int)SubStatusCodes.ServerGenerated410,
                            hitCountRule);
                        this.ValidateHitCount(hitCountRule, i + 1);
                    }
                    else
                    {
                        this.ValidateFaultInjectionRuleNotApplied(response, hitCountRule, 2);
                        this.ValidateHitCount(hitCountRule, 2);
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
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                ItemResponse<FaultInjectionTestObject> response;

                thresholdRule.Enable();

                for (int i = 0; i < 100; i++)
                {
                    response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                        "testId",
                        new PartitionKey("/pk"));

                    Assert.IsNotNull(response);
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
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.Gone)
                        .WithInjectionRate(.5)
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
                this.fiDatabase = this.fiClient.GetDatabase(this.database?.Id);
                this.fiContainer = this.fiDatabase.GetContainer(this.container?.Id);

                ItemResponse<FaultInjectionTestObject> response;

                rule.Enable();

                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    "testId",
                    new PartitionKey("/pk"));

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

                response = await this.fiContainer.ReadItemAsync<FaultInjectionTestObject>(
                    "testId",
                    new PartitionKey("/pk"));

                this.ValidateFaultInjectionRuleApplication(response, (int)HttpStatusCode.Gone, (int)SubStatusCodes.ServerGenerated410, rule);
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
    }
}
