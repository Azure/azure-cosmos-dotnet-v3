namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SummaryDiagnosticsTest
    {
        private Container Container = null;
        private Cosmos.Database Database = null;
        private CosmosClient Client = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.Session
            };
            this.Client = TestCommon.CreateCosmosClient(clientOptions);
            this.Database = (await this.Client.CreateDatabaseAsync(Guid.NewGuid().ToString())).Database;
            this.Container = (await this.Database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk")).Container;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.Database.DeleteAsync();
        }

        [TestMethod]
        public async Task PointOperationSummaryTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response.Diagnostics);
            ITrace trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(trace);

            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value[(201, 0)], 1);
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value.Keys.Count, 1);

            response = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
            trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            summaryDiagnostics = new SummaryDiagnostics(trace);

            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value[(200, 0)], 1);
            Assert.IsFalse(summaryDiagnostics.GatewayRequestsSummary.IsValueCreated);
            Assert.AreEqual(summaryDiagnostics.AllRegionsContacted.Value.Count, 1);
        }

        [TestMethod]
        public async Task GatewayPointOperationSummaryTest()
        {
            CosmosClient gwCosmosClient = TestCommon.CreateCosmosClient(useGateway: true);
            Container container = gwCosmosClient.GetContainer(this.Database.Id, this.Container.Id);

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response.Diagnostics);
            ITrace trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(trace);

            Assert.IsFalse(summaryDiagnostics.DirectRequestsSummary.IsValueCreated);
            Assert.IsTrue(summaryDiagnostics.GatewayRequestsSummary.Value[201] > 0);

            response = await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
            trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            summaryDiagnostics = new SummaryDiagnostics(trace);

            Assert.IsFalse(summaryDiagnostics.DirectRequestsSummary.IsValueCreated);
            Assert.IsTrue(summaryDiagnostics.GatewayRequestsSummary.Value[200] > 0);
        }

        [TestMethod]
        public async Task QuerySummaryTest()
        {
            Container container = (await this.Database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk", throughput: 20000)).Container;

            // cross partition query
            FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>($"select * from c where c.id='{Guid.NewGuid()}'");
            List<ITrace> traces = new List<ITrace>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync();
                traces.Add(((CosmosTraceDiagnostics)response.Diagnostics).Value);
            }

            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(TraceJoiner.JoinTraces(traces));
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value.Keys.Count, 1);
            Assert.IsTrue(summaryDiagnostics.DirectRequestsSummary.Value[(200, 0)] > 1);
            Assert.AreEqual(summaryDiagnostics.AllRegionsContacted.Value.Count, 1);
        }

        [TestMethod]
        public async Task DirectPointOperationsWithTransportErrors()
        {
            int failed = 0;
            Container withTransportErrors = TransportClientHelper.GetContainerWithIntercepter(
                 this.Database.Id,
                 this.Container.Id,
                 (uri, resourceOperation, request) =>
                 {
                     if (request.ResourceType == ResourceType.Document && failed < 3)
                     {
                         failed++;
                         throw Documents.Rntbd.TransportExceptions.GetGoneException(uri, Guid.NewGuid());
                     }
                 },
                 useGatewayMode: false);
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await withTransportErrors.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            ITrace trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(trace);
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value.Keys.Count, 2);
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value[(410, 0)], 3);
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value[(201, 0)], 1);
        }
    }
}