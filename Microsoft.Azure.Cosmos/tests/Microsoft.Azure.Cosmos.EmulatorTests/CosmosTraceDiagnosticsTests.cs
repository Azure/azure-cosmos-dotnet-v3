namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosTraceDiagnosticsTests : BaseCosmosClientHelper
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: true);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        /// <summary>
        /// Validate QueryMetrics retrieved from Diagnostics for single partition query.
        /// </summary>
        [TestMethod]
        public async Task ValidateMetricsSinglePartitionQuery()
        {
            await this.ValidateMetrics(throughput: 5000);
        }

        /// <summary>
        /// Validate QueryMetrics retrieved from Diagnostics for multi-partition query.
        /// </summary>
        [TestMethod]
        public async Task ValidateMetricsMultiplePartitionQuery()
        {
            await this.ValidateMetrics(throughput: 15000);
        }

        private async Task ValidateMetrics(int throughput)
        {
            string PartitionKey = "/pk";
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                containerSettings,
                throughput: throughput,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(containerResponse);
            Assert.IsNotNull(containerResponse.Container);
            Assert.IsNotNull(containerResponse.Resource);
            Container container = containerResponse;

            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(container, 3, randomPartitionKey: true);

            ToDoActivity find = deleteList.First();
            QueryDefinition sql = new QueryDefinition("select * from toDoActivity t where t.id = '" + find.id + "'");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500,
                MaxItemCount = 1,
                MaxConcurrency = 1,
            };

            FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>(
                sql,
                requestOptions: requestOptions);

            bool found = false;
            BackendMetricsAccumulator headerMetricsAccumulator = new BackendMetricsAccumulator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.IsTrue(iter.Count() <= 1);
                if (iter.Count() == 1)
                {
                    found = true;
                    ToDoActivity response = iter.First();
                    Assert.AreEqual(find.id, response.id);
                }

                BackendMetrics backendMetricsFromDiagnostics = iter.Diagnostics.GetQueryMetrics();
                bool tryParseResult = BackendMetrics.TryParseFromDelimitedString(iter.Headers.QueryMetricsText, out BackendMetrics backendMetricsFromHeaders);
                Assert.IsTrue(tryParseResult);

                headerMetricsAccumulator.Accumulate(backendMetricsFromHeaders);
                Assert.IsTrue(headerMetricsAccumulator.GetBackendMetrics().FormatTrace() == backendMetricsFromDiagnostics.FormatTrace());
            }

            Assert.IsTrue(found);
        }
    }
}
