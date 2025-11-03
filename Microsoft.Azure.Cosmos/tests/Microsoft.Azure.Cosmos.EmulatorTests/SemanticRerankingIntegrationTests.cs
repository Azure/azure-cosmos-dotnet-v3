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
            this.connectionString = "https://inferencee2etest.documents.azure.com:443/";

            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions
            {
                TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
                ExcludeVisualStudioCredential = true
            };

            //Create a cosmos client using AAD authentication
            TokenCredential tokenCredential = new DefaultAzureCredential(options);

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

            Dictionary<string, dynamic> options = new Dictionary<string, dynamic>
            {
                { "return_documents", true },
                { "top_k", 10 },
                { "batch_size", 32 },
                { "sort", true }
            };

            SemanticRerankResult results = await container.SemanticRerankAsync(
                reranking_context,
                documents,
                options);

            Assert.IsTrue(results.RerankScores.Count > 0);
            Assert.AreEqual(4, results.RerankScores[0].Index);
            Assert.IsNotNull(results.Latency);
            Assert.IsNotNull(results.TokenUseage);
        }
    }
}
