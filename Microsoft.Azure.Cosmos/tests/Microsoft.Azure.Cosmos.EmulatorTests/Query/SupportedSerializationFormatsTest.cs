namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

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
            List<(Cosmos.PartitionKey?, string[]) > partitionKeyAndExpectedResults = new List<(Cosmos.PartitionKey? partitionKey, string[] documents)>
                {
                    (
                        partitionKey: null,
                        documents: new[] { "document_0", "document_1", "document_2", "document_3", "document_4","document_5", "document_6", "document_7", "document_8", "document_9" }
                    ),
                    (
                        partitionKey: new Cosmos.PartitionKey("0"),
                        documents: new[] { "document_0" }
                    )
                };

            foreach (string query in new string[]
                {
                    null,   // With null query, this turns into a ReadFeed API, for which SupportedSerializationFormats should be ignored.
                    "SELECT * FROM c"
                })
            {
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
                        SupportedSerializationFormats = SupportedSerializationFormats.CosmosBinary
                    },
                    new QueryRequestOptions()
                    {
                        SupportedSerializationFormats = SupportedSerializationFormats.JsonText
                    }
                };

                // GetItemQueryIterator
                foreach (QueryRequestOptions requestOptions in queryRequestOptionsList)
                {
                    QueryDefinition queryDefinition = query != null ? new QueryDefinition(query) : null;
                    foreach ((Cosmos.PartitionKey? partitionKey, string[] expectedResults) in partitionKeyAndExpectedResults)
                    {
                        requestOptions.PartitionKey = partitionKey;

                        List<JObject> queryResults = new List<JObject>();
                        using (FeedIterator<JObject> feedIterator = container.GetItemQueryIterator<JObject>(queryDefinition, requestOptions: requestOptions))
                        {
                            while (feedIterator.HasMoreResults)
                            {
                                FeedResponse<JObject> response = await feedIterator.ReadNextAsync();
                                queryResults.AddRange(response.ToList());
                            }
                        }

                        string[] actualResults = queryResults
                            .Select(doc => doc["name"].ToString())
                            .ToArray();

                        CollectionAssert.AreEquivalent(expectedResults, actualResults);
                    }
                }

                // GetItemQueryStreamIterator
                foreach (QueryRequestOptions requestOptions in queryRequestOptionsList)
                {
                    QueryDefinition queryDefinition = query != null ? new QueryDefinition(query) : null;
                    foreach ((Cosmos.PartitionKey? partitionKey, string[] expectedResults) in partitionKeyAndExpectedResults)
                    {
                        requestOptions.PartitionKey = partitionKey;

                        List<CosmosElement> queryResults = new List<CosmosElement>();
                        using (FeedIterator feedIterator = container.GetItemQueryStreamIterator(queryDefinition, requestOptions: requestOptions))
                        {
                            while (feedIterator.HasMoreResults)
                            {
                                ResponseMessage response = await feedIterator.ReadNextAsync();
                                queryResults.AddRange(Deserialize(response.Content));
                            }
                        }

                        string[] actualResults = queryResults
                            .Select(doc => ((CosmosString)((CosmosObject)doc)["name"]).Value.ToString())
                            .ToArray();

                        CollectionAssert.AreEquivalent(expectedResults, actualResults);
                    }
                }
            }
        }

        private static IEnumerable<CosmosElement> Deserialize(Stream content)
        {
            string contentAsString = new StreamReader(content).ReadToEnd();
            CosmosObject obj = CosmosObject.Parse(contentAsString);
            foreach (CosmosElement element in (CosmosArray)obj["Documents"])
            {
                yield return element;
            }
        }
    }
}