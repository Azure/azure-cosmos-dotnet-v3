
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
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.ReadItem)
            .Build();
        private static readonly FaultInjectionCondition queryConditon = new FaultInjectionConditionBuilder()
            .WithRegion("Central US")
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition readManyCondition = new FaultInjectionConditionBuilder()
            .WithRegion("Central US")
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition changeFeedCondtion = new FaultInjectionConditionBuilder()
            .WithRegion("Central US")
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.All)
            .Build();

        private static readonly FaultInjectionCondition readConditonStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.ReadItem)
            .Build();
        private static readonly FaultInjectionCondition queryConditonStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition readManyConditionStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.QueryItem)
            .Build();
        private static readonly FaultInjectionCondition changeFeedCondtionStep = new FaultInjectionConditionBuilder()
            .WithRegion("North Central US")
            .WithConnectionType(FaultInjectionConnectionType.Direct)
            .WithOperationType(FaultInjectionOperationType.ReadFeed)
            .Build();

        private static readonly IFaultInjectionResult goneResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.Gone)
            .Build();
        private static readonly IFaultInjectionResult retryWithResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.RetryWith)
            .Build();
        private static readonly IFaultInjectionResult internalServerErrorResult = FaultInjectionResultBuilder
            .GetResultBuilder(FaultInjectionServerErrorType.InternalServerError)
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
        [DataRow(false, DisplayName = "ValidateAvailabilityStrategyNoTriggerTest with preferred regions.")]
        [DataRow(true, DisplayName = "ValidateAvailabilityStrategyNoTriggerTest w/o preferred regions.")]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyNoTriggerTest(bool isPreferredLocationsEmpty)
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
                ApplicationPreferredRegions = isPreferredLocationsEmpty ? new List<string>() : new List<string>() { "Central US", "North Central US" },
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

                if (isPreferredLocationsEmpty)
                {
                    Assert.AreEqual(3, hedgeContextList.Count);
                    Assert.IsTrue(hedgeContextList.Contains(CosmosAvailabilityStrategyTests.centralUS));
                    Assert.IsTrue(hedgeContextList.Contains(CosmosAvailabilityStrategyTests.northCentralUS));
                    Assert.IsTrue(hedgeContextList.Contains(CosmosAvailabilityStrategyTests.eastUs));
                }
                else
                {
                    Assert.AreEqual(2, hedgeContextList.Count);
                    Assert.IsTrue(hedgeContextList.Contains(CosmosAvailabilityStrategyTests.centralUS));
                    Assert.IsTrue(hedgeContextList.Contains(CosmosAvailabilityStrategyTests.northCentralUS));
                }
            };
        }

        [TestMethod]
        [DataRow(false, DisplayName = "ValidateAvailabilityStrategyNoTriggerTest with preferred regions.")]
        [DataRow(true, DisplayName = "ValidateAvailabilityStrategyNoTriggerTest w/o preferred regions.")]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyRequestOptionsTriggerTest(bool isPreferredLocationsEmpty)
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
                ApplicationPreferredRegions = isPreferredLocationsEmpty? new List<string>() : new List<string>() { "Central US", "North Central US" },
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
        [DataRow(false, DisplayName = "ValidateAvailabilityStrategyNoTriggerTest with preferred regions.")]
        [DataRow(true, DisplayName = "ValidateAvailabilityStrategyNoTriggerTest w/o preferred regions.")]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyDisableOverideTest(bool isPreferredLocationsEmpty)
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
                ApplicationPreferredRegions = isPreferredLocationsEmpty ? new List<string>() : new List<string>() { "Central US", "North Central US" },
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
        [DataRow("Read", "Read", "Gone", false, DisplayName = "Read | Gone | With Preferred Regions")]
        [DataRow("Read", "Read", "RetryWith", false, DisplayName = "Read | RetryWith | With Preferred Regions")]
        [DataRow("Read", "Read", "InternalServerError", false, DisplayName = "Read | InternalServerError | With Preferred Regions")]
        [DataRow("Read", "Read", "ReadSessionNotAvailable", false, DisplayName = "Read | ReadSessionNotAvailable | With Preferred Regions")]
        [DataRow("Read", "Read", "Timeout", false, DisplayName = "Read | Timeout | With Preferred Regions")]
        [DataRow("Read", "Read", "PartitionIsSplitting", false, DisplayName = "Read | PartitionIsSplitting | With Preferred Regions")]
        [DataRow("Read", "Read", "PartitionIsMigrating", false, DisplayName = "Read | PartitionIsMigrating | With Preferred Regions")]
        [DataRow("Read", "Read", "ServiceUnavailable", false, DisplayName = "Read | ServiceUnavailable | With Preferred Regions")]
        [DataRow("Read", "Read", "ResponseDelay", false, DisplayName = "Read | ResponseDelay | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "Gone", false, DisplayName = "SinglePartitionQuery | Gone | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "RetryWith", false, DisplayName = "SinglePartitionQuery | RetryWith | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "InternalServerError", false, DisplayName = "SinglePartitionQuery | InternalServerError | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "ReadSessionNotAvailable", false, DisplayName = "SinglePartitionQuery | ReadSessionNotAvailable | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "Timeout", false, DisplayName = "SinglePartitionQuery | Timeout | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "PartitionIsSplitting", false, DisplayName = "SinglePartitionQuery | PartitionIsSplitting | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "PartitionIsMigrating", false, DisplayName = "SinglePartitionQuery | PartitionIsMigrating | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "ServiceUnavailable", false, DisplayName = "SinglePartitionQuery | ServiceUnavailable | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "ResponseDelay", false, DisplayName = "SinglePartitionQuery | ResponseDelay | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "Gone", false, DisplayName = "CrossPartitionQuery | Gone | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "RetryWith", false, DisplayName = "CrossPartitionQuery | RetryWith | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "InternalServerError", false, DisplayName = "CrossPartitionQuery | InternalServerError | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "ReadSessionNotAvailable", false, DisplayName = "CrossPartitionQuery | ReadSessionNotAvailable | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "Timeout", false, DisplayName = "CrossPartitionQuery | Timeout | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "PartitionIsSplitting", false, DisplayName = "CrossPartitionQuery | PartitionIsSplitting | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "PartitionIsMigrating", false, DisplayName = "CrossPartitionQuery | PartitionIsMigrating | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "ServiceUnavailable", false, DisplayName = "CrossPartitionQuery | ServiceUnavailable | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "ResponseDelay", false, DisplayName = "CrossPartitionQuery | ResponseDelay | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "Gone", false, DisplayName = "ReadMany | Gone | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "RetryWith", false, DisplayName = "ReadMany | RetryWith | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "InternalServerError", false, DisplayName = "ReadMany | InternalServerError | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ReadSessionNotAvailable", false, DisplayName = "ReadMany | ReadSessionNotAvailable | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "Timeout", false, DisplayName = "ReadMany | Timeout | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "PartitionIsSplitting", false, DisplayName = "ReadMany | PartitionIsSplitting | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "PartitionIsMigrating", false, DisplayName = "ReadMany | PartitionIsMigrating | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ServiceUnavailable", false, DisplayName = "ReadMany | ServiceUnavailable | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ResponseDelay", false, DisplayName = "ReadMany | ResponseDelay | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "Gone", false, DisplayName = "ChangeFeed | Gone | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "RetryWith", false, DisplayName = "ChangeFeed | RetryWith | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "InternalServerError", false, DisplayName = "ChangeFeed | InternalServerError | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ReadSessionNotAvailable", false, DisplayName = "ChangeFeed | ReadSessionNotAvailable | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "Timeout", false, DisplayName = "ChangeFeed | Timeout | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "PartitionIsSplitting", false, DisplayName = "ChangeFeed | PartitionIsSplitting | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "PartitionIsMigrating", false, DisplayName = "ChangeFeed | PartitionIsMigrating | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ServiceUnavailable", false, DisplayName = "ChangeFeed | ServiceUnavailable | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ResponseDelay", false, DisplayName = "ChangeFeed | ResponseDelay | With Preferred Regions")]
        [DataRow("Read", "Read", "Gone", true, DisplayName = "Read | Gone | W/O Preferred Regions")]
        [DataRow("Read", "Read", "RetryWith", true, DisplayName = "Read | RetryWith | W/O Preferred Regions")]
        [DataRow("Read", "Read", "InternalServerError", true, DisplayName = "Read | InternalServerError | W/O Preferred Regions")]
        [DataRow("Read", "Read", "ReadSessionNotAvailable", true, DisplayName = "Read | ReadSessionNotAvailable | W/O Preferred Regions")]
        [DataRow("Read", "Read", "Timeout", true, DisplayName = "Read | Timeout | W/O Preferred Regions")]
        [DataRow("Read", "Read", "PartitionIsSplitting", true, DisplayName = "Read | PartitionIsSplitting | W/O Preferred Regions")]
        [DataRow("Read", "Read", "PartitionIsMigrating", true, DisplayName = "Read | PartitionIsMigrating | W/O Preferred Regions")]
        [DataRow("Read", "Read", "ServiceUnavailable", true, DisplayName = "Read | ServiceUnavailable | W/O Preferred Regions")]
        [DataRow("Read", "Read", "ResponseDelay", true, DisplayName = "Read | ResponseDelay | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "Gone", true, DisplayName = "SinglePartitionQuery | Gone | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "RetryWith", true, DisplayName = "SinglePartitionQuery | RetryWith | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "InternalServerError", true, DisplayName = "SinglePartitionQuery | InternalServerError | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "ReadSessionNotAvailable", true, DisplayName = "SinglePartitionQuery | ReadSessionNotAvailable | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "Timeout", true, DisplayName = "SinglePartitionQuery | Timeout | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "PartitionIsSplitting", true, DisplayName = "SinglePartitionQuery | PartitionIsSplitting | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "PartitionIsMigrating", true, DisplayName = "SinglePartitionQuery | PartitionIsMigrating | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "ServiceUnavailable", true, DisplayName = "SinglePartitionQuery | ServiceUnavailable | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "ResponseDelay", true, DisplayName = "SinglePartitionQuery | ResponseDelay | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "Gone", true, DisplayName = "CrossPartitionQuery | Gone | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "RetryWith", true, DisplayName = "CrossPartitionQuery | RetryWith | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "InternalServerError", true, DisplayName = "CrossPartitionQuery | InternalServerError | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "ReadSessionNotAvailable", true, DisplayName = "CrossPartitionQuery | ReadSessionNotAvailable | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "Timeout", true, DisplayName = "CrossPartitionQuery | Timeout | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "PartitionIsSplitting", true, DisplayName = "CrossPartitionQuery | PartitionIsSplitting | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "PartitionIsMigrating", true, DisplayName = "CrossPartitionQuery | PartitionIsMigrating | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "ServiceUnavailable", true, DisplayName = "CrossPartitionQuery | ServiceUnavailable | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "ResponseDelay", true, DisplayName = "CrossPartitionQuery | ResponseDelay | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "Gone", true, DisplayName = "ReadMany | Gone | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "RetryWith", true, DisplayName = "ReadMany | RetryWith | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "InternalServerError", true, DisplayName = "ReadMany | InternalServerError | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ReadSessionNotAvailable", true, DisplayName = "ReadMany | ReadSessionNotAvailable | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "Timeout", true, DisplayName = "ReadMany | Timeout | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "PartitionIsSplitting", true, DisplayName = "ReadMany | PartitionIsSplitting | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "PartitionIsMigrating", true, DisplayName = "ReadMany | PartitionIsMigrating | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ServiceUnavailable", true, DisplayName = "ReadMany | ServiceUnavailable | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ResponseDelay", true, DisplayName = "ReadMany | ResponseDelay | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "Gone", true, DisplayName = "ChangeFeed | Gone | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "RetryWith", true, DisplayName = "ChangeFeed | RetryWith | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "InternalServerError", true, DisplayName = "ChangeFeed | InternalServerError | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ReadSessionNotAvailable", true, DisplayName = "ChangeFeed | ReadSessionNotAvailable | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "Timeout", true, DisplayName = "ChangeFeed | Timeout | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "PartitionIsSplitting", true, DisplayName = "ChangeFeed | PartitionIsSplitting | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "PartitionIsMigrating", true, DisplayName = "ChangeFeed | PartitionIsMigrating | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ServiceUnavailable", true, DisplayName = "ChangeFeed | ServiceUnavailable | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ResponseDelay", true, DisplayName = "ChangeFeed | ResponseDelay | W/O Preferred Regions")]
        public async Task AvailabilityStrategyAllFaultsTests(string operation, string conditonName, string resultName, bool isPreferredLocationsEmpty)
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
                ApplicationPreferredRegions = isPreferredLocationsEmpty ? new List<string>() : new List<string>() { "Central US", "North Central US" },
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

                        ItemRequestOptions itemRequestOptions = new ItemRequestOptions();

                        if (isPreferredLocationsEmpty)
                        {
                            itemRequestOptions.ExcludeRegions = new List<string>() { "East US" };
                        }

                        ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                            "testId",
                            new PartitionKey("pk"),
                            itemRequestOptions);

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

                        if (isPreferredLocationsEmpty)
                        {
                            requestOptions.ExcludeRegions = new List<string>() { "East US" };
                        }

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

                        QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
                        
                        if (isPreferredLocationsEmpty)
                        {
                            queryRequestOptions.ExcludeRegions = new List<string>() { "East US" };
                        }
                        
                        FeedIterator<AvailabilityStrategyTestObject> crossPartitionQueryIterator = container.GetItemQueryIterator<AvailabilityStrategyTestObject>(
                            new QueryDefinition(crossPartitionQueryString),
                            null,
                            queryRequestOptions);

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

                        ReadManyRequestOptions readManyRequestOptions = new ReadManyRequestOptions();
                        
                        if (isPreferredLocationsEmpty)
                        {
                            readManyRequestOptions.ExcludeRegions = new List<string>() { "East US" };
                        }

                        FeedResponse<AvailabilityStrategyTestObject> readManyResponse = await container.ReadManyItemsAsync<AvailabilityStrategyTestObject>(
                            new List<(string, PartitionKey)>()
                            {
                            ("testId", new PartitionKey("pk")),
                            ("testId2", new PartitionKey("pk2")),
                            ("testId3", new PartitionKey("pk3")),
                            ("testId4", new PartitionKey("pk4"))
                            },
                            readManyRequestOptions);

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
        [DataRow("Read", "Read", "ReadStep", false, DisplayName = "Read | ReadStep | With Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "QueryStep", false, DisplayName = "Query | SinglePartitionQueryStep | With Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "QueryStep", false, DisplayName = "Query | CrossPartitionQueryStep | With Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ReadManyStep", false, DisplayName = "ReadMany | ReadManyStep | With Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ChangeFeedStep", false, DisplayName = "ChangeFeed | ChangeFeedStep | With Preferred Regions")]
        [DataRow("Read", "Read", "ReadStep", true, DisplayName = "Read | ReadStep | W/O Preferred Regions")]
        [DataRow("SinglePartitionQuery", "Query", "QueryStep", true, DisplayName = "Query | SinglePartitionQueryStep | W/O Preferred Regions")]
        [DataRow("CrossPartitionQuery", "Query", "QueryStep", true, DisplayName = "Query | CrossPartitionQueryStep | W/O Preferred Regions")]
        [DataRow("ReadMany", "ReadMany", "ReadManyStep", true, DisplayName = "ReadMany | ReadManyStep | W/O Preferred Regions")]
        [DataRow("ChangeFeed", "ChangeFeed", "ChangeFeedStep", true, DisplayName = "ChangeFeed | ChangeFeedStep | W/O Preferred Regions")]
        public async Task AvailabilityStrategyStepTests(string operation, string conditonName1, string conditionName2, bool isPreferredRegionsEmpty)
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
                ApplicationPreferredRegions = isPreferredRegionsEmpty ? new List<string>() : new List<string>() { "Central US", "North Central US", "East US" },
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