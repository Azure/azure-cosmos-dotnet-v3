namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class TestUserRepro2
    {
        [TestMethod]
        public async Task Info()
        {
            var container = await Open();

            IReadOnlyList<FeedRange> feedRanges = await container.GetFeedRangesAsync();
            foreach(FeedRange feedRange in feedRanges)
            {
                Console.WriteLine(feedRange.ToString());
            }

            // (c.PartitionKey = '06f43ec1-b920-440b-91fb-6dfc5b3af47d') AND
            // c.PartitionKey, c.PartitionKeyLevel2, c.EntityId, c.id
            string query = "SELECT c.PartitionKey, c.PartitionKeyLevel2, c.EntityId, c.Entity.CaseNumber, c.id FROM c where c.PartitionKey = '06f43ec1-b920-440b-91fb-6dfc5b3af47d' AND (c.Entity.LastUpdatedTimeUtcV2 >= '2024-06-20T18:29:53.231Z') AND (c.Entity.LastUpdatedTimeUtcV2 <= '2024-06-27T18:29:53.231Z')";

            // Query multiple items from container
            using FeedIterator<JObject> feed = container.GetItemQueryIterator<JObject>(
                queryText: query
            );

            // Iterate query result pages
            while (feed.HasMoreResults)
            {
                Console.WriteLine($"ReadNextAsync");
                FeedResponse<JObject> response = await feed.ReadNextAsync();

                // Iterate query results
                foreach (JObject value in response)
                {
                    // Console.WriteLine($"PartitionKey: {item["PartitionKey"]}; PartitionKeyLevel2: {item["PartitionKeyLevel2"]}; EntityId: {item["EntityId"]}; CaseNumber: {item["Entity"]["CaseNumber"]}");
                    Console.WriteLine($"\tPartitionKey: {value["PartitionKey"]}; PartitionKeyLevel2: {value["PartitionKeyLevel2"]}; EntityId: {value["EntityId"]}; CaseNumber: {value["CaseNumber"]}; id: {value["id"]}");

                    // if (i++ == 5) break;
                }

                // break;
            }
        }

        [TestMethod]
        public async Task IncorrectResults()
        {
            await MainAsync();
        }

        static async Task MainAsync()
        {
            await OrderBy_Issue();
        }

        static async Task<Container> Open()
        {
            CosmosClient client = new(accountEndpoint: "https://geo-weu-asisf-dev-db.documents.azure.com:443/", authKeyOrResourceToken: << KEY HERE >>);
            Database database = await client.CreateDatabaseIfNotExistsAsync(id: "ASI");

            ContainerProperties containerProperties = new ContainerProperties()
            {
                Id = "Incidents",
                PartitionKeyPaths = new List<string>() { "/PartitionKey", "/PartitionKeyLevel2" }
            };

            Container container = await database.CreateContainerIfNotExistsAsync(containerProperties, throughput: 400);
            return container;
        }

        static async Task OrderBy_Issue()
        {
            try
            {
                var container = await Open();
                string PartitionKey = "06f43ec1-b920-440b-91fb-6dfc5b3af47d";

                var partitionKeyBuilder = new PartitionKeyBuilder();
                partitionKeyBuilder.Add(PartitionKey);

                var requestOptions = new QueryRequestOptions()
                {
                    PartitionKey = partitionKeyBuilder.Build()
                };

                // (c.PartitionKey = '06f43ec1-b920-440b-91fb-6dfc5b3af47d') AND
                string query = "SELECT * FROM c WHERE (c.Entity.LastUpdatedTimeUtcV2 >= '2024-06-20T18:29:53.231Z') AND (c.Entity.LastUpdatedTimeUtcV2 <= '2024-06-27T18:29:53.231Z') ORDER BY  c.Entity.LastUpdatedTimeUtcV2 DESC ";

                // Query multiple items from container
                using FeedIterator<JObject> feed = container.GetItemQueryIterator<JObject>(
                    queryText: query,
                    requestOptions: requestOptions
                );

                List<JToken> actualResults = new List<JToken>();
                // Iterate query result pages
                while (feed.HasMoreResults)
                {
                    Console.WriteLine($"ReadNextAsync");
                    FeedResponse<JObject> response = await feed.ReadNextAsync();

                    List<JObject> result = response.ToList();

                    List<IGrouping<string, JObject>> groups = response.GroupBy(jo => jo["PartitionKey"].Value<string>()).ToList();
                    foreach (IGrouping<string, JObject> group in groups)
                    {
                        Console.WriteLine($"PartitionKey: {group.Key}; {group.Count()} Items");
                        if (group.Key == PartitionKey)
                        {
                            actualResults.AddRange(group);
                        }
                    }
                }

                Console.WriteLine("-------------------------");
                Console.WriteLine("Final Results : ");
                foreach (JToken value in actualResults)
                {
                    Console.WriteLine($"\tPartitionKey: {value["PartitionKey"]}; PartitionKeyLevel2: {value["PartitionKeyLevel2"]}; EntityId: {value["EntityId"]}; CaseNumber: {value["Entity"]["CaseNumber"]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
