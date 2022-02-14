//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Http;
    using System.Diagnostics;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using Documents.Rntbd;
    using System.Globalization;
    using global::Azure.Monitor.OpenTelemetry.Exporter;
    using OpenTelemetry;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;

    [TestClass]
    public class OpenTelemetryTests : BaseCosmosClientHelper
    {
        private CosmosClientBuilder cosmosClientBuilder;
        private static TracerProvider Provider;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            OpenTelemetryTests.Provider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Azure.*") // Collect all traces from Azure SDKs
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "Cosmos SDK Emulator Test", serviceVersion: "1.0"))
                .AddAzureMonitorTraceExporter(options => options.ConnectionString =
                    "InstrumentationKey=2fabff39-6a32-42da-9e8f-9fcff7d99c6b;IngestionEndpoint=https://westus2-2.in.applicationinsights.azure.com/") // Export traces to Azure Monitor
                .AddHttpClientInstrumentation()
                .Build();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
        }

        [ClassCleanup]
        public static void FinalCleanup()
        {
            OpenTelemetryTests.Provider.Dispose();
        }
        
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task PointSuccessOperationsTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode);
            // Create an item
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
            ItemResponse<ToDoActivity> createResponse = await container.CreateItemAsync<ToDoActivity>(testItem);
            ToDoActivity testItemCreated = createResponse.Resource;

            // Read an Item
            await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));

            // Upsert an Item
            await container.UpsertItemAsync<ToDoActivity>(testItem);

            // Replace an Item
            await container.ReplaceItemAsync<ToDoActivity>(testItemCreated, testItemCreated.id.ToString());

            // Patch an Item
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/new", "patched")
            };
            await ((ContainerInternal)container).PatchItemAsync<ToDoActivity>(
                testItem.id,
                new Cosmos.PartitionKey(testItem.id),
                patch);

            // Delete an Item
            await container.DeleteItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
        }

        private static ItemBatchOperation CreateItem(string itemId)
        {
            var testItem = new { id = itemId, Status = itemId };
            return new ItemBatchOperation(Documents.OperationType.Create, 0, new Cosmos.PartitionKey(itemId), itemId, TestCommon.SerializerCore.ToStream(testItem));
        }

        private async Task<Container> CreateClientAndContainer(ConnectionMode mode,
            Microsoft.Azure.Cosmos.ConsistencyLevel? consistency = null,
            bool isLargeContainer = false)
        {
            if (consistency.HasValue)
            {
                this.cosmosClientBuilder = this.cosmosClientBuilder.WithConsistencyLevel(consistency.Value);
            }

            this.cosmosClient = mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build();

            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());

            return await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: isLargeContainer ? 15000 : 400);

        }

    }
}
