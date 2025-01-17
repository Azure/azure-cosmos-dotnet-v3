namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;

    [TestClass]
    public class CosmosMultiRegionDiagnosticsTests
    {
        CosmosClient client;
        Database database;
        Container container;

        string connectionString;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);
            this.client = new CosmosClient(this.connectionString);

            DatabaseResponse db = await this.client.CreateDatabaseIfNotExistsAsync(
                id: MultiRegionSetupHelpers.dbName,
                throughput: 400);
            this.database = db.Database;

            (this.database, this.container, _) = await MultiRegionSetupHelpers.GetOrCreateMultiRegionDatabaseAndContainers(this.client);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            //Do not delete the resources, georeplication is slow and we want to reuse the resources
            this.client.Dispose();
        }


        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task ExlcudeRegionDiagnosticsTest()
        {
            this.container = this.database.GetContainer(MultiRegionSetupHelpers.containerName);
            ItemResponse<CosmosIntegrationTestObject> itemResponse = await this.container.ReadItemAsync<CosmosIntegrationTestObject>(
                "testId", new Cosmos.PartitionKey("pk"),
                new ItemRequestOptions()
                {
                    ExcludeRegions = new List<string>() { "North Central US", "East US" }
                });

            List<string> excludeRegionsList;
            CosmosTraceDiagnostics traceDiagnostic = itemResponse.Diagnostics as CosmosTraceDiagnostics;
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionObject);
            excludeRegionsList = excludeRegionObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("North Central US"));
            Assert.IsTrue(excludeRegionsList.Contains("East US"));
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task ExcludeRegionWithReadManyDiagnosticsTest()
        {
            this.container = this.database.GetContainer(MultiRegionSetupHelpers.containerName);

            FeedResponse<CosmosIntegrationTestObject> feedResonse = await this.container.ReadManyItemsAsync<CosmosIntegrationTestObject>(
                            new List<(string, PartitionKey)>()
                            {
                            ("testId", new PartitionKey("pk")),
                            ("testId2", new PartitionKey("pk2")),
                            ("testId3", new PartitionKey("pk3")),
                            ("testId4", new PartitionKey("pk4"))
                            },
                new ReadManyRequestOptions()
                {
                    ExcludeRegions = new List<string>() { "North Central US", "East US" }
                });

            List<string> excludeRegionsList;
            CosmosTraceDiagnostics traceDiagnostic = feedResonse.Diagnostics as CosmosTraceDiagnostics;
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionObject);
            excludeRegionsList = excludeRegionObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("North Central US"));
            Assert.IsTrue(excludeRegionsList.Contains("East US"));
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task HedgeNestingDiagnosticsTest()
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
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                responseDelay.Enable();

                ItemRequestOptions requestOptions = new ItemRequestOptions
                {
                    AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50))
                };

                //Request should be hedged to North Central US
                ItemResponse<CosmosIntegrationTestObject> itemResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                    "testId", new PartitionKey("pk"),
                    requestOptions);

                CosmosTraceDiagnostics traceDiagnostic = itemResponse.Diagnostics as CosmosTraceDiagnostics;

                //Walthrough the diagnostics to ensure at Request Invoker Handler Level
                //has two Diagnostics Handler Children
                IReadOnlyList<ITrace> traceChildren = traceDiagnostic.Value.Children;
                int diagnosticsHandlerCount = 0;
                foreach (ITrace trace in traceChildren)
                {
                    if (trace.Name == "Microsoft.Azure.Cosmos.Handlers.RequestInvokerHandler")
                    {
                        foreach (ITrace childTrace in trace.Children)
                        {
                            if (childTrace.Name == "Microsoft.Azure.Cosmos.Handlers.DiagnosticsHandler")
                            {
                                diagnosticsHandlerCount++;
                            }
                        }
                    }
                }
            }
        }
    }
}
