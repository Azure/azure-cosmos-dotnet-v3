namespace Microsoft.Azure.Cosmos.Query
{
    using System;
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
            string[] inputDocuments = new[]
            {
                @"{""id"":""0"",""name"":""document_0""}",
                @"{""id"":""1"",""name"":""document_1""}",
                @"{""id"":""2"",""name"":""document_2""}",
                @"{""id"":""3"",""name"":""document_3""}",
                @"{""id"":""4"",""name"":""document_4""}",
                @"{""id"":""5"",""name"":""document_5""}",
                @"{""id"":""6"",""name"":""document_6""}",
                @"{""id"":""7"",""name"":""document_7""}",
                @"{""id"":""8"",""name"":""document_8""}",
                @"{""id"":""9"",""name"":""document_9""}",
            };

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.SinglePartition | CollectionTypes.MultiPartition,
                inputDocuments,
                this.TestSupportedSerializationFormatsHelper,
                    "/id");
        }

        private async Task TestSupportedSerializationFormatsHelper(Container container, IReadOnlyList<CosmosObject> documents)
        {
            string[] expectedResults = new[] { "document_0", "document_1", "document_2", "document_3", "document_4", "document_5", "document_6", "document_7", "document_8", "document_9" };
            string query = string.Format("SELECT c.name FROM c");
            List<QueryRequestOptions> queryRequestOptionsList = new List<QueryRequestOptions>()
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
                },
                new QueryRequestOptions()
                {
                    TransportSerializationFormat = TransportSerializationFormat.Text
                },
                new QueryRequestOptions()
                {
                    TransportSerializationFormat = TransportSerializationFormat.Binary
                },
                new QueryRequestOptions()
                {
                    TransportSerializationFormat = TransportSerializationFormat.Text,
                    SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary
                },
                new QueryRequestOptions()
                {
                    TransportSerializationFormat = TransportSerializationFormat.Binary,
                    SupportedSerializationFormats = SupportedSerializationFormats.JsonText
                }
            };

            foreach (QueryRequestOptions requestOptions in queryRequestOptionsList)
            {
                List<CosmosElement> results = new List<CosmosElement>();
                using (FeedIterator<CosmosElement> feedIterator = container.GetItemQueryIterator<CosmosElement>(new QueryDefinition(query), requestOptions: requestOptions))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<CosmosElement> response = await feedIterator.ReadNextAsync();
                        results.AddRange(response.ToList());
                    }
                }

                string[] actualResults = results
                    .Select(doc => ((CosmosString)(doc as CosmosObject)["name"]).Value.ToString())
                    .ToArray();

                CollectionAssert.AreEquivalent(expectedResults, actualResults);
            }
        }
    }
}