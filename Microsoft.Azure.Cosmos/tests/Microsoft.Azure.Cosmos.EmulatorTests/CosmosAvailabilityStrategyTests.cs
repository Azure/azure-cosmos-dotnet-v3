
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Database = Database;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class CosmosAvailabilityStrategyTests
    {
        private const string centralUS = "Central US";
        private const string northCentralUS = "North Central US";
        private const string eastUs = "East US";
        private const string dbName = "availabilityStrategyTestDb";
        private const string containerName = "availabilityStrategyTestContainer";
        private const string changeFeedContainerName = "availabilityStrategyTestChangeFeedContainer";

        private CosmosClient client;
        private Database database;
        private Container container;
        private Container changeFeedContainer;
        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private string connectionString;
        

        [TestCleanup]
        public void TestCleanup()
        {
            //Do not delete the resources, georeplication is slow and we want to reuse the resources
            this.client?.Dispose();
        }

        private static readonly FaultInjectionCondition readConditon = new FaultInjectionConditionBuilder()
            .WithRegion("Central US")
            .WithOperationType(FaultInjectionOperationType.ReadItem)
            .Build();
        private static readonly FaultInjectionCondition queryConditon = new FaultInjectionConditionBuilder()
            .WithRegion("Central US")
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition readManyCondition = new FaultInjectionConditionBuilder()
            .WithRegion("Central US")
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition changeFeedCondtion = new FaultInjectionConditionBuilder()
            .WithRegion("Central US")
            .WithOperationType(FaultInjectionOperationType.All)
            .Build();

        private static readonly FaultInjectionCondition readConditonStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithOperationType(FaultInjectionOperationType.ReadItem)
            .Build();
        private static readonly FaultInjectionCondition queryConditonStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition readManyConditionStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition changeFeedCondtionStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithOperationType(FaultInjectionOperationType.ReadFeed)
            .Build();

        private static readonly IFaultInjectionResult goneResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.Gone)
            .Build();
        private static readonly IFaultInjectionResult retryWithResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.RetryWith)
            .Build();
        private static readonly IFaultInjectionResult internalServerErrorResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.InternalServerEror)
            .Build();
        private static readonly IFaultInjectionResult readSessionNotAvailableResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.ReadSessionNotAvailable)
            .Build();
        private static readonly IFaultInjectionResult timeoutResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.Timeout)
            .Build();
        private static readonly IFaultInjectionResult partitionIsSplittingResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.PartitionIsSplitting)
            .Build();
        private static readonly IFaultInjectionResult partitionIsMigratingResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.PartitionIsMigrating)
            .Build();
        private static readonly IFaultInjectionResult serviceUnavailableResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
            .Build();
        private static readonly IFaultInjectionResult responseDelayResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
            .WithDelay(TimeSpan.FromMilliseconds(4000))
            .Build();

        private readonly Dictionary<string, FaultInjectionCondition> conditions = new Dictionary<string, FaultInjectionCondition>()
        {
            { "Read", readConditon },
            { "Query", queryConditon },
            { "ReadMany", readManyCondition },
            { "ChangeFeed", changeFeedCondtion },
            { "ReadStep", readConditonStep },
            { "QueryStep", queryConditonStep },
            { "ReadManyStep", readManyConditionStep },
            { "ChangeFeedStep", changeFeedCondtionStep}
        };

        private readonly Dictionary<string, IFaultInjectionResult> results = new Dictionary<string, IFaultInjectionResult>()
        {
            { "Gone", goneResult },
            { "RetryWith", retryWithResult },
            { "InternalServerError", internalServerErrorResult },
            { "ReadSessionNotAvailable", readSessionNotAvailableResult },
            { "Timeout", timeoutResult },
            { "PartitionIsSplitting", partitionIsSplittingResult },
            { "PartitionIsMigrating", partitionIsMigratingResult },
            { "ServiceUnavailable", serviceUnavailableResult },
            { "ResponseDelay", responseDelayResult }
        };

        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }
            this.client = new CosmosClient(
                this.connectionString,
                new CosmosClientOptions()
                {
                    Serializer = this.cosmosSystemTextJsonSerializer,
                });

            (this.database, this.container, this.changeFeedContainer) = await MultiRegionSetupHelpers.GetOrCreateMultiRegionDatabaseAndContainers(this.client);
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyNoTriggerTest()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("Central US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(300))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            FaultInjectionRule responseDelay2 = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("North Central US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(3000))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { responseDelay, responseDelay2 };
            FaultInjector faultInjector = new FaultInjector(rules);

            responseDelay.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "Central US", "North Central US" },
                AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                        threshold: TimeSpan.FromMilliseconds(300),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(CosmosAvailabilityStrategyTests.dbName);
                Container container = database.GetContainer(CosmosAvailabilityStrategyTests.containerName);

                responseDelay.Enable();
                ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>("testId", new PartitionKey("pk"));

                CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);
                traceDiagnostic.Value.Data.TryGetValue("Response Region", out object responseRegion);
                Assert.IsNotNull(responseRegion);
                Assert.AreEqual(CosmosAvailabilityStrategyTests.centralUS, (string)responseRegion);

                //Should send out hedge request but original should be returned
                traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);
                Assert.IsNotNull(hedgeContext);
                IReadOnlyCollection<string> hedgeContextList;
                hedgeContextList = hedgeContext as IReadOnlyCollection<string>;
                Assert.AreEqual(2, hedgeContextList.Count);
                Assert.IsTrue(hedgeContextList.Contains(CosmosAvailabilityStrategyTests.centralUS));
                Assert.IsTrue(hedgeContextList.Contains(CosmosAvailabilityStrategyTests.northCentralUS));
            };
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyRequestOptionsTriggerTest()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("Central US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(4000))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { responseDelay };
            FaultInjector faultInjector = new FaultInjector(rules);

            responseDelay.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "Central US", "North Central US" },
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(CosmosAvailabilityStrategyTests.dbName);
                Container container = database.GetContainer(CosmosAvailabilityStrategyTests.containerName);

                responseDelay.Enable();

                ItemRequestOptions requestOptions = new ItemRequestOptions
                {
                    AvailabilityStrategy = new CrossRegionHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50))
                };
                ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                    "testId",
                    new PartitionKey("pk"),
                    requestOptions);

                CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);
                traceDiagnostic.Value.Data.TryGetValue("Response Region", out object hedgeContext);
                Assert.IsNotNull(hedgeContext);
                Assert.AreEqual(CosmosAvailabilityStrategyTests.northCentralUS, (string)hedgeContext);
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyDisableOverideTest()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("Central US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(6000))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .WithHitLimit(2)
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { responseDelay };
            FaultInjector faultInjector = new FaultInjector(rules);

            responseDelay.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "Central US", "North Central US" },
                AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(CosmosAvailabilityStrategyTests.dbName);
                Container container = database.GetContainer(CosmosAvailabilityStrategyTests.containerName);

                responseDelay.Enable();
                ItemRequestOptions requestOptions = new ItemRequestOptions
                {
                    AvailabilityStrategy = new DisabledAvailabilityStrategy()
                };

                ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                    "testId",
                    new PartitionKey("pk"),
                    requestOptions);

                CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);

                Assert.IsFalse(traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out _));
            }
        }

        [DataTestMethod]
        [TestCategory("MultiRegion")]
        [DataRow("Read", "Read", "Gone", DisplayName = "Read | Gone")]
        [DataRow("Read", "Read", "RetryWith", DisplayName = "Read | RetryWith")]
        [DataRow("Read", "Read", "InternalServerError", DisplayName = "Read | InternalServerError")]
        [DataRow("Read", "Read", "ReadSessionNotAvailable", DisplayName = "Read | ReadSessionNotAvailable")]
        [DataRow("Read", "Read", "Timeout", DisplayName = "Read | Timeout")]
        [DataRow("Read", "Read", "PartitionIsSplitting", DisplayName = "Read | PartitionIsSplitting")]
        [DataRow("Read", "Read", "PartitionIsMigrating", DisplayName = "Read | PartitionIsMigrating")]
        [DataRow("Read", "Read", "ServiceUnavailable", DisplayName = "Read | ServiceUnavailable")]
        [DataRow("Read", "Read", "ResponseDelay", DisplayName = "Read | ResponseDelay")]
        [DataRow("SinglePartitionQuery", "Query", "Gone", DisplayName = "SinglePartitionQuery | Gone")]
        [DataRow("SinglePartitionQuery", "Query", "RetryWith", DisplayName = "SinglePartitionQuery | RetryWith")]
        [DataRow("SinglePartitionQuery", "Query", "InternalServerError", DisplayName = "SinglePartitionQuery | InternalServerError")]
        [DataRow("SinglePartitionQuery", "Query", "ReadSessionNotAvailable", DisplayName = "SinglePartitionQuery | ReadSessionNotAvailable")]
        [DataRow("SinglePartitionQuery", "Query", "Timeout", DisplayName = "SinglePartitionQuery | Timeout")]
        [DataRow("SinglePartitionQuery", "Query", "PartitionIsSplitting", DisplayName = "SinglePartitionQuery | PartitionIsSplitting")]
        [DataRow("SinglePartitionQuery", "Query", "PartitionIsMigrating", DisplayName = "SinglePartitionQuery | PartitionIsMigrating")]
        [DataRow("SinglePartitionQuery", "Query", "ServiceUnavailable", DisplayName = "SinglePartitionQuery | ServiceUnavailable")]
        [DataRow("SinglePartitionQuery", "Query", "ResponseDelay", DisplayName = "SinglePartitionQuery | ResponseDelay")]
        [DataRow("CrossPartitionQuery", "Query", "Gone", DisplayName = "CrossPartitionQuery | Gone")]
        [DataRow("CrossPartitionQuery", "Query", "RetryWith", DisplayName = "CrossPartitionQuery | RetryWith")]
        [DataRow("CrossPartitionQuery", "Query", "InternalServerError", DisplayName = "CrossPartitionQuery | InternalServerError")]
        [DataRow("CrossPartitionQuery", "Query", "ReadSessionNotAvailable", DisplayName = "CrossPartitionQuery | ReadSessionNotAvailable")]
        [DataRow("CrossPartitionQuery", "Query", "Timeout", DisplayName = "CrossPartitionQuery | Timeout")]
        [DataRow("CrossPartitionQuery", "Query", "PartitionIsSplitting", DisplayName = "CrossPartitionQuery | PartitionIsSplitting")]
        [DataRow("CrossPartitionQuery", "Query", "PartitionIsMigrating", DisplayName = "CrossPartitionQuery | PartitionIsMigrating")]
        [DataRow("CrossPartitionQuery", "Query", "ServiceUnavailable", DisplayName = "CrossPartitionQuery | ServiceUnavailable")]
        [DataRow("CrossPartitionQuery", "Query", "ResponseDelay", DisplayName = "CrossPartitionQuery | ResponseDelay")]
        [DataRow("ReadMany", "ReadMany", "Gone", DisplayName = "ReadMany | Gone")]
        [DataRow("ReadMany", "ReadMany", "RetryWith", DisplayName = "ReadMany | RetryWith")]
        [DataRow("ReadMany", "ReadMany", "InternalServerError", DisplayName = "ReadMany | InternalServerError")]
        [DataRow("ReadMany", "ReadMany", "ReadSessionNotAvailable", DisplayName = "ReadMany | ReadSessionNotAvailable")]
        [DataRow("ReadMany", "ReadMany", "Timeout", DisplayName = "ReadMany | Timeout")]
        [DataRow("ReadMany", "ReadMany", "PartitionIsSplitting", DisplayName = "ReadMany | PartitionIsSplitting")]
        [DataRow("ReadMany", "ReadMany", "PartitionIsMigrating", DisplayName = "ReadMany | PartitionIsMigrating")]
        [DataRow("ReadMany", "ReadMany", "ServiceUnavailable", DisplayName = "ReadMany | ServiceUnavailable")]
        [DataRow("ReadMany", "ReadMany", "ResponseDelay", DisplayName = "ReadMany | ResponseDelay")]
        [DataRow("ChangeFeed", "ChangeFeed", "Gone", DisplayName = "ChangeFeed | Gone")]
        [DataRow("ChangeFeed", "ChangeFeed", "RetryWith", DisplayName = "ChangeFeed | RetryWith")]
        [DataRow("ChangeFeed", "ChangeFeed", "InternalServerError", DisplayName = "ChangeFeed | InternalServerError")]
        [DataRow("ChangeFeed", "ChangeFeed", "ReadSessionNotAvailable", DisplayName = "ChangeFeed | ReadSessionNotAvailable")]
        [DataRow("ChangeFeed", "ChangeFeed", "Timeout", DisplayName = "ChangeFeed | Timeout")]
        [DataRow("ChangeFeed", "ChangeFeed", "PartitionIsSplitting", DisplayName = "ChangeFeed | PartitionIsSplitting")]
        [DataRow("ChangeFeed", "ChangeFeed", "PartitionIsMigrating", DisplayName = "ChangeFeed | PartitionIsMigrating")]
        [DataRow("ChangeFeed", "ChangeFeed", "ServiceUnavailable", DisplayName = "ChangeFeed | ServiceUnavailable")]
        [DataRow("ChangeFeed", "ChangeFeed", "ResponseDelay", DisplayName = "ChangeFeed | ResponseDelay")]
        public async Task AvailabilityStrategyAllFaultsTests(string operation, string conditonName, string resultName)
        {
            FaultInjectionCondition conditon = this.conditions[conditonName];
            IFaultInjectionResult result = this.results[resultName];

            FaultInjectionRule rule = new FaultInjectionRuleBuilder(
                id: operation,
                condition: conditon,
                result: result)
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { rule };
            FaultInjector faultInjector = new FaultInjector(rules);

            rule.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "Central US", "North Central US" },
                AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(CosmosAvailabilityStrategyTests.dbName);
                Container container = database.GetContainer(CosmosAvailabilityStrategyTests.containerName);

                CosmosTraceDiagnostics traceDiagnostic;
                object hedgeContext;

                switch (operation)
                {
                    case "Read":
                        rule.Enable();

                        ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                            "testId",
                            new PartitionKey("pk"));

                        Assert.IsTrue(rule.GetHitCount() > 0);
                        traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
                        Assert.IsNotNull(traceDiagnostic);
                        traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual(CosmosAvailabilityStrategyTests.northCentralUS, (string)hedgeContext);

                        break;

                    case "SinglePartitionQuery":
                        string queryString = "SELECT * FROM c";

                        QueryRequestOptions requestOptions = new QueryRequestOptions()
                        {
                            PartitionKey = new PartitionKey("pk"),
                        };

                        FeedIterator<AvailabilityStrategyTestObject> queryIterator = container.GetItemQueryIterator<AvailabilityStrategyTestObject>(
                            new QueryDefinition(queryString),
                            requestOptions: requestOptions);

                        rule.Enable();

                        while (queryIterator.HasMoreResults)
                        {
                            FeedResponse<AvailabilityStrategyTestObject> feedResponse = await queryIterator.ReadNextAsync();

                            Assert.IsTrue(rule.GetHitCount() > 0);
                            traceDiagnostic = feedResponse.Diagnostics as CosmosTraceDiagnostics;
                            Assert.IsNotNull(traceDiagnostic);
                            traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                            Assert.IsNotNull(hedgeContext);
                            Assert.AreEqual(CosmosAvailabilityStrategyTests.northCentralUS, (string)hedgeContext);
                        }

                        break;

                    case "CrossPartitionQuery":
                        string crossPartitionQueryString = "SELECT * FROM c";
                        FeedIterator<AvailabilityStrategyTestObject> crossPartitionQueryIterator = container.GetItemQueryIterator<AvailabilityStrategyTestObject>(
                            new QueryDefinition(crossPartitionQueryString));

                        rule.Enable();

                        while (crossPartitionQueryIterator.HasMoreResults)
                        {
                            FeedResponse<AvailabilityStrategyTestObject> feedResponse = await crossPartitionQueryIterator.ReadNextAsync();

                            Assert.IsTrue(rule.GetHitCount() > 0);
                            traceDiagnostic = feedResponse.Diagnostics as CosmosTraceDiagnostics;
                            Assert.IsNotNull(traceDiagnostic);
                            traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                            Assert.IsNotNull(hedgeContext);
                            Assert.AreEqual(CosmosAvailabilityStrategyTests.northCentralUS, (string)hedgeContext);
                        }

                        break;

                    case "ReadMany":
                        rule.Enable();

                        FeedResponse<AvailabilityStrategyTestObject> readManyResponse = await container.ReadManyItemsAsync<AvailabilityStrategyTestObject>(
                            new List<(string, PartitionKey)>()
                            {
                            ("testId", new PartitionKey("pk")),
                            ("testId2", new PartitionKey("pk2")),
                            ("testId3", new PartitionKey("pk3")),
                            ("testId4", new PartitionKey("pk4"))
                            });

                        Assert.IsTrue(rule.GetHitCount() > 0);
                        traceDiagnostic = readManyResponse.Diagnostics as CosmosTraceDiagnostics;
                        Assert.IsNotNull(traceDiagnostic);
                        traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual(CosmosAvailabilityStrategyTests.northCentralUS, (string)hedgeContext);

                        break;

                    case "ChangeFeed":
                        Container leaseContainer = database.GetContainer(CosmosAvailabilityStrategyTests.changeFeedContainerName);
                        ChangeFeedProcessor changeFeedProcessor = container.GetChangeFeedProcessorBuilder<AvailabilityStrategyTestObject>(
                            processorName: "AvialabilityStrategyTest",
                            onChangesDelegate: HandleChangesAsync)
                            .WithInstanceName("test")
                            .WithLeaseContainer(leaseContainer)
                            .Build();
                        await changeFeedProcessor.StartAsync();
                        await Task.Delay(1000);

                        AvailabilityStrategyTestObject testObject = new AvailabilityStrategyTestObject
                        {
                            Id = "item4",
                            Pk = "pk4",
                            Other = Guid.NewGuid().ToString()
                        };
                        await container.UpsertItemAsync<AvailabilityStrategyTestObject>(testObject);

                        rule.Enable();

                        await Task.Delay(15000);

                        Assert.IsTrue(rule.GetHitCount() > 0);

                        rule.Disable();
                        await changeFeedProcessor.StopAsync();

                        break;

                    default:

                        Assert.Fail("Invalid operation");
                        break;
                }

                rule.Disable();
            }
        }

        [DataTestMethod]
        [TestCategory("MultiRegion")]
        [DataRow("Read", "Read", "ReadStep", DisplayName = "Read | ReadStep")]
        [DataRow("SinglePartitionQuery", "Query", "QueryStep", DisplayName = "Query | SinglePartitionQueryStep")]
        [DataRow("CrossPartitionQuery", "Query", "QueryStep", DisplayName = "Query | CrossPartitionQueryStep")]
        [DataRow("ReadMany", "ReadMany", "ReadManyStep", DisplayName = "ReadMany | ReadManyStep")]
        [DataRow("ChangeFeed", "ChangeFeed", "ChangeFeedStep", DisplayName = "ChangeFeed | ChangeFeedStep")]
        public async Task AvailabilityStrategyStepTests(string operation, string conditonName1, string conditionName2)
        {
            FaultInjectionCondition conditon1 = this.conditions[conditonName1];
            FaultInjectionCondition conditon2 = this.conditions[conditionName2];
            IFaultInjectionResult result = responseDelayResult;

            FaultInjectionRule rule1 = new FaultInjectionRuleBuilder(
                id: operation,
                condition: conditon1,
                result: result)
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            FaultInjectionRule rule2 = new FaultInjectionRuleBuilder(
                id: operation,
                condition: conditon2,
                result: result)
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { rule1, rule2 };
            FaultInjector faultInjector = new FaultInjector(rules);

            rule1.Disable();
            rule2.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "Central US", "North Central US", "East US" },
                AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(CosmosAvailabilityStrategyTests.dbName);
                Container container = database.GetContainer(CosmosAvailabilityStrategyTests.containerName);

                CosmosTraceDiagnostics traceDiagnostic;
                object hedgeContext;

                switch (operation)
                {
                    case "Read":
                        rule1.Enable();
                        rule2.Enable();

                        ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                            "testId",
                            new PartitionKey("pk"));

                        traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
                        Assert.IsNotNull(traceDiagnostic);
                        traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual(CosmosAvailabilityStrategyTests.eastUs, (string)hedgeContext);

                        break;

                    case "SinglePartitionQuery":
                        string queryString = "SELECT * FROM c";

                        QueryRequestOptions requestOptions = new QueryRequestOptions()
                        {
                            PartitionKey = new PartitionKey("pk"),
                        };

                        FeedIterator<AvailabilityStrategyTestObject> queryIterator = container.GetItemQueryIterator<AvailabilityStrategyTestObject>(
                            new QueryDefinition(queryString),
                            requestOptions: requestOptions);

                        rule1.Enable();
                        rule2.Enable();

                        while (queryIterator.HasMoreResults)
                        {
                            FeedResponse<AvailabilityStrategyTestObject> feedResponse = await queryIterator.ReadNextAsync();

                            traceDiagnostic = feedResponse.Diagnostics as CosmosTraceDiagnostics;
                            Assert.IsNotNull(traceDiagnostic);
                            traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                            Assert.IsNotNull(hedgeContext);
                            Assert.AreEqual(CosmosAvailabilityStrategyTests.eastUs, (string)hedgeContext);
                        }

                        break;

                    case "CrossPartitionQuery":
                        string crossPartitionQueryString = "SELECT * FROM c";
                        FeedIterator<AvailabilityStrategyTestObject> crossPartitionQueryIterator = container.GetItemQueryIterator<AvailabilityStrategyTestObject>(
                            new QueryDefinition(crossPartitionQueryString));

                        rule1.Enable();
                        rule2.Enable();

                        while (crossPartitionQueryIterator.HasMoreResults)
                        {
                            FeedResponse<AvailabilityStrategyTestObject> feedResponse = await crossPartitionQueryIterator.ReadNextAsync();

                            traceDiagnostic = feedResponse.Diagnostics as CosmosTraceDiagnostics;
                            Assert.IsNotNull(traceDiagnostic);
                            traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                            Assert.IsNotNull(hedgeContext);
                            Assert.AreEqual(CosmosAvailabilityStrategyTests.eastUs, (string)hedgeContext);
                        }

                        break;

                    case "ReadMany":
                        rule1.Enable();
                        rule2.Enable();

                        FeedResponse<AvailabilityStrategyTestObject> readManyResponse = await container.ReadManyItemsAsync<AvailabilityStrategyTestObject>(
                            new List<(string, PartitionKey)>()
                            {
                            ("testId", new PartitionKey("pk")),
                            ("testId2", new PartitionKey("pk2")),
                            ("testId3", new PartitionKey("pk3")),
                            ("testId4", new PartitionKey("pk4"))
                            });

                        traceDiagnostic = readManyResponse.Diagnostics as CosmosTraceDiagnostics;
                        Assert.IsNotNull(traceDiagnostic);
                        traceDiagnostic.Value.Data.TryGetValue("Response Region", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual(CosmosAvailabilityStrategyTests.eastUs, (string)hedgeContext);

                        break;

                    case "ChangeFeed":
                        Container leaseContainer = database.GetContainer(CosmosAvailabilityStrategyTests.changeFeedContainerName);
                        ChangeFeedProcessor changeFeedProcessor = container.GetChangeFeedProcessorBuilder<AvailabilityStrategyTestObject>(
                            processorName: "AvialabilityStrategyTest",
                            onChangesDelegate: HandleChangesStepAsync)
                            .WithInstanceName("test")
                            .WithLeaseContainer(leaseContainer)
                            .Build();
                        await changeFeedProcessor.StartAsync();
                        await Task.Delay(1000);

                        AvailabilityStrategyTestObject testObject = new AvailabilityStrategyTestObject
                        {
                            Id = "item4",
                            Pk = "pk4",
                            Other = Guid.NewGuid().ToString()
                        };
                        await container.UpsertItemAsync<AvailabilityStrategyTestObject>(testObject);

                        rule1.Enable();
                        rule2.Enable();

                        await Task.Delay(5000);

                        rule1.Disable();
                        rule2.Disable();

                        await changeFeedProcessor.StopAsync();

                        break;

                    default:
                        Assert.Fail("Invalid operation");
                        break;
                }

                rule1.Disable();
                rule2.Disable();
            }
        }

        private static async Task HandleChangesAsync(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<AvailabilityStrategyTestObject> changes,
            CancellationToken cancellationToken)
        {
            if (context.Diagnostics.GetClientElapsedTime() > TimeSpan.FromSeconds(1))
            {
                Assert.Fail("Change Feed Processor took too long");
            }

            CosmosTraceDiagnostics traceDiagnostic = context.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            traceDiagnostic.Value.Data.TryGetValue("Response Region", out object hedgeContext);
            Assert.IsNotNull(hedgeContext);
            Assert.AreNotEqual(CosmosAvailabilityStrategyTests.centralUS, (string)hedgeContext);
            await Task.Delay(1);
        }

        private static async Task HandleChangesStepAsync(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<AvailabilityStrategyTestObject> changes,
            CancellationToken cancellationToken)
        {
            if (context.Diagnostics.GetClientElapsedTime() > TimeSpan.FromSeconds(1))
            {
                Assert.Fail("Change Feed Processor took too long");
            }
            
            CosmosTraceDiagnostics traceDiagnostic = context.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            traceDiagnostic.Value.Data.TryGetValue("Response Region", out object hedgeContext);
            Assert.IsNotNull(hedgeContext);
            Assert.AreNotEqual(CosmosAvailabilityStrategyTests.centralUS, (string)hedgeContext);
            Assert.AreNotEqual(CosmosAvailabilityStrategyTests.northCentralUS, (string)hedgeContext);
            await Task.Delay(1);
        }

        internal class AvailabilityStrategyTestObject
        {

            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("pk")]
            public string Pk { get; set; }

            [JsonPropertyName("other")]
            public string Other { get; set; }
        }

        private class CosmosSystemTextJsonSerializer : CosmosSerializer
        {
            private readonly JsonObjectSerializer systemTextJsonSerializer;

            public CosmosSystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
            {
                this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
            }

            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                {
                    if (stream.CanSeek
                           && stream.Length == 0)
                    {
                        return default;
                    }

                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        return (T)(object)stream;
                    }

                    return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new MemoryStream();
                this.systemTextJsonSerializer.Serialize(streamPayload, input, input.GetType(), default);
                streamPayload.Position = 0;
                return streamPayload;
            }
        }
    }
}