//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Azure.Cosmos.ApplicationInsights;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ApplicationInsightWithDiagSourceTest : BaseCosmosClientHelper
    {
        private CosmosClientBuilder cosmosClientBuilder;

        [TestInitialize]
        public void TestInitialize()
        {
            IDictionary<string, IObserver<KeyValuePair<string, object>>> listener = new Dictionary<string, IObserver<KeyValuePair<string, object>>>
            {
                { "AppInsight", TelemetryInitializer.Initialize("2fabff39-6a32-42da-9e8f-9fcff7d99c6b") }
            };
            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration()
                                                 .AddListener(listener);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        public async Task PointSuccessOperationsTest(ConnectionMode mode)
        { 
            Container container = await this.CreateClientAndContainer(mode);

            // Create an item
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
            await container.CreateItemAsync<ToDoActivity>(testItem);
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