//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GatewayHedgingDiagnosticsEmulatorTests
    {
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;

        [TestInitialize]
        public async Task Initialize()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                    threshold: TimeSpan.FromMilliseconds(1000),
                    thresholdStep: TimeSpan.FromMilliseconds(500))
            };

            this.cosmosClient = TestCommon.CreateCosmosClient(clientOptions);
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            this.container = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.database != null)
            {
                await this.database.DeleteStreamAsync();
            }

            this.cosmosClient?.Dispose();
        }

        [TestMethod]
        [TestCategory("GatewayHedgingDiagnostics")]
        public async Task GatewayDrivenHedgingSuppressionDiagnostics_EmitsOnceAndReEmitsAfterFlagCycle()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> createResponse = await this.container.CreateItemAsync(item);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            ItemResponse<ToDoActivity> initialResponse = await this.ReadItemAsync(item);
            GatewayHedgingDiagnosticsEmulatorTests.AssertNoHedgingDisabledByGatewayDiagnostic(initialResponse);

            this.cosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: true,
                latestDisableCrossRegionalHedging: true);

            ItemResponse<ToDoActivity> firstSuppressedResponse = await this.ReadItemAsync(item);
            GatewayHedgingDiagnosticsEmulatorTests.AssertHedgingDisabledByGatewayDiagnostic(firstSuppressedResponse);

            ItemResponse<ToDoActivity> secondSuppressedResponse = await this.ReadItemAsync(item);
            GatewayHedgingDiagnosticsEmulatorTests.AssertNoHedgingDisabledByGatewayDiagnostic(secondSuppressedResponse);

            this.cosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: true,
                latestDisableCrossRegionalHedging: false);

            ItemResponse<ToDoActivity> falseFlagResponse = await this.ReadItemAsync(item);
            GatewayHedgingDiagnosticsEmulatorTests.AssertNoHedgingDisabledByGatewayDiagnostic(falseFlagResponse);

            this.cosmosClient.DocumentClient.UpdatePartitionLevelFailoverConfigWithAccountRefresh(
                latestIsEnabled: true,
                latestDisableCrossRegionalHedging: true);

            ItemResponse<ToDoActivity> secondTrueResponse = await this.ReadItemAsync(item);
            GatewayHedgingDiagnosticsEmulatorTests.AssertHedgingDisabledByGatewayDiagnostic(secondTrueResponse);
        }

        private Task<ItemResponse<ToDoActivity>> ReadItemAsync(ToDoActivity item)
        {
            return this.container.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk));
        }

        private static void AssertHedgingDisabledByGatewayDiagnostic(ItemResponse<ToDoActivity> response)
        {
            string diagnosticsText = response.Diagnostics.ToString();
            StringAssert.Contains(
                diagnosticsText,
                $"\"{TraceDatumKeys.HedgingDisabledByGateway}\":true");
        }

        private static void AssertNoHedgingDisabledByGatewayDiagnostic(ItemResponse<ToDoActivity> response)
        {
            string diagnosticsText = response.Diagnostics.ToString();
            Assert.IsFalse(
                diagnosticsText.Contains(TraceDatumKeys.HedgingDisabledByGateway),
                $"Did not expect serialized CosmosDiagnostics to contain {TraceDatumKeys.HedgingDisabledByGateway}: {diagnosticsText}");
        }
    }
}
