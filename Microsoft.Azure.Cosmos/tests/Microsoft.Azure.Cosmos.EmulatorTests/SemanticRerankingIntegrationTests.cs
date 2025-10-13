namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;

    [TestClass]
    public class SemanticRerankingIntegrationTests
    {
        private string connectionString;
        private CosmosClient client;

        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;

        [TestInitialize]
        public void TestInitAsync()
        {
            this.connectionString = "";

            //Create a cosmos client using AAD authentication
            TokenCredential tokenCredential = new DefaultAzureCredential();

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }
            this.client = new CosmosClient(
                this.connectionString,
                tokenCredential,
                new CosmosClientOptions()
                {
                    Serializer = this.cosmosSystemTextJsonSerializer,
                });
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.client?.Dispose();
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [Timeout(70000)]
        public async Task SemanticRerankTest()
        {
            Database db = this.client.GetDatabase("virtualstore");
            Container container = db.GetContainer("sportinggoods");

            string search_text = "integrated pull-up bar";

            // Fix: Use string interpolation instead of raw string literal and 'f' prefix
            string queryString = $@"
                SELECT TOP 15 c.id, c.Name, c.Brand, c.Description
                FROM c
                WHERE FullTextContains(c.Description, ""{search_text}"")
                ORDER BY RANK FullTextScore(c.Description, ""{search_text}"")
                ";

            string reranking_context = "most economical with multiple pulley adjustmnets and ideal for home gyms";

            List<string> documents = new List<string>();

            FeedIterator<dynamic> resultSetIterator = container.GetItemQueryIterator<dynamic>(
                new QueryDefinition(queryString),
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 15,
                });

            while (resultSetIterator.HasMoreResults)
            {
                FeedResponse<dynamic> response = await resultSetIterator.ReadNextAsync();
                foreach (JsonElement item in response)
                {
                    documents.Add(item.ToString());
                }
            }

            SemanticRerankRequestOptions options = new SemanticRerankRequestOptions()
            {
                ReturnDocuments = true,
                TopK = 10,
                BatchSize = 32,
                Sort = true,
            };

            IReadOnlyDictionary<string, string> results = await container.SemanticRerankAsync<string, string>(
                reranking_context,
                documents,
                options);

            Console.WriteLine("Reranked results:");
            foreach (KeyValuePair<string, string> result in results)
            {
                Console.WriteLine($"Document: {result.Key}, Score: {result.Value}");
            }
        }
    }
}
