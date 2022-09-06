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
                
                // Build the expected string
                Assert.IsTrue(IndexUtilizationInfo.TryCreateFromDelimitedString("eyJVdGlsaXplZFNpbmdsZUluZGV4ZXMiOlt7IkZpbHRlckV4cHJlc3Npb24iOiIoUk9PVC5uYW1lID0gXCJBQkNcIikiLCJJbmRleFNwZWMiOiJcL25hbWVcLz8iLCJGaWx0ZXJQcmVjaXNlU2V0Ijp0cnVlLCJJbmRleFByZWNpc2VTZXQiOnRydWUsIkluZGV4SW1wYWN0U2NvcmUiOiJIaWdoIn0seyJGaWx0ZXJFeHByZXNzaW9uIjoiKFJPT1QuYWdlID4gMTIpIiwiSW5kZXhTcGVjIjoiXC9hZ2VcLz8iLCJGaWx0ZXJQcmVjaXNlU2V0Ijp0cnVlLCJJbmRleFByZWNpc2VTZXQiOnRydWUsIkluZGV4SW1wYWN0U2NvcmUiOiJIaWdoIn1dLCJQb3RlbnRpYWxTaW5nbGVJbmRleGVzIjpbXSwiVXRpbGl6ZWRDb21wb3NpdGVJbmRleGVzIjpbXSwiUG90ZW50aWFsQ29tcG9zaXRlSW5kZXhlcyI6W3siSW5kZXhTcGVjcyI6WyJcL25hbWUgQVNDIiwiXC9hZ2UgQVNDIl0sIkluZGV4UHJlY2lzZVNldCI6ZmFsc2UsIkluZGV4SW1wYWN0U2NvcmUiOiJIaWdoIn1dfQ==",
                    out IndexUtilizationInfo parsedInfo));
                StringBuilder stringBuilder = new StringBuilder();
                IndexMetricWriter indexMetricWriter = new IndexMetricWriter(stringBuilder);
                indexMetricWriter.WriteIndexMetrics(parsedInfo);
                string expectedIndexMetricsString = stringBuilder.ToString();
                
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
                    Assert.AreEqual(expectedIndexMetricsString, page.IndexMetrics);
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
                        Assert.AreEqual(expectedIndexMetricsString, response.IndexMetrics);
                    }
                }
            }
        }
    }
}