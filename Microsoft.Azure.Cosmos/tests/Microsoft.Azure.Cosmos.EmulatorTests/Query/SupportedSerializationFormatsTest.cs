namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Query")]
    public sealed class SupportedSerializationFormatsTest
    {
        private readonly string endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
        private readonly string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];

        [TestMethod]
        public async Task ConnectEndpoint()
        {
            using (CosmosClient client = new CosmosClient(this.endpoint, this.authKey,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Direct
                }))
            {
                await this.TestSetup(client);
            }
        }

        private async Task TestSetup(CosmosClient client)
        {
            await client.CreateDatabaseIfNotExistsAsync("db");
            Container container = await client.GetDatabase("db").CreateContainerIfNotExistsAsync("container", "/partitionKey");
            for (int i = 0; i < 10; i++)
            {
                await container.CreateItemAsync(new { id = Guid.NewGuid().ToString(), name = "document_" + i, score = i + 1 });
            }

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
                }
            };

            await this.RunQueryWithOptionsAsync(client, container, queryRequestOptionsList);
        }

        private async Task RunQueryWithOptionsAsync(CosmosClient client, Container container, List<QueryRequestOptions> queryRequestOptionsList)
        {
            string query = string.Format("SELECT c.name FROM c");
            List<dynamic> expectedResults = new List<dynamic>()
            {
               new { name = "document_0"},
               new { name = "document_1"},
               new { name = "document_2"},
               new { name = "document_3"},
               new { name = "document_4"},
               new { name = "document_5"},
               new { name = "document_6"},
               new { name = "document_7"},
               new { name = "document_8"},
               new { name = "document_9"},
            };

            foreach (QueryRequestOptions requestOptions in queryRequestOptionsList)
            {
                List<dynamic> actualResults = new List<dynamic>();
                using (FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(new QueryDefinition(query), requestOptions: requestOptions))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> response = await feedIterator.ReadNextAsync();
                        actualResults.AddRange(response.ToList());
                    }
                }

                for (int i = 0; i < actualResults.Count; i++)
                {
                    Assert.AreEqual(expectedResults[i].name, actualResults[i].name.ToString());
                }
            }

            await client.GetDatabase("db").DeleteAsync();
        }
    }
}