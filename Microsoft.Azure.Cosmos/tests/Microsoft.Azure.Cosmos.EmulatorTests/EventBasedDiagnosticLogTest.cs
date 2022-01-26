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
        /// <summary>
        /// This test make sure multiple clients are sending events to their corresponding listeners
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        [TestMethod]
        public void ListenerWithMultipleClientsTest()
        {
            TelemetryListener actualListener1 = new();
            this.ClientWithListener(actualListener1);
            TelemetryListener actualListener2 = new();
            this.ClientWithListener(actualListener2);

            Assert.AreEqual(4, actualListener1.actualCosmosDiagnostics.Count);
            Assert.AreEqual(4, actualListener2.actualCosmosDiagnostics.Count);
        }

        private async void ClientWithListener(TelemetryListener actualListener1)
        {
            IReadOnlyList<IObserver<KeyValuePair<string, object>>> listeners = new List<IObserver<KeyValuePair<string, object>>>
            {
                actualListener1
            };
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");

            CosmosClientBuilder cosmosClientBuilder1 = TestCommon.GetDefaultConfiguration()
                                                                 .AddListeners(listeners);
            using (CosmosClient cosmosClient1 = cosmosClientBuilder1.Build())
            {
                Database database1 = await cosmosClient1.CreateDatabaseAsync(Guid.NewGuid().ToString());
                Container container1 = await database1.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/id");
                // Create an item
                await container1.CreateItemAsync<ToDoActivity>(testItem);
                await container1.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
            }
        }
    }

    /// <summary>
    /// Telemetry Listener will receive the diagnostic logs
    /// </summary>
    public class TelemetryListener : IObserver<KeyValuePair<string, object>>
    {
        public readonly IList<CosmosDiagnostics> actualCosmosDiagnostics = new List<CosmosDiagnostics>();

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
