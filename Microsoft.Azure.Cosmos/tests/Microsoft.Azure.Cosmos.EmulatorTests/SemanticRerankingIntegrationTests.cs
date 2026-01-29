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
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;

    [TestClass]
    public class SemanticRerankingIntegrationTests
    {
        private string connectionString;
        private CosmosClient client;

        private TokenCredential tokenCredential;

        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;

        [TestInitialize]
        public void TestInitAsync()
        {
            this.connectionString = "https://inferencee2etest.documents.azure.com:443/";
            Environment.SetEnvironmentVariable("AZURE_COSMOS_SEMANTIC_RERANKER_INFERENCE_ENDPOINT", "https://inferencee2etest.westus3.dbinference.azure.com");
            DefaultAzureCredentialOptions options = new DefaultAzureCredentialOptions
            {
                TenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47",
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true,
            };

            //Create a cosmos client using AAD authentication
            this.tokenCredential = new DefaultAzureCredential(options);

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
                this.tokenCredential,
                new CosmosClientOptions()
                {
                    Serializer = this.cosmosSystemTextJsonSerializer,
                });
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_SEMANTIC_RERANKER_INFERENCE_ENDPOINT", null);
            this.client?.Dispose();
        }

#if PREVIEW
        [TestMethod]
        [TestCategory("Ignore")]
        [Timeout(70000)]
        public async Task SemanticRerankTest()
        {
            Database db = this.client.GetDatabase("virtualstore");
            Container container = db.GetContainer("sportinggoods");

            string search_text = "integrated pull-up bar";

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

        [TestMethod]
        [TestCategory("Ignore")]
        [Timeout(70000)]
        public async Task SemanticRerankTimeoutFaultInjectionTest()
        {
            // Create a fault injection rule with a delay greater than the default inference timeout (5 seconds)
            string timeoutRuleId = "inferenceTimeoutRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule timeoutRule = new FaultInjectionRuleBuilder(
                id: timeoutRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromSeconds(10))
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { timeoutRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer = 
                new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                Serializer = cosmosSystemTextJsonSerializer,
                InferenceRequestTimeout = TimeSpan.FromSeconds(5)
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                accountEndpoint: this.connectionString,
                tokenCredential: this.tokenCredential,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database db = faultInjectionClient.GetDatabase("virtualstore");
                Container container = db.GetContainer("sportinggoods");

                List<string> documents = new List<string>
                {
                    "{\"id\":\"1\",\"text\":\"document 1\"}",
                    "{\"id\":\"2\",\"text\":\"document 2\"}"
                };

                string rerankingContext = "test context";

                await container.ReadContainerAsync();
                // Enable the fault injection rule
                timeoutRule.Enable();

                // Verify that a CosmosException with request timeout is thrown
                CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(
                    async () => await container.SemanticRerankAsync(
                        rerankingContext,
                        documents));

                // Verify it's a timeout exception (status code 408)
                Assert.AreEqual(System.Net.HttpStatusCode.RequestTimeout, cosmosException.StatusCode);
                Assert.IsTrue(cosmosException.Message.Contains("Inference Service Request Timeout"));

                // Disable the rule after test
                timeoutRule.Disable();
            }
        }
#endif
    }
}
