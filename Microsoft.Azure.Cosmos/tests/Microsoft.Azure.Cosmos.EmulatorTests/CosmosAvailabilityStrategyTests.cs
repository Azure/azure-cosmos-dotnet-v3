
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
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

        public CosmosClient client;

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyNoTriggerTest()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("West US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(400))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { responseDelay };
            FaultInjector faultInjector = new FaultInjector(rules);

            responseDelay.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "West US", "East US" },
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(1000),
                        thresholdStep: TimeSpan.FromMilliseconds(50))
            };

            this.client = new CosmosClient(
                accountEndpoint: "",
                authKeyOrResourceToken: "",
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            string dbName = "db";
            string containerName = "container";

            Database database = await this.client.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await database.CreateContainerIfNotExistsAsync(containerName, "/pk");

            responseDelay.Enable();
            ItemResponse<dynamic> ir = await container.ReadItemAsync<dynamic>("testId", new PartitionKey("pk"));

            CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            Assert.IsFalse(traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out _));

            this.client.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyTriggerTest()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("West US")
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
                ApplicationPreferredRegions = new List<string>() { "West US", "East US" },
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50))
            };

            this.client = new CosmosClient(
                accountEndpoint: "",
                authKeyOrResourceToken: "",
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            string dbName = "db";
            string containerName = "container";

            Database database = await this.client.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await database.CreateContainerIfNotExistsAsync(containerName, "/pk");

            responseDelay.Enable();
            ItemResponse<dynamic> ir = await container.ReadItemAsync<dynamic>("testId", new PartitionKey("pk"));

            CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsObject);
            List<string> excludeRegionsList = excludeRegionsObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("West US"));

            this.client.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyStepTest()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("West US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(4000))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            FaultInjectionRule responseDelay2 = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("East US")
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(4000))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(90))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { responseDelay, responseDelay2 };
            FaultInjector faultInjector = new FaultInjector(rules);

            responseDelay.Disable();
            responseDelay2.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "West US", "East US", "Central US" },
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50))
            };

            this.client = new CosmosClient(
                accountEndpoint: "",
                authKeyOrResourceToken: "",
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            string dbName = "db";
            string containerName = "container";

            Database database = await this.client.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await database.CreateContainerIfNotExistsAsync(containerName, "/pk");

            responseDelay.Enable();
            responseDelay2.Enable();
            ItemResponse<dynamic> ir = await container.ReadItemAsync<dynamic>("testId", new PartitionKey("pk"));

            CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out object excludeRegionsObject);
            List<string> excludeRegionsList = excludeRegionsObject as List<string>;
            Assert.IsTrue(excludeRegionsList.Contains("West US"));
            Assert.IsTrue(excludeRegionsList.Contains("East US"));

            this.client.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AvailabilityStrategyDisableOverideTest()
        {
            FaultInjectionRule responseDelay = new FaultInjectionRuleBuilder(
                id: "responseDely",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithRegion("West US")
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
                ApplicationPreferredRegions = new List<string>() { "West US", "East US" },
                AvailabilityStrategy = new CrossRegionParallelHedgingAvailabilityStrategy(
                        threshold: TimeSpan.FromMilliseconds(100),
                        thresholdStep: TimeSpan.FromMilliseconds(50))
            };

            this.client = new CosmosClient(
                accountEndpoint: "",
                authKeyOrResourceToken: "",
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions));

            string dbName = "db";
            string containerName = "container";

            Database database = await this.client.CreateDatabaseIfNotExistsAsync(dbName);
            Container container = await database.CreateContainerIfNotExistsAsync(containerName, "/pk");

            responseDelay.Enable();
            ItemRequestOptions requestOptions = new ItemRequestOptions
            {
                AvailabilityStrategy = new DisabledAvailabilityStrategy()
            };
            
            ItemResponse<dynamic> ir = await container.ReadItemAsync<dynamic>(
                "testId", 
                new PartitionKey("pk"),
                requestOptions);

            CosmosTraceDiagnostics traceDiagnostic = ir.Diagnostics as CosmosTraceDiagnostics;
            Assert.IsNotNull(traceDiagnostic);
            Assert.IsFalse(traceDiagnostic.Value.Data.TryGetValue("ExcludedRegions", out _));

            this.client.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public void RequestMessageCloneTests()
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

            RequestMessage clone = httpRequest.Clone(httpRequest.Trace);

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
}