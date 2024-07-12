
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Database = Database;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class CosmosAvailabilityStrategyTests
    {

        private CosmosClient client;
        private Database database;
        private Container container;
        private Container changeFeedContainer;
        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private string connectionString;
        private string dbName;
        private string containerName;
        private string changeFeedContainerName;

        [TestCleanup]
        public async Task TestCleanup()
        {
            await this.database?.DeleteAsync();
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

            this.dbName = Guid.NewGuid().ToString();
            this.containerName = Guid.NewGuid().ToString();
            this.changeFeedContainerName = Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(this.dbName);
            this.container = await this.database.CreateContainerIfNotExistsAsync(this.containerName, "/pk");
            this.changeFeedContainer = await this.database.CreateContainerIfNotExistsAsync(this.changeFeedContainerName, "/partitionKey");

            await this.container.CreateItemAsync<AvailabilityStrategyTestObject>(new AvailabilityStrategyTestObject { Id = "testId", Pk = "pk" });
            await this.container.CreateItemAsync<AvailabilityStrategyTestObject>(new AvailabilityStrategyTestObject { Id = "testId2", Pk = "pk2" });
            await this.container.CreateItemAsync<AvailabilityStrategyTestObject>(new AvailabilityStrategyTestObject { Id = "testId3", Pk = "pk3" });
            await this.container.CreateItemAsync<AvailabilityStrategyTestObject>(new AvailabilityStrategyTestObject { Id = "testId4", Pk = "pk4" });

            //Must Ensure the data is replicated to all regions
            await Task.Delay(60000);
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

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { responseDelay };
            FaultInjector faultInjector = new FaultInjector(rules);

            responseDelay.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "Central US", "North Central US" },
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(1500),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

            responseDelay.Enable();
            ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>("testId", new PartitionKey("pk"));

            CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);
            Assert.IsNotNull(hedgeContext);
            Assert.AreEqual("Original Request", (string)hedgeContext);

            faultInjectionClient.Dispose();
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

            CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

            responseDelay.Enable();

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                    threshold: TimeSpan.FromMilliseconds(100),
                    thresholdStep: TimeSpan.FromMilliseconds(50))
            };
            ItemResponse<AvailabilityStrategyTestObject> ir = await container.ReadItemAsync<AvailabilityStrategyTestObject>(
                "testId",
                new PartitionKey("pk"),
                requestOptions);

            CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);
            Assert.IsNotNull(hedgeContext);
            Assert.AreEqual("Hedged Request", (string)hedgeContext);
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsObject);
            Assert.IsNotNull(excludeRegionsObject);
            List<string> excludeRegionsList = excludeRegionsObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("Central US"));
            faultInjectionClient.Dispose();
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
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

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
            Assert.IsFalse(traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out _));

            faultInjectionClient.Dispose();
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
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

            CosmosTraceDiagnostics traceDiagnostic;
            object hedgeContext;
            List<string> excludeRegionsList;
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
                    traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                    Assert.IsNotNull(hedgeContext);
                    Assert.AreEqual("Hedged Request", (string)hedgeContext);
                    traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsObject);
                    excludeRegionsList = excludeRegionsObject as List<string>;
                    Assert.IsTrue(excludeRegionsList.Contains("Central US"));

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
                        traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual("Hedged Request", (string)hedgeContext);
                        traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsQueryObject);
                        excludeRegionsList = excludeRegionsQueryObject as List<string>;
                        Assert.IsTrue(excludeRegionsList.Contains("Central US"));
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
                        traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual("Hedged Request", (string)hedgeContext);
                        traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsQueryObject);
                        excludeRegionsList = excludeRegionsQueryObject as List<string>;
                        Assert.IsTrue(excludeRegionsList.Contains("Central US"));
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
                    traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                    Assert.IsNotNull(hedgeContext);
                    Assert.AreEqual("Hedged Request", (string)hedgeContext);
                    traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsReadManyObject);
                    excludeRegionsList = excludeRegionsReadManyObject as List<string>;
                    Assert.IsTrue(excludeRegionsList.Contains("Central US"));

                    break;

                case "ChangeFeed":
                    Container leaseContainer = database.GetContainer(this.changeFeedContainerName);
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
                        Id = "testId5",
                        Pk = "pk5",
                        Other = "other"
                    };
                    await container.CreateItemAsync<AvailabilityStrategyTestObject>(testObject);

                    rule.Enable();

                    await Task.Delay(5000);

                    Assert.IsTrue(rule.GetHitCount() > 0);

                    break;

                default:

                    Assert.Fail("Invalid operation");
                    break;
            }

            rule.Disable();

            faultInjectionClient.Dispose();
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
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50)),
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

            CosmosTraceDiagnostics traceDiagnostic;
            object hedgeContext;
            List<string> excludeRegionsList;
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
                    traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                    Assert.IsNotNull(hedgeContext);
                    Assert.AreEqual("Hedged Request", (string)hedgeContext);
                    traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsObject);
                    excludeRegionsList = excludeRegionsObject as List<string>;
                    Assert.IsTrue(excludeRegionsList.Contains("Central US"));
                    Assert.IsTrue(excludeRegionsList.Contains("North Central US"));

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
                        traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual("Hedged Request", (string)hedgeContext);
                        traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsQueryObject);
                        excludeRegionsList = excludeRegionsQueryObject as List<string>;
                        Assert.IsTrue(excludeRegionsList.Contains("Central US"));
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
                        traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                        Assert.IsNotNull(hedgeContext);
                        Assert.AreEqual("Hedged Request", (string)hedgeContext);
                        traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsQueryObject);
                        excludeRegionsList = excludeRegionsQueryObject as List<string>;
                        Assert.IsTrue(excludeRegionsList.Contains("Central US"));
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
                    traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out hedgeContext);
                    Assert.IsNotNull(hedgeContext);
                    Assert.AreEqual("Hedged Request", (string)hedgeContext);
                    traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsReadManyObject);
                    excludeRegionsList = excludeRegionsReadManyObject as List<string>;
                    Assert.IsTrue(excludeRegionsList.Contains("Central US"));
                    Assert.IsTrue(excludeRegionsList.Contains("North Central US"));

                    break;

                case "ChangeFeed":
                    Container leaseContainer = database.GetContainer(this.changeFeedContainerName);
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
                        Id = "testId5",
                        Pk = "pk5",
                        Other = "other"
                    };
                    await container.CreateItemAsync<AvailabilityStrategyTestObject>(testObject);

                    rule1.Enable();
                    rule2.Enable();

                    await Task.Delay(5000);

                    break;

                default: 
                    Assert.Fail("Invalid operation");
                    break;
            }

            rule1.Disable();
            rule2.Disable();

            faultInjectionClient.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task RequestMessageCloneTests()
        {
            RequestMessage httpRequest = new RequestMessage(
                HttpMethod.Get,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            string key = Guid.NewGuid().ToString();
            Dictionary<string, object> properties = new Dictionary<string, object>()
            {
                { key, Guid.NewGuid() }
            };

            RequestOptions requestOptions = new RequestOptions()
            {
                Properties = properties
            };
           
            httpRequest.RequestOptions = requestOptions;
            httpRequest.ResourceType = ResourceType.Document;
            httpRequest.OperationType = OperationType.Read;
            httpRequest.Headers.CorrelatedActivityId = Guid.NewGuid().ToString();
            httpRequest.PartitionKeyRangeId = new PartitionKeyRangeIdentity("0", "1");
            httpRequest.UseGatewayMode = true;
            httpRequest.ContainerId = "testcontainer";
            httpRequest.DatabaseId = "testdb";
            httpRequest.Content = Stream.Null;

            using (CloneableStream clonedBody = await StreamExtension.AsClonableStreamAsync(httpRequest.Content))
            {
                RequestMessage clone = httpRequest.Clone(httpRequest.Trace, clonedBody);

                Assert.AreEqual(httpRequest.RequestOptions.Properties, clone.RequestOptions.Properties);
                Assert.AreEqual(httpRequest.ResourceType, clone.ResourceType);
                Assert.AreEqual(httpRequest.OperationType, clone.OperationType);
                Assert.AreEqual(httpRequest.Headers.CorrelatedActivityId, clone.Headers.CorrelatedActivityId);
                Assert.AreEqual(httpRequest.PartitionKeyRangeId, clone.PartitionKeyRangeId);
                Assert.AreEqual(httpRequest.UseGatewayMode, clone.UseGatewayMode);
                Assert.AreEqual(httpRequest.ContainerId, clone.ContainerId);
                Assert.AreEqual(httpRequest.DatabaseId, clone.DatabaseId);
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
            traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);
            Assert.IsNotNull(hedgeContext);
            Assert.AreEqual("Hedged Request", (string)hedgeContext);
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsObject);
            List<string> excludeRegionsList = excludeRegionsObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("Central US"));
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
            traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);
            Assert.IsNotNull(hedgeContext);
            Assert.AreEqual("Hedged Request", (string)hedgeContext);
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsObject);
            List<string> excludeRegionsList = excludeRegionsObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("Central US"));
            Assert.IsTrue(excludeRegionsList.Contains("North Central US"));
            await Task.Delay(1);
        }

        private class AvailabilityStrategyTestObject
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