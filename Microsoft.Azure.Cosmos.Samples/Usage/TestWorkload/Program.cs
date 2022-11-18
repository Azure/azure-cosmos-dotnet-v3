namespace Cosmos.Samples.Shared
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json.Linq;

    public class Program
    {
        private class Configuration
        {
            public string EndpointUrl { get; set; }
            public string AuthorizationKey { get; set; }
            public string DatabaseName { get; set; }
            public string ContainerName { get; set; }
            public bool IsGatewayMode { get; set; }
        }

        private static Configuration configuration;
        private static CosmosClient client;

        public static async Task Main(string[] args)
        {
            Container container;
            try
            {
                container = await Program.InitializeAsync(args);
                FeedIterator<JObject> feedIterator = container.GetItemQueryIterator<JObject>();
                while(feedIterator.HasMoreResults)
                {
                    FeedResponse<JObject> response = await feedIterator.ReadNextAsync();

                    foreach(JObject result in response)
                    {
                        try
                        {
                            await container.ReplaceItemAsync(result, result["id"].Value<string>(), requestOptions: new ItemRequestOptions()
                            {
                                IfMatchEtag = result["_etag"].Value<string>()
                            });
                        }
                        catch(CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                        {
                            // expected, doc should be updated recently - can move ahead.
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex);
            }
            finally
            {
                client.Dispose();
            }
        }


        private static async Task<Container> InitializeAsync(string[] args)
        {
            IConfigurationRoot configurationRoot = new ConfigurationBuilder()
                    .AddJsonFile("appSettings.json")
                    .AddCommandLine(args)
                    .Build();

            Program.configuration = new Configuration();
            configurationRoot.Bind(Program.configuration);

            Program.client = GetClientInstance(configuration.EndpointUrl, configuration.AuthorizationKey);
            Container container = client.GetDatabase(configuration.DatabaseName).GetContainer(configuration.ContainerName);

            try
            {
                await container.ReadContainerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in reading collection: {0}", ex);
                throw;
            }

            return container;
        }

        private static CosmosClient GetClientInstance(
            string endpoint,
            string authKey)
        {
            return new CosmosClient(endpoint, authKey, new CosmosClientOptions()
            {
                ConnectionMode = configuration.IsGatewayMode ? ConnectionMode.Gateway : ConnectionMode.Direct,
                MaxRetryAttemptsOnRateLimitedRequests = 1000
            });
        }
    }
}