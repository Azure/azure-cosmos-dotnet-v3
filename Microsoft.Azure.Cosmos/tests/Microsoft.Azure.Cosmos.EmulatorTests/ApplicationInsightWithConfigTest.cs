//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DiagnosticSourceListener;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Azure.Cosmos.ApplicationInsights;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ApplicationInsightWithConfigTest : BaseCosmosClientHelper
    {
        private CosmosClientBuilder cosmosClientBuilder;

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        public void PointSuccessOperationsTest(ConnectionMode mode)
        {
            using (var module = new DiagnosticSourceTelemetryModule())
            {
                DiagnosticSourceListeningRequest listeningRequest = new DiagnosticSourceListeningRequest("AppInsight");
                module.Sources.Add(listeningRequest);

                TelemetryConfiguration configuration = new();
                configuration.InstrumentationKey = "2fabff39-6a32-42da-9e8f-9fcff7d99c6b";
                module.Initialize(configuration);

                this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration();

                /*Container container = await this.CreateClientAndContainer(mode);

                // Create an item
                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
                await container.CreateItemAsync<ToDoActivity>(testItem);*/

                DiagnosticSource testDiagnosticSource = new DiagnosticListener("AppInsight");

                //var testDiagnosticSource = new TestDiagnosticSource();
                testDiagnosticSource.Write("Hey!", new { Prop1 = 1234 });
            } 
           
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