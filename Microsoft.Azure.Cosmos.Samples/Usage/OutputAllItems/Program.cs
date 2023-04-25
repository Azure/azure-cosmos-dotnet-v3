namespace Cosmos.Samples.Shared
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    public class Program
    {
        private class Configuration
        {
            public string EndpointUrl { get; set; }
            public string AuthorizationKey { get; set; }
            public string DatabaseName { get; set; }
            public string ContainerName { get; set; }
        }

        private static Configuration configuration;
        private static CosmosClient client;

        public static async Task Main(string[] args)
        {
            Container container = null;
            try
            {
                container = await Program.InitializeAsync(args);

                FeedIterator queryIterator = container.GetItemQueryStreamIterator((string)null);
                ResponseMessage responseMessage = await queryIterator.ReadNextAsync();
                Console.WriteLine(responseMessage.Content.Length);
                StreamReader streamReader = new StreamReader(responseMessage.Content);
                string responseString = streamReader.ReadToEnd();
                Console.WriteLine(responseString);
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

            Program.client = new CosmosClient(configuration.EndpointUrl, configuration.AuthorizationKey);
            Container container = client.GetDatabase(configuration.DatabaseName).GetContainer(configuration.ContainerName);
            try
            {
                await container.ReadContainerAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading container: {0}", ex);
                throw;
            }

            return container;
        }
   }
}