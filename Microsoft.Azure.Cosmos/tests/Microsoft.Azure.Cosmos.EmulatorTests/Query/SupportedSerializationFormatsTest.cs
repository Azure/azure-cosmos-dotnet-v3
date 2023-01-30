namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.EmulatorTests.Query;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class SupportedSerializationFormatsTest : QueryTestsBase
    {
        [TestMethod]
        public async Task TestSupportedSerializationFormats()
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
                string query = string.Format("SELECT * FROM c");

                QueryRequestOptions requestOptions = new QueryRequestOptions
                {
                    SupportedSerializationFormats = Documents.SupportedSerializationFormats.JsonText | Documents.SupportedSerializationFormats.CosmosBinary
                };

                FeedIterator<CosmosElement> feedIterator = container.GetItemQueryIterator<CosmosElement>(
                    query,
                    requestOptions: requestOptions);

                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<CosmosElement> page = await feedIterator.ReadNextAsync();
                }
            }
        }
    }
}
