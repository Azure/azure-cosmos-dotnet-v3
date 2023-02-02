namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.EmulatorTests.Query;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.QueryOracle;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class SupportedSerializationFormatsTest : QueryTestsBase
    {
        [TestMethod]
        public async Task TestSupportedSerializationFormats()
        {
            uint numberOfDocuments = 1;
            QueryOracleUtil util = new QueryOracle2(seed: 1675371967);
            IEnumerable<string> inputDocuments = util.GetDocuments(numberOfDocuments);

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct | ConnectionModes.Gateway,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                ImplementationAsync,
                "/id");

            static async Task ImplementationAsync(Container container, IReadOnlyList<CosmosObject> documents)
            {
                string query = string.Format("SELECT * FROM c");
                List<ArrayList> resultsList = new List<ArrayList>();
                List<QueryRequestOptions> requestOptionsList = new List<QueryRequestOptions>()
                {
                    new QueryRequestOptions()
                    {
                    SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow
                    },
                    new QueryRequestOptions()
                    {
                    SupportedSerializationFormats = SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary
                    },
                    new QueryRequestOptions()
                    {
                    SupportedSerializationFormats = SupportedSerializationFormats.JsonText | SupportedSerializationFormats.HybridRow
                    },
                    new QueryRequestOptions()
                    {
                    SupportedSerializationFormats = SupportedSerializationFormats.JsonText | SupportedSerializationFormats.CosmosBinary | SupportedSerializationFormats.HybridRow
                    },
                    new QueryRequestOptions()
                    {
                    SupportedSerializationFormats = SupportedSerializationFormats.JsonText
                    },
                    new QueryRequestOptions()
                    {
                    SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary
                    }
                };

                foreach (QueryRequestOptions requestOptions in requestOptionsList)
                {
                    FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(new QueryDefinition(query), requestOptions: requestOptions);
                    ArrayList results = new ArrayList();

                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> page = await feedIterator.ReadNextAsync();
                        results.AddRange(page.ToList());
                    }

                    resultsList.Add(results);
                }

                for (int i = 0; i < resultsList.Count - 1; i++)
                {
                    Assert.AreEqual(resultsList[i].Count, resultsList[i + 1].Count);
                }
            }
        }
    }
}
