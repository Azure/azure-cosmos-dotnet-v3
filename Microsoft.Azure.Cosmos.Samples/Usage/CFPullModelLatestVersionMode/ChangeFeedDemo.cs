namespace Cosmos.Samples.CFPullModelLatestVersionMode
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Samples.CFPullModelLatestVersionMode.Models;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    internal class ChangeFeedDemo
    {
        private static readonly string databaseName = "db";
        private static readonly string containerName = "dotnetPullTest";

        private static CosmosClient cosmosClient;
        private Container container;
        private string? latestVersionContinuationToken;

        public ChangeFeedDemo()
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

            cosmosClient = new CosmosClient(endpoint, authKey);
        }

        public async Task GetOrCreateContainer()
        {
            Console.WriteLine($"Getting container reference for {containerName}.");

            ContainerProperties properties = new ContainerProperties(containerName, partitionKeyPath: "/Pk");

            await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            this.container = await cosmosClient.GetDatabase(databaseName).CreateContainerIfNotExistsAsync(properties);
        }

        public async Task CreateLatestVersionChangeFeedIterator()
        {
            Console.WriteLine("Creating ChangeFeedIterator to read the change feed in Latest Version mode.");

            this.latestVersionContinuationToken = null;
            FeedIterator<Item> latestVersionIterator = this.container
                .GetChangeFeedIterator<Item>(ChangeFeedStartFrom.Now(), ChangeFeedMode.Incremental);

            while (latestVersionIterator.HasMoreResults)
            {
                FeedResponse<Item> response = await latestVersionIterator.ReadNextAsync();

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    this.latestVersionContinuationToken = response.ContinuationToken;
                    break;
                }
            }
        }

        public async Task IngestData()
        {
            Console.Clear();

            await Console.Out.WriteLineAsync("Press any key to begin ingesting data.");

            Console.ReadKey(true);

            await Console.Out.WriteLineAsync("Press any key to stop.");

            List<Task> tasks = new List<Task>();

            while (!Console.KeyAvailable)
            {
                Item item = GenerateItem();
                await this.container.UpsertItemAsync(item, new PartitionKey(item.Pk));
                Console.Write("*");
            }

            await Task.WhenAll(tasks);
        }

        public async Task ReadLatestVersionChangeFeed()
        {
            Console.ReadKey(true);
            Console.Clear();

            await Console.Out.WriteLineAsync("Press any key to begin reading the change feed in Latest Version mode.");

            Console.ReadKey(true);

            FeedIterator<Item> latestVersionIterator = this.container.GetChangeFeedIterator<Item>(ChangeFeedStartFrom.ContinuationToken(this.latestVersionContinuationToken), ChangeFeedMode.Incremental, new ChangeFeedRequestOptions { PageSizeHint = 10 });

            await Console.Out.WriteLineAsync("Press any key to stop.");

            while (latestVersionIterator.HasMoreResults)
            {
                FeedResponse<Item> response = await latestVersionIterator.ReadNextAsync();

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    this.latestVersionContinuationToken = response.ContinuationToken;
                    Console.WriteLine($"No new changes");
                }
                else
                {
                    foreach (Item item in response)
                    {
                        // for any operation
                        Console.WriteLine($"Change in item: {item.Id}. New value: {item.Value}.");
                    }
                }

                if (Console.KeyAvailable)
                {
                    break;
                }
                Thread.Sleep(1000);
            }
        }

        private static Item GenerateItem()
        {
            Random random = new Random();

            return new Item
            {
                Id = random.Next(1, 999).ToString(),
                Value = random.Next(1, 100000),
                Pk = "pk"
            };
        }
    }
}
