//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EventBasedDiagnosticLogTest : BaseCosmosClientHelper
    {
        private static readonly TelemetryListener actualListener1 = new TelemetryListener("TelemetryListener1");
        private static readonly TelemetryListener actualListener2 = new TelemetryListener("TelemetryListener2");

        /// <summary>
        /// This test make sure multiple clients are sending events to their corresponding listeners
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        public async Task ListenerWithMultipleClientsTest(ConnectionMode mode)
        {
            IReadOnlyList<IObserver<KeyValuePair<string, object>>> listeners = 
                new List<IObserver<KeyValuePair<string, object>>> { actualListener1 };
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");

            CosmosClientBuilder cosmosClientBuilder1 = TestCommon.GetDefaultConfiguration()
                 .AddListeners(listeners);
            using (CosmosClient cosmosClient1 = mode == ConnectionMode.Gateway
                ? cosmosClientBuilder1.WithConnectionModeGateway().Build()
                : cosmosClientBuilder1.Build())
            {
                Database database1 = await cosmosClient1.CreateDatabaseAsync(Guid.NewGuid().ToString());
                Container container1 = await database1.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id");
                // Create an item
                await container1.CreateItemAsync<ToDoActivity>(testItem);
                await container1.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
            }

            IReadOnlyList<IObserver<KeyValuePair<string, object>>> listeners1 = 
                new List<IObserver<KeyValuePair<string, object>>> { actualListener2 };

            CosmosClientBuilder cosmosClientBuilder2 = TestCommon.GetDefaultConfiguration()
                .AddListeners(listeners1);
            using (CosmosClient cosmosClient2 = mode == ConnectionMode.Gateway
                ? cosmosClientBuilder2.WithConnectionModeGateway().Build()
                : cosmosClientBuilder2.Build())
            {
                Database database2 = await cosmosClient2.CreateDatabaseAsync(Guid.NewGuid().ToString());
                Container container2 = await database2.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id");
                // Create an item
                await container2.CreateItemAsync<ToDoActivity>(testItem);
                await container2.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
            }

            Assert.AreEqual(4, actualListener1.actualCosmosDiagnostics.Count);
            Assert.AreEqual(4, actualListener2.actualCosmosDiagnostics.Count);
        }
    }

    /// <summary>
    /// Telemetry Listener will receive the diagnostic logs
    /// </summary>
    public class TelemetryListener : IObserver<KeyValuePair<string, object>>
    {
        public readonly IList<CosmosDiagnostics> actualCosmosDiagnostics = new List<CosmosDiagnostics>();

        private readonly string name;

        public TelemetryListener(string name)
        {
            this.name = name;
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            this.actualCosmosDiagnostics.Add((CosmosDiagnostics)value.Value);
        }
    }
}
