//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Antlr4.Runtime.Tree;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GatewayClientSideRequestStats
    {
        private CosmosClient CosmosClient;
        private Container Container;
        private Database Database;

        [TestInitialize]
        public async Task Initialize()
        {
            this.CosmosClient = TestCommon.CreateCosmosClient(useGateway: true);
            this.Database = await this.CosmosClient.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            this.Container = await this.Database.CreateContainerAsync(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk");
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            await this.Database.DeleteStreamAsync();
            this.CosmosClient.Dispose();
        }

        [TestMethod]
        public async Task GatewayRequestStatsTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync(item);
            ClientSideRequestStatisticsTraceDatum datum = this.GetClientSideRequestStatsFromTrace(((CosmosTraceDiagnostics)response.Diagnostics).Value);
            Assert.IsNotNull(datum.HttpResponseStatisticsList);
            Assert.AreEqual(datum.HttpResponseStatisticsList.Count, 1);
            Assert.IsNotNull(datum.HttpResponseStatisticsList[0].HttpResponseMessage);
            Assert.AreEqual(datum.RequestEndTimeUtc, datum.HttpResponseStatisticsList[0].RequestEndTime);
        }

        [TestMethod]
        public async Task GatewayRetryRequestStatsTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> createResponse = await this.Container.CreateItemAsync(item);
            HttpClient httpClient = new HttpClient(new TimeOutHttpClientHandler(maxRetries: 3))
            {
                Timeout = TimeSpan.FromSeconds(1)
            };

            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient(x => x.WithConnectionModeGateway().WithHttpClientFactory(() => httpClient)))
            {
                Container container = cosmosClient.GetContainer(this.Database.Id, this.Container.Id);
                ItemResponse<ToDoActivity> response = await container.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk));
                ClientSideRequestStatisticsTraceDatum datum = this.GetClientSideRequestStatsFromTrace(((CosmosTraceDiagnostics)response.Diagnostics).Value);
                Assert.IsNotNull(datum.HttpResponseStatisticsList);
                Assert.AreEqual(datum.HttpResponseStatisticsList.Count, 3);
                Assert.IsTrue(datum.HttpResponseStatisticsList[0].Exception is OperationCanceledException);
                Assert.IsTrue(datum.HttpResponseStatisticsList[1].Exception is OperationCanceledException);
                Assert.IsNull(datum.HttpResponseStatisticsList[2].Exception);
                Assert.IsNotNull(datum.HttpResponseStatisticsList[2].HttpResponseMessage);
                Assert.AreEqual(datum.HttpResponseStatisticsList[2].RequestEndTime, datum.RequestEndTimeUtc);
            }
        }

        private ClientSideRequestStatisticsTraceDatum GetClientSideRequestStatsFromTrace(ITrace trace)
        {
            foreach (object datum in trace.Data.Values)
            {
                if (datum is ClientSideRequestStatisticsTraceDatum clientSideStats)
                {
                    return clientSideStats;
                }
            }

            foreach (ITrace child in trace.Children)
            {
                ClientSideRequestStatisticsTraceDatum datum = this.GetClientSideRequestStatsFromTrace(child);
                if (datum != null)
                {
                    return datum;
                }
            }

            return null;
        }

        private class TimeOutHttpClientHandler : DelegatingHandler
        {
            private int retries;
            private readonly int maxRetries;
            public TimeOutHttpClientHandler(int maxRetries) : base(new HttpClientHandler())
            {
                this.retries = 0;
                this.maxRetries = maxRetries;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri.ToString().Contains("doc"))
                {
                    this.retries++;
                    if(this.retries < this.maxRetries)
                    {
                        throw new OperationCanceledException();
                    }
                }

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
