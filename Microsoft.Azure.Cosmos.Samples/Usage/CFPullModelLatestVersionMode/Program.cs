namespace CFPullModelLatestVersionMode
{
    using System;
    using System.Threading.Tasks;
    using Cosmos.Samples.CFPullModelLatestVersionMode;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    class Program
    {
        static async Task Main()
        {
            try
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                           .AddJsonFile("appSettings.json")
                           .Build();

                string endpoint = configuration["EndPointUrl"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid EndPointUrl in the appSettings.json");
                }

                string authKey = configuration["AuthorizationKey"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json");
                }

                using (CosmosClient client = new CosmosClient(endpoint, authKey))
                {
                    ChangeFeedDemo changeFeedDemo = new ChangeFeedDemo(client);
                    await changeFeedDemo.GetOrCreateContainer();
                    await changeFeedDemo.CreateLatestVersionChangeFeedIterator();
                    await changeFeedDemo.IngestData();
                    await changeFeedDemo.ReadLatestVersionChangeFeed();
                }
            }
            finally 
            {
                Console.WriteLine("End of demo, press any key to exit.");
                Console.ReadKey();
            }
        }
    }
}
