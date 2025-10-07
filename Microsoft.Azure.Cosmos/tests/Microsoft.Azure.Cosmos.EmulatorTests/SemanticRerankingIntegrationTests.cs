namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.ClientModel.Primitives;
    using System.Collections.Generic;
    using System.Data;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Routing.GlobalPartitionEndpointManagerCore;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;

    [TestClass]
    public class SemanticRerankingIntegrationTests
    {
        private readonly string connectionString;
        private CosmosClient client;

        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;

        [TestInitialize]
        public void TestInitAsync()
        {
            this.connectionString = "_";

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
            Database db = this.client.GetDatabase("ProductsDatabase");
            Container container = db.GetContainer("FitnessEquipment");

            string queryString = "SELECT * FROM c WHERE c.Category = 'Cardio'";

            List<string> documents = new List<string>()
            {
                "Berlin is the capitol of Germany",
                "Paris is the capitol of France",
                "Madrid is the capitol of Spain",
                "Rome is the capitol of Italy",
            };

            SemanticRerankRequestOptions options = new SemanticRerankRequestOptions()
            {
                ReturnDocuments = true,
                TopK = 10,
                BatchSize = 32,
                Sort = true,
            };

            IReadOnlyDictionary<string, string> results = await container.SemanticRerankAsync<string, string>(
                queryString,
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
