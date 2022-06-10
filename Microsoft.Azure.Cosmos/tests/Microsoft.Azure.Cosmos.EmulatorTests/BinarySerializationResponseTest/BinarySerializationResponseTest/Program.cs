using Microsoft.Azure.Cosmos;

namespace DotNetTool
{
    public class StressTest
    {
        private static readonly string databaseId = "db";
        private static readonly string containerId = "container3";

        private static async Task Main(string[] args)
        {
            try
            {
                string endpoint = "https://binarycomparison-binary.documents.azure.com:443/";
                string authKey = "7jJjxJcoUzsDfgR7nnYeGrlJI7dizU4u6dhM7xYlZidTjYPhbh1wdABIncBwNpCLjUwjzxOFtVRz3U3hZ4rRzQ==";
                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    await RunQueryAsync(client);
                }
            }
            catch (CosmosException ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static async Task RunQueryAsync(CosmosClient client)
        {
            Database database = client.GetDatabase(databaseId);
            Container container = database.GetContainer(containerId);
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                CosmosSerializationFormatOptions = new CosmosSerializationFormatOptions(
                            contentSerializationFormat: "CosmosBinary",
                            createCustomNavigator: (content) => JsonNavigator.Create(content),
                            createCustomWriter: () => JsonWriter.Create(JsonSerializationFormat.Binary))
            };
            FeedIterator iterator = container.GetItemQueryStreamIterator(
                queryText: "select * from c where c.id = 'bb67343e-3651-40fe-ae8e-bb1613f7d9e0'");

            {
                while (iterator.HasMoreResults)
                {
                    using (ResponseMessage response = await iterator.ReadNextAsync())
                    {
                        response.Diagnostics.ToString();
                        Console.WriteLine(response.Diagnostics.ToString());
                    }
                }
            }
        }
    }
}