namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public class DistributedQueryClientTests : QueryTestsBase
    {
        private const int DocumentCount = 420;

        [TestMethod]
        public async Task SanityTestsAsync()
        {
            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                CreateDocuments(DocumentCount),
                RunTests);
        }

        private static async Task RunTests(Container container, IReadOnlyList<CosmosObject> _)
        {
            QueryRequestOptions options = new QueryRequestOptions()
            {
                MaxItemCount = 100,
                EnableDistributedQueryGatewayMode = true,
            };

            List<int> results = await RunQueryCombinationsAsync<int>(
                container,
                "SELECT VALUE c.x FROM c",
                options,
                QueryDrainingMode.HoldState | QueryDrainingMode.ContinuationToken);

            Assert.AreEqual(DistributedQueryClientTests.DocumentCount, results.Count);
        }

        private static IEnumerable<string> CreateDocuments(int count)
        {
            return Enumerable
                .Range(0, count)
                .Select(x => $"{{\"id\": \"{x}\", \"x\": {x}}}");
        }
    }
}