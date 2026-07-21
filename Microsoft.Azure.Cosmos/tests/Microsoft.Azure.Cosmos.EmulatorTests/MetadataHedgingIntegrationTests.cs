//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;

    /// <summary>
    /// Cross-region metadata hedging integration tests (see <c>docs/metadata-hedging-simple-design.md</c>).
    ///
    /// Each test injects a response delay on the <b>primary region</b> for exactly one metadata read
    /// (<see cref="FaultInjectionOperationType.MetadataContainer"/> = Collection Read, or
    /// <see cref="FaultInjectionOperationType.MetadataPartitionKeyRange"/> = PartitionKeyRange ReadFeed)
    /// and asserts whether the read is hedged to a secondary region based on the opt-in.
    ///
    /// Detection is deterministic: the strategy emits the <c>"Metadata Hedge"</c> trace datum with
    /// <c>HedgeFired=True</c> only when a hedge is actually dispatched, and the strategy is not even
    /// constructed when hedging is explicitly disabled. So the datum's presence/absence in the operation
    /// diagnostics is a reliable hedged/not-hedged signal.
    ///
    /// The matrix is {cold start, warm path} x {enabled, disabled} x {Collection Read, PartitionKeyRange}:
    /// <list type="bullet">
    /// <item>cold start: a brand-new client whose caches are empty, so the first query lazily reads the metadata.</item>
    /// <item>warm path: a client already warmed on one container, then a second (still-uncached) container is
    /// read so the metadata fetch happens with hot account/endpoint/connection state.</item>
    /// </list>
    ///
    /// Requires a live multi-region account (>= 3 read regions) via the <c>COSMOSDB_MULTI_REGION</c>
    /// environment variable, matching the other <c>[TestCategory("MultiRegion")]</c> integration tests.
    /// </summary>
    [TestClass]
    [TestCategory("MultiRegion")]
    public class MetadataHedgingIntegrationTests
    {
        // Key of the per-request trace datum emitted by MetadataHedgingStrategy when a hedge fires.
        private const string MetadataHedgeDatumKey = "Metadata Hedge";

        // The hedge threshold is the first control-plane attempt timeout (1s) + 500ms = 1.5s. A 6s
        // primary-region delay keeps the primary metadata read slow well past the threshold on every
        // attempt, so the hedge (to an undelayed secondary region) reliably wins when hedging is on.
        private static readonly TimeSpan PrimaryRegionDelay = TimeSpan.FromSeconds(6);

        private string connectionString;
        private MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer serializer;
        private CosmosClient setupClient;
        private CosmosClient fiClient;

        private static string region1;
        private static string region2;
        private static string region3;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.serializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            this.setupClient = new CosmosClient(
                this.connectionString,
                new CosmosClientOptions()
                {
                    Serializer = this.serializer,
                });

            // Ensure the multi-region database and both containers exist and are replicated.
            await MultiRegionSetupHelpers.GetOrCreateMultiRegionDatabaseAndContainers(this.setupClient);

            IDictionary<string, Uri> readRegionsMapping = this.setupClient.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation();
            Assert.IsTrue(
                readRegionsMapping.Count >= 3,
                "Metadata hedging tests require an account with at least 3 read regions.");

            region1 = readRegionsMapping.Keys.ElementAt(0);
            region2 = readRegionsMapping.Keys.ElementAt(1);
            region3 = readRegionsMapping.Keys.ElementAt(2);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Always clear the opt-in override so it cannot leak into other tests in the run.
            Environment.SetEnvironmentVariable(ConfigurationManager.MetadataHedgingEnabled, null);
            this.setupClient?.Dispose();
            this.fiClient?.Dispose();
        }

        // ---- 1..4: cold start ----

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Cold start + primary region delayed + metadata hedging enabled => Collection Read is hedged.")]
        public Task ColdStart_PrimaryDelayed_HedgingEnabled_CollectionRead_IsHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataContainer,
                hedgingEnabled: true,
                warmPath: false);
        }

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Cold start + primary region delayed + metadata hedging enabled => PartitionKeyRange ReadFeed is hedged.")]
        public Task ColdStart_PrimaryDelayed_HedgingEnabled_PartitionKeyRange_IsHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataPartitionKeyRange,
                hedgingEnabled: true,
                warmPath: false);
        }

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Cold start + primary region delayed + metadata hedging disabled => Collection Read is NOT hedged.")]
        public Task ColdStart_PrimaryDelayed_HedgingDisabled_CollectionRead_IsNotHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataContainer,
                hedgingEnabled: false,
                warmPath: false);
        }

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Cold start + primary region delayed + metadata hedging disabled => PartitionKeyRange ReadFeed is NOT hedged.")]
        public Task ColdStart_PrimaryDelayed_HedgingDisabled_PartitionKeyRange_IsNotHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataPartitionKeyRange,
                hedgingEnabled: false,
                warmPath: false);
        }

        // ---- 5..8: warm path ----

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Warm path + primary region delayed + metadata hedging enabled => Collection Read is hedged.")]
        public Task WarmPath_PrimaryDelayed_HedgingEnabled_CollectionRead_IsHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataContainer,
                hedgingEnabled: true,
                warmPath: true);
        }

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Warm path + primary region delayed + metadata hedging enabled => PartitionKeyRange ReadFeed is hedged.")]
        public Task WarmPath_PrimaryDelayed_HedgingEnabled_PartitionKeyRange_IsHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataPartitionKeyRange,
                hedgingEnabled: true,
                warmPath: true);
        }

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Warm path + primary region delayed + metadata hedging disabled => Collection Read is NOT hedged.")]
        public Task WarmPath_PrimaryDelayed_HedgingDisabled_CollectionRead_IsNotHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataContainer,
                hedgingEnabled: false,
                warmPath: true);
        }

        [TestMethod]
        [Timeout(120000)]
        [Owner("kundadebdatta")]
        [Description("Warm path + primary region delayed + metadata hedging disabled => PartitionKeyRange ReadFeed is NOT hedged.")]
        public Task WarmPath_PrimaryDelayed_HedgingDisabled_PartitionKeyRange_IsNotHedgedAsync()
        {
            return this.RunMetadataHedgingScenarioAsync(
                metadataOperationType: FaultInjectionOperationType.MetadataPartitionKeyRange,
                hedgingEnabled: false,
                warmPath: true);
        }

        /// <summary>
        /// Shared driver for all 8 scenarios: injects a primary-region response delay on a single
        /// metadata read, triggers that read (cold cache), and asserts whether it hedged.
        /// </summary>
        private async Task RunMetadataHedgingScenarioAsync(
            FaultInjectionOperationType metadataOperationType,
            bool hedgingEnabled,
            bool warmPath)
        {
            // Delay the target metadata read on the PRIMARY region only. The delay applies on every
            // attempt (no WithTimes cap, bounded by duration) so the primary stays slow across the
            // control-plane retries; the hedge to an undelayed secondary region can then win.
            string ruleId = $"metadata-hedge-delay-{metadataOperationType}-{Guid.NewGuid()}";
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition: new FaultInjectionConditionBuilder()
                    .WithOperationType(metadataOperationType)
                    .WithRegion(region1)
                    .Build(),
                result: FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                    .WithDelay(PrimaryRegionDelay)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            delayRule.Disable();

            // The opt-in is resolved once at client construction, so it must be set before the client
            // is created. Explicit true/false makes the outcome independent of the account's PPAF state.
            Environment.SetEnvironmentVariable(
                ConfigurationManager.MetadataHedgingEnabled,
                hedgingEnabled ? "true" : "false");

            try
            {
                FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { delayRule });
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    Serializer = this.serializer,
                    FaultInjector = faultInjector,
                    ApplicationPreferredRegions = new List<string> { region1, region2, region3 },
                };

                this.fiClient = new CosmosClient(this.connectionString, cosmosClientOptions);

                string targetContainerName;
                if (warmPath)
                {
                    // Warm the client (account, endpoint manager, connections) and one container's
                    // metadata caches while the delay is still disabled, then target a DIFFERENT
                    // container whose metadata is still uncached so the fetch happens on a warm client.
                    Container warmupContainer = this.fiClient
                        .GetDatabase(MultiRegionSetupHelpers.dbName)
                        .GetContainer(MultiRegionSetupHelpers.containerName);
                    await DrainQueryAsync(warmupContainer);

                    targetContainerName = MultiRegionSetupHelpers.changeFeedContainerName;
                }
                else
                {
                    // Cold start: the very first operation on this fresh client lazily reads metadata.
                    targetContainerName = MultiRegionSetupHelpers.containerName;
                }

                Container targetContainer = this.fiClient
                    .GetDatabase(MultiRegionSetupHelpers.dbName)
                    .GetContainer(targetContainerName);

                // Enable the delay only now, so it applies to the target container's (cold) metadata read.
                delayRule.Enable();

                (HttpStatusCode status, string diagnostics) = await DrainQueryCollectingDiagnosticsAsync(targetContainer);

                delayRule.Disable();

                Assert.AreEqual(
                    HttpStatusCode.OK,
                    status,
                    "The query (and its underlying metadata read) must succeed regardless of hedging.");

                bool hedged = diagnostics.Contains(MetadataHedgeDatumKey)
                    && diagnostics.Contains("HedgeFired=True");

                if (hedgingEnabled)
                {
                    Assert.IsTrue(
                        delayRule.GetHitCount() >= 1,
                        "The primary-region metadata response delay should have been injected at least once.");
                    Assert.IsTrue(
                        hedged,
                        $"Expected metadata hedging to fire for {metadataOperationType} when enabled and the primary region is delayed. Diagnostics: {diagnostics}");
                    Assert.IsTrue(
                        diagnostics.Contains($"WinningRegion={region2}") || diagnostics.Contains($"WinningRegion={region3}"),
                        $"The hedge should have been won by a secondary region (region2/region3). Diagnostics: {diagnostics}");
                }
                else
                {
                    Assert.IsFalse(
                        diagnostics.Contains(MetadataHedgeDatumKey),
                        $"The '{MetadataHedgeDatumKey}' trace datum must not appear when hedging is disabled. Diagnostics: {diagnostics}");
                }
            }
            finally
            {
                delayRule.Disable();
                Environment.SetEnvironmentVariable(ConfigurationManager.MetadataHedgingEnabled, null);
                this.fiClient?.Dispose();
                this.fiClient = null;
            }
        }

        // Drains a "SELECT * FROM c" query without inspecting diagnostics (used to warm caches).
        private static async Task DrainQueryAsync(Container container)
        {
            using FeedIterator<CosmosIntegrationTestObject> iterator =
                container.GetItemQueryIterator<CosmosIntegrationTestObject>("SELECT * FROM c");
            while (iterator.HasMoreResults)
            {
                await iterator.ReadNextAsync();
            }
        }

        // Drains a "SELECT * FROM c" query and returns the last page status plus the concatenated
        // diagnostics of every page (the cold metadata reads surface on the first page).
        private static async Task<(HttpStatusCode status, string diagnostics)> DrainQueryCollectingDiagnosticsAsync(Container container)
        {
            HttpStatusCode lastStatus = HttpStatusCode.OK;
            StringBuilder diagnostics = new StringBuilder();
            using FeedIterator<CosmosIntegrationTestObject> iterator =
                container.GetItemQueryIterator<CosmosIntegrationTestObject>("SELECT * FROM c");
            while (iterator.HasMoreResults)
            {
                FeedResponse<CosmosIntegrationTestObject> page = await iterator.ReadNextAsync();
                lastStatus = page.StatusCode;
                diagnostics.AppendLine(page.Diagnostics.ToString());
            }

            return (lastStatus, diagnostics.ToString());
        }
    }
}
