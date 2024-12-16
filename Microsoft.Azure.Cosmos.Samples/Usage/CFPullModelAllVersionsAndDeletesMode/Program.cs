namespace CFPullModelAllVersionsAndDeletesMode
{
    using System;
    using System.Net;
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

                    string allVersionsContinuationToken = await CreateAllVersionsAndDeletesChangeFeedIterator(container);

                    await IngestData(container);
                    await DeleteData(container);

                    await ReadAllVersionsAndDeletesChangeFeed(container, allVersionsContinuationToken);
                }
            }
            finally
            {
                Console.WriteLine("End of demo.");
            }
        }

        static async Task<string> CreateAllVersionsAndDeletesChangeFeedIterator(Container container)
        {
            Console.WriteLine("Creating ChangeFeedIterator to read the change feed in All Versions and Deletes mode.");

            // <InitializeFeedIterator>
            using (FeedIterator<dynamic> allVersionsIterator = container
                .GetChangeFeedIterator<dynamic>(ChangeFeedStartFrom.Now(), ChangeFeedMode.AllVersionsAndDeletes))
            {
                while (allVersionsIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> response = await allVersionsIterator.ReadNextAsync();

                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        return response.ContinuationToken;
                    }
                }
            }
            // <InitializeFeedIterator>

            return null;
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

        static async Task DeleteData(Container container)
        {
            Console.ReadKey(true);
            Console.Clear();

            Console.WriteLine("Press any key to begin deleting data.");
            Console.ReadKey(true);

            Console.WriteLine("Press any key to stop");

            int deleteItemCounter = 0;
            while (!Console.KeyAvailable)
            {
                deleteItemCounter++;
                try
                {
                    await container.DeleteItemAsync<Item>(
                        partitionKey: new PartitionKey(deleteItemCounter.ToString()),
                        id: deleteItemCounter.ToString());
                    Console.Write("-");
                }
                catch (CosmosException cosmosException) when (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    // Deleting by a random id that might not exist in the container will likely throw errors that are safe to ignore for this purpose
                }
            }
        }

        static async Task ReadAllVersionsAndDeletesChangeFeed(Container container, string allVersionsContinuationToken)
        {
            Console.ReadKey(true);
            Console.Clear();

            Console.WriteLine("Press any key to start reading the change feed in All Versions and Deletes mode.");
            Console.ReadKey(true);

            Console.WriteLine("Press any key to stop.");

            // <ReadAllVersionsAndDeletesChanges>
            using (FeedIterator<dynamic> allVersionsIterator = container.GetChangeFeedIterator<dynamic>(ChangeFeedStartFrom.ContinuationToken(allVersionsContinuationToken), ChangeFeedMode.AllVersionsAndDeletes, new ChangeFeedRequestOptions { PageSizeHint = 10 }))
            {
                while (allVersionsIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> response = await allVersionsIterator.ReadNextAsync();

                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                        Console.WriteLine($"No new changes");
                        await Task.Delay(1000);
                    }
                    else
                    {
                        foreach (dynamic item in response)
                        {
                            // if operaiton is delete
                            if (item.metadata.operationType == "delete")
                            {
                                bool isTTL = (item.metadata?.timeToLiveExpired == null) ? false : true;
                                Console.WriteLine($"Operation: {item.metadata.operationType}. Item id: {item.metadata.id}. Due to ttl: {isTTL}");
                            }
                            // if operation is create or replace
                            else
                            {
                                Console.WriteLine($"Operation: {item.metadata.operationType}. Item id: {item.current.Id}. Current value: {item.current.Value}");
                            }

                            Console.WriteLine($"{item}");
                        }
                    }

                    if (Console.KeyAvailable)
                    {
                        break;
                    }
                }
            }
            // <ReadAllVersionsAndDeletesChanges>
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
