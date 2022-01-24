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
        private CosmosClientBuilder cosmosClientBuilder;

        [TestInitialize]
        public void TestInitialize()
        {
            IReadOnlyList<IObserver<KeyValuePair<string, object>>> listeners = 
                new List<IObserver<KeyValuePair<string, object>>>
                {
                    new TelemetryListener()
                };

            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration()
                .AddListeners(listeners);
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
            await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
        }

        private async Task<Container> CreateClientAndContainer(ConnectionMode mode)
        { 
            this.cosmosClient = mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build();

            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());

            return await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id");
        }
    }

    /// <summary>
    /// Telemetry Listener will receive the diagnostic logs
    /// </summary>
    public class TelemetryListener : IObserver<KeyValuePair<string, object>>
    {
        public void OnCompleted()
        {
            Console.WriteLine("completed");
        }

        public void OnError(Exception error)
        {
            Console.WriteLine("error " + error.ToString());
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            Console.WriteLine($"{value.Key} => {value.Value}");
        }
    }
}