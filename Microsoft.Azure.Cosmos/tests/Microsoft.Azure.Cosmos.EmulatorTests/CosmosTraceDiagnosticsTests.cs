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
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: true);
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                throughput: 15000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        /// <summary>
        /// Validate QueryMetrics retrieved from Diagnostics for multi-partition query.
        /// </summary>
        [TestMethod]
        public async Task ValidateMetricsMultiplePartitionQuery()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);

            ToDoActivity find = deleteList.First();
            QueryDefinition sql = new QueryDefinition("select * from toDoActivity t where t.id = '" + find.id + "'");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500,
                MaxItemCount = 1,
                MaxConcurrency = 1,
            };

            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
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
                bool tryParseResult = BackendMetrics.TryParseFromDelimitedString(iter.Headers.QueryMetricsText, out BackendMetrics backendMetricsFromTrace);
                Assert.IsTrue(tryParseResult);

                headerMetricsAccumulator.Accumulate(backendMetricsFromTrace);
                Assert.IsTrue(headerMetricsAccumulator.GetBackendMetrics().Equals(backendMetricsFromDiagnostics));
            }

            Assert.IsTrue(found);
        }
    }
}
