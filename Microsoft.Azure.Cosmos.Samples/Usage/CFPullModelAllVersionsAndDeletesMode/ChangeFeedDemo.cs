namespace Cosmos.Samples.ChangeFeedPullModel.CFPullModelAllVersionsAndDeletesMode
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Samples.CFPullModelAllVersionsAndDeletesMode.Models;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Extensions.Configuration;

    internal class ChangeFeedDemo
    {
        private static readonly string databaseName = "db";
        private static readonly string containerName = "container";

        private static CosmosClient cosmosClient;
        private Container container;
        private int deleteItemCounter;
        private string? allVersionsContinuationToken;

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

            this.deleteItemCounter = 0;
        }

        public async Task GetOrCreateContainer()
        {
            Console.WriteLine($"Getting container reference for {containerName}.");

            ContainerProperties properties = new ContainerProperties(containerName, partitionKeyPath: "/Pk");

            await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            this.container = await cosmosClient.GetDatabase(databaseName).CreateContainerIfNotExistsAsync(properties);
        }

        public async Task CreateAllVersionsAndDeletesChangeFeedIterator()
        {
            Console.WriteLine("Creating ChangeFeedIterator to read the change feed in All Versions and Deletes mode.");

            this.allVersionsContinuationToken = null;
            FeedIterator<AllVersionsAndDeletesCFResponse> allVersionsIterator = this.container
                .GetChangeFeedIterator<AllVersionsAndDeletesCFResponse>(ChangeFeedStartFrom.Now(), ChangeFeedMode.FullFidelity);

            while (allVersionsIterator.HasMoreResults)
            {
                FeedResponse<AllVersionsAndDeletesCFResponse> response = await allVersionsIterator.ReadNextAsync();

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    this.allVersionsContinuationToken = response.ContinuationToken;
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

        public async Task DeleteData()
        {
            Console.ReadKey(true);
            Console.Clear();
            await Console.Out.WriteLineAsync("Press any key to begin deleting data.");
            Console.ReadKey(true);

            await Console.Out.WriteLineAsync("Press any key to stop");

            while (!Console.KeyAvailable)
            {
                this.deleteItemCounter++;
                try
                {
                    await this.container.DeleteItemAsync<Item>(
                       partitionKey: new PartitionKey("pk"),
                       id: this.deleteItemCounter.ToString());
                    Console.Write("-");
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    // Deleting by a random id that might not exist in the container will likely throw errors that are safe to ignore for this purpose
                }
            }
        }

        public async Task ReadAllVersionsAndDeletesChangeFeed()
        {
            Console.ReadKey(true);
            Console.Clear();

            await Console.Out.WriteLineAsync("Press any key to start reading the change feed in All Versions and Deletes mode.");

            Console.ReadKey(true);

            FeedIterator<AllVersionsAndDeletesCFResponse> allVersionsIterator = this.container.GetChangeFeedIterator<AllVersionsAndDeletesCFResponse>(ChangeFeedStartFrom.ContinuationToken(this.allVersionsContinuationToken), ChangeFeedMode.FullFidelity, new ChangeFeedRequestOptions { PageSizeHint = 10 });

            await Console.Out.WriteLineAsync("Press any key to stop.");

            while (allVersionsIterator.HasMoreResults)
            {  

                FeedResponse<AllVersionsAndDeletesCFResponse> response = await allVersionsIterator.ReadNextAsync();

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    this.allVersionsContinuationToken = response.ContinuationToken;
                    Console.WriteLine($"No new changes");
                }
                else
                {
                    foreach (AllVersionsAndDeletesCFResponse r in response)
                    {
                        // if operaiton is delete
                        if (r.Metadata.OperationType == "delete")
                        {
                            Item item = r.Previous;

                            if (r.Metadata.TimeToLiveExpired == true)
                            {
                                Console.WriteLine($"Operation: {r.Metadata.OperationType} (due to TTL). Item id: {item.Id}. Previous value: {item.Value}");
                            }
                            else
                            {
                                Console.WriteLine($"Operation: {r.Metadata.OperationType} (not due to TTL). Item id: {item.Id}. Previous value: {item.Value}");
                            }
                        }
                        // if operation is create or replace
                        else
                        {
                            Item item = r.Current;

                            Console.WriteLine($"Operation: {r.Metadata.OperationType}. Item id: {item.Id}. Current value: {item.Value}");
                        }
                    }
                }

                Thread.Sleep(1000);
                if (Console.KeyAvailable)
                {
                    break;
                }
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
