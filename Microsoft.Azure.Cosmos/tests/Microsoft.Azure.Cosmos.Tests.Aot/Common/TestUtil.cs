namespace Microsoft.Azure.Cosmos.Tests.Aot.Common
{
    using Microsoft.Azure.Documents;
    using System.Net;
    using System.Security.Cryptography;

    internal class TestUtil
    {
        public static FeedIterator<T> GetQueryIterator<T>(string database, string collection, string query = null)
        {
            CosmosClient client = CreateCosmosClient();
            Container container = client.GetContainer(database, collection);
            return container.GetItemQueryIterator<T>(query);
        }

        public static async Task SetupTestDataAsync(string databaseId, string containerId, int documentCount = 100)
        {
            (string accountEndpoint, string cosmosKey) = TestUtil.GetAccountDetails();

            RestHelper restHelper = new RestHelper(accountEndpoint, cosmosKey);

            await restHelper.CreateDatabase(databaseId, RestHelper.DatabaseThoughputMode.@fixed);
            await restHelper.CreateContainer(databaseId, containerId, RestHelper.DatabaseThoughputMode.none);

            for (int i = 0; i < documentCount; i++)
            {
                RestHelper.Item item = new RestHelper.Item($"id{i}", $"pk{i % 10}", $"value{i}");
                await restHelper.CreateDocument(databaseId, containerId, item);
            }
        }

        public string GenerateMasterKeyAuthorizationSignature(HttpMethod verb, ResourceType resourceType, string resourceLink, string date, string key)
        {
            string keyType = "master";
            string tokenVersion = "1.0";
            string payload = $"{verb.ToString().ToLowerInvariant()}\n{resourceType.ToString().ToLowerInvariant()}\n{resourceLink}\n{date.ToLowerInvariant()}\n\n";

            HMACSHA256 hmacSha256 = new HMACSHA256 { Key = Convert.FromBase64String(key) };
            byte[] hashPayload = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
            string signature = Convert.ToBase64String(hashPayload);
            string authSet = WebUtility.UrlEncode($"type={keyType}&ver={tokenVersion}&sig={signature}");

            return authSet;
        }

        public static CosmosClient CreateCosmosClient()
        {
            (string endpoint, string accountKey) = GetAccountDetails();
            return new CosmosClient(endpoint, accountKey);
        }

        public static (string endpoint, string accountKey) GetAccountDetails()
        {
            return (
                ConfigurationManager.AppSettings["GatewayEndpoint"],
                ConfigurationManager.AppSettings["MasterKey"]
                );
        }
    }
}
