//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Telemetry.DiagnosticSource;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class EventBasedDiagnosticLogTest : BaseCosmosClientHelper
    {
        private CosmosClientBuilder cosmosClientBuilder;

        [TestInitialize]
        public void TestInitialize()
        {
            IList<ICosmosDiagnosticListener> listeners = new List<ICosmosDiagnosticListener>
            {
                new ListenerWithDefaultFilter(),
                new ListenerWithCustomFilter(),
                new ListenerWithBothFilter()
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

    class ListenerWithDefaultFilter : ICosmosDiagnosticListener
    {
        public IObserver<KeyValuePair<string, object>> Listener => new TelemetryListener("ListenerWithDefaultFilter");

        public DiagnosticSourceFilterType? DefaultFilter => DiagnosticSourceFilterType.Create;

        Func<CosmosDiagnostics, bool> ICosmosDiagnosticListener.Filter => null;
    }

    class ListenerWithCustomFilter : ICosmosDiagnosticListener
    {
        public IObserver<KeyValuePair<string, object>> Listener => new TelemetryListener("ListenerWithCustomFilter");

        public DiagnosticSourceFilterType? DefaultFilter => null;

        Func<CosmosDiagnostics, bool> ICosmosDiagnosticListener.Filter => (diagnostics) => {

            //Return all the requests with latancy more than 10 milliseconds
            if (diagnostics.GetClientElapsedTime() > TimeSpan.FromMilliseconds(10))
            {
                return true;
            }
            return false;
        };
    }

    class ListenerWithBothFilter : ICosmosDiagnosticListener
    {
        public IObserver<KeyValuePair<string, object>> Listener => new TelemetryListener("ListenerWithBothFilter");

        public DiagnosticSourceFilterType? DefaultFilter => DiagnosticSourceFilterType.Create;

        Func<CosmosDiagnostics, bool> ICosmosDiagnosticListener.Filter => (diagnostics) => {

            //Return all the requests with latency more than 10 milliseconds
            if (diagnostics.GetClientElapsedTime() > TimeSpan.FromMilliseconds(150))
            {
                return true;
            }
            return false;
        };
    }

    /// <summary>
    /// Telemetry Listener will receive the diagnostic logs
    /// </summary>
    public class TelemetryListener : IObserver<KeyValuePair<string, object>>
    {
        private readonly string name;
        public TelemetryListener(string listenerName)
        {
            this.name = listenerName;
        }

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
            Console.WriteLine($"{this.name} : {value.Key} => {(CosmosDiagnostics)value.Value}");
        }
    }
}