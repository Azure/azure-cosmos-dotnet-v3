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

    [TestClass]
    public class CosmosMultiRegionDiagnosticsTests
    {
        CosmosClient client;
        CosmosClient faultInjectionClient;
        Database database;
        Container container;

        string connectionString;
        string dbName;
        string containerName;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);
            this.client = new CosmosClient(this.connectionString);

            this.dbName = Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(this.dbName);

            this.containerName = Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(this.containerName, "/pk");

            await this.container.CreateItemAsync(new ToDoActivity() { id = "1", pk = "1" });
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }

            this.client.Dispose();
            this.faultInjectionClient?.Dispose();
        }


        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task ExlcudeRegionDiagnosticsTest()
        {
            ItemResponse<ToDoActivity> itemResponse = await this.container.ReadItemAsync<ToDoActivity>(
                "1", new Cosmos.PartitionKey("1"),
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
        public async Task HedgeNestingDiagnosticsTest()
        {
            //Wait for global replication
            await Task.Delay(60 * 1000);

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

            this.faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            Database database = this.faultInjectionClient.GetDatabase(this.dbName);
            Container container = database.GetContainer(this.containerName);

            responseDelay.Enable();

            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                    threshold: TimeSpan.FromMilliseconds(100),
                    thresholdStep: TimeSpan.FromMilliseconds(50))
            };

            //Request should be hedged to North Central US
            ItemResponse<ToDoActivity> itemResponse = await container.ReadItemAsync<ToDoActivity>(
                "1", new PartitionKey("1"),
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
                    foreach(ITrace childTrace in trace.Children)
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
