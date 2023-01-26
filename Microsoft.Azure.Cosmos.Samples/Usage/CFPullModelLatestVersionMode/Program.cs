namespace CFPullModelLatestVersionMode
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;
    using Newtonsoft.Json;

    class Program
    {
        private static readonly string databaseName = "db";
        private static readonly string containerName = "container";

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
                    Console.WriteLine($"Getting container reference for {containerName}.");

                    ContainerProperties properties = new ContainerProperties(containerName, partitionKeyPath: "/id");

                    await client.CreateDatabaseIfNotExistsAsync(databaseName);
                    Container container = await client.GetDatabase(databaseName).CreateContainerIfNotExistsAsync(properties);

                    string latestVersionContinuationToken = await CreateLatestVersionChangeFeedIterator(container);

                    await IngestData(container);
                    await ReadLatestVersionChangeFeed(container, latestVersionContinuationToken);
                }
            }
            finally 
            {
                Console.WriteLine("End of demo.");
            }
        }

        static async Task<string> CreateLatestVersionChangeFeedIterator(Container container)
        {
            Console.WriteLine("Creating ChangeFeedIterator to read the change feed in Latest Version mode.");

            // <InitializeFeedIterator>
            using (FeedIterator<Item> latestVersionIterator = container.GetChangeFeedIterator<Item>(ChangeFeedStartFrom.Now(), ChangeFeedMode.Incremental))
            {
                while (latestVersionIterator.HasMoreResults)
                {
                    FeedResponse<Item> response = await latestVersionIterator.ReadNextAsync();

                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        return response.ContinuationToken;
                    }
                }
            }
            // <InitializeFeedIterator>

            return null;
        }

        static async Task ReadLatestVersionChangeFeed(Container container, string latestVersionContinuationToken)
        {
            Console.ReadKey(true);
            Console.Clear();

            Console.WriteLine("Press any key to begin reading the change feed in Latest Version mode.");
            Console.ReadKey(true);

            Console.WriteLine("Press any key to stop.");

            // <ReadLatestVersionChanges>
            using (FeedIterator<Item> latestVersionIterator = container.GetChangeFeedIterator<Item>(ChangeFeedStartFrom.ContinuationToken(latestVersionContinuationToken), ChangeFeedMode.Incremental, new ChangeFeedRequestOptions { PageSizeHint = 10 }))
            {
                while (latestVersionIterator.HasMoreResults)
                {
                    FeedResponse<Item> response = await latestVersionIterator.ReadNextAsync();

                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        Console.WriteLine($"No new changes");
                    }
                    else
                    {
                        foreach (Item item in response)
                        {
                            Console.WriteLine($"Change in item: {item.Id}. New value: {item.Value}.");
                        }
                    }

                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                    await Task.Delay(1000);
                }
            }
            // <ReadLatestVersionChanges>
        }

        static async Task IngestData(Container container)
        {
            Console.Clear();

            Console.WriteLine("Press any key to begin ingesting data.");
            Console.ReadKey(true);

            Console.WriteLine("Press any key to stop.");

            while (!Console.KeyAvailable)
            {
                Item item = GenerateItem();
                await container.UpsertItemAsync(item, new PartitionKey(item.Id));
                Console.Write("*");
            }
        }

        private static Item GenerateItem()
        {
            Random random = new Random();

            return new Item
            {
                Id = random.Next(1, 999).ToString(),
                Value = random.Next(1, 100000),
            };
        }
    }

    internal class Item
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        public double Value { get; set; }
    }
}
