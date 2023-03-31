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
            this.Client.Dispose();
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
            Assert.AreEqual(trace.Summary.RegionsContacted.Count, 1);
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
            Assert.IsTrue(summaryDiagnostics.GatewayRequestsSummary.Value[(201, 0)] > 0);

            response = await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
            trace = ((CosmosTraceDiagnostics)response.Diagnostics).Value;
            summaryDiagnostics = new SummaryDiagnostics(trace);

            Assert.IsFalse(summaryDiagnostics.DirectRequestsSummary.IsValueCreated);
            Assert.IsTrue(summaryDiagnostics.GatewayRequestsSummary.Value[(200, 0)] > 0);
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
            HashSet<(string, Uri)> headers = new HashSet<(string, Uri)>();
            foreach (Trace item in traces)
            {
                headers.UnionWith(item.Summary.RegionsContacted);
            }

            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(TraceJoiner.JoinTraces(traces));
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value.Keys.Count, 1);
            Assert.IsTrue(summaryDiagnostics.DirectRequestsSummary.Value[(200, 0)] > 1);
            Assert.AreEqual(headers.Count, 1);
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
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value[(410, (int)SubStatusCodes.TransportGenerated410)], 3);
            Assert.AreEqual(summaryDiagnostics.DirectRequestsSummary.Value[(201, 0)], 1);
        }

        /// <summary>
        /// Test to validate that when a read operation is done on a database and a container that
        /// does not exists, then the <see cref="SummaryDiagnostics"/> should capture the sub status
        /// codes successfully.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task SummaryDiagnostics_WhenContainerDoesNotExists_ShouldRecordSubStatusCode()
        {
            string partitionKey = "/pk";
            string databaseName = "testdb";
            string containerName = "testcontainer";
            int notFoundStatusCode = 404, notFoundSubStatusCode = 1003;
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(useGateway: false);

            try
            {
                Container container = cosmosClient.GetContainer(databaseName, containerName);
                ItemResponse<dynamic> readResponse = await container.ReadItemAsync<dynamic>(partitionKey, new Cosmos.PartitionKey(partitionKey));
            }
            catch (CosmosException ex)
            {
                ITrace trace = ((CosmosTraceDiagnostics)ex.Diagnostics).Value;
                SummaryDiagnostics summaryDiagnostics = new(trace);

                Assert.IsNotNull(value: summaryDiagnostics);
                Assert.IsTrue(condition: summaryDiagnostics.GatewayRequestsSummary.Value.ContainsKey((notFoundStatusCode, notFoundSubStatusCode)));
            }
        }
    }
}