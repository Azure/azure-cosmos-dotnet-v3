namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;

    [TestClass]
    public sealed class PopulateIndexMetricsTest : QueryTestsBase
    {
        [TestMethod]
        public async Task TestIndexMetricsHeaderExistence()
        {
            int seed = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            uint numberOfDocuments = 1;
            QueryOracleUtil util = new QueryOracle2(seed);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync,
                "/id");

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                string queryWithoutDistinct = string.Format("SELECT * FROM c WHERE c.name = 'ABC' AND c.age > 12");

                QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateIndexMetrics = true };
                FeedIterator<CosmosElement> itemQuery = container.GetItemQueryIterator<CosmosElement>(
                    queryWithoutDistinct,
                    requestOptions: requestOptions);

                while (itemQuery.HasMoreResults)
                {
                    FeedResponse<CosmosElement> page = await itemQuery.ReadNextAsync();
                    Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                    Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.IndexUtilization), "Expected index utilization headers for query");
                }
            }
        }
    }
}