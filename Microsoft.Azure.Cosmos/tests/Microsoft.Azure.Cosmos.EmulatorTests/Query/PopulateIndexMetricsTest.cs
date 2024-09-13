namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;

    [TestClass]
    [TestCategory("Query")]
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
                string query = string.Format("SELECT * FROM c WHERE c.name = 'ABC' AND c.age > 12");
                
                // Test using GetItemQueryIterator
                QueryRequestOptions requestOptions = new QueryRequestOptions() { PopulateIndexMetrics = true };
                
                FeedIterator<CosmosElement> itemQuery = container.GetItemQueryIterator<CosmosElement>(
                    query,
                    requestOptions: requestOptions);

                while (itemQuery.HasMoreResults)
                {
                    FeedResponse<CosmosElement> page = await itemQuery.ReadNextAsync();
                    Assert.IsTrue(page.Headers.AllKeys().Length > 1);
                    Assert.IsNotNull(page.Headers.Get(HttpConstants.HttpHeaders.IndexUtilization), "Expected index utilization headers for query");
                    Assert.IsNotNull(page.IndexMetrics, "Expected index metrics response for query");
                }

                // Test using Stream API
                using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(
                queryText: query,
                continuationToken: null,
                requestOptions: new QueryRequestOptions
                {
                    PopulateIndexMetrics = true,
                }))
                {
                    using (ResponseMessage response = await feedIterator.ReadNextAsync())
                    {
                        Assert.IsNotNull(response.Content);
                        Assert.IsTrue(response.Headers.AllKeys().Length > 1);
                        Assert.IsNotNull(response.Headers.Get(HttpConstants.HttpHeaders.IndexUtilization), "Expected index utilization headers for query");
                    }
                }
            }
        }
    }
}